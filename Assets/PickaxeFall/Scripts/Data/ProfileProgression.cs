using System;

namespace PickaxeFall
{
    public enum GoalKind { None, Upgrade, Ability, Bridge, Portal }
    public readonly struct SkillRequirement
    {
        public readonly UpgradeKind Kind;
        public readonly int Level;
        public readonly bool AnyTier;
        public SkillRequirement(UpgradeKind kind,int level,bool anyTier=false){Kind=kind;Level=level;AnyTier=anyTier;}
        public bool Met(PlayerProfile p){for(int t=0;t<(AnyTier?5:1);t++)if(p.Level(new(Kind,t))>=Level)return true;return false;}
    }
    public static class ProfileRules
    {
        public static readonly long[] LevelCoins={250,1200,4000,10000};
        public static readonly long[][] MiningRequirements={
            Materials(150,300,30,0,0,0,0),Materials(0,1800,160,60,15,0,0),
            Materials(0,5000,0,250,100,20,0),Materials(0,12000,1400,650,260,75,3)};
        public static readonly SkillRequirement[][] Skills={
            new[]{new SkillRequirement(UpgradeKind.Damage,3,true),new SkillRequirement(UpgradeKind.Durability,3,true)},
            new[]{new SkillRequirement(UpgradeKind.Fortune,3),new SkillRequirement(UpgradeKind.DoubleBench,3)},
            new[]{new SkillRequirement(UpgradeKind.Careful,5),new SkillRequirement(UpgradeKind.Fortune,5)},
            new[]{new SkillRequirement(UpgradeKind.DoubleBlast,5),new SkillRequirement(UpgradeKind.ImpactPower,3),new SkillRequirement(UpgradeKind.TeamChance,3)}};
        static long[] Materials(params long[] counts){var a=new long[ResourceInventory.SlotCount];Array.Copy(counts,a,counts.Length);return a;}
        public static UpgradeKey Key(int index)=>index<5?new(UpgradeKind.Damage,index):index<10?new(UpgradeKind.Durability,index-5):new((UpgradeKind)(index-8));
        public static int RequiredLevel(int index)=>index switch{
            <10=>1,25=>2,26=>3,
            10 or 14 or 18 or 21=>2,
            16 or 19 or 22=>3,
            15 or 17 or 23 or 11=>4,
            _=>5};
        public static bool Revealed(PlayerProfile p,int index)=>index<10 || p.revealed[index];
        public static bool ValidGoal(GoalKind kind,int index)=>kind switch {GoalKind.None or GoalKind.Bridge or GoalKind.Portal=>index==0,GoalKind.Upgrade=>index>=0&&index<25,GoalKind.Ability=>index==0||index==1,_=>false};
        public static bool Complete(PlayerProfile p,GoalKind kind)=>kind==GoalKind.Bridge?p.bridgeStone==1000&&p.bridgeIron==50:kind==GoalKind.Portal&&p.portalObsidian==10;
    }

