using System;
using System.Globalization;
using UnityEngine;

namespace PickaxeFall
{
    public enum UpgradeKind { Damage, Durability, Fortune, TeamChance, TeamSize, TntFind, DoubleBench, DoubleBlast, Careful, LastChance, EnergyCapacity, EnergyCharge, EnergyEfficiency, DashForce, PlungeDepth, ImpactPower, Shrapnel, Underwater, HeatShield, TreasureHunter, EarthDigger }

    public readonly struct UpgradeKey
    {
        public readonly UpgradeKind Kind;
        public readonly int Tier;
        public UpgradeKey(UpgradeKind kind, int tier = 0) { Kind = kind; Tier = tier; }
        public bool IsValid => (int)Kind >= 0 && (int)Kind < Progression.KindCount && (Kind > UpgradeKind.Durability || Tier >= 0 && Tier < 5);
        public int RevealIndex => Index < 25 ? Index : Index + 2;
        public int Index => Kind == UpgradeKind.Damage ? Tier : Kind == UpgradeKind.Durability ? 5 + Tier : 8 + (int)Kind;
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public int version = 8;
        public bool[] discovered = new bool[ResourceInventory.SlotCount];
        public MapId selectedMap;
        public int[] mapRecords = new int[3];
        public int profileLevel = 1;
        public long[] lifetimeMined = new long[ResourceInventory.SlotCount];
        public bool[] revealed = new bool[Progression.RevealCount];
        public int bridgeStone, bridgeIron, portalObsidian;
        public GoalKind goalKind;
        public int goalIndex;
        public bool resetNotice;
        public long coins;
        public int bestDepth;
        public long[] resources = new long[ResourceInventory.SlotCount];
        public int[] upgrades = new int[Progression.KeyCount];
        public bool dashUnlocked, plungeUnlocked;
        // JsonUtility expands null inline classes into empty objects. Keep the
        // optional snapshot as a JSON string so a finished run stays truly absent.
        [NonSerialized] public RunSnapshot run;
        public string activeRunJson;
        public PlayerProfile Copy() => new() { version = version, coins = coins, bestDepth = bestDepth,discovered=(bool[])discovered.Clone(),selectedMap=selectedMap,mapRecords=(int[])mapRecords.Clone(),
            profileLevel=profileLevel, lifetimeMined=(long[])lifetimeMined.Clone(), revealed=(bool[])revealed.Clone(),
            bridgeStone=bridgeStone,bridgeIron=bridgeIron,portalObsidian=portalObsidian,goalKind=goalKind,goalIndex=goalIndex,resetNotice=resetNotice,
            resources = (long[])resources.Clone(), upgrades = (int[])upgrades.Clone(), dashUnlocked=dashUnlocked,plungeUnlocked=plungeUnlocked,run=run,activeRunJson=activeRunJson };
        public int Level(UpgradeKey key) => key.IsValid ? upgrades[key.Index] : 0;
        public void Validate()
        {
            if (version != 8 || coins < 0 || bestDepth < 0 || resources == null || resources.Length != ResourceInventory.SlotCount || upgrades == null || upgrades.Length != Progression.KeyCount)
                throw new InvalidOperationException("Invalid player profile.");
            if(profileLevel<1 || profileLevel>5 || lifetimeMined?.Length!=ResourceInventory.SlotCount || revealed?.Length!=Progression.RevealCount || bridgeStone<0 || bridgeStone>1000 || bridgeIron<0 || bridgeIron>50 || portalObsidian<0 || portalObsidian>10 || !ProfileRules.ValidGoal(goalKind,goalIndex))throw new InvalidOperationException("Invalid profile progression.");
            if(discovered?.Length!=ResourceInventory.SlotCount || mapRecords?.Length!=3 || !Enum.IsDefined(typeof(MapId),selectedMap))throw new InvalidOperationException("Invalid world progression.");
            foreach(int n in mapRecords)if(n<0)throw new InvalidOperationException("Invalid world record.");
            foreach(long count in lifetimeMined)if(count<0)throw new InvalidOperationException("Negative lifetime mining.");
            for(int i=0;i<lifetimeMined.Length;i++)if(!ResourceInventory.IsResource((BlockKind)i) && lifetimeMined[i]!=0)throw new InvalidOperationException("Invalid mining slot.");
            foreach (long count in resources) if (count < 0) throw new InvalidOperationException("Negative resource count.");
            for(int i=0;i<resources.Length;i++)if(!ResourceInventory.IsResource((BlockKind)i) && resources[i]!=0)throw new InvalidOperationException("Invalid reserved resource slots.");
            foreach (var key in Progression.Keys)
                if (Level(key) < 0 || Level(key) > Progression.MaxLevel(key.Kind)) throw new InvalidOperationException("Invalid upgrade level.");
            run?.Validate();
        }
    }

