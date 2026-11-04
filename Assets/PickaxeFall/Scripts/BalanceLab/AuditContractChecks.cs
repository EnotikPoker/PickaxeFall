#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

namespace PickaxeFall.BalanceLab
{
    public static class AuditContractChecks
    {
        public static void Validate()
        {
            void Check(bool test,string message){if(!test)throw new Exception("Progression audit contract: "+message);}
            var plan=new LabPlan {mode=4,samples=32,seconds=7200};var scenarios=LabPlanner.Scenarios(plan);var jobs=LabPlanner.Jobs(plan,scenarios).ToArray();
            Check(scenarios.Select(s=>s.id).Distinct().Count()==scenarios.Count,"unique comparisons");
            Check(jobs.Select(j=>j.Id).Distinct().Count()==jobs.Length,"unique jobs");
            Check(jobs.Length==scenarios.Sum(s=>s.sampleCount),"exact coverage count including shorter hourly sample");
            Check(jobs.Where(j=>j.scenario.stage==0).Count()==5*2*3*32,"all tiers/controllers/regimes precede skills");
            Check(jobs.Where(j=>j.scenario.hourly).Count()==3*16,"three hourly strategies");
            var train=jobs.Where(j=>j.scenario.stage==0).Select(j=>j.seed).ToHashSet();Check(jobs.Where(j=>j.scenario.holdout).All(j=>!train.Contains(j.seed)),"independent holdout seeds");
            Check(Array.FindLastIndex(jobs,j=>j.scenario.stage<=2)<Array.FindIndex(jobs,j=>j.scenario.stage>=3),"mandatory measurements first");
            foreach(var kind in Enum.GetValues(typeof(UpgradeKind)).Cast<UpgradeKind>())Check(scenarios.Any(s=>s.order==1 && (s.mask & (1<<new UpgradeKey(kind).Index))!=0),"skill covered "+kind);
            bool failed=false;try{new LabPlan {mode=4,seconds=0}.Validate();}catch(ArgumentException){failed=true;}Check(failed,"audit cannot run indefinitely");
            var config=new ProgressionBalance();config.basePrices[0]=123;var price=Progression.Cost(new UpgradeKey(UpgradeKind.Damage),1,config);Check(price.Coins==123,"config drives actual transaction price");
            var profile=new PlayerProfile();profile.upgrades[new UpgradeKey(UpgradeKind.EnergyCharge).Index]=20;profile.upgrades[new UpgradeKey(UpgradeKind.EnergyEfficiency).Index]=10;
            var upgrades=new RunUpgrades(profile,config);Check(Math.Abs(upgrades.EnergyCost(config.dashCost)/upgrades.EnergyPerSecond-7.5)<1e-5,"fully upgraded 7.5 second cadence");
            config.energyRegen=99;Check(Progression.Default.energyRegen==3,"config does not mutate globals");
            var summary=new LabSummary {rows=new() {new LabResult {outcomes=new[] {new LabOutcome {weight=.99,depth=10},new LabOutcome {weight=.01,depth=1000}}}}};
            Check(summary.Percentile(.5)==10 && summary.Percentile(.95)==10 && summary.Percentile(1)==1000,"percentiles preserve rare branch weights");
            string path=System.IO.Path.Combine(Application.temporaryCachePath,"audit-prices-"+Guid.NewGuid().ToString("N"));
            try
            {
                var manifest=new LabManifest().CaptureEconomy();using var session=LabSession.Create(path,plan,manifest);var analysis=new LabAnalysis(session);
                var first=session.Scenarios.First(s=>s.point==0 && s.mask==(1<<new UpgradeKey(UpgradeKind.TeamChance).Index) && string.IsNullOrEmpty(s.cohort));
                var cost=analysis.Cost(analysis.Get(first));Check(cost.coins==Progression.Cost(new UpgradeKey(UpgradeKind.TeamChance),1).Coins,"team-size prerequisite cannot inflate first team-chance price");
                var full=session.Scenarios.First(s=>s.cohort=="full-max");Check(analysis.Cost(analysis.Get(full)).coins>1000,"full profile diagnostic has actual cumulative cost");
            }
            finally{if(System.IO.Directory.Exists(path))System.IO.Directory.Delete(path,true);}
            Debug.Log("PICKAXEFALL_AUDIT_CONTRACT_OK: "+scenarios.Count+" scenarios / "+jobs.Length+" jobs, mandatory coverage, independent maps, every skill, editable prices, cadence and weighted quantiles.");
        }
    }
}
#endif
