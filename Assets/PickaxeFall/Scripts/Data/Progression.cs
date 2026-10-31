using System;
using System.Globalization;

namespace PickaxeFall
{
    public enum UpgradeKind { Damage, Durability, Fortune, TeamChance, TeamSize, TntFind, DoubleBench, DoubleBlast, Careful, LastChance }

    public readonly struct UpgradeKey
    {
        public readonly UpgradeKind Kind;
        public readonly int Tier;
        public UpgradeKey(UpgradeKind kind, int tier = 0) { Kind = kind; Tier = tier; }
        public bool IsValid => (int)Kind >= 0 && (int)Kind <= 9 && (Kind > UpgradeKind.Durability || Tier >= 0 && Tier < 5);
        public int Index => Kind == UpgradeKind.Damage ? Tier : Kind == UpgradeKind.Durability ? 5 + Tier : 8 + (int)Kind;
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public int version = 2;
        public long coins;
        public int bestDepth;
        public long[] resources = new long[8];
        public int[] upgrades = new int[18];
        public PlayerProfile Copy() => new() { version = version, coins = coins, bestDepth = bestDepth,
            resources = (long[])resources.Clone(), upgrades = (int[])upgrades.Clone() };
        public int Level(UpgradeKey key) => key.IsValid ? upgrades[key.Index] : 0;
        public void Validate()
        {
            if (version != 2 || coins < 0 || bestDepth < 0 || resources == null || resources.Length != 8 || upgrades == null || upgrades.Length != 18)
                throw new InvalidOperationException("Invalid player profile.");
            foreach (long count in resources) if (count < 0) throw new InvalidOperationException("Negative resource count.");
            foreach (var key in Progression.Keys)
                if (Level(key) < 0 || Level(key) > Progression.MaxLevel(key.Kind)) throw new InvalidOperationException("Invalid upgrade level.");
        }
    }

    public sealed class UpgradeCost
    {
        public long Coins;
        public readonly long[] Resources = new long[8];
    }

    public sealed class RunUpgrades
    {
        readonly int[] levels;
        public RunUpgrades(PlayerProfile profile) { levels = (int[])profile.upgrades.Clone(); }
        public int Level(UpgradeKind kind, int tier = 0) => levels[new UpgradeKey(kind, tier).Index];
        public double Chance(UpgradeKind kind) => Level(kind) * Progression.ChanceStep(kind);
        public int TeamSize => 2 + Level(UpgradeKind.TeamSize);
        public int DamageBonusHalfUnits(int originalTier) => Level(UpgradeKind.Damage, originalTier) * Progression.DamageStepHalfUnits(originalTier);
        public int DurabilityBonus(int originalTier) => Progression.DurabilityStep(originalTier) * Level(UpgradeKind.Durability, originalTier);
    }

