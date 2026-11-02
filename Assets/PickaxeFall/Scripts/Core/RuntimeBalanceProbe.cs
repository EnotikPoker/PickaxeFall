#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PickaxeFall
{
    public class RuntimeBalanceProbe : MonoBehaviour
    {
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string message, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Error) Application.Quit(1); }
        bool active;
        string outputDirectory = "/tmp";
        float nextAction;
        void BeginPolicy() { nextAction = Time.time + 2; }
        void ActivePolicy(GameController game)
        {
            if (!active || Time.time < nextAction || !game.SelectedTool.IsAlive) return;
            Vector2 position = game.SelectedTool.Body.position, direction = Vector2.down; int best = 0;
            int centerX = Mathf.RoundToInt(position.x), centerRow = Mathf.FloorToInt(-position.y);
            for (int row = centerRow - 3; row <= centerRow + 3; row++) for (int x = centerX - 3; x <= centerX + 3; x++)
            {
                var cell = game.World.CellAt(x, row); if (!cell) continue;
                Vector2 delta = (Vector2)cell.transform.position - position;
                if (delta.magnitude < 0.5f || delta.magnitude > 3.2f) continue;
                int score = cell.Kind == BlockKind.Workbench ? 100 : Progression.IsOre(cell.Kind) ? 10 + (int)cell.Kind * 4 : 0;
                if (score > best && Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) { best = score; direction = new Vector2(Mathf.Sign(delta.x), 0); }
            }
            bool used = game.TryUseAbility(best > 0 ? AbilityKind.Dash : AbilityKind.Plunge, direction);
            nextAction = Time.time + (used ? 10 : 0.2f);
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = -1;
            string outputArg = Array.Find(Environment.GetCommandLineArgs(), a => a.StartsWith("--probe-output-dir="));
            if (outputArg != null) outputDirectory = outputArg.Substring(19);
            Directory.CreateDirectory(outputDirectory);
            active = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--active-controls");
            if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "--economy-probe")) { yield return EconomyTrace(); yield break; }
            var game = GetComponent<GameController>();
            var csv = new List<string> { "profile,tier,seed,tools,seconds,depth,potentialSaleValue,blocks,strikes,benches,dirt,stone,coal,copper,iron,gold,diamond,deepstone" };
            int[] seeds = { 8127, 19073, 40211, 73417, 120011, 88191, 55513, 90913 };
            string seedArgument = Array.Find(Environment.GetCommandLineArgs(), a => a.StartsWith("--balance-seed="));
            int singleSeed = 0;
            if (seedArgument != null && (!int.TryParse(seedArgument.Substring(15), out singleSeed) || singleSeed <= 0)) throw new Exception("Invalid balance seed.");
            if (singleSeed > 0) seeds = new[] { singleSeed };
            string output = Path.Combine(outputDirectory, "pickaxefall-action-balance-" + (singleSeed > 0 ? singleSeed.ToString() + "-" : "") + (active ? "active" : "idle") + ".csv");
            bool quick = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--quick-balance");
            bool legendary = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--legendary-balance");
            foreach (int profile in legendary ? new[] { 2 } : quick ? new[] { 0 } : new[] { 0, 1, 2 })
            {
                foreach (var key in Progression.Keys) game.Economy.Profile.upgrades[key.Index] = profile == 0 ? 0 : profile == 1 ? Math.Min(5, Progression.MaxLevel(key.Kind)) : Progression.MaxLevel(key.Kind);
                for (int tier = 0; tier < 5; tier++)
                {
                    if (legendary && tier != 4) continue;
                    if (profile > 0 && tier != 0 && tier != 1 && tier != 4) continue;
                    int count = profile == 2 ? 5 : 1;
                    for (int si = 0; si < Math.Min(seeds.Length, profile == 0 && !quick ? 8 : 3); si++)
                    {
                        int seed = seeds[si]; game.BeginProbeRun(tier, seed, count); Time.timeScale = 8; float start = Time.time; BeginPolicy();
                        float timeout = profile == 2 ? 1800 : 600;
                        while (game.Phase == RunPhase.Mining && Time.time - start < timeout) { ActivePolicy(game); yield return null; }
                        if (game.Phase == RunPhase.Mining) throw new Exception("Run did not end within " + timeout + " simulated seconds.");
                        float duration = Time.time - start; int strikes = 0; foreach (var tool in game.Pickaxes) strikes += tool.TotalStrikes;
                        string line = string.Join(",", profile, tier, seed, count, duration.ToString("F2", CultureInfo.InvariantCulture), game.Depth, game.PotentialSaleValue,
                            game.BlocksBroken, strikes, game.WorkbenchesUsed, string.Join(",", ResourceCounts(game)));
                        csv.Add(line); Debug.Log("BALANCE_RUN: " + line); Time.timeScale = 8;
                        File.WriteAllLines(output, csv);
                        while (game.Phase == RunPhase.Breaking) yield return null;
                    }
                }
            }
            File.WriteAllLines(output, csv); Debug.Log("PICKAXEFALL_BALANCE_PROBE_OK: " + (csv.Count - 1) + " physical runs."); Application.Quit(0);
        }
        static string[] ResourceCounts(GameController game) { var values = new string[8]; for (int i = 0; i < 8; i++) values[i] = game.OreCounts[i].ToString(); return values; }
        static bool TradeAfterRun(GameController game, UpgradeKey[] targets, ref int target, int maximum)
        {
            var keys = new List<UpgradeKey>(); var positions = new List<int>();
            for (int i = 0; i < targets.Length && keys.Count < maximum; i++)
            {
                var key = targets[(target + i) % targets.Length];
                if (game.Economy.Profile.Level(key) < Progression.MaxLevel(key.Kind)) { keys.Add(key); positions.Add(target + i + 1); }
            }
            if (keys.Count == 0) return false;
            int count = 1; var reserve = new long[8];
            for (int n = keys.Count; n >= 1; n--)
            {
                Array.Clear(reserve, 0, 8); long coins = 0, available = game.Wallet; bool materials = true;
                for (int k = 0; k < n; k++)
                {
                    var cost = Progression.Cost(keys[k], game.Economy.Profile.Level(keys[k]) + 1); coins += cost.Coins;
                    for (int i = 0; i < 8; i++) reserve[i] += cost.Resources[i];
                }
                for (int i = 0; i < 8; i++)
                {
                    long surplus = game.Inventory[(BlockKind)i] - reserve[i];
                    if (surplus < 0) materials = false; else available += surplus * Progression.SellPrices[i];
                }
                if (materials && available >= coins || n == 1) { count = n; break; }
            }
            for (int i = 0; i < 8; i++)
            {
                long surplus = Math.Max(0, game.Inventory[(BlockKind)i] - reserve[i]);
                if (surplus > 0 && !game.Economy.TrySell((BlockKind)i, surplus)) throw new Exception(game.Economy.LastError);
            }
            bool bought = false;
            for (int i = 0; i < count; i++)
            {
                if (!game.Economy.CanBuy(keys[i], out _)) break;
                if (!game.Economy.TryBuy(keys[i])) throw new Exception(game.Economy.LastError);
                bought = true; target = positions[i];
            }
            return bought;
        }
        IEnumerator EconomyTrace()
        {
            var game = GetComponent<GameController>();
            Array.Clear(game.Economy.Profile.upgrades, 0, game.Economy.Profile.upgrades.Length); Array.Clear(game.Economy.Profile.resources, 0, 8); game.Economy.Profile.coins = 0; game.Economy.Profile.bestDepth = 0;
            var targets = new[] { new UpgradeKey(UpgradeKind.Damage, 0), new UpgradeKey(UpgradeKind.Durability, 0), new UpgradeKey(UpgradeKind.Damage, 1), new UpgradeKey(UpgradeKind.Durability, 1), new UpgradeKey(UpgradeKind.EnergyCharge), new UpgradeKey(UpgradeKind.Fortune), new UpgradeKey(UpgradeKind.EnergyEfficiency), new UpgradeKey(UpgradeKind.TeamChance) };
            string seedArg = Array.Find(Environment.GetCommandLineArgs(), a => a.StartsWith("--economy-seed="));
            int economySeed = seedArg == null ? 190738127 : int.Parse(seedArg.Substring(15));
            int maxPurchases = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--single-buy") ? 1 : 3;
            string economyOutput = Path.Combine(outputDirectory, "pickaxefall-action-economy-" + economySeed + "-" + (active ? "active" : "idle") + "-buys" + maxPurchases + ".csv");
            var random = new System.Random(economySeed); int target = 0, run = 0; float elapsed = 0, firstPurchase = -1;
            var csv = new List<string> { "run,elapsedSeconds,tier,runSeconds,depth,saleValue,wallet,woodDamage,woodDurability,stoneDamage,stoneDurability,teamChanceLevel,bestDepth,energyCharge,fortune,efficiency,abilities,energyEarned,censored" };
            while (elapsed < 3600)
            {
                if (3600 - elapsed <= 8) { elapsed = 3600; break; }
                int tier = game.Balance.Roll(random), seed = random.Next(1, int.MaxValue);
                float remainingTime = 3600 - elapsed - 8;
                game.BeginProbeRun(tier, seed); Time.timeScale = 8; float start = Time.time; BeginPolicy();
                while (game.Phase == RunPhase.Mining && Time.time - start < Mathf.Min(1200, remainingTime)) { ActivePolicy(game); yield return null; }
                bool censored = game.Phase == RunPhase.Mining && Time.time - start >= remainingTime;
                if (censored) game.FinishRun();
                else if (game.Phase == RunPhase.Mining) throw new Exception("Economy run timeout");
                float duration = Time.time - start; elapsed = Mathf.Min(3600, elapsed + duration + 8); Time.timeScale = 8;
                while (game.Phase == RunPhase.Breaking) yield return null;
                bool bought = !censored && TradeAfterRun(game, targets, ref target, maxPurchases);
                if (bought && firstPurchase < 0) firstPurchase = elapsed;
                string line = string.Join(",", ++run, elapsed.ToString("F2", CultureInfo.InvariantCulture), tier, duration.ToString("F2", CultureInfo.InvariantCulture), game.Depth, game.PotentialSaleValue,
                    game.Wallet, game.Economy.Profile.upgrades[0], game.Economy.Profile.upgrades[5], game.Economy.Profile.upgrades[1], game.Economy.Profile.upgrades[6], game.Economy.Profile.Level(new(UpgradeKind.TeamChance)), game.BestDepth, game.Economy.Profile.Level(new(UpgradeKind.EnergyCharge)), game.Economy.Profile.Level(new(UpgradeKind.Fortune)), game.Economy.Profile.Level(new(UpgradeKind.EnergyEfficiency)), game.Benefits.Abilities, game.Energy.Earned.ToString("F1", CultureInfo.InvariantCulture), censored ? 1 : 0);
                csv.Add(line); File.WriteAllLines(economyOutput, csv); Debug.Log("ECONOMY_RUN: " + line);
            }
            File.WriteAllLines(economyOutput, csv);
            Debug.Log("PICKAXEFALL_ECONOMY_PROBE_OK: firstPurchaseSeconds=" + firstPurchase + "; runs=" + run + "; elapsed=" + elapsed); Application.Quit(0);
        }
    }
}
#endif
