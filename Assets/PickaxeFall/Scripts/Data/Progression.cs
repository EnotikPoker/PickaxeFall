using System;
using System.Globalization;

namespace PickaxeFall
{
    public enum UpgradeKind { Damage, Durability, Fortune, TeamChance, TeamSize, TntFind, DoubleBench, DoubleBlast, Careful, LastChance, EnergyCapacity, EnergyCharge, EnergyEfficiency, DashForce, PlungeDepth, ImpactPower, Shrapnel }

    public readonly struct UpgradeKey
    {
        public readonly UpgradeKind Kind;
        public readonly int Tier;
        public UpgradeKey(UpgradeKind kind, int tier = 0) { Kind = kind; Tier = tier; }
        public bool IsValid => (int)Kind >= 0 && (int)Kind <= 16 && (Kind > UpgradeKind.Durability || Tier >= 0 && Tier < 5);
        public int Index => Kind == UpgradeKind.Damage ? Tier : Kind == UpgradeKind.Durability ? 5 + Tier : 8 + (int)Kind;
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public int version = 4;
        public long coins;
        public int bestDepth;
        public long[] resources = new long[ResourceInventory.SlotCount];
        public int[] upgrades = new int[25];
        public bool dashUnlocked, plungeUnlocked;
        // JsonUtility expands null inline classes into empty objects. Keep the
        // optional snapshot as a JSON string so a finished run stays truly absent.
        [NonSerialized] public RunSnapshot run;
        public string activeRunJson;
        public PlayerProfile Copy() => new() { version = version, coins = coins, bestDepth = bestDepth,
            resources = (long[])resources.Clone(), upgrades = (int[])upgrades.Clone(), dashUnlocked=dashUnlocked,plungeUnlocked=plungeUnlocked,run=run,activeRunJson=activeRunJson };
        public int Level(UpgradeKey key) => key.IsValid ? upgrades[key.Index] : 0;
        public void Validate()
        {
            if (version != 4 || coins < 0 || bestDepth < 0 || resources == null || resources.Length != ResourceInventory.SlotCount || upgrades == null || upgrades.Length != 25)
                throw new InvalidOperationException("Invalid player profile.");
            foreach (long count in resources) if (count < 0) throw new InvalidOperationException("Negative resource count.");
            if(resources[8]!=0 || resources[9]!=0)throw new InvalidOperationException("Invalid reserved resource slots.");
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
        public RunUpgrades(PlayerProfile profile) { levels = (int[])profile.upgrades.Clone();DashUnlocked=profile.dashUnlocked;PlungeUnlocked=profile.plungeUnlocked; }
        public int[] CaptureLevels() => (int[])levels.Clone();
        public int Level(UpgradeKind kind, int tier = 0) => levels[new UpgradeKey(kind, tier).Index];
        public double Chance(UpgradeKind kind) => Level(kind) * Progression.ChanceStep(kind);
        public float MaxEnergy => 60 + 8 * Level(UpgradeKind.EnergyCapacity);
        // EnergyCharge keeps its saved index; purchased levels now improve regeneration.
        public float EnergyPerSecond => 2 + 0.04f * Level(UpgradeKind.EnergyCharge);
        public float EnergyCost(float baseCost) => baseCost * (1 - 0.01f * Level(UpgradeKind.EnergyEfficiency));
        public float DashSpeed => 10 + 0.6f * Level(UpgradeKind.DashForce);
        public int PlungeBlocks => 1 + Level(UpgradeKind.PlungeDepth);
        public int StrongBonusHalfUnits => Level(UpgradeKind.ImpactPower);
        public int ShrapnelHalfUnits => Level(UpgradeKind.Shrapnel);
        public int TeamSize => 2 + Level(UpgradeKind.TeamSize);
        public int DamageBonusHalfUnits(int originalTier) => Level(UpgradeKind.Damage, originalTier) * Progression.DamageStepHalfUnits(originalTier);
        public int DurabilityBonus(int originalTier) => Progression.DurabilityStep(originalTier) * Level(UpgradeKind.Durability, originalTier);
    }

    public static class Progression
    {
        public static readonly UpgradeKey[] Keys = CreateKeys();
        public static readonly long[] SellPrices = { 1, 2, 5, 10, 22, 45, 120, 3, 0, 0, 240, 16, 12, 30 };
        static readonly double[] TierPrices = { 1, 1.4, 2.3, 3.4, 5 };
        static readonly int[] BasePrices = { 30, 25, 10, 10, 250, 8, 12, 12, 6, 12, 40, 25, 30, 60, 250, 60, 55 };
        static readonly int[] DamageSteps = { 1, 2, 3, 4, 8 }, DurabilitySteps = { 3, 6, 9, 14, 32 };
        static readonly double[] Growth = { 1.28, 1.26, 1.3, 1.35, 1, 1.19, 1.32, 1.32, 1.18, 1.18, 2.2, 1.22, 1.5, 2.5, 1, 1.55, 1.45 };
        public static string Number(double value) => value.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU"));
        public static int MaxLevel(UpgradeKind kind) => kind switch
        {
            UpgradeKind.TeamSize or UpgradeKind.PlungeDepth => 3,
            UpgradeKind.EnergyCapacity or UpgradeKind.DashForce => 5,
            UpgradeKind.EnergyEfficiency or UpgradeKind.ImpactPower or UpgradeKind.Shrapnel => 10,
            UpgradeKind.Fortune or UpgradeKind.DoubleBench or UpgradeKind.DoubleBlast => 25,
            UpgradeKind.LastChance or UpgradeKind.TntFind or UpgradeKind.Careful => 40,
            _ => 20
        };
        public static double ChanceStep(UpgradeKind kind) => kind switch
        {
            UpgradeKind.Fortune => 0.02,
            UpgradeKind.DoubleBench or UpgradeKind.DoubleBlast => 0.01,
            UpgradeKind.TeamChance or UpgradeKind.TntFind or UpgradeKind.Careful or UpgradeKind.LastChance => 0.005,
            _ => 0
        };
        public static int DamageStepHalfUnits(int tier) => DamageSteps[tier];
        public static int DurabilityStep(int tier) => DurabilitySteps[tier];
        public static bool IsOre(BlockKind kind) => kind >= BlockKind.Coal && kind <= BlockKind.Diamond || kind >= BlockKind.Emerald && kind <= BlockKind.AmethystCluster;
        public static readonly BlockKind[] VeinOres={BlockKind.Coal,BlockKind.Copper,BlockKind.Iron,BlockKind.Gold,BlockKind.Diamond,BlockKind.Emerald,BlockKind.Lapis,BlockKind.Redstone};
        public static UpgradeCost AbilityCost(AbilityKind kind)
        {
            var cost=new UpgradeCost { Coins=kind==AbilityKind.Dash?200:350 };
            cost.Resources[(int)BlockKind.Stone]=kind==AbilityKind.Dash?20:30;
            cost.Resources[(int)(kind==AbilityKind.Dash?BlockKind.Coal:BlockKind.Copper)]=kind==AbilityKind.Dash?10:6;
            return cost;
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
            UpgradeKind.PlungeDepth => "Глубокий пробой", UpgradeKind.ImpactPower => "Мощный удар", _ => "Осколочный удар"
        };
        public static string Description(UpgradeKind kind) => kind switch
        {
            UpgradeKind.Damage => "Повышает урон выбранного типа. Редкие кирки получают более сильную прибавку.",
            UpgradeKind.Durability => "Увеличивает запас выбранного типа. Редкие кирки получают больше прочности.",
            UpgradeKind.Fortune => "Шанс получить ещё одну единицу руды, в том числе от взрыва. +2 п.п. за уровень, до 50%.",
            UpgradeKind.TeamChance => "Один бросок шанса даёт весь отряд одинаковых кирок. +0,5 п.п. за уровень.",
            UpgradeKind.TeamSize => "Увеличивает отряд при удаче. Не повышает шанс его выпадения.",
            UpgradeKind.TntFind => "После прямой добычи руда иногда взрывается малым взрывом радиусом 1,5 м. Ресурс остаётся в рюкзаке. Максимум 20%.",
            UpgradeKind.DoubleBench => "Верстак полностью чинит кирку, повышает тип на две ступени и заполняет общую энергию. +1 п.п. за уровень, до 25%.",
            UpgradeKind.DoubleBlast => "Взрыв охватывает вдвое большую площадь. +1 п.п. за уровень, до 25%.",
            UpgradeKind.Careful => "Шанс не потратить прочность при ударе. +0,5 п.п. за уровень, до 20%.",
            UpgradeKind.LastChance => "Один раз за забег последняя кирка может восстановить 50% прочности и энергию на пробой. +0,5 п.п. за уровень, до 20%.",
            UpgradeKind.EnergyCapacity => "+8 к общему запасу. Спуск начинается с полной энергией. Максимум 100.",
            UpgradeKind.EnergyCharge => "+0,04 энергии в секунду за уровень: от 2 до 2,8. Восстановление идёт только во время спуска и не зависит от ударов и числа кирок.",
            UpgradeKind.EnergyEfficiency => "Рывок и пробой расходуют на 1% меньше энергии за уровень. Максимум −10%.",
            UpgradeKind.DashForce => "+0,6 м/с к начальному импульсу рывка. Пробивает один блок при любой прокачке.",
            UpgradeKind.PlungeDepth => "+1 пробиваемый блок к удару вниз за уровень. От одного до четырёх блоков.",
            UpgradeKind.ImpactPower => "+0,5 урона при обычном столкновении от 4 м/с. Не влияет на пробитие способностей.",
            _ => "Сильный обычный удар повреждает один соседний материал: +0,5 урона за уровень. Направления чередуются. Без дополнительного износа."
        };

        public static UpgradeCost Cost(UpgradeKey key, int nextLevel)
        {
            if (!key.IsValid || nextLevel < 1 || nextLevel > MaxLevel(key.Kind)) throw new ArgumentOutOfRangeException(nameof(nextLevel));
            var cost = new UpgradeCost();
            if (key.Kind == UpgradeKind.TeamSize)
            {
                cost.Coins = new long[] { 250, 1000, 4000 }[nextLevel - 1];
                if (nextLevel == 1) { cost.Resources[1] = 8; cost.Resources[2] = 8; }
                if (nextLevel == 2) { cost.Resources[1] = 20; cost.Resources[2] = 10; cost.Resources[3] = 8; }
                if (nextLevel == 3) { cost.Resources[3] = 20; cost.Resources[4] = 10; cost.Resources[5] = 4; cost.Resources[6] = 1; }
                return cost;
            }
            if (key.Kind == UpgradeKind.PlungeDepth)
            {
                cost.Coins = new long[] { 250, 1200, 5000 }[nextLevel - 1];
                if (nextLevel == 1) { cost.Resources[1] = 12; cost.Resources[2] = 6; cost.Resources[3] = 2; }
                if (nextLevel == 2) { cost.Resources[1] = 30; cost.Resources[2] = 12; cost.Resources[3] = 8; cost.Resources[4] = 4; }
                if (nextLevel == 3) { cost.Resources[3] = 20; cost.Resources[4] = 10; cost.Resources[5] = 4; cost.Resources[6] = 1; }
                return cost;
            }
            int recipeLevel = key.Kind >= UpgradeKind.EnergyCapacity ? 1 + 19 * (nextLevel - 1) / (MaxLevel(key.Kind) - 1) : nextLevel;
            double tierMultiplier = key.Kind <= UpgradeKind.Durability ? TierPrices[key.Tier] : 1;
            cost.Coins = (long)Math.Ceiling(BasePrices[(int)key.Kind] * tierMultiplier * Math.Pow(Growth[(int)key.Kind], nextLevel - 1));
            cost.Resources[0] = recipeLevel <= 5 ? (long)Math.Ceiling((key.Kind == UpgradeKind.Damage ? 8 : 6) * Math.Pow(1.12, recipeLevel - 1)) : 0;
            cost.Resources[1] = (long)Math.Ceiling((key.Kind == UpgradeKind.Damage ? 4 : 3) * Math.Pow(1.18, recipeLevel - 1));
            for (int stage = 0; stage < 4; stage++)
            {
                int start = 6 + stage * 5;
                if (recipeLevel >= start) cost.Resources[stage + 2] += (long)Math.Ceiling(2 * Math.Pow(1.15, recipeLevel - start));
            }
            if (key.Kind <= UpgradeKind.Durability && key.Tier >= 2 && recipeLevel >= 11)
            {
                int resource = key.Tier == 2 ? 5 : key.Tier == 3 ? 4 : 6;
                cost.Resources[resource] += (long)Math.Ceiling(Math.Pow(1.2, recipeLevel - 11));
            }
            return cost;
        }

        static UpgradeKey[] CreateKeys()
        {
            var keys = new UpgradeKey[25];
            for (int tier = 0; tier < 5; tier++) { keys[tier] = new(UpgradeKind.Damage, tier); keys[tier + 5] = new(UpgradeKind.Durability, tier); }
            for (int i = 2; i < 17; i++) keys[i + 8] = new((UpgradeKind)i);
            return keys;
        }
    }
}
