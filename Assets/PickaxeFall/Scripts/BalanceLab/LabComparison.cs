#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Linq;
using System.Text;

namespace PickaxeFall.BalanceLab
{
    public static class LabComparison
    {
        public static string Describe(LabSession current, LabSession previous, int tier, int point, int background, bool active)
        {
            if (current.Manifest.schema != previous.Manifest.schema || current.Manifest.unity != previous.Manifest.unity || current.Manifest.policy != previous.Manifest.policy || current.Plan.limit != previous.Plan.limit || current.Plan.diagnostic != previous.Plan.diagnostic)
                return "Отчёты несовместимы: отличаются схема, Unity, стратегия, лимит забега или диагностический режим.";
            var now = new LabAnalysis(current); var before = new LabAnalysis(previous);
            var old = before.View(tier,point,background,active).ToDictionary(x=>x.scenario.id);
            var result = new StringBuilder($"Сравнение: {previous.Manifest.version} ({previous.Manifest.fingerprint.Substring(0,8)}) → {current.Manifest.version} ({current.Manifest.fingerprint.Substring(0,8)})\n");
            foreach (var item in now.View(tier,point,background,active))
            {
                if (!old.TryGetValue(item.scenario.id,out var baseline) || !item.scenario.levels.SequenceEqual(baseline.scenario.levels)) continue;
                var (a,b) = LabAnalysis.Paired(item,baseline); if (a.Count == 0) continue;
                result.AppendLine(item.scenario.Title+": карт "+a.Count+"; Δ м "+LabFiles.Number(a.Average(r=>r.depth)-b.Average(r=>r.depth))+"; Δ стоимость "+LabFiles.Number(a.Average(r=>r.value)-b.Average(r=>r.value))+"; Δ монет/мин "+LabFiles.Number(LabAnalysis.Rate(a)-LabAnalysis.Rate(b)));
            }
            result.AppendLine("Сравниваются только одинаковые уровни и общие seed. Отпечаток баланса может различаться; новая случайность и физика способны изменить траектории."); return result.ToString();
        }
    }
}
#endif
