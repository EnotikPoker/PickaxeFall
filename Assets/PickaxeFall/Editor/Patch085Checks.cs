using System;
using UnityEditor;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class Patch085Checks
    {
        public static void Validate(GameBalance balance)
        {
            int checks = 0;
            void Check(bool value, string message)
            {
                checks++;
                if (!value) throw new InvalidOperationException("Patch 0.8.5: " + message);
            }

            Check(!string.IsNullOrEmpty(GameVersion.Name) && GameVersion.AndroidVersionCode >= 15, "release identity");
            Check(PlayReleaseConfig.SaveEpoch == 0, "existing progress stays in the same season");
            var maxEarth = new PlayerProfile();
            maxEarth.upgrades[new UpgradeKey(UpgradeKind.EarthDigger).Index] = 20;
            Check(Math.Abs(new RunUpgrades(maxEarth, balance.progression).Chance(UpgradeKind.EarthDigger) - .4) < .00001, "EarthDigger reaches forty percent");
            long earthCoins = 0;
            for (int level = 1; level <= 20; level++) earthCoins += Progression.Cost(new(UpgradeKind.EarthDigger), level, balance.progression).Coins;
            Check(earthCoins < 1500, "EarthDigger remains an affordable utility branch");
            Check(ProfileRules.MiningRequirements[0][(int)BlockKind.Stone] == 300 && ProfileRules.LevelCoins[0] == 250, "profile progression requirements retained");

            // Every purchased ingredient must agree with its own inventory slot.
            foreach (var key in Progression.Keys)
            {
                foreach (int next in new[] { 1, Progression.MaxLevel(key.Kind) })
                {
                    var profile = new PlayerProfile { profileLevel = 5, dashUnlocked = true, plungeUnlocked = true };
                    Array.Fill(profile.revealed, true);
                    profile.upgrades[key.Index] = next - 1;
                    if (key.Kind == UpgradeKind.TeamSize) profile.upgrades[new UpgradeKey(UpgradeKind.TeamChance).Index] = 1;
                    var price = Progression.Cost(key, next, balance.progression);
                    profile.coins = price.Coins;
                    Array.Copy(price.Resources, profile.resources, profile.resources.Length);
                    var bank = new EconomyService(profile, config: balance.progression);
                    Check(bank.CanBuy(key, out _), "exact recipe permits " + key.Kind);
                    foreach (var material in ResourceInventory.Materials)
                    {
                        int slot = (int)material;
                        if (price.Resources[slot] == 0) continue;
                        bank.Profile.resources[slot]--;
                        Check(!bank.CanBuy(key, out _), "missing displayed ingredient blocks " + key.Kind + "/" + material);
                        bank.Profile.resources[slot]++;
                    }
                    Check(bank.TryBuy(key) && bank.Profile.coins == 0, "transaction charges displayed coin price");
                    foreach (long amount in bank.Profile.resources) Check(amount == 0, "transaction charges each displayed ingredient exactly");
                }
            }

            var plunge = Progression.Cost(new(UpgradeKind.PlungeDepth), 2, balance.progression);
            Check(plunge.Resources[(int)BlockKind.SmokyQuartz] == 1 && plunge.Resources[(int)BlockKind.Quartz] == 0, "desert and Nether quartz are distinct");
            for (int tier = 0; tier < 5; tier++) Check(Progression.DurabilityStep(tier, balance.progression) == new[] { 3, 6, 9, 14, 32 }[tier], "per-tier durability bonuses retained");
            Debug.Log("PICKAXEFALL_PATCH_085_OK: " + checks + " checks; exact recipes, resource slots, upgrade costs, EarthDigger and unchanged profile progression.");
        }
    }
}
