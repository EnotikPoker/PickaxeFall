using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class JournalReleaseChecks
    {
        public static void Validate(GameBalance balance)
        {
            int checks = 0;
            void Check(bool ok, string message) { checks++; if (!ok) throw new InvalidOperationException("Journal: " + message); }
            const long today = 20782L * DailyTasks.DayMilliseconds;
            for (int level = 1; level <= 5; level++)
            for (int day = 0; day < 365; day++)
            {
                var p = new PlayerProfile { profileLevel = level };
                DailyTasks.Ensure(p, today + day * DailyTasks.DayMilliseconds);
                p.Validate();
                Check(p.journal.tasks.Length == 3 && p.journal.tasks.Select(t => t.goal).Distinct().Count() == 3, "three distinct achievable tasks");
                Check(p.journal.tasks.All(t => t.goal != DailyGoal.MineResource || t.resource == BlockKind.Stone), "new players never need locked maps");
                string before = JsonUtility.ToJson(p.journal);
                Check(!DailyTasks.Ensure(p, today + day * DailyTasks.DayMilliseconds + 3600000) && before == JsonUtility.ToJson(p.journal), "same-day tasks stable");
                Check(!DailyTasks.Ensure(p, today - 1000) && before == JsonUtility.ToJson(p.journal), "clock rollback preserves claims");
                var clone = p.Copy();
                clone.journal.tasks[0].progress = 1;
                Check(p.journal.tasks[0].progress == 0, "transaction copy does not alias tasks");
            }
            var profile = new PlayerProfile { coins = 123 };
            long clock = today;
            bool failSave = false;
            PlayerProfile saved = null;
            var bank = new EconomyService(profile, p => { if (failSave) throw new IOException("fixture"); saved = p.Copy(); }, config: balance.progression) { JournalTime = () => clock };
            bank.RefreshDailyTasks();
            var task = profile.journal.tasks[0];
            for (int i = 0; i < task.target; i++) bank.RecordMined(BlockKind.Stone);
            Check(task.progress == task.target, "actual mined blocks advance task");
            long coins = bank.Profile.coins, dayId = bank.Profile.journal.day;
            failSave = true;
            Check(!bank.TryClaimDaily(0, dayId) && bank.Profile.coins == coins && !task.claimed, "failed persistence cannot consume reward");
            failSave = false;
            Check(bank.TryClaimDaily(0, dayId) && bank.Profile.coins == coins + task.reward && saved.journal.tasks[0].claimed, "claim committed with coins");
            Check(!bank.TryClaimDaily(0, dayId), "duplicate claim rejected");
            Check(saved.journal.dailyRewardsClaimed == 1, "one statistic per reward");
            clock += DailyTasks.DayMilliseconds;
            Check(!bank.TryClaimDaily(1, dayId) && bank.Profile.journal.day == dayId + 1 && bank.Profile.journal.tasks.All(t => t.progress == 0 && !t.claimed), "midnight rejects stale button");
            var oreTask = new DailyTask { goal = DailyGoal.MineOre, target = 3, reward = 1 };
            bank.Profile.journal.tasks[1] = oreTask;
            bank.RecordMined(BlockKind.Stone); Check(oreTask.progress == 0, "stone is not ore");
            bank.Collect(BlockKind.Coal, 25); Check(oreTask.progress == 0, "chest and bonus collection is not mining");
            bank.RecordMined(BlockKind.Coal); Check(oreTask.progress == 1, "ore block counted once");
            bank.Profile.journal.tasks[2] = new DailyTask { goal = DailyGoal.SellCoins, target = 100000, reward = 10 };
            long sale = balance.progression.sellPrices[(int)BlockKind.Coal];
            Check(bank.TrySell(BlockKind.Coal, 2) && bank.Profile.journal.soldCoins == 2 * sale && bank.Profile.journal.tasks[2].progress == 2 * sale, "sale totals and daily goal agree");
            var restored = ProfileStore.Parse(ProfileStore.Serialize(bank.Profile));
            Check(restored.journal.tasks[2].progress == bank.Profile.journal.tasks[2].progress && restored.journal.soldCoins == bank.Profile.journal.soldCoins, "journal save roundtrip");
            foreach (var kind in ResourceInventory.Materials)
            {
                Check(Enum.GetValues(typeof(MapId)).Cast<MapId>().Any(m => MineralCatalog.InMap(kind, m)), "every material has a catalog page");
                Check(!MineralCatalog.Found(new PlayerProfile(), kind), "unknown hidden");
                var found = new PlayerProfile(); found.discovered[(int)kind] = true;
                Check(MineralCatalog.Found(found, kind) && MineralCatalog.FoundCount(found) == 1, "sold discoveries retained");
            }
            var legacy = new PlayerProfile { version = 8, coins = 987, bestDepth = 73, journal = null };
            legacy.mapRecords[0] = 73; legacy.resources[(int)BlockKind.Quartz] = 25; legacy.lifetimeMined[(int)BlockKind.Stone] = 456;
            string dir = Path.Combine(Path.GetTempPath(), "journal-migration-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try
            {
                string text = JsonUtility.ToJson(legacy), path = Path.Combine(dir, "profile-v2.json"); File.WriteAllText(path, text);
                var migrated = new ProfileStore(dir).Load();
                Check(migrated.version == 9 && migrated.coins == 987 && migrated.bestDepth == 73 && migrated.resources[(int)BlockKind.Quartz] == 25 && migrated.lifetimeMined[(int)BlockKind.Stone] == 456 && migrated.journal.completedRuns == 0, "v8 migration preserves progress without inventing history");
                Check(File.ReadAllText(path + ".pre-v9.bak") == text, "migration retains exact backup");
            }
            finally { Directory.Delete(dir, true); }
            Debug.Log("PICKAXEFALL_JOURNAL_DATA_OK: " + checks + " checks; daily rotation, transactions, clock rollback, migration, catalog and sales.");
        }
    }
}
