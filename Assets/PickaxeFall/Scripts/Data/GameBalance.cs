using System;
using UnityEngine;

namespace PickaxeFall
{
    // Append-only IDs preserve all existing resource slots and saved block kinds.
    public enum BlockKind { Dirt, Stone, Coal, Copper, Iron, Gold, Diamond, Deepstone, Workbench, Tnt,
        Emerald, Lapis, Redstone, Amethyst, AmethystCluster, Cobblestone, MossyCobblestone, Chest, Spawner, UpgradeWorkbench, Obsidian, Water, Lava, Sand, Sandstone, DarkQuartzite, Malachite, Turquoise, Garnet, SmokyQuartz, Netherrack, Basalt, Blackstone, Quartz, Sulfur, Cinnabar, FireOpal, AncientDebris, SoulCrystal, Timber, Rail, MinecartChest, NetherBrick }
    public enum RunPhase { Ready, Spinning, Equipped, Mining, Breaking, Finished }

    [Serializable]
    public class BlockStats
    {
        public BlockKind kind;
        [Min(1)] public int health;
        [Min(0)] public int minimumDepth;
        [Range(0, 1)] public float oreChance;
        public BlockStats(BlockKind kind, int health, int depth, float chance = 0)
        { this.kind = kind; this.health = health; minimumDepth = depth; oreChance = chance; }
    }

    [Serializable]
    public class PickaxeStats
    {
        public string title;
        public string rarity;
        public Color color;
        [Min(1)] public int damage;
        [Min(1)] public int durability;
        [Min(0.01f)] public float weight;
        [Min(0.1f)] public float mass;

        public PickaxeStats(string title, string rarity, Color color, int damage, int durability, float weight, float mass)
        {
            this.title = title; this.rarity = rarity; this.color = color;
            this.damage = damage; this.durability = durability; this.weight = weight; this.mass = mass;
        }
    }

