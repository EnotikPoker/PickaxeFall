namespace PickaxeFall
{
    public partial class GameController
    {
        internal void InitializeJournal()
        {
            if (IsSimulation) return;
            Economy.JournalTime = () => Platform.UtcMilliseconds;
            Economy.RefreshDailyTasks();
        }
        void TickJournal()
        {
            if (IsSimulation) return;
            Economy.RefreshDailyTasks();
            var journal = Economy.Profile.journal;
            journal.miningTicks = ProfileJournal.Add(journal.miningTicks, 1);
            journal.Record(DailyGoal.ReachDepth, Depth);
        }
        void RecordFinishedRun()
        {
            if (IsSimulation) return;
            Economy.RefreshDailyTasks();
            var journal = Economy.Profile.journal;
            journal.completedRuns = ProfileJournal.Add(journal.completedRuns, 1);
            journal.bestRunValue = System.Math.Max(journal.bestRunValue, PotentialSaleValue);
            journal.bestRunBlocks = System.Math.Max(journal.bestRunBlocks, BlocksBroken);
            journal.longestRunTicks = System.Math.Max(journal.longestRunTicks, Clock.Ticks);
            journal.Record(DailyGoal.FinishRuns);
            journal.Record(DailyGoal.ReachDepth, Depth);
        }
        public bool ClaimDaily(int index, long day)
        {
            if (!CanTrade) return false;
            bool success = Economy.TryClaimDaily(index, day);
            if (success) Audio?.Play("reward"); else hud?.Toast(Economy.LastError);
            Notify(); return success;
        }
    }
}
