#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            int horizon=job.limit;
            var result=new LabResult {id=job.Id,scenario=job.scenario.id,seed=job.seed,limit=horizon,branches=1,firstPurchase=-1,dashOpenedAt=-1,plungeOpenedAt=-1,
                profileLevelTimes=new double[]{0,-1,-1,-1,-1},profileCoinsBlocked=new double[4],profileMiningBlocked=new double[4],profileSkillsBlocked=new double[4]};
            var purchases=new List<PurchaseEvent>();double elapsed=0;int cursor=0;
            while(elapsed+8<horizon)
            {
                if(cancelled?.Invoke()==true)throw new OperationCanceledException();
                int tier=balance.Roll(random),seed=random.Next(1,int.MaxValue);var scene=LabWorker.NewWorld();var root=new GameObject("Profile progression run");SceneManager.MoveGameObjectToScene(root,scene);
                bool finished;double segment;
                try
                {
                    var game=root.AddComponent<GameController>();game.InitializeSimulation(balance);game.BeginSimulation(tier,seed,bank.Profile.upgrades,-1,-1,bank.Profile.dashUnlocked,bank.Profile.plungeUnlocked);
                    var policy=new LabPolicy {PriorityOre=bank.Profile.profileLevel==4&&bank.Profile.lifetimeMined[(int)BlockKind.Diamond]<3?BlockKind.Diamond:(BlockKind?)null};int ticks=(int)Math.Floor((horizon-elapsed-8)*60);
                    while(game.Phase==RunPhase.Mining && game.Clock.Ticks<ticks)
                    {if(game.Clock.Ticks%120==0 && cancelled?.Invoke()==true)throw new OperationCanceledException();LabWorker.Step(game,policy);}
                    finished=game.Phase!=RunPhase.Mining;segment=game.Clock.Now+8;elapsed+=segment;result.depth=Math.Max(result.depth,game.Depth);result.runCount++;
                    result.value+=game.PotentialSaleValue;result.blocks+=game.BlocksBroken;result.benches+=game.WorkbenchesUsed;result.abilities+=game.Benefits.Abilities;result.upgradeBenches+=game.Benefits.UpgradeBenches;result.lavaWear+=game.Benefits.LavaWear;result.restoredWear+=game.Benefits.RestoredDurability;result.energySpent+=game.Benefits.EnergySpent;result.tools+=game.RunToolCount;
                    for(int i=0;i<17;i++){result.attempts[i]+=game.Rolls.Attempts[i];result.successes[i]+=game.Rolls.Successes[i];}
                    foreach(var tool in game.Pickaxes)result.strikes+=tool.TotalStrikes;
                    foreach(var kind in ResourceInventory.Materials){int i=(int)kind;result.resources[i]+=game.OreCounts[i];bank.Collect(kind,game.OreCounts[i]);bank.Profile.lifetimeMined[i]=checked(bank.Profile.lifetimeMined[i]+game.Economy.Profile.lifetimeMined[i]);}
                    if(!finished)result.censored=1;
                }
                finally{UnityEngine.Object.DestroyImmediate(root);LabWorker.Unload(scene);}
                if(!finished)break;
                int gate=bank.Profile.profileLevel-1;
                if(gate<4)
                {
                    bool missingSkills=false,missingMining=false;
                    foreach(var req in ProfileRules.Skills[gate])missingSkills|=!req.Met(bank.Profile);
                    foreach(var material in ResourceInventory.Materials)missingMining|=bank.Profile.lifetimeMined[(int)material]<ProfileRules.MiningRequirements[gate][(int)material];
                    if(missingSkills)result.profileSkillsBlocked[gate]+=segment;
                    if(missingMining)result.profileMiningBlocked[gate]+=segment;
                    if(!missingSkills&&!missingMining&&!bank.CanLevelUp(out _))result.profileCoinsBlocked[gate]+=segment;
                }
                // The construction comparison deliberately contributes before selling,
                // so the measured opportunity cost includes materials unavailable to skills.
                if(job.scenario.cohort=="profile"&&job.scenario.regime>0)
                {
                    var construction=job.scenario.regime==1?GoalKind.Bridge:GoalKind.Portal;
                    long total=0;foreach(long n in bank.ConstructionQuote(construction))total+=n;
                    if(total>0)
                    {
                        if(!bank.TryContribute(construction))throw new InvalidOperationException(bank.LastError);
                        if(ProfileRules.Complete(bank.Profile,construction)){
                            if(construction==GoalKind.Bridge&&result.bridgeCompletedAt<0)result.bridgeCompletedAt=elapsed;
                            if(construction==GoalKind.Portal&&result.portalCompletedAt<0)result.portalCompletedAt=elapsed;
                        }
                    }
                }
                for(int purchase=0;purchase<3;purchase++)
                {
                    for(int i=10;i<27;i++)if(!bank.Profile.revealed[i]&&bank.Profile.profileLevel>=ProfileRules.RequiredLevel(i))
                        if(!bank.TryReveal(i))throw new InvalidOperationException(bank.LastError);
                    bool missingSkills=false,missingMining=false;UpgradeKey? mandatory=null;
                    if(bank.Profile.profileLevel<5)
                    {
                        gate=bank.Profile.profileLevel-1;
                        foreach(var req in ProfileRules.Skills[gate])if(!req.Met(bank.Profile)){missingSkills=true;mandatory??=K(req.Kind);}
                        foreach(var material in ResourceInventory.Materials)missingMining|=bank.Profile.lifetimeMined[(int)material]<ProfileRules.MiningRequirements[gate][(int)material];
                        if(!missingSkills&&!missingMining)
                        {
                            var levelCost=new UpgradeCost {Coins=ProfileRules.LevelCoins[gate]};
                            if(!Trade(bank,levelCost))break;
                            if(!bank.TryLevelUp())throw new InvalidOperationException(bank.LastError);
                            result.profileLevelTimes[bank.Profile.profileLevel-1]=elapsed;result.profileCoinsSpent+=levelCost.Coins;
                            purchases.Add(new PurchaseEvent {seconds=elapsed,key=-3,level=bank.Profile.profileLevel,title="Уровень профиля",coins=levelCost.Coins});
                            continue;
                        }
                    }
                    UpgradeKey? desired=mandatory;bool bought=false;
                    if(!desired.HasValue && (job.scenario.strategy==1 || purchases.Count>=4))
                    foreach(var ability in new[]{AbilityKind.Dash,AbilityKind.Plunge})
                    {
                        if(bank.AbilityUnlocked(ability)||!bank.SkillAvailable(ability==AbilityKind.Dash?25:26,out _))continue;
                        var cost=Progression.AbilityCost(ability,bank.Config);if(!Trade(bank,cost))break;
                        if(!bank.TryUnlock(ability))throw new InvalidOperationException(bank.LastError);
                        purchases.Add(new PurchaseEvent {seconds=elapsed,key=ability==AbilityKind.Dash?-1:-2,level=1,title=ability.ToString(),coins=cost.Coins});
                        if(ability==AbilityKind.Dash)result.dashOpenedAt=elapsed;else result.plungeOpenedAt=elapsed;
                        bought=true;break;
                    }
                    if(bought)continue;
                    if(!desired.HasValue)
                    {
                        var targets=Strategies[job.scenario.strategy];
                        for(int attempt=0;attempt<targets.Length;attempt++)
                        {
                            var key=targets[cursor++%targets.Length];
                            if(bank.Profile.Level(key)>=Progression.MaxLevel(key.Kind)||!bank.SkillAvailable(key.Index,out _)||
                               key.Kind==UpgradeKind.DashForce&&!bank.Profile.dashUnlocked||key.Kind==UpgradeKind.PlungeDepth&&!bank.Profile.plungeUnlocked||
                               key.Kind>=UpgradeKind.EnergyCapacity&&key.Kind<=UpgradeKind.EnergyEfficiency&&!bank.Profile.dashUnlocked&&!bank.Profile.plungeUnlocked)continue;
                            desired=key;break;
                        }
                    }
                    if(!desired.HasValue)break;
                    var target=desired.Value;var price=Progression.Cost(target,bank.Profile.Level(target)+1,bank.Config);
                    if(!Trade(bank,price))break;
                    if(!bank.TryBuy(target))throw new InvalidOperationException(bank.LastError);
                    if(mandatory.HasValue)result.requiredSkillCoinsSpent+=price.Coins;
                    purchases.Add(new PurchaseEvent {seconds=elapsed,key=target.Index,level=bank.Profile.Level(target),title=Progression.Title(target.Kind),coins=price.Coins});
                }
            }
            result.seconds=Math.Min(horizon,elapsed);result.purchases=purchases.ToArray();result.firstPurchase=purchases.Count==0?-1:purchases[0].seconds;
            result.finalLevels=(int[])bank.Profile.upgrades.Clone();result.finalProfileLevel=bank.Profile.profileLevel;result.lifetimeMined=(long[])bank.Profile.lifetimeMined.Clone();
            result.bridgeStone=bank.Profile.bridgeStone;result.bridgeIron=bank.Profile.bridgeIron;result.portalObsidian=bank.Profile.portalObsidian;
            result.wallSeconds=watch.Elapsed.TotalSeconds;return result;
        }
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
