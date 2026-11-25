using System;

namespace PickaxeFall
{
    public enum DailyGoal { MineBlocks, MineOre, MineResource, FinishRuns, ReachDepth, SellCoins }

    [Serializable]
    public sealed class DailyTask
    {
        public DailyGoal goal;
        public BlockKind resource;
        public long target, progress, reward;
        public bool claimed;
        public DailyTask Copy() => (DailyTask)MemberwiseClone();
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(DailyGoal), goal) || target < 1 || target > 100000 ||
                progress < 0 || progress > target || reward < 1 || reward > 10000 ||
                claimed && progress < target || goal == DailyGoal.MineResource && !ResourceInventory.IsResource(resource))
                throw new InvalidOperationException("Invalid daily task.");
        }
    }

    [Serializable]
    public sealed class ProfileJournal
    {
        public long miningTicks, completedRuns, soldCoins, chests, workbenches, trialsWon, abilities;
        public long bestRunValue, bestRunBlocks, longestRunTicks, dailyRewardsClaimed;
        // A high-water day prevents clock rollback from regenerating already claimed tasks.
        public long day = -1;
        public DailyTask[] tasks = Array.Empty<DailyTask>();
        public ProfileJournal Copy()
        {
            var copy = (ProfileJournal)MemberwiseClone();
            copy.tasks = Array.ConvertAll(tasks, task => task.Copy());
            return copy;
        }
        public void Validate()
        {
            foreach (long n in new[] { miningTicks, completedRuns, soldCoins, chests, workbenches, trialsWon,
                abilities, bestRunValue, bestRunBlocks, longestRunTicks, dailyRewardsClaimed })
                if (n < 0) throw new InvalidOperationException("Invalid journal statistics.");
            if (day < -1 || day > 3652058 || tasks == null || tasks.Length != (day < 0 ? 0 : 3))
                throw new InvalidOperationException("Invalid daily journal.");
            foreach (var task in tasks) { if (task == null) throw new InvalidOperationException("Missing daily task."); task.Validate(); }
        }
        public void Record(DailyGoal goal, long amount = 1, BlockKind resource = BlockKind.Dirt)
        {
            if (amount <= 0) return;
            foreach (var task in tasks)
            {
                if (task.goal != goal || task.goal == DailyGoal.MineResource && task.resource != resource) continue;
                task.progress = goal == DailyGoal.ReachDepth
                    ? Math.Min(task.target, Math.Max(task.progress, amount))
                    : task.progress + Math.Min(amount, task.target - task.progress);
            }
        }
        public static long Add(long value, long amount) => value + Math.Min(Math.Max(0, amount), long.MaxValue - value);
    }

    public static class DailyTasks
    {
        public const long DayMilliseconds = 86400000;
        public static bool Ensure(PlayerProfile profile, long utcMilliseconds)
        {
            long day = Math.Max(0, utcMilliseconds) / DayMilliseconds;
            if (day <= profile.journal.day) return false;
            var random = new Random(unchecked((int)day * 7919 + 63847));
            int level = profile.profileLevel;
            var tasks = new DailyTask[3];
            tasks[0] = new DailyTask { goal = DailyGoal.MineBlocks, target = (60 + random.Next(4) * 20) * level, reward = 35 * level };
            var choices = new System.Collections.Generic.List<BlockKind> { BlockKind.Stone };
            if (WorldCatalog.Open(profile, MapId.Desert) && profile.discovered[(int)BlockKind.Sandstone]) choices.Add(BlockKind.Sandstone);
            if (WorldCatalog.Open(profile, MapId.Nether) && profile.discovered[(int)BlockKind.Netherrack]) choices.Add(BlockKind.Netherrack);
            tasks[1] = random.Next(2) == 0
                ? new DailyTask { goal = DailyGoal.MineOre, target = 6 + level * 3, reward = 45 * level }
                : new DailyTask { goal = DailyGoal.MineResource, resource = choices[random.Next(choices.Count)], target = 25 + level * 15, reward = 45 * level };
            int third = random.Next(3);
            tasks[2] = third == 0 ? new DailyTask { goal = DailyGoal.FinishRuns, target = 3, reward = 50 * level }
                : third == 1 ? new DailyTask { goal = DailyGoal.ReachDepth, target = Math.Min(45, 8 + profile.bestDepth / 4), reward = 50 * level }
                : new DailyTask { goal = DailyGoal.SellCoins, target = 100 * level, reward = 50 * level };
            profile.journal.day = day;
            profile.journal.tasks = tasks;
            return true;
        }
    }

    public sealed partial class EconomyService
    {
        // Injected by the player; lab workers keep time-dependent tasks disabled.
        public Func<long> JournalTime { get; set; }
        public void RefreshDailyTasks() { if (JournalTime != null) DailyTasks.Ensure(Profile, JournalTime()); }
        public bool TryClaimDaily(int index, long expectedDay)
        {
            RefreshDailyTasks();
            if (index < 0 || index >= Profile.journal.tasks.Length || Profile.journal.day != expectedDay)
            { LastError = L.T("Задания обновились. Проверь новый список."); return false; }
            var task = Profile.journal.tasks[index];
            if (task.claimed || task.progress < task.target) { LastError = L.T("Задание ещё не выполнено или награда уже получена."); return false; }
            var next = Profile.Copy();
            try { next.coins = checked(next.coins + task.reward); }
            catch (OverflowException) { LastError = L.T("Слишком большая сумма."); return false; }
            next.journal.tasks[index].claimed = true;
            next.journal.dailyRewardsClaimed = ProfileJournal.Add(next.journal.dailyRewardsClaimed, 1);
            return Commit(next);
        }
        void RecordSale(PlayerProfile next, long value)
        {
            next.journal.soldCoins = ProfileJournal.Add(next.journal.soldCoins, value);
            next.journal.Record(DailyGoal.SellCoins, value);
        }
    }
}
