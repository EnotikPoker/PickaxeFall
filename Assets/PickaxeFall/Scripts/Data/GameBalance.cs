using System;
using UnityEngine;

namespace PickaxeFall
{
    public enum BlockKind { Dirt, Stone, Coal, Copper, Iron, Gold, Diamond, Deepstone, Workbench }
    public enum RunPhase { Ready, Spinning, Equipped, Mining, Finished }

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
        [Min(10)] public int workbenchInterval = 14;
        [Range(0, 1)] public float caveChance = 0.075f;
        [Header("Physics")]
        public float gravity = -15;
        public float maxFallSpeed = 22;
        public float strikeInterval = 0.24f;
        public float nudgeImpulse = 2.8f;
        public float nudgeCooldown = 0.35f;
        [Header("Blocks / ore chance is an absolute probability per cell")]
        public BlockStats[] blocks =
        {
            new(BlockKind.Dirt, 1, 1, 0), new(BlockKind.Stone, 3, 1, 3),
            new(BlockKind.Coal, 4, 3, 3, 0.16f), new(BlockKind.Copper, 5, 5, 10, 0.09f),
            new(BlockKind.Iron, 7, 8, 22, 0.07f), new(BlockKind.Gold, 9, 15, 40, 0.05f),
            new(BlockKind.Diamond, 13, 35, 65, 0.035f), new(BlockKind.Deepstone, 6, 1, 55),
            new(BlockKind.Workbench, 1, 0, 14)
        };
        [Header("Pickaxes / weighted roulette")]
        public PickaxeStats[] pickaxes =
        {
            new("Деревянная", "ОБЫЧНАЯ", new Color32(184, 130, 78, 255), 1, 100, 38, 1),
            new("Каменная", "НЕОБЫЧНАЯ", new Color32(173, 188, 198, 255), 2, 140, 30, 1.2f),
            new("Железная", "РЕДКАЯ", new Color32(226, 235, 232, 255), 3, 180, 20, 1.4f),
            new("Золотая", "ЭПИЧЕСКАЯ", new Color32(255, 203, 92, 255), 4, 150, 9, 1.6f),
            new("Алмазная", "ЛЕГЕНДАРНАЯ", new Color32(95, 241, 213, 255), 6, 260, 3, 1.5f)
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
            BlockKind.Diamond => "Алмаз", BlockKind.Deepstone => "Глубинный сланец", _ => "Верстак"
        };
    }
}
