#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PickaxeFall.BalanceLab
{
    public sealed class LabSession : IDisposable
    {
        sealed class Worker { public Process process; public bool busy; public string job, response; }
        public string DirectoryPath { get; private set; }
        public LabManifest Manifest { get; private set; }
        public LabPlan Plan { get; private set; }
        public List<LabScenario> Scenarios { get; private set; }
        public readonly Dictionary<string, LabResult> Results = new();
        readonly HashSet<string> assigned = new();
        readonly List<Worker> workers = new();
        readonly ConcurrentQueue<(Worker, string)> messages = new();
        IEnumerator<LabJob> pending;
        StreamWriter journal;
        Stopwatch timer;
        double totalWall,nextProgress;
        int branches;
        public bool Running { get; private set; }
        public string Status { get; private set; } = "Готово к запуску";
        public long Total => (long)Scenarios.Count * Plan.samples;
        public double Elapsed => timer?.Elapsed.TotalSeconds ?? 0;
        public int Busy => workers.Count(w => w.busy);
        public int CompletedBranches => branches;
        public static LabSession Create(string path, LabPlan plan, LabManifest manifest)
        {
            var session = new LabSession { DirectoryPath = Path.GetFullPath(path), Plan = plan, Manifest = manifest, Scenarios = LabPlanner.Scenarios(plan) };
            if (session.Scenarios.Count == 0) throw new ArgumentException("Не выбраны сценарии.");
            if (Directory.Exists(path)) throw new IOException("Каталог отчёта уже существует.");
            Directory.CreateDirectory(path); LabFiles.Write(Path.Combine(path,"plan.json"), plan); LabFiles.Write(Path.Combine(path,"manifest.json"),manifest);
            LabFiles.Write(Path.Combine(path,"scenarios.json"), new ScenarioList { scenarios = session.Scenarios }); return session;
        }
        public static LabSession Load(string path)
        {
            var session = new LabSession { DirectoryPath = Path.GetFullPath(path), Plan = LabFiles.Read<LabPlan>(Path.Combine(path,"plan.json")), Manifest = LabFiles.Read<LabManifest>(Path.Combine(path,"manifest.json")), Scenarios = LabFiles.Read<ScenarioList>(Path.Combine(path,"scenarios.json")).scenarios };
            session.Plan.Validate(false); string journal = Path.Combine(path,"results.jsonl");
            if (File.Exists(journal))
            {
                // A killed process can leave only the last append incomplete. Keep its bytes for inspection.
                long end,length;
                using(var reader=new FileStream(journal,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                {
                    length=end=reader.Length;
                    if(length>0)
                    {
                        reader.Position=length-1;
                        if(reader.ReadByte()!='\n')
                        {
                            var buffer=new byte[4096];long scan=length;end=0;
                            while(scan>0)
                            {
                                int count=(int)Math.Min(buffer.Length,scan);scan-=count;reader.Position=scan;
                                int read=reader.Read(buffer,0,count);int last=Array.LastIndexOf(buffer,(byte)'\n',read-1,read);
                                if(last>=0){end=scan+last+1;break;}
                            }
                            reader.Position=end;
                            using var tail=File.Create(journal+".incomplete-"+DateTime.UtcNow.Ticks);reader.CopyTo(tail);
                        }
                    }
                }
                if(end<length) { using var stream=new FileStream(journal,FileMode.Open,FileAccess.Write);stream.SetLength(end); }
                foreach (string line in File.ReadLines(journal))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var row = JsonUtility.FromJson<LabResult>(line);
                    if (string.IsNullOrEmpty(row.id) || row.resources == null || row.resources.Length != 8) throw new IOException("Повреждён журнал результатов: " + journal);
                    if (string.IsNullOrEmpty(row.error))
                    {
                        if(session.Results.TryGetValue(row.id,out var old)){session.totalWall-=old.wallSeconds;session.branches-=old.branches;}
                        session.Results[row.id]=row;session.totalWall+=row.wallSeconds;session.branches+=row.branches;
                    }
                }
            }
            return session;
        }
        public void EnsureCompatible(LabManifest current)
        {
            if (Manifest.schema != current.schema || Manifest.fingerprint != current.fingerprint || Manifest.policy != current.policy || Manifest.unity != current.unity) throw new InvalidOperationException("Отпечаток сборки изменился. Создай новый анализ; старый отчёт доступен для просмотра.");
        }
        public void Start(string executable, LabManifest current)
        {
            if (Running) throw new InvalidOperationException("Проверка уже выполняется.");
            EnsureCompatible(current);
            string cancel = Path.Combine(DirectoryPath,"cancel"); if (File.Exists(cancel)) File.Delete(cancel);
            assigned.Clear(); pending = LabPlanner.Jobs(Plan, Scenarios).GetEnumerator();
            journal = new StreamWriter(new FileStream(Path.Combine(DirectoryPath,"results.jsonl"), FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
            timer = Stopwatch.StartNew();nextProgress=0; Running = true; Status = "Калибровка и первые сценарии…";
            try
            {
                for (int i = 0; i < Plan.workers; i++)
                {
                    string response = Path.Combine(DirectoryPath,"response-"+i+".json"); if (File.Exists(response)) File.Delete(response);
                    var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                        Arguments = "-batchmode -nographics -noaudio --balance-worker " + Quote("--cancel=" + cancel) + " " + Quote("--response="+response) + " -logFile " + Quote(Path.Combine(DirectoryPath,"worker-"+i+".log")) };
                    var worker = new Worker { process = new Process { StartInfo = start }, response = response };
                    worker.process.OutputDataReceived += (_,e) => { if (e.Data != null && e.Data.StartsWith("BALANCE_RESULT ",StringComparison.Ordinal)) messages.Enqueue((worker,e.Data.Substring(15))); };
                    worker.process.ErrorDataReceived += (_,e) => { if (!string.IsNullOrEmpty(e.Data)) messages.Enqueue((worker,"!"+e.Data)); };
                    workers.Add(worker); worker.process.Start(); worker.process.BeginOutputReadLine(); worker.process.BeginErrorReadLine();
                }
            }
            catch { Stop("Не удалось запустить работников"); throw; }
        }
        public void ContinueWith(int samples, int workers, int seconds)
        {
            if (Running) throw new InvalidOperationException("Останови анализ перед изменением продолжения.");
            var next = JsonUtility.FromJson<LabPlan>(JsonUtility.ToJson(Plan));
            next.samples = Math.Max(Plan.samples,samples); next.workers = workers; next.seconds = seconds; next.Validate();
            LabFiles.Write(Path.Combine(DirectoryPath,"plan.json"),next); Plan = next;
        }
        static string Quote(string text) => "\"" + text.Replace("\\","\\\\").Replace("\"","\\\"") + "\"";
        public void Pump()
        {
            if (!Running) return;
            try
            {
                foreach (var worker in workers)
                    if (worker.busy && File.Exists(worker.response)) { messages.Enqueue((worker,File.ReadAllText(worker.response))); File.Delete(worker.response); }
                while (messages.TryDequeue(out var message))
                {
                    if (message.Item2.StartsWith("!",StringComparison.Ordinal)) continue;
                    var row = JsonUtility.FromJson<LabResult>(message.Item2);
                    if (row.id != message.Item1.job) throw new IOException("Работник вернул чужое задание.");
                    if (!string.IsNullOrEmpty(row.error)) throw new IOException(row.error);
                    journal.WriteLine(JsonUtility.ToJson(row)); Results[row.id] = row;totalWall+=row.wallSeconds;branches+=row.branches; message.Item1.busy = false;
                }
                if (Plan.seconds > 0 && Elapsed >= Plan.seconds) { Stop("Лимит времени: частичный отчёт сохранён; можно продолжить"); return; }
                bool exhausted = false;
                foreach (var worker in workers)
                {
                    if (worker.process.HasExited) throw new IOException("Работник завершился неожиданно. Журнал: " + DirectoryPath);
                    if (worker.busy) continue;
                    LabJob job = null;
                    while (pending.MoveNext()) if (!Results.ContainsKey(pending.Current.Id) && assigned.Add(pending.Current.Id)) { job = pending.Current; break; }
                    if (job == null) { exhausted = true; continue; }
                    worker.job = job.Id; worker.busy = true; worker.process.StandardInput.WriteLine(JsonUtility.ToJson(job)); worker.process.StandardInput.Flush();
                }
                if (exhausted && Busy == 0) { Stop("Все сценарии завершены"); return; }
                double perSecond = totalWall / Math.Max(1, Plan.workers);
                Status = $"Выполнено {Results.Count:N0} / {Total:N0}; занято работников {Busy}/{workers.Count}; время {Elapsed/60:0.0} мин";
                if (Results.Count >= 5 && perSecond > 0) Status += $"; ориентир остатка {(Total-Results.Count) * perSecond / Results.Count / 60:0.0} мин";
                if(Elapsed>=nextProgress){nextProgress=Elapsed+5;WriteProgress();}
            }
            catch (Exception e) { Stop("Ошибка: " + e.Message); }
        }
        public void Stop(string reason = "Остановлено; результаты сохранены")
        {
            Running = false; Status = reason; timer?.Stop();
            if (Directory.Exists(DirectoryPath)) File.WriteAllText(Path.Combine(DirectoryPath,"cancel"),"");
            foreach (var worker in workers)
            {
                try { worker.process.StandardInput.Close(); if (!worker.process.WaitForExit(300)) worker.process.Kill(); } catch (InvalidOperationException) { } catch (IOException) { }
                worker.process.Dispose();
            }
            workers.Clear(); assigned.Clear(); pending?.Dispose(); pending = null; journal?.Dispose(); journal = null;
            while (messages.TryDequeue(out _)) { }
            if (Directory.Exists(DirectoryPath)){ LabFiles.Atomic(Path.Combine(DirectoryPath,"status.txt"),reason);WriteProgress();}
        }
        void WriteProgress()
        {
            LabFiles.Write(Path.Combine(DirectoryPath,"progress.json"),new LabProgress {completed=Results.Count,total=Total,branches=branches,elapsedSeconds=Elapsed,running=Running,status=Status});
        }
        public void Dispose() { if (Running) Stop(); journal?.Dispose(); }
    }
}
#endif
