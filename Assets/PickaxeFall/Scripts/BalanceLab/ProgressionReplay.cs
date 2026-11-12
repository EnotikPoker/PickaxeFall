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
            new[] {K(UpgradeKind.Damage),K(UpgradeKind.Durability),K(UpgradeKind.Damage,1),K(UpgradeKind.Durability,1),K(UpgradeKind.Damage,2),K(UpgradeKind.Durability,2),K(UpgradeKind.Damage,3),K(UpgradeKind.Durability,3),K(UpgradeKind.Damage,4),K(UpgradeKind.Durability,4),K(UpgradeKind.Careful),K(UpgradeKind.DoubleBench),K(UpgradeKind.HeatShield),K(UpgradeKind.Underwater)},
            new[] {K(UpgradeKind.Damage),K(UpgradeKind.Durability),K(UpgradeKind.Damage,1),K(UpgradeKind.Durability,1),K(UpgradeKind.EnergyCharge),K(UpgradeKind.EnergyEfficiency),K(UpgradeKind.PlungeDepth),K(UpgradeKind.EnergyCapacity),K(UpgradeKind.DashForce),K(UpgradeKind.Underwater),K(UpgradeKind.HeatShield)},
            new[] {K(UpgradeKind.Damage),K(UpgradeKind.Durability),K(UpgradeKind.Damage,1),K(UpgradeKind.Durability,1),K(UpgradeKind.Fortune),K(UpgradeKind.TntFind),K(UpgradeKind.DoubleBlast),K(UpgradeKind.Shrapnel),K(UpgradeKind.ImpactPower),K(UpgradeKind.EarthDigger),K(UpgradeKind.TreasureHunter)}
        };
        public static LabResult Execute(GameBalance balance,LabJob job,Func<bool> cancelled)
        {
            var watch=Stopwatch.StartNew();var bank=new EconomyService(new PlayerProfile(),config:balance.progression);var random=new System.Random(job.seed);
            int horizon=job.limit;
            var result=new LabResult {id=job.Id,scenario=job.scenario.id,seed=job.seed,limit=horizon,branches=1,firstPurchase=-1,dashOpenedAt=-1,plungeOpenedAt=-1,
                profileLevelTimes=new double[]{0,-1,-1,-1,-1},profileCoinsBlocked=new double[4],profileMiningBlocked=new double[4],profileSkillsBlocked=new double[4]};
            var purchases=new List<PurchaseEvent>();double elapsed=0;int cursor=0;UpgradeCost pending=null;
            while(elapsed+8<horizon)
            {
                if(cancelled?.Invoke()==true)throw new OperationCanceledException();
                int tier=balance.Roll(random),seed=random.Next(1,int.MaxValue);var scene=LabWorker.NewWorld();var root=new GameObject("Profile progression run");SceneManager.MoveGameObjectToScene(root,scene);
                bool finished;double segment;
                try
                {
                    balance.map=ChooseMap(bank,pending);var game=root.AddComponent<GameController>();game.InitializeSimulation(balance);game.BeginSimulation(tier,seed,bank.Profile.upgrades,-1,-1,bank.Profile.dashUnlocked,bank.Profile.plungeUnlocked);
                    var policy=new LabPolicy {PriorityOre=balance.map==MapId.Desert&&!ProfileRules.Complete(bank.Profile,GoalKind.Portal)?BlockKind.Obsidian:bank.Profile.profileLevel==4&&bank.Profile.lifetimeMined[(int)BlockKind.Diamond]<3?BlockKind.Diamond:(BlockKind?)null};int ticks=(int)Math.Floor((horizon-elapsed-8)*60);
                    while(game.Phase==RunPhase.Mining && game.Clock.Ticks<ticks)
                    {if(game.Clock.Ticks%120==0 && cancelled?.Invoke()==true)throw new OperationCanceledException();LabWorker.Step(game,policy);}
                    finished=game.Phase!=RunPhase.Mining;segment=game.Clock.Now+8;elapsed+=segment;result.depth=Math.Max(result.depth,game.Depth);result.runCount++;
                    result.mapSeconds[(int)balance.map]+=segment;result.mapValues[(int)balance.map]+=game.PotentialSaleValue;result.mapRuns[(int)balance.map]++;result.waterSeconds+=game.Benefits.WaterSeconds;result.lavaSeconds+=game.Benefits.LavaSeconds;result.lavaWearSaved+=game.Benefits.LavaWearSaved;result.treasureBonuses+=game.Benefits.TreasureBonuses;result.bonusEarth+=game.Benefits.BonusEarth;result.chests+=game.Benefits.Chests;result.trials+=game.Benefits.Trials;result.failedTrials+=game.Benefits.FailedTrials;
                    result.value+=game.PotentialSaleValue;result.blocks+=game.BlocksBroken;result.benches+=game.WorkbenchesUsed;result.abilities+=game.Benefits.Abilities;result.upgradeBenches+=game.Benefits.UpgradeBenches;result.lavaWear+=game.Benefits.LavaWear;result.restoredWear+=game.Benefits.RestoredDurability;result.energySpent+=game.Benefits.EnergySpent;result.tools+=game.RunToolCount;
                    for(int i=0;i<Progression.KindCount;i++){result.attempts[i]+=game.Rolls.Attempts[i];result.successes[i]+=game.Rolls.Successes[i];}
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
                if(job.scenario.cohort=="profile" && bank.Profile.profileLevel>=3)
                {
                    var construction=!ProfileRules.Complete(bank.Profile,GoalKind.Bridge)?GoalKind.Bridge:GoalKind.Portal;
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
                    for(int i=10;i<Progression.RevealCount;i++)if(!bank.Profile.revealed[i]&&bank.Profile.profileLevel>=ProfileRules.RequiredLevel(i))
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
                    UpgradeKey? desired=mandatory;
                    if(!desired.HasValue && bank.Profile.profileLevel>=3){int foundation=bank.Profile.profileLevel>=4?5:3;for(int t=1;t<5&&!desired.HasValue;t++)foreach(var kind in new[]{UpgradeKind.Damage,UpgradeKind.Durability})if(bank.Profile.Level(K(kind,t))<foundation){desired=K(kind,t);break;}}
                    bool bought=false;
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
                            if(bank.Profile.Level(key)>=Progression.MaxLevel(key.Kind)||!bank.SkillAvailable(key.RevealIndex,out _)||
                               key.Kind==UpgradeKind.DashForce&&!bank.Profile.dashUnlocked||key.Kind==UpgradeKind.PlungeDepth&&!bank.Profile.plungeUnlocked||
                               key.Kind>=UpgradeKind.EnergyCapacity&&key.Kind<=UpgradeKind.EnergyEfficiency&&!bank.Profile.dashUnlocked&&!bank.Profile.plungeUnlocked)continue;
                            desired=key;break;
                        }
                    }
                    if(!desired.HasValue)break;
                    var target=desired.Value;var price=Progression.Cost(target,bank.Profile.Level(target)+1,bank.Config);
                    pending=price;if(!Trade(bank,price))break;pending=null;
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
        static MapId ChooseMap(EconomyService bank,UpgradeCost pending)
        {
            var p=bank.Profile;int gate=Math.Min(3,p.profileLevel-1);
            // Visit the map for the largest unmet lifetime target. Ratios avoid
            // one large stone requirement starving the new locations.
            float best=0;MapId chosen=MapId.Mine;
            foreach(var k in ResourceInventory.Materials){long need=ProfileRules.MiningRequirements[gate][(int)k];if(need==0)continue;float missing=(need-p.lifetimeMined[(int)k])/(float)need;MapId map=k>=BlockKind.Netherrack?MapId.Nether:k>=BlockKind.Sand?MapId.Desert:MapId.Mine;if(missing>best&&WorldCatalog.Open(p,map)){best=missing;chosen=map;}}
            if(!ProfileRules.Complete(p,GoalKind.Portal)&&WorldCatalog.Open(p,MapId.Desert)&&p.profileLevel>=4)return MapId.Desert;
            if(best>0)return chosen;
            if(pending!=null)foreach(var k in ResourceInventory.Materials)if(p.resources[(int)k]<pending.Resources[(int)k]){var map=k>=BlockKind.Netherrack?MapId.Nether:k>=BlockKind.Sand?MapId.Desert:MapId.Mine;if(WorldCatalog.Open(p,map))return map;}
            // When a purchase is blocked by a late recipe, obtain that material.
            foreach(var key in Strategies[0])if(p.Level(key)<Progression.MaxLevel(key.Kind)){
                var cost=Progression.Cost(key,p.Level(key)+1,bank.Config);foreach(var k in ResourceInventory.Materials)if(p.resources[(int)k]<cost.Resources[(int)k]){var map=k>=BlockKind.Netherrack?MapId.Nether:k>=BlockKind.Sand?MapId.Desert:MapId.Mine;if(WorldCatalog.Open(p,map))return map;}}
            return WorldCatalog.Open(p,MapId.Nether)?MapId.Nether:WorldCatalog.Open(p,MapId.Desert)?MapId.Desert:MapId.Mine;
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