    public sealed class UpgradeCost
    {
        public long Coins;
        public readonly long[] Resources = new long[ResourceInventory.SlotCount];
    }

    public sealed class RunUpgrades
    {
        readonly int[] levels;
        public readonly bool DashUnlocked,PlungeUnlocked;
        public readonly ProgressionBalance Config;
        public RunUpgrades(PlayerProfile profile, ProgressionBalance config = null) { Config=config??Progression.Default; levels = (int[])profile.upgrades.Clone();DashUnlocked=profile.dashUnlocked;PlungeUnlocked=profile.plungeUnlocked; }
        public int[] CaptureLevels() => (int[])levels.Clone();
        public int Level(UpgradeKind kind, int tier = 0) => levels[new UpgradeKey(kind, tier).Index];
        public double Chance(UpgradeKind kind) => Level(kind) * Progression.ChanceStep(kind,Config);
        public float MaxEnergy
        {
            get
            {
                int level=Level(UpgradeKind.EnergyCapacity);
                if(Config.energyChargeModel==0)return Config.energyCapacity+Config.capacityStep*level;
                float original=Config.energyCapacity+Config.capacityStep*Math.Min(5,level);
                // Preserve levels 0–5; new levels approach exactly three paid uses.
                float target=3*EnergyCost(Math.Min(Config.dashCost,Config.plungeCost));
                return level<=5?original:Mathf.Lerp(original,target,(level-5)/15f);
            }
        }
        public float EnergyUseCost => EnergyCost(Math.Min(Config.dashCost,Config.plungeCost));
        public float StoredUses => MaxEnergy/EnergyUseCost;
        public float RechargeSeconds => EnergyUseCost/EnergyPerSecond;
        // EnergyCharge keeps its saved index; purchased levels now improve regeneration.
        public float EnergyPerSecond
        {
            get { int level=Level(UpgradeKind.EnergyCharge);return Config.energyRegen+Config.regenStep*(Config.energyChargeModel==0?level:Math.Min(level,20)+1.5f*Math.Max(0,level-20)); }
        }
        public float EnergyCost(float baseCost) => baseCost * (1 - Config.efficiencyStep * Level(UpgradeKind.EnergyEfficiency));
        public float DashSpeed => Config.dashSpeed + Config.dashSpeedStep * Level(UpgradeKind.DashForce);
        public int PlungeBlocks => 1 + Level(UpgradeKind.PlungeDepth);
        public int StrongBonusHalfUnits => Config.impactStep * Level(UpgradeKind.ImpactPower);
        public int ShrapnelHalfUnits => Config.shrapnelStep * Level(UpgradeKind.Shrapnel);
        public float WaterDamageMultiplier(float original) => original + (1-original)*Level(UpgradeKind.Underwater)/10f;
        public float LavaWearMultiplier => 1-.05f*Level(UpgradeKind.HeatShield);
        public int TeamSize => 2 + Level(UpgradeKind.TeamSize);
        public int DamageBonusHalfUnits(int originalTier) => Level(UpgradeKind.Damage, originalTier) * Progression.DamageStepHalfUnits(originalTier,Config);
        public int DurabilityBonus(int originalTier) => Progression.DurabilityStep(originalTier,Config) * Level(UpgradeKind.Durability, originalTier);
    }

