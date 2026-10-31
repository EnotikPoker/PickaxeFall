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
        IEnumerator Start()
        {
            Application.targetFrameRate = -1;
            if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "--economy-probe")) { yield return EconomyTrace(); yield break; }
            var game = GetComponent<GameController>();
            var csv = new List<string> { "profile,tier,seed,tools,seconds,depth,potentialSaleValue,blocks,strikes,benches,dirt,stone,coal,copper,iron,gold,diamond,deepstone" };
            int[] seeds = { 8127, 19073, 40211, 73417, 120011, 88191, 55513, 90913 };
            foreach (int profile in new[] { 0, 1, 2 })
            {
                foreach (var key in Progression.Keys) game.Economy.Profile.upgrades[key.Index] = profile == 0 ? 0 : profile == 1 ? Math.Min(5, Progression.MaxLevel(key.Kind)) : Progression.MaxLevel(key.Kind);
                for (int tier = 0; tier < 5; tier++)
                {
                    if (profile > 0 && tier != 0 && tier != 1 && tier != 4) continue;
                    int count = profile == 2 ? 5 : 1;
                    for (int si = 0; si < (profile == 0 ? 8 : 3); si++)
                    {
                        int seed = seeds[si]; game.BeginProbeRun(tier, seed, count); Time.timeScale = 8; float start = Time.time;
                        while (game.Phase == RunPhase.Mining && Time.time - start < 600) yield return null;
                        if (game.Phase == RunPhase.Mining) throw new Exception("Run did not end within 600 simulated seconds.");
                        float duration = Time.time - start; int strikes = 0; foreach (var tool in game.Pickaxes) strikes += tool.TotalStrikes;
                        string line = string.Join(",", profile, tier, seed, count, duration.ToString("F2", CultureInfo.InvariantCulture), game.Depth, game.PotentialSaleValue,
                            game.BlocksBroken, strikes, game.WorkbenchesUsed, string.Join(",", ResourceCounts(game)));
                        csv.Add(line); Debug.Log("BALANCE_RUN: " + line); Time.timeScale = 8;
                        while (game.Phase == RunPhase.Breaking) yield return null;
                    }
                }
            }
            File.WriteAllLines("/tmp/pickaxefall-progression-balance.csv", csv); Debug.Log("PICKAXEFALL_BALANCE_PROBE_OK: " + (csv.Count - 1) + " physical runs."); Application.Quit(0);
        }
        static string[] ResourceCounts(GameController game) { var values = new string[8]; for (int i = 0; i < 8; i++) values[i] = game.OreCounts[i].ToString(); return values; }
        IEnumerator EconomyTrace()
        {
            var game = GetComponent<GameController>();
            Array.Clear(game.Economy.Profile.upgrades, 0, 18); Array.Clear(game.Economy.Profile.resources, 0, 8); game.Economy.Profile.coins = 0;
            var targets = new[] { new UpgradeKey(UpgradeKind.Damage, 0), new UpgradeKey(UpgradeKind.Durability, 0), new UpgradeKey(UpgradeKind.Damage, 1), new UpgradeKey(UpgradeKind.Durability, 1), new UpgradeKey(UpgradeKind.TeamChance) };
            var random = new System.Random(190738127); int target = 0, run = 0; float elapsed = 0, firstPurchase = -1;
            var csv = new List<string> { "run,elapsedSeconds,tier,runSeconds,depth,saleValue,wallet,woodDamage,woodDurability,stoneDamage,stoneDurability,teamChanceLevel" };
            while (elapsed < 3600)
            {
                int tier = game.Balance.Roll(random), seed = random.Next(1, int.MaxValue);
                game.BeginProbeRun(tier, seed); Time.timeScale = 8; float start = Time.time;
                while (game.Phase == RunPhase.Mining && Time.time - start < 600) yield return null;
                if (game.Phase == RunPhase.Mining) throw new Exception("Economy run timeout");
                float duration = Time.time - start; elapsed += duration + 8; Time.timeScale = 8;
                while (game.Phase == RunPhase.Breaking) yield return null;
                UpgradeKey next = targets[target % targets.Length]; int level = game.Economy.Profile.Level(next);
                if (level < Progression.MaxLevel(next.Kind))
                {
                    var cost = Progression.Cost(next, level + 1);
                    for (int i = 0; i < 8; i++)
                    {
                        long surplus = Math.Max(0, game.Inventory[(BlockKind)i] - cost.Resources[i]);
                        if (surplus > 0 && !game.Economy.TrySell((BlockKind)i, surplus)) throw new Exception(game.Economy.LastError);
                    }
                    if (game.Economy.TryBuy(next)) { if (firstPurchase < 0) firstPurchase = elapsed; target++; }
                }
                else target++;
                string line = string.Join(",", ++run, elapsed.ToString("F2", CultureInfo.InvariantCulture), tier, duration.ToString("F2", CultureInfo.InvariantCulture), game.Depth, game.PotentialSaleValue,
                    game.Wallet, game.Economy.Profile.upgrades[0], game.Economy.Profile.upgrades[5], game.Economy.Profile.upgrades[1], game.Economy.Profile.upgrades[6], game.Economy.Profile.Level(new(UpgradeKind.TeamChance)));
                csv.Add(line); Debug.Log("ECONOMY_RUN: " + line);
            }
            File.WriteAllLines("/tmp/pickaxefall-economy-trace.csv", csv);
            Debug.Log("PICKAXEFALL_ECONOMY_PROBE_OK: firstPurchaseSeconds=" + firstPurchase + "; runs=" + run + "; elapsed=" + elapsed); Application.Quit(0);
        }
    }
}
#endif
