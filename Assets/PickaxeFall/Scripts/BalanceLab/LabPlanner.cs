#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace PickaxeFall.BalanceLab
{
    public static class LabPlanner
    {
        static readonly UpgradeKind[,] QuickPairs = {
            { UpgradeKind.Damage, UpgradeKind.Durability }, { UpgradeKind.Damage, UpgradeKind.ImpactPower },
            { UpgradeKind.Damage, UpgradeKind.Shrapnel }, { UpgradeKind.Durability, UpgradeKind.Careful },
            { UpgradeKind.Fortune, UpgradeKind.TntFind }, { UpgradeKind.TntFind, UpgradeKind.DoubleBlast },
            { UpgradeKind.TeamChance, UpgradeKind.TeamSize }, { UpgradeKind.EnergyCapacity, UpgradeKind.EnergyCharge },
            { UpgradeKind.EnergyCharge, UpgradeKind.EnergyEfficiency }, { UpgradeKind.EnergyCharge, UpgradeKind.PlungeDepth }
        };
        public static int Bits(int mask) { int n = 0; for (; mask != 0; mask &= mask - 1) n++; return n; }
        public static int Level(UpgradeKind kind, int point) => point == 0 ? 1 : point == 1 ? (Progression.MaxLevel(kind) + 1) / 2 : Progression.MaxLevel(kind);
        public static List<LabScenario> Scenarios(LabPlan plan)
        {
            plan.Validate();
            if(plan.mode==6){
                var audit=new List<LabScenario>();
                for(int map=0;map<3;map++)if(plan.maps[map])for(int tier=0;tier<5;tier++)if(plan.tiers[tier])for(int stage=0;stage<3;stage++)for(int active=0;active<2;active++){
                    if(active==0&&!plan.passive||active==1&&!plan.active)continue;var levels=new int[25];foreach(var k in Progression.Keys)levels[k.Index]=stage==0?0:Math.Max(1,(int)(Progression.MaxLevel(k.Kind)*(stage==1?.25f:.75f)));
                    audit.Add(new LabScenario {id=$"world-{map}-t{tier}-s{stage}-a{active}",map=(MapId)map,tier=tier,point=stage,stage=stage,cohort="world",mask=Progression.Keys.Where(k=>levels[k.Index]>0).Aggregate(0,(v,k)=>v|(1<<k.Index)),active=active==1,levels=levels,sampleCount=plan.samples});}
                for(int strategy=0;strategy<3;strategy++)audit.Add(new LabScenario {id="world-profile-"+strategy,cohort="profile",hourly=true,active=true,strategy=strategy,levels=new int[25],sampleCount=Math.Min(16,plan.samples)});return audit;
            }
            if(plan.mode==5){
                var campaigns=new List<LabScenario>();
                for(int construction=0;construction<1;construction++)for(int strategy=0;strategy<3;strategy++)
                    campaigns.Add(new LabScenario {id="profile-"+strategy+"-build-"+construction,tier=0,point=0,active=true,hourly=true,strategy=strategy,regime=construction,cohort="profile",levels=new int[25]});
                return campaigns;
            }
            if(plan.mode==4){var list=new List<LabScenario>();for(int m=0;m<3;m++)if(plan.maps[m])foreach(var s in ProgressionAudit.Scenarios(plan)){s.map=(MapId)m;if(m>0)s.id="map"+m+"-"+s.id;list.Add(s);}return list;} var result = new List<LabScenario>();
            for (int tier = 0; tier < 5; tier++)
            {
                if (!plan.tiers[tier]) continue;
                // Purchased stats follow the current type after a workbench.
                // Include every future reachable type, rather than just the roll.
                var keys = Progression.Keys.Where(k => k.Kind > UpgradeKind.Durability || k.Tier >= tier).ToArray();
                var masks = new HashSet<int> { 0 };
                if (plan.mode == 3) masks.Add(Progression.Keys.Where(k => plan.customLevels[k.Index] > 0).Aggregate(0, (a,k) => a | (1 << k.Index)));
                else
                {
                    foreach (var key in keys) masks.Add(1 << key.Index);
                    for (int i = 0; i < keys.Length; i++) for (int j = i + 1; j < keys.Length; j++)
                    {
                        bool quick = false;
                        for (int q = 0; q < QuickPairs.GetLength(0); q++)
                            if (keys[i].Kind == QuickPairs[q,0] && keys[j].Kind == QuickPairs[q,1] || keys[j].Kind == QuickPairs[q,0] && keys[i].Kind == QuickPairs[q,1]) quick = true;
                        if (plan.mode != 0 || quick) masks.Add((1 << keys[i].Index) | (1 << keys[j].Index));
                        if (plan.mode == 2) for (int k = j + 1; k < keys.Length; k++) masks.Add((1 << keys[i].Index) | (1 << keys[j].Index) | (1 << keys[k].Index));
                    }
                }
                for (int point = 0; point < (plan.mode == 0 || plan.mode == 3 ? 1 : 3); point++)
                for (int background = 0; background < (plan.mode == 0 || plan.mode == 3 ? 1 : 2); background++)
                for (int active = 0; active <= 1; active++)
                {
                    if (active == 0 && !plan.passive || active == 1 && !plan.active || background == 0 && !plan.isolated || background == 1 && !plan.background) continue;
                    foreach (int mask in masks.OrderBy(Bits).ThenBy(x => x))
                    {
                        int[] levels = new int[25];
                        foreach (var key in Progression.Keys)
                        {
                            bool on = (mask & (1 << key.Index)) != 0;
                            if (background == 1) on = !on;
                            levels[key.Index] = on ? plan.mode == 3 ? plan.customLevels[key.Index] : Level(key.Kind, point) : 0;
                        }
                        var s = new LabScenario { tier = tier, point = point, background = background, active = active != 0, mask = mask, order = Bits(mask), levels = levels };
                        s.id = s.Family + "-m" + mask; result.Add(s);
                    }
                }
            }
            var expanded=new List<LabScenario>();
            for(int m=0;m<3;m++)if(plan.maps[m])foreach(var s in result){var copy=UnityEngine.JsonUtility.FromJson<LabScenario>(UnityEngine.JsonUtility.ToJson(s));copy.map=(MapId)m;copy.id=copy.Family+"-m"+copy.mask;expanded.Add(copy);}
            return expanded.OrderBy(s => s.order).ThenBy(s => s.point).ThenBy(s => s.background).ThenBy(s => s.tier).ThenBy(s => s.active).ThenBy(s => s.mask).ToList();
        }
        public static IEnumerable<LabJob> Jobs(LabPlan plan, List<LabScenario> scenarios)
        {
            if(plan.mode==6){foreach(var s in scenarios)for(int n=0;n<s.sampleCount;n++)yield return new LabJob{scenario=s,seed=(int)((plan.seed+104729L*n)%2147483646L)+1,limit=s.hourly?21600:600,balanceJson=plan.balanceJson,diagnostic=plan.diagnostic};yield break;}
            if(plan.mode==4)
            {
                foreach(var job in ProgressionAudit.Jobs(plan,scenarios))yield return job;
                yield break;
            }
            foreach (int order in scenarios.Select(s => s.order).Distinct().OrderBy(x => x))
            for (int sample = 0; sample < plan.samples; sample++)
            foreach (var scenario in scenarios.Where(s => s.order == order))
                yield return new LabJob { scenario = scenario, seed = (int)((plan.seed + 104729L * sample) % 2147483646L) + 1, limit = plan.limit, diagnostic = plan.diagnostic,balanceJson=plan.balanceJson };
        }
    }
}
#endif
