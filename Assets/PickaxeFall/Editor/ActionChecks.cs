using System;
using System.IO;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class ActionChecks
    {
        static int checks;
        static void Check(bool value, string message) { checks++; if (!value) throw new Exception("Action audit: " + message); }
        public static void Validate()
        {
            checks = 0;
            var profile = new PlayerProfile(); Check(profile.version == 3 && profile.upgrades.Length == 25, "schema v3");
            for (int i = 0; i < Progression.Keys.Length; i++) Check(Progression.Keys[i].Index == i && Progression.Keys[i].IsValid, "stable upgrade index " + i);
            var run = new RunUpgrades(profile); var energy = new RunEnergy(); energy.Reset(run);
            Check(energy.Maximum == 60 && energy.Value == 60, "initial energy allows one action");
            Check(energy.TrySpend(45) && !energy.TrySpend(60) && energy.Value == 15, "failed action is free");
            energy.Regenerate(10, run); Check(energy.Value == 35 && energy.Earned == 20, "time-based regeneration");
            energy.Regenerate(0, run); energy.Regenerate(-1, run); Check(energy.Value == 35, "invalid time cannot grant energy");
            energy.Regenerate(1000, run); Check(energy.Value == 60 && energy.Earned == 45, "regeneration clamps and records actual gain");
            energy.TrySpend(60);
            for(int i=0;i<1349;i++)energy.Regenerate(1f/60,run);
            Check(!energy.CanSpend(45), "base dash needs at least 22.5 seconds from empty");
            energy.Regenerate(1f/60,run); Check(energy.CanSpend(45), "base dash recharges after 22.5 seconds");
            energy.Ensure(60); Check(energy.Value == 60, "rescue energy");
            foreach (var key in Progression.Keys) profile.upgrades[key.Index] = Progression.MaxLevel(key.Kind);
            run = new RunUpgrades(profile); energy.Reset(run);
            Check(run.MaxEnergy == 100 && Math.Abs(run.EnergyPerSecond - 2.8f) < .001f && Math.Abs(run.EnergyCost(45) - 40.5f) < 0.001f && run.PlungeBlocks == 4 && run.DashSpeed == 13, "bounded maximum action upgrades");
            for (int tier = 1; tier < 5; tier++) Check(run.DurabilityBonus(tier) > run.DurabilityBonus(tier - 1), "different durability steps");
            profile.upgrades[new UpgradeKey(UpgradeKind.EnergyEfficiency).Index] = 1;
            Check(new RunUpgrades(profile).EnergyCost(40) < 40, "first efficiency level actually reduces cost");
            var gesture = new MiningGesture();
            gesture.Begin(Vector2.zero, 0); Check(gesture.End(new Vector2(10, 0), 0.2f, out var kind, out _) && kind == AbilityKind.Plunge, "short tap");
            Vector2[] motions = { Vector2.right * 100, Vector2.left * 100, Vector2.up * 100, Vector2.down * 100 };
            Vector2[] expected = { Vector2.right, Vector2.left, Vector2.down, Vector2.up };
            for (int i = 0; i < motions.Length; i++) { gesture.Begin(Vector2.zero, 0); Check(gesture.End(motions[i], 0.3f, out kind, out var direction) && kind == AbilityKind.Dash && direction == expected[i], "swipe " + i); }
            gesture.Begin(Vector2.zero, 0); Check(!gesture.End(new Vector2(30, 0), 0.2f, out _, out _), "dead zone");
            gesture.Begin(Vector2.zero, 0); Check(!gesture.End(Vector2.zero, 0.4f, out _, out _), "long press");
            gesture.Begin(Vector2.zero, 0); gesture.Move(Vector2.right * 100); Check(!gesture.End(Vector2.zero, 0.2f, out _, out _), "drag back is not a tap");
            gesture.Begin(Vector2.zero, 0); gesture.Cancel(); Check(!gesture.End(Vector2.zero, 0.1f, out _, out _), "cancelled gesture");
            var gate = new ImpactGate(); var a = new Vector2Int(0, 5); var b = new Vector2Int(1, 5);
            Check(gate.TryHit(a, 1, 4, 1.2f, Vector2.up, Vector2.zero), "first impact");
            Check(!gate.TryHit(a, 1, 4, 1.2f, Vector2.up, Vector2.zero), "duplicate collider");
            Check(gate.TryHit(b, 1, 4, 1.2f, Vector2.up, Vector2.zero), "another block in same step has no global cooldown");
            Check(!gate.TryHit(a, 2, 4, 1.2f, Vector2.up, Vector2.zero), "continuous contact is not a strike");
            gate.Exit(a); Check(gate.TryHit(a, 3, 4, 1.2f, Vector2.up, Vector2.zero), "new episode hits immediately");
            gate.Exit(a); Check(!gate.TryHit(a, 4, 0.1f, 1.2f, Vector2.up, Vector2.zero), "weak contact");
            string directory = Path.Combine(Application.temporaryCachePath, "action-migration-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory); string file = Path.Combine(directory, "profile-v2.json");
                var old = new PlayerProfile { version = 2, coins = 900719925474099L, bestDepth = 123, upgrades = new int[18] };
                old.upgrades[0] = 7; old.upgrades[10] = 25; old.resources[6] = 3000000000L;
                string original = JsonUtility.ToJson(old); File.WriteAllText(file, original);
                var store = new ProfileStore(directory); var migrated = store.Load();
                Check(migrated.version == 3 && migrated.upgrades.Length == 25 && migrated.coins == old.coins && migrated.resources[6] == old.resources[6] && migrated.bestDepth == 123, "v2 data preserved");
                Check(migrated.upgrades[0] == 7 && migrated.upgrades[10] == 25 && migrated.upgrades[24] == 0, "old levels kept, new zero");
                Check(File.ReadAllText(file + ".pre-v3.bak") == original, "original migration backup");
                Check(store.Load().coins == old.coins, "migration is idempotent");
                // Valid primary + blocked migration: never substitute the older backup.
                File.WriteAllText(file, original); File.WriteAllText(file + ".bak", JsonUtility.ToJson(new PlayerProfile { coins = 1 }));
                File.Delete(file + ".pre-v3.bak"); Directory.CreateDirectory(file + ".pre-v3.bak");
                bool failed = false; try { store.Load(); } catch (IOException) { failed = true; } catch (UnauthorizedAccessException) { failed = true; }
                Check(failed && File.ReadAllText(file) == original, "failed migration cannot roll progress back");
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            Debug.Log("PICKAXEFALL_ACTION_CHECKS_OK: " + checks + " checks; gestures, impact episodes, energy and v2 migration.");
        }
    }
}