    public sealed partial class EconomyService
    {
        public bool SkillAvailable(int index,out string reason)
        {
            reason=null;
            if(index<0||index>=27){reason="Неизвестный навык";return false;}
            int required=ProfileRules.RequiredLevel(index);
            if(Profile.profileLevel<required){reason="Нужен уровень профиля "+required;return false;}
            if(!ProfileRules.Revealed(Profile,index)){reason="Нажми на замок, чтобы открыть ветвь бесплатно";return false;}
            return true;
        }
        public bool TryReveal(int index)
        {
            if(index<10 || index>=27 || Profile.profileLevel<ProfileRules.RequiredLevel(index) || Profile.revealed[index]){LastError="Ветвь ещё недоступна или уже открыта.";return false;}
            var next=Profile.Copy();next.revealed[index]=true;return Commit(next);
        }
        public bool CanLevelUp(out string reason)
        {
            reason=null;if(Profile.profileLevel>=5){reason="Максимальный уровень профиля";return false;}
            int i=Profile.profileLevel-1;
            foreach(var r in ProfileRules.Skills[i])if(!r.Met(Profile)){reason="Нужен навык «"+Progression.Title(r.Kind)+"» уровня "+r.Level;return false;}
            foreach(var kind in ResourceInventory.Materials)if(Profile.lifetimeMined[(int)kind]<ProfileRules.MiningRequirements[i][(int)kind]){reason="Недостаточно добыто: "+GameBalance.BlockName(kind);return false;}
            if(Profile.coins<ProfileRules.LevelCoins[i]){reason="Не хватает монет";return false;}return true;
        }
        public bool TryLevelUp()
        {
            if(!CanLevelUp(out string reason)){LastError=reason;return false;}
            var next=Profile.Copy();next.coins-=ProfileRules.LevelCoins[next.profileLevel-1];next.profileLevel++;return Commit(next);
        }
        public void RecordMined(BlockKind kind)
        {
            kind=ResourceInventory.DropKind(kind);if(!ResourceInventory.IsResource(kind))return;
            Profile.lifetimeMined[(int)kind]=checked(Profile.lifetimeMined[(int)kind]+1);
        }
        public bool TrySetGoal(GoalKind kind,int index=0)
        {
            if(!ProfileRules.ValidGoal(kind,index)){LastError="Некорректная цель";return false;}
            var next=Profile.Copy();next.goalKind=kind;next.goalIndex=index;AdvanceGoal(next);return Commit(next);
        }
        static void AdvanceGoal(PlayerProfile p)
        {
            if(p.goalKind==GoalKind.Upgrade && p.Level(ProfileRules.Key(p.goalIndex))>=Progression.MaxLevel(ProfileRules.Key(p.goalIndex).Kind) ||
               p.goalKind==GoalKind.Ability && (p.goalIndex==0?p.dashUnlocked:p.plungeUnlocked) || ProfileRules.Complete(p,p.goalKind))
            {p.goalKind=GoalKind.None;p.goalIndex=0;}
        }
        public UpgradeCost GoalCost()=>GoalCost(Profile);
        UpgradeCost GoalCost(PlayerProfile p)
        {
            if(p.goalKind==GoalKind.Upgrade){var key=ProfileRules.Key(p.goalIndex);int n=p.Level(key)+1;return n<=Progression.MaxLevel(key.Kind)?Progression.Cost(key,n,Config):new();}
            if(p.goalKind==GoalKind.Ability)return (p.goalIndex==0?p.dashUnlocked:p.plungeUnlocked)?new():Progression.AbilityCost((AbilityKind)p.goalIndex,Config);
            var cost=new UpgradeCost();
            if(p.goalKind==GoalKind.Bridge){cost.Resources[(int)BlockKind.Stone]=1000-p.bridgeStone;cost.Resources[(int)BlockKind.Iron]=50-p.bridgeIron;}
            if(p.goalKind==GoalKind.Portal)cost.Resources[(int)BlockKind.Obsidian]=10-p.portalObsidian;
            return cost;
        }
        public string GoalTitle=>Profile.goalKind switch{
            GoalKind.Upgrade=>Progression.Title(ProfileRules.Key(Profile.goalIndex).Kind)+(Profile.goalIndex<10?" · "+new[]{"дерево","камень","золото","железо","алмаз"}[ProfileRules.Key(Profile.goalIndex).Tier]:"")+" · ур. "+(Profile.Level(ProfileRules.Key(Profile.goalIndex))+1),
            GoalKind.Ability=>Profile.goalIndex==0?"Покупка рывка":"Покупка пробоя",GoalKind.Bridge=>"Мост в пустыню",GoalKind.Portal=>"Портал в ад",_=>"Цель не выбрана"};
        public long Reserved(BlockKind kind)=>Math.Min(Profile.resources[(int)kind],GoalCost().Resources[(int)kind]);
        public long[] SurplusQuote(out long value)
        {
            value=0;var quantities=new long[ResourceInventory.SlotCount];var cost=GoalCost();
            foreach(var kind in ResourceInventory.Materials){int i=(int)kind;quantities[i]=Math.Max(0,Profile.resources[i]-cost.Resources[i]);value=checked(value+checked(quantities[i]*Config.sellPrices[i]));}
            return quantities;
        }
        public bool TrySellSurplus()
        {
            if(Profile.goalKind==GoalKind.None){LastError="Сначала выбери цель.";return false;}
            try{
                var amounts=SurplusQuote(out long value);if(value==0){LastError="Лишних ресурсов пока нет.";return false;}
                var next=Profile.Copy();next.coins=checked(next.coins+value);
                foreach(var kind in ResourceInventory.Materials)next.resources[(int)kind]-=amounts[(int)kind];return Commit(next);
            }catch(OverflowException){LastError="Слишком большая сумма.";return false;}
        }
        public long[] ConstructionQuote(GoalKind kind)
        {
            var p=Profile.Copy();p.goalKind=kind;p.goalIndex=0;var cost=GoalCost(p);var amounts=new long[ResourceInventory.SlotCount];
            if(kind!=GoalKind.Bridge&&kind!=GoalKind.Portal)return amounts;
            foreach(var material in ResourceInventory.Materials)amounts[(int)material]=Math.Min(p.resources[(int)material],cost.Resources[(int)material]);return amounts;
        }
        public bool TryContribute(GoalKind kind)
        {
            var amounts=ConstructionQuote(kind);long total=0;foreach(long amount in amounts)total+=amount;
            if(total==0){LastError="Нет материалов для строительства.";return false;}
            var next=Profile.Copy();foreach(var material in ResourceInventory.Materials)next.resources[(int)material]-=amounts[(int)material];
            if(kind==GoalKind.Bridge){next.bridgeStone+=(int)amounts[(int)BlockKind.Stone];next.bridgeIron+=(int)amounts[(int)BlockKind.Iron];}
            else next.portalObsidian+=(int)amounts[(int)BlockKind.Obsidian];
            AdvanceGoal(next);return Commit(next);
        }
    }
}
namespace PickaxeFall
{
    public sealed partial class EconomyService
    {
        public bool TryAcknowledgeReset(){var next=Profile.Copy();next.resetNotice=false;return Commit(next);}
    }
}
