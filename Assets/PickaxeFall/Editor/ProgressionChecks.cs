using System;
using System.IO;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class ProgressionChecks
    {
        static int assertions;
        static void Check(bool value, string message) { assertions++; if (!value) throw new Exception("Economy audit: " + message); }
        public static void Validate()
        {
            assertions = 0;
            var p = new PlayerProfile(); var economy = new EconomyService(p);
            foreach (var key in Progression.Keys)
            {
                long last = 0;
                for (int n = 1; n <= Progression.MaxLevel(key.Kind); n++)
                {
                    var cost = Progression.Cost(key, n); Check(cost.Coins > last, "coin costs must grow: " + key.Kind); last = cost.Coins;
                    Check(Array.Exists(cost.Resources, value => value > 0), "upgrades require resources");
                }
            }
            Check(Progression.Cost(new(UpgradeKind.TeamChance), 1).Coins == 20 && Progression.Cost(new(UpgradeKind.TeamChance), 3).Coins == 34, "cheap initial prices and exponential rounding");
            Check(!economy.TryBuy(new(UpgradeKind.Damage)) && economy.Profile.coins == 0, "unaffordable purchase is rejected");
            economy.Collect(BlockKind.Stone, 12); economy.Collect(BlockKind.Dirt, 40);
            Check(economy.Profile.coins == 0, "collecting grants no coins");
            Check(economy.TrySell(BlockKind.Dirt, 30) && economy.Profile.coins == 30, "partial sale grants only selected value");
            Check(!economy.TrySell(BlockKind.Dirt, 11) && economy.Profile.resources[0] == 10, "overselling cannot debit resources");
            Check(!economy.TrySell(BlockKind.Stone, 0) && !economy.TrySell(BlockKind.Stone, -1), "invalid quantities rejected");
            Check(economy.TryBuy(new(UpgradeKind.Damage)) && economy.Profile.coins == 0 && economy.Profile.resources[0] == 2 && economy.Profile.resources[1] == 8, "purchase debits exactly one complete recipe");
            var snapshot = new RunUpgrades(economy.Profile); economy.Profile.upgrades[0] = 2;
            Check(snapshot.DamageBonusHalfUnits(0) == 1 && snapshot.DamageBonusHalfUnits(1) == 0, "run snapshot and tier upgrades independent");
            Check(!economy.TryBuy(new(UpgradeKind.TeamSize)), "team size needs team chance");
            Check(economy.TrySellAll() && economy.Profile.coins == 18 && Array.TrueForAll(economy.Profile.resources, v => v == 0), "bulk sale exact and clears all materials");
            Check(!economy.TrySellAll() && economy.Profile.coins == 18, "empty bulk sale cannot duplicate coins");
            var failureProfile = new PlayerProfile { coins = 1000 }; Array.Fill(failureProfile.resources, 100L);
            var failing = new EconomyService(failureProfile, _ => throw new IOException("test storage failure"));
            Check(!failing.TryBuy(new(UpgradeKind.Damage)) && failing.Profile.coins == 1000 && failing.Profile.upgrades[0] == 0, "save failure rolls purchase back");
            Check(!failing.TrySell(BlockKind.Stone, 3) && failing.Profile.resources[1] == 100 && failing.Profile.coins == 1000, "save failure rolls sale back");
            var huge = new PlayerProfile { coins = long.MaxValue }; huge.resources[6] = 1;
            var overflow = new EconomyService(huge);
            Check(!overflow.TrySell(BlockKind.Diamond, 1) && overflow.Profile.resources[6] == 1, "coin overflow rejected atomically");
            huge.resources[6] = long.MaxValue;
            Check(!overflow.TrySellAll() && overflow.Profile.coins == long.MaxValue, "bulk overflow rejected");
            Check(!GameHud.TrySaleQuantity("1.5", 10, out _) && !GameHud.TrySaleQuantity("-2", 10, out _) && !GameHud.TrySaleQuantity("9999999999999999999999", 10, out _), "numeric input cannot accept fractions, negatives or overflow");
            Check(GameHud.TrySaleQuantity(" 5 ", 10, out long count) && count == 5, "valid numeric input");
            foreach (var key in Progression.Keys) p.upgrades[key.Index] = Progression.MaxLevel(key.Kind);
            var maxSnapshot = new RunUpgrades(p);
            Check(maxSnapshot.TeamSize == 5 && Math.Abs(maxSnapshot.Chance(UpgradeKind.TeamChance) - 0.1) < 1e-8, "team cap");
            Check(Math.Abs(maxSnapshot.Chance(UpgradeKind.LastChance) - 0.2) < 1e-8 && Math.Abs(maxSnapshot.Chance(UpgradeKind.Fortune) - 0.25) < 1e-8, "rescue and fortune caps");
            Check(Math.Abs(maxSnapshot.Chance(UpgradeKind.DoubleBench) - 0.25) < 1e-8 && Math.Abs(maxSnapshot.Chance(UpgradeKind.DoubleBlast) - 0.25) < 1e-8, "25 percent workshop and blast caps");
            Check(Math.Abs(maxSnapshot.Chance(UpgradeKind.TntFind) - 0.2) < 1e-8 && Math.Abs(maxSnapshot.Chance(UpgradeKind.Careful) - 0.2) < 1e-8, "20 percent ore TNT and careful caps");
            Check(Progression.Cost(new(UpgradeKind.Damage), 10).Resources[0] == 0, "late progress does not require farming surface dirt");
            float oldVolume = AudioListener.volume; int oldFps = Application.targetFrameRate;
            var settings = new GameSettings(true); settings.SetVolume(2); settings.SetFrameRate(30); settings.SetReducedEffects(true);
            Check(settings.Volume == 1 && settings.FrameRate == 30 && settings.ReducedEffects, "settings clamp and apply");
            settings.Reset(); Check(settings.Volume == 0.8f && settings.FrameRate == 60 && !settings.ReducedEffects, "settings defaults");
            AudioListener.volume = oldVolume; Application.targetFrameRate = oldFps;
            foreach (var kind in new[] { UpgradeKind.TeamChance, UpgradeKind.TntFind, UpgradeKind.DoubleBench, UpgradeKind.DoubleBlast, UpgradeKind.Careful, UpgradeKind.LastChance })
            {
                double chance = maxSnapshot.Chance(kind); var random = new System.Random(8127 + (int)kind); int hits = 0;
                for (int i = 0; i < 100000; i++) if (random.NextDouble() < chance) hits++;
                Check(Math.Abs(hits / 100000d - chance) < 0.006, "probability audit " + kind);
            }
            string dir = Path.Combine(Application.temporaryCachePath, "profile-audit-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new ProfileStore(dir); var first = new PlayerProfile { coins = 123, bestDepth = 42 }; first.resources[6] = 9; first.upgrades[0] = 3;
                first.resources[1] = 3000000000L;
                store.Save(first); var loaded = store.Load();
                Check(loaded.coins == 123 && loaded.resources[6] == 9 && loaded.upgrades[0] == 3 && loaded.bestDepth == 42, "profile round trip");
                Check(loaded.resources[1] == 3000000000L, "64-bit inventory JSON round trip");
                File.WriteAllText(Path.Combine(dir, "profile-v2.json"), "corrupted"); var recovered = store.Load();
                Check(recovered.coins == 123, "backup recovery");
                recovered.coins = 124;
                store.Save(recovered); Check(store.Load().resources[6] == 9 && store.Load().coins == 124, "save after recovery");
                var backup = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(Path.Combine(dir, "profile-v2.json.bak"))); backup.Validate();
                Check(backup.coins == 123, "recovery transaction preserves previous valid backup");
                File.WriteAllText(Path.Combine(dir, "profile-v2.json"), "{}");
                Check(store.Load().coins == 123, "incomplete JSON cannot silently reset the player");
                int migrations = 0;
                var legacy = new ProfileStore(Path.Combine(dir, "legacy"), () => { migrations++; return new PlayerProfile { coins = 456, bestDepth = 17 }; });
                Check(legacy.Load().coins == 456 && legacy.Load().bestDepth == 17 && migrations == 1, "legacy data imported once without overwriting new saves");
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            var font = Resources.Load<Font>("Fonts/Monocraft-Bold");
            Check(font && font.HasCharacter('Ж') && font.HasCharacter('я') && font.HasCharacter('ё'), "bold Cyrillic glyph coverage");
            Debug.Log("PICKAXEFALL_PROGRESSION_OK: " + assertions + " checks; prices, transactions, save recovery, numeric input, upgrade caps and RNG.");
        }
    }
}