    public static class Progression
    {
        public static readonly UpgradeKey[] Keys = CreateKeys();
        public static readonly long[] SellPrices = { 1, 2, 5, 10, 22, 45, 120, 3 };
        static readonly double[] TierPrices = { 1, 1.4, 2.3, 3.4, 5 };
        static readonly int[] BasePrices = { 30, 25, 25, 20, 250, 20, 30, 30, 20, 45 };
        static readonly int[] DamageSteps = { 1, 2, 3, 4, 6 }, DurabilitySteps = { 3, 5, 7, 10, 15 };
        static readonly double[] Growth = { 1.28, 1.26, 1.22, 1.3, 1, 1.25, 1.28, 1.28, 1.25, 1.27 };
        public static string Number(double value) => value.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU"));
        public static int MaxLevel(UpgradeKind kind) => kind switch
        {
            UpgradeKind.TeamSize => 3,
            UpgradeKind.Fortune or UpgradeKind.DoubleBench or UpgradeKind.DoubleBlast => 25,
            UpgradeKind.LastChance or UpgradeKind.TntFind or UpgradeKind.Careful => 40,
            _ => 20
        };
        public static double ChanceStep(UpgradeKind kind) => kind is UpgradeKind.Fortune or UpgradeKind.DoubleBench or UpgradeKind.DoubleBlast ? 0.01 : 0.005;
        public static int DamageStepHalfUnits(int tier) => DamageSteps[tier];
        public static int DurabilityStep(int tier) => DurabilitySteps[tier];
        public static bool IsOre(BlockKind kind) => kind >= BlockKind.Coal && kind <= BlockKind.Diamond;
        public static string Title(UpgradeKind kind) => kind switch
        {
            UpgradeKind.Damage => "Острота", UpgradeKind.Durability => "Крепкая рукоять",
            UpgradeKind.Fortune => "Богатая жила", UpgradeKind.TeamChance => "Отряд шахтёров",
            UpgradeKind.TeamSize => "Размер отряда", UpgradeKind.TntFind => "Взрывная находка",
            UpgradeKind.DoubleBench => "Двойной верстак", UpgradeKind.DoubleBlast => "Усиленный TNT",
            UpgradeKind.Careful => "Бережный удар", _ => "Последний шанс"
        };
        public static string Description(UpgradeKind kind) => kind switch
        {
            UpgradeKind.Damage => "Повышает урон выбранного типа. Редкие кирки получают более сильную прибавку.",
            UpgradeKind.Durability => "Увеличивает запас выбранного типа. Редкие кирки получают больше прочности.",
            UpgradeKind.Fortune => "Шанс получить ещё одну единицу руды, в том числе от взрыва. +1 п.п. за уровень.",
            UpgradeKind.TeamChance => "Один бросок шанса даёт весь отряд одинаковых кирок. +0,5 п.п. за уровень.",
            UpgradeKind.TeamSize => "Увеличивает отряд при удаче. Не повышает шанс его выпадения.",
            UpgradeKind.TntFind => "После добычи киркой руда иногда становится TNT. Ресурс остаётся в рюкзаке. Максимум 20%.",
            UpgradeKind.DoubleBench => "Верстак может повысить тип на две ступени и отремонтировать вдвое больше. +1 п.п. за уровень, до 25%.",
            UpgradeKind.DoubleBlast => "Взрыв охватывает вдвое большую площадь. +1 п.п. за уровень, до 25%.",
            UpgradeKind.Careful => "Шанс не потратить прочность при ударе. +0,5 п.п. за уровень, до 20%.",
            _ => "Один раз за забег последняя кирка может восстановить 25% прочности. +0,5 п.п. за уровень, до 20%."
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
            double tierMultiplier = key.Kind <= UpgradeKind.Durability ? TierPrices[key.Tier] : 1;
            cost.Coins = (long)Math.Ceiling(BasePrices[(int)key.Kind] * tierMultiplier * Math.Pow(Growth[(int)key.Kind], nextLevel - 1));
            cost.Resources[0] = nextLevel <= 5 ? (long)Math.Ceiling((key.Kind == UpgradeKind.Damage ? 8 : 6) * Math.Pow(1.12, nextLevel - 1)) : 0;
            cost.Resources[1] = (long)Math.Ceiling((key.Kind == UpgradeKind.Damage ? 4 : 3) * Math.Pow(1.18, nextLevel - 1));
            for (int stage = 0; stage < 4; stage++)
            {
                int start = 6 + stage * 5;
                if (nextLevel >= start) cost.Resources[stage + 2] += (long)Math.Ceiling(2 * Math.Pow(1.15, nextLevel - start));
            }
            if (key.Kind <= UpgradeKind.Durability && key.Tier >= 2 && nextLevel >= 11)
            {
                int resource = key.Tier == 2 ? 5 : key.Tier == 3 ? 4 : 6;
                cost.Resources[resource] += (long)Math.Ceiling(Math.Pow(1.2, nextLevel - 11));
            }
            return cost;
        }

        static UpgradeKey[] CreateKeys()
        {
            var keys = new UpgradeKey[18];
            for (int tier = 0; tier < 5; tier++) { keys[tier] = new(UpgradeKind.Damage, tier); keys[tier + 5] = new(UpgradeKind.Durability, tier); }
            for (int i = 2; i < 10; i++) keys[i + 8] = new((UpgradeKind)i);
            return keys;
        }
    }
}
