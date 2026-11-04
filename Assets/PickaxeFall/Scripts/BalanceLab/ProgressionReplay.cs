#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickaxeFall.BalanceLab
{
    [Serializable] public sealed class PurchaseEvent
    {
        public double seconds;
        public int key,level;
        public string title;
        public long coins;
    }
    public static class ProgressionReplay
    {
        static UpgradeKey K(UpgradeKind kind,int tier=0)=>new(kind,tier);
        static readonly UpgradeKey[][] Strategies={
            new[] {K(UpgradeKind.Damage),K(UpgradeKind.Durability),K(UpgradeKind.Damage,1),K(UpgradeKind.Durability,1),K(UpgradeKind.Damage,2),K(UpgradeKind.Durability,2),K(UpgradeKind.Damage,3),K(UpgradeKind.Durability,3),K(UpgradeKind.Damage,4),K(UpgradeKind.Durability,4),K(UpgradeKind.Careful),K(UpgradeKind.DoubleBench)},
            new[] {K(UpgradeKind.Damage),K(UpgradeKind.Durability),K(UpgradeKind.Damage,1),K(UpgradeKind.Durability,1),K(UpgradeKind.EnergyCharge),K(UpgradeKind.EnergyEfficiency),K(UpgradeKind.PlungeDepth),K(UpgradeKind.EnergyCapacity),K(UpgradeKind.DashForce)},
            new[] {K(UpgradeKind.Damage),K(UpgradeKind.Durability),K(UpgradeKind.Damage,1),K(UpgradeKind.Durability,1),K(UpgradeKind.Fortune),K(UpgradeKind.TntFind),K(UpgradeKind.DoubleBlast),K(UpgradeKind.Shrapnel),K(UpgradeKind.ImpactPower)}
        };
        public static LabResult Execute(GameBalance balance,LabJob job,Func<bool> cancelled)
        {
            var watch=Stopwatch.StartNew();var bank=new EconomyService(new PlayerProfile(),config:balance.progression);var random=new System.Random(job.seed);
            var result=new LabResult {id=job.Id,scenario=job.scenario.id,seed=job.seed,limit=job.limit,branches=1,firstPurchase=-1,dashOpenedAt=-1,plungeOpenedAt=-1};
            var purchases=new List<PurchaseEvent>();double elapsed=0;int cursor=0;
            while(elapsed+8<3600)
            {
                if(cancelled?.Invoke()==true)throw new OperationCanceledException();
                int tier=balance.Roll(random),seed=random.Next(1,int.MaxValue);var scene=LabWorker.NewWorld();var root=new GameObject("Hour progression run");SceneManager.MoveGameObjectToScene(root,scene);
                bool finished;
                try
                {
                    var game=root.AddComponent<GameController>();game.InitializeSimulation(balance);game.BeginSimulation(tier,seed,bank.Profile.upgrades,-1,-1,bank.Profile.dashUnlocked,bank.Profile.plungeUnlocked);
                    var policy=new LabPolicy();int ticks=(int)Math.Floor((3600-elapsed-8)*60);
                    while(game.Phase==RunPhase.Mining && game.Clock.Ticks<ticks)
                    {if(game.Clock.Ticks%120==0 && cancelled?.Invoke()==true)throw new OperationCanceledException();LabWorker.Step(game,policy);}
                    finished=game.Phase!=RunPhase.Mining;elapsed+=game.Clock.Now+8;result.depth=Math.Max(result.depth,game.Depth);result.runCount++;
                    result.value+=game.PotentialSaleValue;result.blocks+=game.BlocksBroken;result.benches+=game.WorkbenchesUsed;result.abilities+=game.Benefits.Abilities;result.upgradeBenches+=game.Benefits.UpgradeBenches;result.lavaWear+=game.Benefits.LavaWear;result.restoredWear+=game.Benefits.RestoredDurability;result.energySpent+=game.Benefits.EnergySpent;result.tools+=game.RunToolCount;
                    for(int i=0;i<17;i++){result.attempts[i]+=game.Rolls.Attempts[i];result.successes[i]+=game.Rolls.Successes[i];}
                    foreach(var tool in game.Pickaxes)result.strikes+=tool.TotalStrikes;
                    foreach(var kind in ResourceInventory.Materials){int i=(int)kind;result.resources[i]+=game.OreCounts[i];bank.Collect(kind,game.OreCounts[i]);}
                    if(!finished)result.censored=1;
                }
                finally{UnityEngine.Object.DestroyImmediate(root);LabWorker.Unload(scene);}
                // Only actual completed runs allow trading; the last partial run is measured, never a forced sale.
                if(!finished)break;
                for(int purchase=0;purchase<3;purchase++)
                {
                    bool bought=false;
                    if(job.scenario.strategy==1 || purchases.Count>=4)
                    foreach(var ability in new[] {AbilityKind.Dash,AbilityKind.Plunge})
                    {
                        if(bank.AbilityUnlocked(ability))continue;
                        var cost=Progression.AbilityCost(ability,bank.Config);
                        if(!Trade(bank,cost))break;
                        if(!bank.TryUnlock(ability))throw new InvalidOperationException(bank.LastError);
                        purchases.Add(new PurchaseEvent {seconds=elapsed,key=ability==AbilityKind.Dash?-1:-2,level=1,title=ability.ToString(),coins=cost.Coins});
                        if(ability==AbilityKind.Dash)result.dashOpenedAt=elapsed;else result.plungeOpenedAt=elapsed;
                        bought=true;break;
                    }
                    if(!bought)
                    {
                        var targets=Strategies[job.scenario.strategy];
                        for(int attempt=0;attempt<targets.Length;attempt++)
                        {
                            var key=targets[cursor%targets.Length];int level=bank.Profile.Level(key);
                            if(level>=Progression.MaxLevel(key.Kind)||key.Kind>=UpgradeKind.EnergyCapacity && key.Kind<=UpgradeKind.EnergyEfficiency && !bank.Profile.dashUnlocked && !bank.Profile.plungeUnlocked ||key.Kind==UpgradeKind.DashForce&&!bank.Profile.dashUnlocked ||key.Kind==UpgradeKind.PlungeDepth&&!bank.Profile.plungeUnlocked){cursor++;continue;}
                            var cost=Progression.Cost(key,level+1,bank.Config);
                            if(!Trade(bank,cost))break;
                            if(!bank.TryBuy(key))throw new InvalidOperationException(bank.LastError);
                            purchases.Add(new PurchaseEvent {seconds=elapsed,key=key.Index,level=level+1,title=Progression.Title(key.Kind),coins=cost.Coins});cursor++;bought=true;break;
                        }
                    }
                    if(!bought)break;
                }
            }
            result.seconds=Math.Min(3600,elapsed);result.purchases=purchases.ToArray();result.firstPurchase=purchases.Count==0?-1:purchases[0].seconds;result.finalLevels=(int[])bank.Profile.upgrades.Clone();result.wallSeconds=watch.Elapsed.TotalSeconds;
            return result;
        }
        // Retain exactly the next recipe. A failed affordability check cannot sell anything.
        static bool Trade(EconomyService bank,UpgradeCost cost)
        {
            long available=bank.Profile.coins;
            foreach(var kind in ResourceInventory.Materials){int i=(int)kind;long surplus=bank.Profile.resources[i]-cost.Resources[i];if(surplus<0)return false;available=checked(available+surplus*bank.Config.sellPrices[i]);}
            if(available<cost.Coins)return false;
            foreach(var kind in ResourceInventory.Materials){int i=(int)kind;long surplus=bank.Profile.resources[i]-cost.Resources[i];if(surplus>0&&!bank.TrySell(kind,surplus))throw new InvalidOperationException(bank.LastError);}
            return true;
        }
    }
}
#endif
