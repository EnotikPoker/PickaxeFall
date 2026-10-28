#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PickaxeFall
{
    // Measures real collision-driven runs without replacing physics with a depth formula.
    public class RuntimeBalanceProbe : MonoBehaviour
    {
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error) Application.Quit(1); }

        IEnumerator Start()
        {
            var game = GetComponent<GameController>();
            var csv = new List<string> { "tier,seed,seconds,depth,coins,blocks,strikes,benches,finalTier" };
            int[] seeds = { 8127, 19073, 40211, 73417, 120011, 88191, 55513, 90913 };
            Application.targetFrameRate = -1;
            for (int tier = 0; tier < game.Balance.pickaxes.Length; tier++)
            {
                var seconds = new List<float>(); var depths = new List<int>(); var coins = new List<int>();
                foreach (int seed in seeds)
                {
                    game.BeginProbeRun(tier, seed); Time.timeScale = 8;
                    float start = Time.time;
                    while (game.Phase == RunPhase.Mining && Time.time - start < 90) yield return null;
                    if (game.Phase == RunPhase.Mining) throw new Exception("A starter run exceeded 90 simulated seconds.");
                    float duration = Time.time - start;
                    seconds.Add(duration); depths.Add(game.Depth); coins.Add(game.Coins);
                    string record = string.Join(",", tier, seed, duration.ToString("F2", CultureInfo.InvariantCulture),
                        game.Depth, game.Coins, game.BlocksBroken, game.Pickaxe.TotalStrikes, game.WorkbenchesUsed, game.Pickaxe.Tier);
                    csv.Add(record); Debug.Log("BALANCE_RUN: " + record);
                    Time.timeScale = 8;
                    while (game.Phase == RunPhase.Breaking) yield return null;
                }
                seconds.Sort(); depths.Sort(); coins.Sort();
                Debug.Log($"BALANCE_SUMMARY: tier={tier} seconds={seconds[0]:F1}/{seconds[4]:F1}/{seconds[7]:F1} depth={depths[0]}/{depths[4]}/{depths[7]} coins={coins[0]}/{coins[4]}/{coins[7]} (min/median/max)");
            }
            File.WriteAllLines("/tmp/pickaxefall-balance.csv", csv);
            Time.timeScale = 1;
            Debug.Log("PICKAXEFALL_BALANCE_PROBE_OK: 40 complete physical runs; /tmp/pickaxefall-balance.csv");
            Application.Quit(0);
        }
    }
}
#endif