    [CreateAssetMenu(menuName = "PickaxeFall/Game balance")]
    public partial class GameBalance : ScriptableObject
    {
        public int rulesVersion = 3;
        public MapId map;
        void OnEnable() { ExpandBlocks(); progression.ExpandSlots();if(locations==null||locations.Length!=3)locations=LocationBalance.Defaults(); }
        public void ExpandBlocks() { int count=Enum.GetValues(typeof(BlockKind)).Length;int old=blocks.Length;Array.Resize(ref blocks,count);for(int i=old;i<count;i++)blocks[i]=new BlockStats((BlockKind)i,WorldCatalog.Health((BlockKind)i),0); }
        public ProgressionBalance progression = new();
        public static bool IsWorkbench(BlockKind kind)=>kind==BlockKind.Workbench || kind==BlockKind.UpgradeWorkbench;
        [Range(0,1)] public float upgradeWorkbenchFraction=.2f;
        public int[] benchDepths = {6,30,75,125,200,300};
        public float[] benchDensity = {1,.8f,.5f,.3f,.18f,.1f};
        public float WorkbenchChanceAt(int row)
        {
            if(rulesVersion<2)return workbenchChance;
            float multiplier=benchDensity[benchDensity.Length-1];
            if(row<=benchDepths[0])multiplier=benchDensity[0];
            else for(int i=1;i<benchDepths.Length;i++)if(row<=benchDepths[i]){multiplier=Mathf.Lerp(benchDensity[i-1],benchDensity[i],(row-benchDepths[i-1])/(float)(benchDepths[i]-benchDepths[i-1]));break;}
            return workbenchChance*multiplier;
        }
        [Header("Small finite lakes / chance per 32-row band")]
        [Range(0,1)] public float waterLakeChance=.16f,lavaLakeChance=.20f,mixedLakeChance=.05f;
        [Range(0,.01f)] public float lavaPocketChance=.0015f;
        public int waterMinimumDepth=8,lavaMinimumDepth=35;
        public float lavaWearPerSecond=8,waterDamageMultiplier=.7f;
        [Header("Fluid resistance / per second at full immersion")]
        public float waterDrag=1.6f,lavaDrag=2.4f,waterSpinDrag=2.8f,lavaSpinDrag=3.6f;
        [Header("World (one block = one metre)")]
        [Min(15)] public int worldWidth = 41;
        [Min(20)] public int rowsAhead = 32;
        [Min(8)] public int rowsBehind = 18;
        [Min(1)] public int workbenchMinimumDepth = 6;
        [Range(0, 0.05f)] public float workbenchChance = 0.011f;
        [Min(2)] public int workbenchSpacing = 6;
        [Range(0, 1)] public float caveChance = 0.04f;
        [Min(4)] public float spawnHeight = 7.5f;
        [Header("TNT")]
        [Min(1)] public int tntMinimumDepth = 5;
        [Range(0, 0.1f)] public float tntChance = 0.007f;
        [Min(2)] public int tntSpacing = 3;
        [Min(1)] public float explosionRadius = 2.1f;
        [Header("Physics")]
        public float gravity = -15;
        public float maxFallSpeed = 22;
        public float minimumImpactSpeed = 1.2f;
        public float strongImpactSpeed = 4;
        public float impactRearmSpeed = 0.5f;
        public float surfaceFriction = 0.3f;
        public float launchAngularSpeed = 260;
        public float maximumAngularSpeed = 540;
        [Range(0, 1)] public float restitution = 0.78f;
        [Min(0)] public float minimumReboundSpeed = 2.8f;
        [Min(0)] public float maximumReboundSpeed = 6.2f;
        [Header("Depth hardness")]
        [Min(0)] public int hardnessStartDepth = 60;
        [Min(20)] public int hardnessDoublingDepth = 70;
        [Header("Rare structures / probability per 64-row band")]
        [Range(0,1)] public float dungeonChance=.12f;
        [Range(0,1)] public float geodeChance=.18f;
        [Header("Blocks / ore chance is an absolute probability per cell")]
        public BlockStats[] blocks =
        {
            new(BlockKind.Dirt, 1, 0), new(BlockKind.Stone, 6, 3),
            new(BlockKind.Coal, 8, 3, 0.15f), new(BlockKind.Copper, 11, 8, 0.08f),
            new(BlockKind.Iron, 14, 18, 0.055f), new(BlockKind.Gold, 18, 35, 0.03f),
            new(BlockKind.Diamond, 22, 90, 0.006f), new(BlockKind.Deepstone, 10, 75),
            new(BlockKind.Workbench, 1, 6), new(BlockKind.Tnt, 1, 5),
            new(BlockKind.Emerald,30,75,.0015f),new(BlockKind.Lapis,18,75,.024f),new(BlockKind.Redstone,16,75,.035f),
            new(BlockKind.Amethyst,12,30),new(BlockKind.AmethystCluster,4,30),new(BlockKind.Cobblestone,8,30),
            new(BlockKind.MossyCobblestone,8,30),new(BlockKind.Chest,1,30),new(BlockKind.Spawner,18,30),new(BlockKind.UpgradeWorkbench,1,6),new(BlockKind.Obsidian,55,35),new(BlockKind.Water,1,8),new(BlockKind.Lava,1,35)
        };
        [Header("Pickaxes / weighted roulette")]
        public PickaxeStats[] pickaxes =
        {
            new("Деревянная", "ОБЫЧНАЯ", new Color32(184, 130, 78, 255), 2, 64, 45, 1),
            new("Каменная", "НЕОБЫЧНАЯ", new Color32(173, 188, 198, 255), 4, 100, 30, 1.2f),
            new("Золотая", "РЕДКАЯ", new Color32(255, 203, 92, 255), 6, 140, 15, 1.3f),
            new("Железная", "ЭПИЧЕСКАЯ", new Color32(226, 235, 232, 255), 10, 160, 8, 1.4f),
            new("Алмазная", "ЛЕГЕНДАРНАЯ", new Color32(95, 241, 213, 255), 26, 280, 2, 1.5f)
        };

        public int Roll(System.Random random)
        {
            float total = 0;
            foreach (var tier in pickaxes) total += tier.weight;
            double value = random.NextDouble() * total;
            for (int i = 0; i < pickaxes.Length; i++)
            {
                value -= pickaxes[i].weight;
                if (value < 0) return i;
            }
            return pickaxes.Length - 1;
        }

        public float RollChance(int tier)
        {
            float total = 0;
            foreach (var pickaxe in pickaxes) total += pickaxe.weight;
            return pickaxes[tier].weight / total;
        }

