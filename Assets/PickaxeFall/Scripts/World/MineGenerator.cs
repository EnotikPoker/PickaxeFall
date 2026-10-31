using UnityEngine;

namespace PickaxeFall
{
    // A cell depends only on the map seed, its coordinates and the balance asset.
    // There is deliberately no reference to the pickaxe, camera or streaming order.
    public sealed class MineGenerator
    {
        public readonly struct Cell
        {
            public readonly bool exists, deep, surface;
            public readonly BlockKind kind;
            public readonly int variant;
            public Cell(BlockKind kind, int variant, bool deep, bool surface)
            { exists = true; this.kind = kind; this.variant = variant; this.deep = deep; this.surface = surface; }
        }

        readonly GameBalance balance;
        readonly int seed;
        const uint BenchSalt = 0x68bc21eb, TntSalt = 0x02e5be93;

        public MineGenerator(GameBalance balance, int seed) { this.balance = balance; this.seed = seed; }

        public Cell Sample(int x, int row)
        {
            if (!TerrainExists(x, row)) return default;
            int surface = SurfaceRow(x);
            var veinRandom = new System.Random(unchecked((int)Hash(Mathf.FloorToInt(x / 2f), row / 2, 173)));
            BlockKind kind = row < surface + 3 ? BlockKind.Dirt : balance.ChooseBlock(veinRandom, row);
            if (IsWorkbench(x, row)) kind = BlockKind.Workbench;
            else if (IsIsolated(x, row, TntSalt, balance.tntMinimumDepth, balance.tntChance, balance.tntSpacing) && !NearWorkbench(x, row))
                kind = BlockKind.Tnt;
            return new Cell(kind, (int)(Hash(x, row, 619) % 4),
                row >= balance.Block(BlockKind.Deepstone).minimumDepth, row == surface);
        }

        public int SurfaceRow(int x)
        {
            if (Mathf.Abs(x) <= 2) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.PerlinNoise((x + 500) * 0.16f, (seed % 100003) * 0.013f) * 5) - 2, -2, 2);
        }

        bool TerrainExists(int x, int row)
        {
            if (Mathf.Abs(x) > balance.worldWidth / 2) return false;
            int surface = SurfaceRow(x);
            if (row < surface) return false;
            if (row <= surface + 3 || balance.caveChance <= 0) return true;
            float noise = Mathf.PerlinNoise((x + 1000) * 0.29f + seed % 997,
                row * 0.23f + (seed / 997) % 997);
            return noise >= balance.caveChance + 0.15f;
        }

        bool Candidate(int x, int row, uint salt, int minimumDepth, float chance)
            => row >= minimumDepth && chance > 0 && UnitHash(x, row, salt) < chance && TerrainExists(x, row);

        bool IsWorkbench(int x, int row)
            => IsIsolated(x, row, BenchSalt, balance.workbenchMinimumDepth, balance.workbenchChance, balance.workbenchSpacing);

        bool IsIsolated(int x, int row, uint salt, int minimumDepth, float chance, int spacing)
        {
            if (!Candidate(x, row, salt, minimumDepth, chance)) return false;
            uint priority = Hash(x, row, salt);
            for (int dy = 1 - spacing; dy < spacing; dy++)
            for (int dx = 1 - spacing; dx < spacing; dx++)
            {
                if ((dx == 0 && dy == 0) || dx * dx + dy * dy >= spacing * spacing) continue;
                int xx = x + dx, yy = row + dy;
                uint neighbor = Hash(xx, yy, salt);
                bool winsTie = yy < row || (yy == row && xx < x);
                if ((neighbor < priority || (neighbor == priority && winsTie)) && Candidate(xx, yy, salt, minimumDepth, chance))
                    return false;
            }
            return true;
        }

        bool NearWorkbench(int x, int row)
        {
            for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                if (dx * dx + dy * dy <= 4 && IsWorkbench(x + dx, row + dy)) return true;
            return false;
        }

        double UnitHash(int x, int row, uint salt) => Hash(x, row, salt) / 4294967296.0;
        uint Hash(int x, int row, uint salt)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)x * 0x9e3779b9 ^ (uint)row * 0x85ebca6b ^ salt;
                h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15; h *= 0x846ca68b; return h ^ (h >> 16);
            }
        }
    }
}
