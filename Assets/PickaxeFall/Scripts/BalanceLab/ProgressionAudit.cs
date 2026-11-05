#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace PickaxeFall.BalanceLab
{
    public static class ProgressionAudit
    {
        public static List<LabScenario> Scenarios(LabPlan plan)
        {
            var result=new List<LabScenario>();
            void Add(int tier,bool active,int point,int mask,int[] levels,int stage=3,int regime=0,string cohort=null,bool holdout=false)
            {
                var s=new LabScenario {tier=tier,active=active,point=point,mask=mask,order=LabPlanner.Bits(mask),levels=levels,stage=stage,regime=regime,cohort=cohort,holdout=holdout,sampleCount=plan.samples};
                s.id=s.Family+"-m"+mask;result.Add(s);
            }
            for(int tier=0;tier<5;tier++)if(plan.tiers[tier])for(int a=0;a<2;a++)
            {
                if(a==0&&!plan.passive||a==1&&!plan.active)continue;
                for(int regime=0;regime<3;regime++)Add(tier,a==1,0,0,new int[25],0,regime,"r"+regime);
                Add(tier,a==1,0,0,new int[25],1,0,"holdout",true);
                // Matched full-world bases for each skill level point.
                for(int point=0;point<3;point++)Add(tier,a==1,point,0,new int[25]);
                var keys=Progression.Keys.Where(k=>k.Kind<=UpgradeKind.Durability?k.Tier==tier:tier==0||tier==3||tier==4).ToArray();
                foreach(var key in keys)for(int point=0;point<3;point++)
                {
                    int[] levels=new int[25];levels[key.Index]=LabPlanner.Level(key.Kind,point);
                    if(key.Kind==UpgradeKind.TeamSize)levels[new UpgradeKey(UpgradeKind.TeamChance).Index]=10;
                    int mask=1<<key.Index;Add(tier,a==1,point,mask,levels);
                    // The team-size comparator has the same purchased chance.
                    if(key.Kind==UpgradeKind.TeamSize)
                    {var baseline=new int[25];baseline[new UpgradeKey(UpgradeKind.TeamChance).Index]=10;Add(tier,a==1,point,0,baseline,3,0,"size-base");result[result.Count-2].cohort="size-base";result[result.Count-2].id=result[result.Count-2].Family+"-m"+mask;}
                }
                if(tier==0||tier==3||tier==4)
                {
                    var pairs=new[] {new[] {UpgradeKind.Damage,UpgradeKind.Durability},new[] {UpgradeKind.Durability,UpgradeKind.Careful},new[] {UpgradeKind.Fortune,UpgradeKind.TntFind},new[] {UpgradeKind.TntFind,UpgradeKind.DoubleBlast},new[] {UpgradeKind.EnergyCharge,UpgradeKind.PlungeDepth},new[] {UpgradeKind.TeamChance,UpgradeKind.TeamSize},new[] {UpgradeKind.DoubleBench,UpgradeKind.Durability}};
                    foreach(var pair in pairs)for(int point=0;point<3;point++)
                    {
                        int[] levels=new int[25];int mask=0;
                        foreach(var kind in pair){var key=new UpgradeKey(kind,kind<=UpgradeKind.Durability?tier:0);levels[key.Index]=LabPlanner.Level(kind,point);mask|=1<<key.Index;}
                        Add(tier,a==1,point,mask,levels,4);
                    }
                }
                int[] max=new int[25];foreach(var k in Progression.Keys)max[k.Index]=Progression.MaxLevel(k.Kind);
                Add(tier,a==1,2,0,max,4,0,"full-max");
            }
            for(int strategy=0;strategy<3;strategy++)result.Add(new LabScenario {id="hour-"+strategy,tier=0,active=true,hourly=true,strategy=strategy,stage=2,sampleCount=Math.Min(16,plan.samples),levels=new int[25],cohort="hour-"+strategy});
            return result;
        }
        public static IEnumerable<LabJob> Jobs(LabPlan plan,List<LabScenario> scenarios)
        {
            // Baselines, independent maps and hourly progression precede optional expansion.
            foreach(int stage in new[] {0,1,2,3,4})
            for(int sample=0;sample<Math.Min(plan.samples,stage>=3?8:plan.samples);sample++)
            foreach(var s in scenarios.Where(s=>s.stage==stage))if(sample<s.sampleCount)yield return Job(plan,s,sample);
            for(int sample=8;sample<plan.samples;sample++)foreach(var s in scenarios.Where(s=>s.stage>=3))yield return Job(plan,s,sample);
        }
        static LabJob Job(LabPlan plan,LabScenario s,int sample)=>new() {scenario=s,seed=(int)((plan.seed+104729L*sample+(s.holdout?1000000007L:0))%2147483646)+1,limit=s.hourly?3600:s.cohort=="full-max"?1800:600,balanceJson=plan.balanceJson,diagnostic=plan.diagnostic};
    }
}
#endif
