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
        public int schema = 6;
        public string version, unity, fingerprint, project, builtUtc, policy = "world-progression-v2";
        public string balance;
        public double[] roulette;
        public long[] sellPrices;
        public double[] chanceSteps;
        public LabPrice[] prices;
        public long[] profileLevelCoins,profileMiningRequirements;
        public int[] requiredProfileLevels;
        public LabManifest CaptureEconomy()
        {
            var c=string.IsNullOrEmpty(balance)?Progression.Default:JsonUtility.FromJson<BalanceEnvelope>(balance).progression;
            profileLevelCoins=(long[])ProfileRules.LevelCoins.Clone();profileMiningRequirements=ProfileRules.MiningRequirements.SelectMany(x=>x).ToArray();requiredProfileLevels=Enumerable.Range(0,27).Select(ProfileRules.RequiredLevel).ToArray();
            c??=Progression.Default;c.Validate();sellPrices=(long[])c.sellPrices.Clone();
            chanceSteps=Enumerable.Range(0,17).Select(i=>Progression.ChanceStep((UpgradeKind)i,c)).ToArray();
            var snapshot=new List<LabPrice>();
            foreach(var key in Progression.Keys)
            {
                long coins=0;var resources=new long[ResourceInventory.SlotCount];
                for(int level=1;level<=Progression.MaxLevel(key.Kind);level++)
                {
                    var cost=Progression.Cost(key,level,c);coins+=cost.Coins;
                    for(int i=0;i<ResourceInventory.SlotCount;i++)resources[i]+=cost.Resources[i];
                    snapshot.Add(new LabPrice { key=key.Index,level=level,coins=coins,materials=(long[])resources.Clone() });
                }
            }
            prices=snapshot.ToArray();return this;
        }
    }
    [Serializable] public sealed class BalanceEnvelope { public ProgressionBalance progression; }
    [Serializable] public sealed class LabProgress { public long completed,total;public int branches;public double elapsedSeconds;public bool running;public string status; }
    [Serializable] public sealed class LabPrice { public int key,level; public long coins; public long[] materials; }
    [Serializable] public sealed class LabPlan
    {
        public int mode, samples = 8, workers = Math.Min(6, Math.Max(1, Environment.ProcessorCount / 2)), seconds = 900, seed = 8127;
        public string balanceJson;
        public bool[] maps={true,false,false};
        public bool passive = true, active = true, isolated = true, background = true, diagnostic;
        public bool[] tiers = { true, true, true, true, true };
        public int[] customLevels = new int[25];
        public int limit => mode==5?21600:mode == 0 || mode==4 ? 600 : 1800;
        public void Validate(bool checkLevels = true)
        {
            if(maps==null || maps.Length!=3 || !maps.Any(x=>x))throw new ArgumentException("Выбери локацию.");
            if (mode < 0 || mode > 6 || samples < 1 || samples > 100000 || workers < 1 || workers > 32 || seconds < 0 || seconds > 604800 || seed < 1)
                throw new ArgumentException("Недопустимые параметры запуска.");
            if((mode==4||mode==5||mode==6) && (seconds<=0 || seconds>7200))throw new ArgumentException("Аудит прогрессии ограничен двумя часами; выбери 1–120 минут.");
            if (!passive && !active || !isolated && !background || tiers == null || tiers.Length != 5 || !tiers.Any(x => x)) throw new ArgumentException("Выбери кирку, управление и способ сравнения.");
            if (customLevels == null || customLevels.Length != 25) throw new ArgumentException("Нужно 25 уровней.");
            if(checkLevels) new PlayerProfile { upgrades = customLevels }.Validate();
        }
    }
    [Serializable] public sealed class LabScenario
    {
        public string id;
        public MapId map;
        public int tier, point, mask, background, order;
        public bool active,hourly,holdout;
        public int regime,stage,sampleCount,strategy;
        public string cohort;

        public int[] levels;
        public string Family => (map==MapId.Mine?"":$"map{(int)map}-")+$"t{tier}-p{point}-b{background}-a{(active ? 1 : 0)}"+(string.IsNullOrEmpty(cohort)?"":"-"+cohort);
        static readonly string[] ToolTitles={"дерево","камень","золото","железо","алмаз"};
        public string Title=>hourly?LocalTitle:WorldCatalog.Name(map)+" · "+LocalTitle;
        string LocalTitle
        {
            get
            {
                if(cohort=="world")return "Прокачка · "+new[]{"начало","25% ветвей","75% ветвей"}[point];
                if(cohort=="full-max")return "Вся прокачка · максимум";
                if(cohort=="profile")return "Профиль · 6 часов · "+new[]{"Кирки","Приёмы","Добыча"}[strategy]+" · Все маршруты";
                if(hourly)return "Час развития · "+new[] {"Кирки","Приёмы","Добыча"}[strategy];
                string prefix=cohort=="r0"?"Обычная генерация · ":regime==1?"Без верстаков · ":regime==2?"Только ремонт · ":holdout?"Независимые карты · ":"";
                var names = Progression.Keys.Where(k => (mask & (1 << k.Index)) != 0).Select(k => Progression.Title(k.Kind)+(k.Kind<=UpgradeKind.Durability?" · "+ToolTitles[k.Tier]:""));
                return prefix+(mask == 0 ? (background == 0 ? "Без улучшений" : "Вся прокачка") : (background == 1 ? "Без: " : "") + string.Join(" + ", names));
            }
        }
    }
    [Serializable] public sealed class LabJob
    {
        public LabScenario scenario;
        public int seed, limit;
        public bool diagnostic;
        public string balanceJson;
        public string Id => scenario.id + "-" + seed;
    }
    [Serializable] public sealed class LabOutcome { public double weight,depth,seconds; }
    [Serializable] public sealed class LabResult
    {
        public string id, scenario, error;
        public int seed, branches, limit;
        public double seconds, depth, value, blocks, strikes, tools, benches, abilities, censored;
        public double[] resources = new double[ResourceInventory.SlotCount], attempts = new double[17], successes = new double[17];
        public double chests,trials,failedTrials;
        public double[] mapSeconds=new double[3],mapValues=new double[3],mapRuns=new double[3];
        public double wallSeconds,upgradeBenches,restoredWear,lavaWear,energySpent;
        public LabOutcome[] outcomes;
        public BenchVisit[] benchVisits;
        public PurchaseEvent[] purchases;
        public double firstPurchase,runCount,dashOpenedAt,plungeOpenedAt;
        public int[] finalLevels;
        public double[] profileLevelTimes,profileCoinsBlocked,profileMiningBlocked,profileSkillsBlocked;
        public long[] lifetimeMined;
        public int finalProfileLevel,bridgeStone,bridgeIron,portalObsidian;
        public double bridgeCompletedAt=-1,portalCompletedAt=-1;
        public long profileCoinsSpent,requiredSkillCoinsSpent;

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
            string tools=Path.Combine(root,"Assets/PickaxeFall/Resources/Pickaxes");
            if(Directory.Exists(tools))foreach(string art in Directory.GetFiles(tools,"*.png")){files.Add(art);files.Add(art+".meta");}
            foreach (string name in new[] { "ProjectVersion.txt", "Physics2DSettings.asset", "TimeManager.asset" }) files.Add(Path.Combine(root, "ProjectSettings", name));
            files.Sort(StringComparer.Ordinal);
            using var hash = SHA256.Create();
            foreach (string path in files)
            {
                byte[] name=Encoding.UTF8.GetBytes(path.Substring(root.Length).Replace('\\','/')+"\n");
                hash.TransformBlock(name,0,name.Length,null,0);
                byte[] bytes=path.EndsWith(".png",StringComparison.Ordinal)?File.ReadAllBytes(path):Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n","\n"));
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
