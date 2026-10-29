using System;
using UnityEngine;

namespace PickaxeFall
{
    public enum BlockKind { Dirt, Stone, Coal, Copper, Iron, Gold, Diamond, Deepstone, Workbench, Tnt }
    public enum RunPhase { Ready, Spinning, Equipped, Mining, Breaking, Finished }

    [Serializable]
    public class BlockStats
    {
        public BlockKind kind;
        [Min(1)] public int health;
        [Min(0)] public int reward;
        [Min(0)] public int minimumDepth;
        [Range(0, 1)] public float oreChance;
        public BlockStats(BlockKind kind, int health, int reward, int depth, float chance = 0)
        { this.kind = kind; this.health = health; this.reward = reward; minimumDepth = depth; oreChance = chance; }
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
    public class GameBalance : ScriptableObject
    {
        [Header("World (one block = one metre)")]
        [Min(15)] public int worldWidth = 41;
        [Min(20)] public int rowsAhead = 32;
        [Min(8)] public int rowsBehind = 18;
        [Min(1)] public int workbenchMinimumDepth = 12;
        [Range(0, 0.05f)] public float workbenchChance = 0.006f;
        [Min(2)] public int workbenchSpacing = 8;
        [Range(0, 1)] public float caveChance = 0.025f;
        [Min(4)] public float spawnHeight = 7.5f;
        [Header("TNT")]
        [Min(1)] public int tntMinimumDepth = 8;
        [Range(0, 0.1f)] public float tntChance = 0.004f;
        [Min(2)] public int tntSpacing = 2;
        [Min(1)] public float explosionRadius = 2.1f;
        [Header("Physics")]
        public float gravity = -15;
        public float maxFallSpeed = 22;
        public float strikeInterval = 0.7f;
        [Min(0)] public float hitHopSpeed = 2.6f;
        [Header("Blocks / ore chance is an absolute probability per cell")]
        public BlockStats[] blocks =
        {
            new(BlockKind.Dirt, 1, 0, 0), new(BlockKind.Stone, 6, 1, 3),
            new(BlockKind.Coal, 8, 2, 3, 0.10f), new(BlockKind.Copper, 11, 3, 10, 0.055f),
            new(BlockKind.Iron, 14, 5, 22, 0.035f), new(BlockKind.Gold, 18, 8, 40, 0.02f),
            new(BlockKind.Diamond, 22, 12, 65, 0.009f), new(BlockKind.Deepstone, 10, 1, 55),
            new(BlockKind.Workbench, 1, 0, 12), new(BlockKind.Tnt, 1, 0, 8)
        };
        [Header("Pickaxes / weighted roulette")]
        public PickaxeStats[] pickaxes =
        {
            new("Деревянная", "ОБЫЧНАЯ", new Color32(184, 130, 78, 255), 1, 28, 45, 1),
            new("Каменная", "НЕОБЫЧНАЯ", new Color32(173, 188, 198, 255), 2, 30, 30, 1.2f),
            new("Золотая", "РЕДКАЯ", new Color32(255, 203, 92, 255), 3, 32, 15, 1.3f),
            new("Железная", "ЭПИЧЕСКАЯ", new Color32(226, 235, 232, 255), 4, 34, 8, 1.4f),
            new("Алмазная", "ЛЕГЕНДАРНАЯ", new Color32(95, 241, 213, 255), 5, 36, 2, 1.5f)
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

        public BlockStats Block(BlockKind kind) => blocks[(int)kind];

        public BlockKind ChooseBlock(System.Random random, int depth)
        {
            if (depth < Block(BlockKind.Stone).minimumDepth) return BlockKind.Dirt;
            double roll = random.NextDouble();
            foreach (var block in blocks)
            {
                if (block.oreChance <= 0 || depth < block.minimumDepth) continue;
                roll -= block.oreChance;
                if (roll < 0) return block.kind;
            }
            return depth >= Block(BlockKind.Deepstone).minimumDepth ? BlockKind.Deepstone : BlockKind.Stone;
        }

        public static string BlockName(BlockKind kind) => kind switch
        {
            BlockKind.Dirt => "Земля", BlockKind.Stone => "Камень", BlockKind.Coal => "Уголь",
            BlockKind.Copper => "Медь", BlockKind.Iron => "Железо", BlockKind.Gold => "Золото",
            BlockKind.Diamond => "Алмаз", BlockKind.Deepstone => "Глубинный сланец",
            BlockKind.Tnt => "Динамит", _ => "Верстак"
        };
    }
}
