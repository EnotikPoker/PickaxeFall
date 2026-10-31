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
        public double Chance(UpgradeKind kind) => Level(kind) * (kind == UpgradeKind.Fortune ? 0.01 : 0.005);
        public int TeamSize => 2 + Level(UpgradeKind.TeamSize);
        public int DamageBonusHalfUnits(int originalTier) => Level(UpgradeKind.Damage, originalTier);
        public int DurabilityBonus(int originalTier) => 2 * Level(UpgradeKind.Durability, originalTier);
    }

    public static class Progression
    {
        public static readonly UpgradeKey[] Keys = CreateKeys();
        public static readonly long[] SellPrices = { 1, 3, 8, 18, 45, 100, 300, 5 };
        static readonly double[] TierPrices = { 1, 1.3, 1.6, 2, 3 };
        static readonly int[] BasePrices = { 30, 20, 20, 20, 150, 15, 15, 20, 15, 25 };
        static readonly double[] Growth = { 1.4, 1.35, 1.45, 1.6, 1, 1.6, 1.6, 1.6, 1.5, 1.7 };
        public static string Number(double value) => value.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU"));
        public static int MaxLevel(UpgradeKind kind) => kind == UpgradeKind.TeamSize ? 3 : kind == UpgradeKind.Fortune ? 25 : kind == UpgradeKind.LastChance ? 10 : 20;
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
            UpgradeKind.Damage => "+0,5 урона за уровень. Только для выбранной кирки.",
            UpgradeKind.Durability => "+2 прочности за уровень. Только для выбранной кирки.",
            UpgradeKind.Fortune => "Шанс получить ещё одну единицу руды, в том числе от взрыва. +1 п.п. за уровень.",
            UpgradeKind.TeamChance => "Один бросок шанса даёт весь отряд одинаковых кирок. +0,5 п.п. за уровень.",
            UpgradeKind.TeamSize => "Увеличивает отряд при удаче. Не повышает шанс его выпадения.",
            UpgradeKind.TntFind => "После добычи киркой блок иногда становится TNT. Ресурс остаётся в рюкзаке.",
            UpgradeKind.DoubleBench => "Верстак повышает тип кирки сразу на две ступени и полностью чинит её.",
            UpgradeKind.DoubleBlast => "Редкий взрыв охватывает вдвое большую площадь.",
            UpgradeKind.Careful => "Шанс не потратить прочность при ударе. +0,5 п.п. за уровень.",
            _ => "Один раз за забег последняя кирка может восстановить 25% прочности. +0,5 п.п. за уровень."
        };

        public static UpgradeCost Cost(UpgradeKey key, int nextLevel)
        {
            if (!key.IsValid || nextLevel < 1 || nextLevel > MaxLevel(key.Kind)) throw new ArgumentOutOfRangeException(nameof(nextLevel));
            var cost = new UpgradeCost();
            if (key.Kind == UpgradeKind.TeamSize)
            {
                cost.Coins = new long[] { 150, 500, 1500 }[nextLevel - 1];
                if (nextLevel == 1) { cost.Resources[1] = 8; cost.Resources[2] = 8; }
                if (nextLevel == 2) { cost.Resources[1] = 20; cost.Resources[2] = 10; cost.Resources[3] = 8; }
                if (nextLevel == 3) { cost.Resources[3] = 20; cost.Resources[4] = 10; cost.Resources[5] = 4; cost.Resources[6] = 1; }
                return cost;
            }
            double tierMultiplier = key.Kind <= UpgradeKind.Durability ? TierPrices[key.Tier] : 1;
            cost.Coins = (long)Math.Ceiling(BasePrices[(int)key.Kind] * tierMultiplier * Math.Pow(Growth[(int)key.Kind], nextLevel - 1));
            cost.Resources[0] = (long)Math.Ceiling((key.Kind == UpgradeKind.Damage ? 8 : 6) * Math.Pow(1.25, nextLevel - 1));
            cost.Resources[1] = (long)Math.Ceiling((key.Kind == UpgradeKind.Damage ? 4 : 3) * Math.Pow(1.25, nextLevel - 1));
            for (int stage = 0; stage < 4; stage++)
            {
                int start = 6 + stage * 5;
                if (nextLevel >= start) cost.Resources[stage + 2] += (long)Math.Ceiling(2 * Math.Pow(1.2, nextLevel - start));
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
