#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PickaxeFall.BalanceLab
{
    [Serializable] public sealed class LabManifest
    {
        public int schema = 2;
        public string version, unity, fingerprint, project, builtUtc, policy = "active-10s-v1";
        public string balance;
        public double[] roulette;
        public long[] sellPrices;
        public double[] chanceSteps;
        public LabPrice[] prices;
        public LabManifest CaptureEconomy()
        {
            sellPrices=(long[])Progression.SellPrices.Clone();
            chanceSteps=Enumerable.Range(0,17).Select(i=>Progression.ChanceStep((UpgradeKind)i)).ToArray();
            var snapshot=new List<LabPrice>();
            foreach(var key in Progression.Keys)
            {
                long coins=0;var resources=new long[8];
                for(int level=1;level<=Progression.MaxLevel(key.Kind);level++)
                {
                    var cost=Progression.Cost(key,level);coins+=cost.Coins;
                    for(int i=0;i<8;i++)resources[i]+=cost.Resources[i];
                    snapshot.Add(new LabPrice { key=key.Index,level=level,coins=coins,materials=(long[])resources.Clone() });
                }
            }
            prices=snapshot.ToArray();return this;
        }
    }
    [Serializable] public sealed class LabPrice { public int key,level; public long coins; public long[] materials; }
    [Serializable] public sealed class LabPlan
    {
        public int mode, samples = 8, workers = Math.Min(6, Math.Max(1, Environment.ProcessorCount / 2)), seconds = 900, seed = 8127;
        public bool passive = true, active = true, isolated = true, background = true, diagnostic;
        public bool[] tiers = { true, true, true, true, true };
        public int[] customLevels = new int[25];
        public int limit => mode == 0 ? 600 : 1800;
        public void Validate(bool checkLevels = true)
        {
            if (mode < 0 || mode > 3 || samples < 1 || samples > 100000 || workers < 1 || workers > 32 || seconds < 0 || seconds > 604800 || seed < 1)
                throw new ArgumentException("Недопустимые параметры запуска.");
            if (!passive && !active || !isolated && !background || tiers == null || tiers.Length != 5 || !tiers.Any(x => x)) throw new ArgumentException("Выбери кирку, управление и способ сравнения.");
            if (customLevels == null || customLevels.Length != 25) throw new ArgumentException("Нужно 25 уровней.");
            if(checkLevels) new PlayerProfile { upgrades = customLevels }.Validate();
        }
    }
    [Serializable] public sealed class LabScenario
    {
        public string id;
        public int tier, point, mask, background, order;
        public bool active;
        public int[] levels;
        public string Family => $"t{tier}-p{point}-b{background}-a{(active ? 1 : 0)}";
        public string Title
        {
            get
            {
                var names = Progression.Keys.Where(k => (mask & (1 << k.Index)) != 0).Select(k => Progression.Title(k.Kind));
                return mask == 0 ? (background == 0 ? "Без улучшений" : "Вся прокачка") : (background == 1 ? "Без: " : "") + string.Join(" + ", names);
            }
        }
    }
    [Serializable] public sealed class LabJob
    {
        public LabScenario scenario;
        public int seed, limit;
        public bool diagnostic;
        public string Id => scenario.id + "-" + seed;
    }
    [Serializable] public sealed class LabResult
    {
        public string id, scenario, error;
        public int seed, branches;
        public double seconds, depth, value, blocks, strikes, tools, benches, abilities, censored;
        public double[] resources = new double[8], attempts = new double[17], successes = new double[17];
        public double wallSeconds;
    }
    [Serializable] public sealed class ScenarioList { public List<LabScenario> scenarios = new(); }
    public static class LabFiles
    {
        public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        public static string Number(double x) => double.IsNaN(x) || double.IsInfinity(x) ? "—" : x.ToString("0.##", Invariant);
        public static string Fingerprint(string root)
        {
            var files = Directory.GetFiles(Path.Combine(root, "Assets/PickaxeFall"), "*.cs", SearchOption.AllDirectories).ToList();
            files.Add(Path.Combine(root, "Assets/PickaxeFall/Data/DefaultBalance.asset"));
            foreach (string name in new[] { "ProjectVersion.txt", "Physics2DSettings.asset", "TimeManager.asset" }) files.Add(Path.Combine(root, "ProjectSettings", name));
            files.Sort(StringComparer.Ordinal);
            using var hash = SHA256.Create();
            foreach (string path in files)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(path.Substring(root.Length).Replace('\\','/') + "\n" + File.ReadAllText(path).Replace("\r\n", "\n"));
                hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
        }
        public static void Write(string path, object value) => Atomic(path, JsonUtility.ToJson(value, true));
        public static void Atomic(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path + ".tmp", text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);
        }
        public static T Read<T>(string path) => JsonUtility.FromJson<T>(File.ReadAllText(path));
        public static string Arg(string key)
        {
            string prefix = "--" + key + "=";
            return Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith(prefix, StringComparison.Ordinal))?.Substring(prefix.Length);
        }
    }
}
#endif