    public static class Progression
    {
        public const int KindCount=21, KeyCount=29, RevealCount=31;
        public static readonly UpgradeKey[] Keys = CreateKeys();
        public static readonly ProgressionBalance Default = new();
        public static long[] SellPrices => Default.sellPrices;
        public static string Number(double value) => value.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));
        public static int MaxLevel(UpgradeKind kind) => kind switch
        {
            UpgradeKind.TeamSize or UpgradeKind.PlungeDepth => 3,
            UpgradeKind.DashForce => 5,
            UpgradeKind.EnergyCapacity => 20,
            UpgradeKind.EnergyCharge => 40,
            UpgradeKind.EnergyEfficiency or UpgradeKind.ImpactPower or UpgradeKind.Shrapnel or UpgradeKind.Underwater or UpgradeKind.HeatShield or UpgradeKind.TreasureHunter => 10,
            UpgradeKind.EarthDigger => 20,
            UpgradeKind.Fortune or UpgradeKind.DoubleBench or UpgradeKind.DoubleBlast => 25,
            UpgradeKind.LastChance or UpgradeKind.TntFind or UpgradeKind.Careful => 40,
            _ => 20
        };
        public static double ChanceStep(UpgradeKind kind, ProgressionBalance config=null) => (config??Default).chanceSteps[(int)kind];
        public static int DamageStepHalfUnits(int tier, ProgressionBalance config=null) => (config??Default).damageSteps[tier];
        public static int DurabilityStep(int tier, ProgressionBalance config=null) => (config??Default).durabilitySteps[tier];
        public static bool IsOre(BlockKind kind) => kind >= BlockKind.Coal && kind <= BlockKind.Diamond || kind >= BlockKind.Emerald && kind <= BlockKind.AmethystCluster || WorldCatalog.IsNewOre(kind);
        public static readonly BlockKind[] VeinOres={BlockKind.Coal,BlockKind.Copper,BlockKind.Iron,BlockKind.Gold,BlockKind.Diamond,BlockKind.Emerald,BlockKind.Lapis,BlockKind.Redstone};
        public static UpgradeCost AbilityCost(AbilityKind kind, ProgressionBalance config=null)
        {
            var c=config??Default;int index=kind==AbilityKind.Dash?0:1;
            var cost=new UpgradeCost { Coins=c.unlockCoins[index] };
            cost.Resources[(int)BlockKind.Stone]=c.unlockStone[index];
            cost.Resources[(int)(kind==AbilityKind.Dash?BlockKind.Coal:BlockKind.Copper)]=c.unlockOre[index];
            return Scale(cost,c.recipeScale);
        }
        public static string Title(UpgradeKind kind) => kind switch
        {
            UpgradeKind.Damage => "Острота", UpgradeKind.Durability => "Крепкая рукоять",
            UpgradeKind.Fortune => "Богатая жила", UpgradeKind.TeamChance => "Отряд шахтёров",
            UpgradeKind.TeamSize => "Размер отряда", UpgradeKind.TntFind => "Взрывная находка",
            UpgradeKind.DoubleBench => "Двойной верстак", UpgradeKind.DoubleBlast => "Усиленный TNT",
            UpgradeKind.Careful => "Бережный удар", UpgradeKind.LastChance => "Последний шанс",
            UpgradeKind.EnergyCapacity => "Запас энергии", UpgradeKind.EnergyCharge => "Восстановление энергии",
            UpgradeKind.EnergyEfficiency => "Экономия энергии", UpgradeKind.DashForce => "Сила рывка",
            UpgradeKind.PlungeDepth => "Глубокий пробой", UpgradeKind.ImpactPower => "Мощный удар", UpgradeKind.Shrapnel => "Осколочный удар",
            UpgradeKind.Underwater => "Подводник", UpgradeKind.HeatShield => "Термозащита", UpgradeKind.TreasureHunter => "Искатель сокровищ", _ => "Землекоп"
        };
        public static string Description(UpgradeKind kind, ProgressionBalance config=null) => kind switch
        {
            UpgradeKind.Damage => "Повышает урон выбранного вида кирки. Точная прибавка показана на карточке.",
            UpgradeKind.Durability => "Увеличивает прочность выбранного вида кирки. Верстак восстанавливает весь запас.",
            UpgradeKind.Fortune => "Шанс получить ещё одну единицу руды. Работает и при добыче взрывом.",
            UpgradeKind.TeamChance => "Шанс начать спуск с отрядом одинаковых кирок. Их количество увеличивает «Размер отряда».",
            UpgradeKind.TeamSize => "Добавляет кирку в выпавший отряд. Сам шанс отряда не меняется.",
            UpgradeKind.TntFind => "Добытая прямым ударом руда может вызвать небольшой взрыв. Добыча остаётся в рюкзаке.",
            UpgradeKind.DoubleBench => "Шанс дополнительно повысить вид кирки на верстаке. Если кирка уже алмазная, пополняет энергию.",
            UpgradeKind.DoubleBlast => "Шанс взорвать вдвое большую площадь. Урон и добыча работают как у обычного взрыва.",
            UpgradeKind.Careful => "Шанс не потратить прочность при ударе. От лавы защищает отдельный навык.",
            UpgradeKind.LastChance => "Последняя кирка может восстановить половину прочности и энергию на пробой. Одна попытка за спуск.",
            UpgradeKind.EnergyCapacity => "Увеличивает запас энергии. На полной прокачке помещается три применения приёмов.",
            UpgradeKind.EnergyCharge => "Ускоряет восстановление энергии. На карточке показано время до одного применения.",
            UpgradeKind.EnergyEfficiency => "Снижает расход энергии на рывок и пробой.",
            UpgradeKind.DashForce => "Усиливает толчок рывка. Как и раньше, рывок сразу пробивает один блок.",
            UpgradeKind.PlungeDepth => "Позволяет пробить больше блоков одним нажатием.",
            UpgradeKind.ImpactPower => "Увеличивает урон сильного столкновения. На мгновенное пробитие приёмов не влияет.",
            UpgradeKind.Shrapnel => "Сильный удар повреждает один соседний блок. Дополнительная прочность не тратится.",
            UpgradeKind.Underwater => "Уменьшает штраф урона в воде. На последнем уровне кирка бьёт так же, как на суше. Сопротивление воды остаётся.",
            UpgradeKind.HeatShield => "Уменьшает потерю прочности в лаве. Полная прокачка снижает её вдвое.",
            UpgradeKind.EarthDigger => "Шанс добыть ещё один блок земли или песка прямым ударом. Взрывы и осколки бонус не дают.",
            _ => "Сундук может дать дополнительную порцию ресурсов. Редчайшие находки и гарантированный обсидиан не дублируются. На спавнер не действует."
        };

        public static UpgradeCost Cost(UpgradeKey key, int nextLevel, ProgressionBalance config=null)
        {
            if (!key.IsValid || nextLevel < 1 || nextLevel > MaxLevel(key.Kind)) throw new ArgumentOutOfRangeException(nameof(nextLevel));
            var c=config??Default;
            var cost = new UpgradeCost();
            if (key.Kind == UpgradeKind.TeamSize)
            {
                cost.Coins = c.teamCoins[nextLevel-1];
                for(int i=0;i<ResourceInventory.SlotCount;i++)cost.Resources[i]=c.teamMaterials[(nextLevel-1)*ResourceInventory.SlotCount+i];
                if(c.worldRecipes)WorldCatalog.AddRecipe(cost,key,nextLevel);
                return Scale(cost,c.recipeScale*c.recipeMultipliers[(int)key.Kind]);
            }
            if (key.Kind == UpgradeKind.PlungeDepth)
            {
                cost.Coins = c.plungeCoins[nextLevel-1];
                for(int i=0;i<ResourceInventory.SlotCount;i++)cost.Resources[i]=c.plungeMaterials[(nextLevel-1)*ResourceInventory.SlotCount+i];
                if(c.worldRecipes)WorldCatalog.AddRecipe(cost,key,nextLevel);
                return Scale(cost,c.recipeScale*c.recipeMultipliers[(int)key.Kind]);
            }
            int recipeLevel = key.Kind >= UpgradeKind.EnergyCapacity ? 1 + 19 * (nextLevel - 1) / (MaxLevel(key.Kind) - 1) : nextLevel;
            double tierMultiplier = key.Kind <= UpgradeKind.Durability ? c.tierPrices[key.Tier] : 1;
            cost.Coins = (long)Math.Ceiling(c.basePrices[(int)key.Kind] * tierMultiplier * Math.Pow(c.growth[(int)key.Kind], nextLevel - 1));
            cost.Resources[0] = recipeLevel <= 5 ? (long)Math.Ceiling((key.Kind == UpgradeKind.Damage ? c.dirtDamage : c.dirtOther) * Math.Pow(c.dirtGrowth, recipeLevel - 1)) : 0;
            cost.Resources[1] = (long)Math.Ceiling((key.Kind == UpgradeKind.Damage ? c.stoneDamage : c.stoneOther) * Math.Pow(c.stoneGrowth, recipeLevel - 1));
            for (int stage = 0; stage < 4; stage++)
            {
                int start = c.oreGates[stage];
                if (recipeLevel >= start) cost.Resources[stage + 2] += (long)Math.Ceiling(c.stageOre * Math.Pow(c.oreGrowth, recipeLevel - start));
            }
            if (key.Kind <= UpgradeKind.Durability && key.Tier >= 2 && recipeLevel >= 11)
            {
                int resource = key.Tier == 2 ? 5 : key.Tier == 3 ? 4 : 6;
                cost.Resources[resource] += (long)Math.Ceiling(c.tierOre*Math.Pow(c.tierOreGrowth, recipeLevel - 11));
            }
            if(c.worldRecipes)WorldCatalog.AddRecipe(cost,key,nextLevel);
                return Scale(cost,c.recipeScale*c.recipeMultipliers[(int)key.Kind]);
        }

        static UpgradeCost Scale(UpgradeCost cost,float scale)
        {
            for(int i=0;i<cost.Resources.Length;i++)cost.Resources[i]=(long)Math.Ceiling(cost.Resources[i]*(double)scale);
            return cost;
        }

        static UpgradeKey[] CreateKeys()
        {
            var keys = new UpgradeKey[KeyCount];
            for (int tier = 0; tier < 5; tier++) { keys[tier] = new(UpgradeKind.Damage, tier); keys[tier + 5] = new(UpgradeKind.Durability, tier); }
            for (int i = 2; i < KindCount; i++) keys[i + 8] = new((UpgradeKind)i);
            return keys;
        }
    }
}
