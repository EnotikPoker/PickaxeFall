#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall.BalanceLab
{
    public sealed class LabApp : MonoBehaviour
    {
        [SerializeField] GameBalance balance;
        [SerializeField] PanelSettings gamePanel;
        PanelSettings ownPanel;
        LabManifest manifest;
        LabSession session, previous;
        LabAnalysis analysis;
        string home, cli;
        Label status, detail, buildInfo;
        DropdownField mode, tier, point, background, behavior, order, sorting;
        IntegerField samples, workers, budget, seed;
        Toggle passive, active, isolated, common, diagnostic;
        readonly List<Toggle> tierChoices = new();
        readonly List<IntegerField> levels = new();
        TextField search, folder, compareFolder;
        ListView table;
        VisualElement matrix;
        List<LabSummary> visible = new();
        float nextRefresh;
        Task export;
        bool autoExport;
        public void Configure(GameBalance config, PanelSettings panel) { balance = config; gamePanel = panel; }
        IEnumerator Start()
        {
            home = Path.GetDirectoryName(Application.dataPath); string path = Path.Combine(home,"manifest.json");
            if (!File.Exists(path)) { Debug.LogError("Balance Lab manifest missing: " + path); Application.Quit(1); yield break; }
            manifest = LabFiles.Read<LabManifest>(path);
            JsonUtility.FromJsonOverwrite(manifest.balance,balance);
            if(LabFiles.Arg("lab-export")!=null)
            {
                try
                {
                    using var saved=LabSession.Load(LabFiles.Arg("lab-export"));new LabAnalysis(saved).Export(saved.DirectoryPath);
                    LabFiles.Write(Path.Combine(saved.DirectoryPath,"analysis.json"),new LabManifest {version=manifest.version,fingerprint=manifest.fingerprint,policy=manifest.policy,builtUtc=DateTime.UtcNow.ToString("O")});
                    Debug.Log("PICKAXEFALL_LAB_EXPORT_OK: "+saved.DirectoryPath);Application.Quit(0);
                }
                catch(Exception e){Debug.LogException(e);Application.Quit(1);}
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--balance-worker")) { yield return LabWorker.Serve(balance,LabFiles.Arg("cancel") ?? Path.Combine(home,"cancel"),LabFiles.Arg("response")); yield break; }
            if (Environment.GetCommandLineArgs().Contains("--lab-parity"))
            {
                var routine=LabValidation.ParityChecks(balance);
                while(true)
                {
                    bool more;
                    try { more=routine.MoveNext(); }
                    catch(Exception e) { Debug.LogException(e); Application.Quit(1); yield break; }
                    if(!more)break;
                    yield return routine.Current;
                }
                Application.Quit(0);yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--lab-checks"))
            {
                try { LabValidation.Validate(); LabValidation.PhysicsChecks(balance); Debug.Log("PICKAXEFALL_LAB_CHECKS_OK"); Application.Quit(0); }
                catch (Exception e) { Debug.LogException(e); Application.Quit(1); }
                yield break;
            }
            cli = LabFiles.Arg("lab-cli");
            if (cli != null)
            {
                try
                {
                    Fresh(); Application.targetFrameRate = 30; Application.runInBackground = true;
                    var plan = LabFiles.Arg("plan") != null ? LabFiles.Read<LabPlan>(LabFiles.Arg("plan")) : new LabPlan { mode = cli == "full" ? 1 : cli == "triples" ? 2 : cli=="audit"?4:cli=="profile"?5:0 };
                    plan.samples = int.Parse(LabFiles.Arg("samples") ?? (LabFiles.Arg("plan") != null ? plan.samples.ToString() : plan.mode == 0 ? "8" : plan.mode==5?"16":"32"));
                    plan.seconds = int.Parse(LabFiles.Arg("seconds") ?? (LabFiles.Arg("plan") != null ? plan.seconds.ToString() : plan.mode == 0 ? "900" : plan.mode>=4?"7200":"0"));
                    plan.workers = int.Parse(LabFiles.Arg("workers") ?? plan.workers.ToString());
                    if(LabFiles.Arg("balance")!=null)plan.balanceJson=File.ReadAllText(LabFiles.Arg("balance"));
                    string existing = LabFiles.Arg("resume");
                    string output = existing ?? LabFiles.Arg("output") ?? NewPath();
                    session = existing != null ? LabSession.Load(output) : LabSession.Create(output,plan,manifest);
                    if (existing != null) session.EnsureCompatible(manifest);
                    if (existing != null) session.ContinueWith(LabFiles.Arg("samples") != null ? plan.samples : session.Plan.samples,LabFiles.Arg("workers") != null ? plan.workers : session.Plan.workers,LabFiles.Arg("seconds") != null ? plan.seconds : session.Plan.seconds);
                    session.Start(Executable(),manifest); Debug.Log("LAB_CLI_STARTED: "+output);
                }
                catch (Exception e) { Debug.LogException(e); Application.Quit(1); }
                yield break;
            }
            Application.runInBackground = true; Application.targetFrameRate = 30; Screen.SetResolution(1440,960,false);
            BuildUi();
            yield return null;
            if(LabFiles.Arg("launch-plan")!=null)
            {
                Safe(()=>
                {
                    var p=LabFiles.Read<LabPlan>(LabFiles.Arg("launch-plan"));p.Validate();
                    ShowPlan(p);NewSession();
                });
            }
            if (LabFiles.Arg("open") != null) { Safe(() => { folder.value = LabFiles.Arg("open"); Load(folder.value); table.selectedIndex = 0; }); }
            if (Environment.GetCommandLineArgs().Contains("--lab-ui-check"))
            {
                yield return new WaitForSecondsRealtime(2);
                if (LabFiles.Arg("view") == "matrix") Safe(ShowMatrix);
                var root = GetComponent<UIDocument>().rootVisualElement;
                if (root.Q<Button>("lab-start") == null || root.worldBound.width < 100 || root.Q<ListView>("lab-results") == null) { Debug.LogError("Lab UI missing"); Application.Quit(1); }
                else { Debug.Log("PICKAXEFALL_LAB_UI_OK: "+root.worldBound); }
                if (LabFiles.Arg("screenshot") != null)
                {
                    yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(LabFiles.Arg("screenshot"));
                    yield return new WaitForSecondsRealtime(1); Application.Quit(0);
                }
            }
        }
        string Executable() => Path.Combine(home,"PickaxeFallBalanceLab.x86_64");
        string NewPath() => LabFiles.Arg("output") ?? Path.Combine(home,"Results",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,5));
        void Fresh()
        {
            if (!Directory.Exists(manifest.project) || LabFiles.Fingerprint(manifest.project) != manifest.fingerprint) throw new InvalidOperationException("Сборка устарела. В Unity: PickaxeFall → Balance Lab → Build and Open.");
        }
        static Label Label(VisualElement parent, string text, int size = 16)
        {
            var label = new Label(text); label.style.fontSize = size; label.style.whiteSpace = WhiteSpace.Normal; label.style.marginBottom = 6; parent.Add(label); return label;
        }
        static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexShrink = 0; row.style.marginBottom = 6; parent.Add(row); return row;
        }
        Button Button(VisualElement parent, string title, Action action, string name = null)
        {
            var button = new Button(() => Safe(action)) { text = title, name = name }; button.style.minHeight = 32; button.style.marginRight = 5; parent.Add(button); return button;
        }
        void Safe(Action action) { try { action(); } catch (Exception e) { if (status != null) status.text = e.Message; Debug.LogWarning(e.Message); } }
        DropdownField Drop(VisualElement parent, string title, params string[] choices)
        {
            var field = new DropdownField(title,choices.ToList(),0); parent.Add(field); return field;
        }
        IntegerField Number(VisualElement parent, string title, int value)
        { var field = new IntegerField(title) { value = value }; parent.Add(field); return field; }
        Toggle Toggle(VisualElement parent, string title, bool value)
        { var field = new Toggle(title) { value = value }; parent.Add(field); return field; }
        void BuildUi()
        {
            ownPanel = ScriptableObject.CreateInstance<PanelSettings>(); ownPanel.themeStyleSheet = gamePanel.themeStyleSheet;
            ownPanel.scaleMode = PanelScaleMode.ScaleWithScreenSize; ownPanel.referenceResolution = new Vector2Int(1440,960); ownPanel.match = 0;
            var doc = gameObject.AddComponent<UIDocument>(); doc.panelSettings = ownPanel;
            var root = doc.rootVisualElement; root.style.backgroundColor = (Color)new Color32(24,34,43,255); root.style.color = (Color)new Color32(232,238,244,255); root.style.fontSize = 16;
            root.style.paddingLeft = root.style.paddingRight = 14; root.style.paddingTop = root.style.paddingBottom = 10;
            Label(root,"PickaxeFall · Balance Lab",26);
            buildInfo = Label(root,$"Игра {manifest.version} · Unity {manifest.unity} · {manifest.fingerprint.Substring(0,12)} · физика 1/60 с",14);
            Label(root,"После правок игры: Unity → PickaxeFall → Balance Lab → Build and Open. Профиль игрока не используется.",14);
            status = Label(root,"Выбери режим и нажми «Новый анализ»",16); status.style.minHeight = 42;
            var actions = Row(root); Button(actions,"Новый анализ",NewSession,"lab-start"); Button(actions,"Стоп",() => session?.Stop(),"lab-stop");
            Button(actions,"Продолжить",() => { Fresh(); if (session == null) throw new InvalidOperationException("Сначала открой отчёт"); session.EnsureCompatible(manifest); session.ContinueWith(samples.value,workers.value,checked(budget.value*60)); session.Start(Executable(),manifest); });
            Button(actions,"Экспорт CSV + HTML",Export);
            var split = Row(root); split.style.flexGrow = 1; split.style.flexShrink = 1; split.style.minHeight = 0;
            var config = new ScrollView(); config.style.width = 380; config.style.flexShrink = 0; config.style.paddingRight = 14; split.Add(config);
            mode = Drop(config,"Режим","Быстрый","Все пары","Все тройки","Свой набор","Аудит прогрессии","Профиль · 3 часа");
            mode.RegisterValueChangedCallback(_ => { samples.value = mode.index == 0 ? 8 : mode.index==5?16:32; budget.value = mode.index == 0 ? 15 : mode.index>=4?120:0; });
            samples = Number(config,"Карт на сценарий",8); workers = Number(config,"Работников",Math.Min(6,Math.Max(1,Environment.ProcessorCount/2)));
            budget = Number(config,"Лимит минут (0 — без)",15); seed = Number(config,"Начальный seed",8127);
            passive = Toggle(config,"Без жестов",true); active = Toggle(config,"Активная стратегия",true);
            isolated = Toggle(config,"Изолированные навыки",true); common = Toggle(config,"На фоне всей прокачки",true);
            Label(config,"В быстром режиме: первый уровень, изолированно. Полный анализ может занять часы или дни.",14);
            diagnostic = Toggle(config,"Диагностика: эффекты 100%",false);
            Label(config,"Кирки",18);
            foreach (var tool in balance.pickaxes) tierChoices.Add(Toggle(config,tool.title,true));
            folder = new TextField("Папка отчёта"); config.Add(folder);
            Button(config,"Открыть отчёт",() => Load(folder.value));
            Button(config,"Последний отчёт",() => { string path = Directory.GetDirectories(Path.Combine(home,"Results")).OrderByDescending(Directory.GetCreationTimeUtc).First(); folder.value = path; Load(path); });
            Button(config,"Открыть HTML",() => { if (session == null) return; string path = Path.Combine(session.DirectoryPath,"report.html"); if (!File.Exists(path)) throw new IOException("Сначала выполни экспорт"); Application.OpenURL(new Uri(path).AbsoluteUri); });
            compareFolder = new TextField("Предыдущий отчёт"); config.Add(compareFolder);
            Button(config,"Загрузить для сравнения",() => { previous?.Dispose(); previous = LabSession.Load(compareFolder.value); ShowComparison(); });
            Label(config,"Уровни для «Своего набора»",18);
            foreach (var key in Progression.Keys)
            {
                string title = SkillName(key)+" / "+Progression.MaxLevel(key.Kind);
                levels.Add(Number(config,title,0));
            }
            var content = new VisualElement(); content.style.flexGrow = 1; content.style.minWidth = 0; split.Add(content);
            var filters = Row(content); filters.style.flexWrap = Wrap.Wrap;
            tier = Drop(filters,"Кирка",new[] { "Деревянная","Каменная","Золотая","Железная","Алмазная","По рулетке" });
            point = Drop(filters,"Стадия","Первый уровень","Половина","Максимум"); background = Drop(filters,"Фон","Изолированно","Вся прокачка");
            behavior = Drop(filters,"Бот","Без жестов","Активный"); order = Drop(filters,"Размер","Все","База","Один","Пара","Тройка");
            sorting = Drop(filters,"Сортировка","Монет/мин","Глубина","Δ монет/мин","Цена");
            foreach (var field in new[] { tier,point,background,behavior,order,sorting }) field.RegisterValueChangedCallback(_ => RefreshTable());
            search = new TextField("Поиск"); content.Add(search); search.RegisterValueChangedCallback(_ => RefreshTable());
            var commands = Row(content); Button(commands,"Таблица",() => { table.style.display = DisplayStyle.Flex; matrix.style.display = DisplayStyle.None; });
            Button(commands,"Матрица пар",ShowMatrix); Button(commands,"Обновить",Refresh); Button(commands,"Сравнить отчёты",ShowComparison);
            var headings = Row(content); string[] columnNames = { "Набор", "Карт", "м", "с", "Лут", "Мон/мин", "Δ" };
            for (int i = 0; i < columnNames.Length; i++) { var label = Label(headings,columnNames[i],14); label.style.width = i == 0 ? 370 : 75; label.style.flexShrink = i == 0 ? 1 : 0; }
            table = new ListView { name = "lab-results", fixedItemHeight = 34, selectionType = SelectionType.Single };
            table.style.flexGrow = 1; table.style.minHeight = 160;
            table.makeItem = () => { var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; for (int i = 0; i < 7; i++) { var l = new Label(); l.style.width = i == 0 ? 370 : 75; l.style.flexShrink = i == 0 ? 1 : 0; l.style.fontSize = 15; row.Add(l); } return row; };
            table.bindItem = (element,index) =>
            {
                var x = visible[index]; string[] values = { Title(x.scenario),x.Count.ToString(),LabFiles.Number(x.Mean(r=>r.depth)),LabFiles.Number(x.Mean(r=>r.seconds)),LabFiles.Number(x.Mean(r=>r.value)),LabFiles.Number(x.Rate),LabFiles.Number(analysis.Delta(x)) };
                for (int i = 0; i < values.Length; i++) { ((Label)element[i]).text = values[i]; ((Label)element[i]).tooltip = values[i]; }
            };
            table.selectionChanged += items => { var x = items.Cast<LabSummary>().FirstOrDefault(); if (x != null) detail.text = Title(x.scenario)+"\n"+analysis.Detail(x); };
            content.Add(table); matrix = new ScrollView(ScrollViewMode.VerticalAndHorizontal); matrix.style.flexGrow = 1; matrix.style.display = DisplayStyle.None; content.Add(matrix);
            var details = new ScrollView(); details.style.height = 260; details.style.flexShrink = 0; content.Add(details);
            detail = Label(details,"Выбери строку: ресурсы, разброс, цена, окупаемость и срабатывания.",15);
            foreach (var input in root.Query<VisualElement>(className:"unity-base-field__input").ToList())
            {
                input.style.color = Color.black;
                foreach (var text in input.Query<TextElement>().ToList()) text.style.color = Color.black;
            }
            foreach (var button in root.Query<Button>().ToList())
            {
                button.style.backgroundColor = new StyleColor((Color)new Color32(49,87,109,255)); button.style.color = Color.white;
            }
        }
        public static string SkillName(UpgradeKey key) => Progression.Title(key.Kind)+(key.Kind <= UpgradeKind.Durability ? " ("+new[] {"дерево","камень","золото","железо","алмаз"}[key.Tier]+")" : "");
        static string Title(LabScenario s) => s.mask == 0 || !string.IsNullOrEmpty(s.cohort) ? s.Title : (s.background == 1 ? "Без: " : "")+string.Join(" + ",Progression.Keys.Where(k => (s.mask & (1 << k.Index)) != 0).Select(SkillName));
        void NewSession()
        {
            if (session?.Running == true) throw new InvalidOperationException("Сначала останови текущий анализ"); Fresh();
            var plan = new LabPlan { mode = mode.index, samples = samples.value, workers = workers.value, seconds = checked(budget.value*60), seed = seed.value,
                passive = passive.value, active = active.value, isolated = mode.index == 0 || mode.index == 3 || isolated.value, background = common.value, diagnostic = diagnostic.value,
                tiers = tierChoices.Select(t=>t.value).ToArray(), customLevels = levels.Select(l=>l.value).ToArray() };
            session?.Dispose();autoExport=false; session = LabSession.Create(NewPath(),plan,manifest); folder.value = session.DirectoryPath;
            SelectFilters(); session.Start(Executable(),manifest); nextRefresh = 0;
        }
        void Load(string path)
        {
            if (session?.Running == true) throw new InvalidOperationException("Сначала останови текущий анализ");
            var loaded = LabSession.Load(path); session?.Dispose(); session = loaded;
            ShowPlan(loaded.Plan); SelectFilters(); Refresh();
        }
        void ShowPlan(LabPlan p)
        {
            mode.index=p.mode;samples.value=p.samples;workers.value=p.workers;budget.value=p.seconds/60;seed.value=p.seed;
            passive.value=p.passive;active.value=p.active;isolated.value=p.isolated;common.value=p.background;diagnostic.value=p.diagnostic;
            for(int i=0;i<5;i++)tierChoices[i].value=p.tiers[i];for(int i=0;i<25;i++)levels[i].value=p.customLevels[i];
        }
        void SelectFilters()
        {
            tier.index = Array.FindIndex(session.Plan.tiers,t=>t); behavior.index = session.Plan.passive ? 0 : 1;
            point.index = 0; background.index = session.Plan.isolated ? 0 : 1;
            buildInfo.text = $"Отчёт: игра {session.Manifest.version} · {session.Manifest.fingerprint.Substring(0,12)} · движок: {manifest.fingerprint.Substring(0,12)} · физика 1/60 с";
        }
        void Refresh() { if (session == null) return; analysis = new LabAnalysis(session); RefreshTable(); }
        void RefreshTable()
        {
            if (analysis == null || table == null || search == null) return;
            visible = analysis.View(tier.index == 5 ? -1 : tier.index,point.index,background.index,behavior.index == 1)
                .Where(x => (order.index == 0 || x.scenario.order == order.index-1) && Title(x.scenario).IndexOf(search.value ?? "",StringComparison.OrdinalIgnoreCase)>=0).ToList();
            Func<LabSummary,double> metric = sorting.index == 1 ? x=>x.Mean(r=>r.depth) : sorting.index == 2 ? x=>analysis.Delta(x) : sorting.index == 3 ? x=>analysis.Cost(x).total : x=>x.Rate;
            visible = visible.OrderByDescending(x => { double v = metric(x); return double.IsNaN(v) ? double.NegativeInfinity : v; }).ToList();
            table.itemsSource = visible; table.Rebuild();
        }
        void ShowMatrix()
        {
            if (analysis == null) return; matrix.Clear(); table.style.display = DisplayStyle.None; matrix.style.display = DisplayStyle.Flex;
            var all = analysis.View(tier.index == 5 ? -1 : tier.index,point.index,background.index,behavior.index==1);
            var keys = Progression.Keys.Where(k => all.Any(x => (x.scenario.mask & (1 << k.Index))!=0)).ToList();
            var header = Row(matrix); Label(header,"Δ взаимодействия, монет/мин",13).style.width = 220;
            foreach (var key in keys) { var l = Label(header,SkillName(key),12); l.style.width = 90; l.style.flexShrink = 0; }
            foreach (var a in keys)
            {
                var row = Row(matrix); var title = Label(row,SkillName(a),13); title.style.width = 220; title.style.flexShrink = 0;
                foreach (var b in keys)
                {
                    var x = a.Index==b.Index ? null : all.FirstOrDefault(s => s.scenario.mask == ((1<<a.Index)|(1<<b.Index)));
                    double delta = x==null ? double.NaN : analysis.Interaction(x);
                    var ci=x==null ? (double.NaN,double.NaN,0) : analysis.InteractionInterval(x);
                    var cell = new Button(() => { if (x!=null) detail.text=analysis.Detail(x); }) { text = double.IsNaN(delta) ? "—" : LabFiles.Number(delta) };
                    bool uncertain=ci.Item3<8 || double.IsNaN(ci.Item1) || ci.Item1<=0 && ci.Item2>=0 || x!=null && analysis.Reliability(x).Contains("мало");
                    cell.tooltip="95%: ["+LabFiles.Number(ci.Item1)+"; "+LabFiles.Number(ci.Item2)+"], n="+ci.Item3+(uncertain ? "; предварительно / мало данных" : "");
                    cell.style.color = Color.white; cell.style.width = 90; cell.style.flexShrink = 0; cell.style.backgroundColor = new StyleColor((Color)(uncertain ? new Color32(58,65,70,255) : delta>=0 ? new Color32(45,100,68,255) : new Color32(115,55,55,255))); row.Add(cell);
                }
            }
        }
        void ShowComparison()
        {
            if (session == null || previous == null) throw new InvalidOperationException("Загрузи текущий и предыдущий отчёты");
            detail.text = LabComparison.Describe(session,previous,tier.index == 5 ? -1 : tier.index,point.index,background.index,behavior.index==1);
        }
        void Export()
        {
            if (session == null) throw new InvalidOperationException("Сначала выполни анализ");
            if (export != null && !export.IsCompleted) throw new InvalidOperationException("Экспорт уже выполняется");
            var snapshot = new LabAnalysis(session); string destination = session.DirectoryPath;
            export = Task.Run(() => snapshot.Export(destination)); status.text = "Экспорт отчёта…";
        }
        void Update()
        {
            if (session == null) return;
            bool wasRunning = session.Running; session.Pump();
            if (cli != null)
            {
                if (wasRunning && !session.Running && !autoExport)
                {
                    autoExport = true;
                    try { new LabAnalysis(session).Export(session.DirectoryPath); Debug.Log("PICKAXEFALL_LAB_CLI_OK: "+session.Status+"; rows="+session.Results.Count+"; branches="+session.CompletedBranches); Application.Quit(session.Status.StartsWith("Ошибка",StringComparison.Ordinal)?1:0); }
                    catch (Exception e) { Debug.LogException(e); Application.Quit(1); }
                }
                return;
            }
            if(wasRunning && !session.Running && !autoExport && session.Status=="Все сценарии завершены"){autoExport=true;Safe(Export);}
            status.text = session.Status;
            if (export != null && export.IsCompleted) { status.text = export.IsFaulted ? "Ошибка экспорта: "+export.Exception.GetBaseException().Message : "CSV и HTML сохранены: "+session.DirectoryPath; export = null; }
            if (Time.unscaledTime >= nextRefresh && (session.Running || wasRunning)) { nextRefresh = Time.unscaledTime+(session.Results.Count>50000?30:4); Refresh(); }
        }
        void OnDestroy() { session?.Dispose(); previous?.Dispose(); if (ownPanel) Destroy(ownPanel); }
    }
}
#endif