        public int BlockHealthHalfUnits(BlockKind kind, int row)
        {
            int start=rulesVersion>=3 && map!=MapId.Mine?Location.hardnessStart:hardnessStartDepth;
            int doubling=rulesVersion>=3 && map!=MapId.Mine?Location.hardnessDoubling:hardnessDoublingDepth;
            float hardness = ResourceInventory.IsMaterial(kind) && kind != BlockKind.Dirt && kind!=BlockKind.Sand ? Mathf.Pow(2, Mathf.Clamp((row - start) / (float)Mathf.Max(20, doubling), 0, 20)) : 1;
            return Mathf.CeilToInt((rulesVersion>=3 && map!=MapId.Mine&&Location.healthOverrides[(int)kind]>0?Location.healthOverrides[(int)kind]:Block(kind).health) * 2 * hardness);
        }

        public BlockStats Block(BlockKind kind) => blocks[(int)kind];

        public BlockKind ChooseBlock(System.Random random, int depth)
        {
            if (depth < Block(BlockKind.Stone).minimumDepth) return BlockKind.Dirt;
            double roll = random.NextDouble();
            foreach (var block in blocks)
            {
                double chance = OreChance(block.kind, depth);
                if (chance <= 0) continue;
                roll -= chance;
                if (roll < 0) return block.kind;
            }
            return depth >= Block(BlockKind.Deepstone).minimumDepth ? BlockKind.Deepstone : BlockKind.Stone;
        }

        public double OreChance(BlockKind kind, int depth)
        {
            if(rulesVersion>=3 && map!=MapId.Mine)return Location.OreChance(kind,depth);
            var block = Block(kind);
            if (depth < block.minimumDepth) return 0;
            if(kind>=BlockKind.Emerald && kind<=BlockKind.Redstone && depth<Block(BlockKind.Deepstone).minimumDepth)return 0;
            return block.oreChance * (kind == BlockKind.Coal && depth >= Block(BlockKind.Deepstone).minimumDepth ? .35 : 1);
        }

        public static string BlockName(BlockKind kind) => kind switch
        {
            BlockKind.Sand=>"Песок",BlockKind.Sandstone=>"Песчанник",BlockKind.DarkQuartzite=>"Тёмный кварцит",
            BlockKind.Malachite=>"Малахит",BlockKind.Turquoise=>"Бирюза",BlockKind.Garnet=>"Гранат",BlockKind.SmokyQuartz=>"Дымчатый кварц",
            BlockKind.Netherrack=>"Незерак",BlockKind.Basalt=>"Базальт",BlockKind.Blackstone=>"Чернит",BlockKind.Quartz=>"Кварц",BlockKind.Sulfur=>"Сера",BlockKind.Cinnabar=>"Киноварь",BlockKind.FireOpal=>"Огненный опал",BlockKind.AncientDebris=>"Древние обломки",BlockKind.SoulCrystal=>"Кристалл душ",BlockKind.Timber=>"Дерево",BlockKind.Rail=>"Рельсы",BlockKind.MinecartChest=>"Вагонетка с сундуком",BlockKind.NetherBrick=>"Кирпичи крепости",
            BlockKind.Dirt => "Земля", BlockKind.Stone => "Камень", BlockKind.Coal => "Уголь",
            BlockKind.Copper => "Медь", BlockKind.Iron => "Железо", BlockKind.Gold => "Золото",
            BlockKind.Diamond => "Алмаз", BlockKind.Deepstone => "Глубинный сланец",
            BlockKind.Emerald => "Изумруд",BlockKind.Lapis => "Лазурит",BlockKind.Redstone => "Красная пыль",
            BlockKind.Amethyst or BlockKind.AmethystCluster => "Кристаллы аметиста",BlockKind.Cobblestone => "Булыжник",
            BlockKind.MossyCobblestone => "Замшелый булыжник",BlockKind.Chest => "Сундук",BlockKind.Spawner => "Спавнер",
            BlockKind.UpgradeWorkbench=>"Улучшающий верстак",BlockKind.Obsidian=>"Обсидиан",BlockKind.Water=>"Вода",BlockKind.Lava=>"Лава",BlockKind.Tnt => "Динамит", _ => "Верстак"
        };
    }
}
