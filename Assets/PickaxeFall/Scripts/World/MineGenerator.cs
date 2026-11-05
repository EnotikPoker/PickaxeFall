using System.Collections.Generic;
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
        // Generate a deterministic prefix, including a small look-ahead for growing veins.
        // Future anchors never grow upwards into a finalized row. Thus reverse queries,
        // streaming windows and different pickaxe routes all see the same map.
        readonly List<byte[]> terrain = new();
        readonly List<Vector2Int> frontier = new(), vein = new();
        readonly HashSet<Vector2Int> visited = new();
        static readonly Vector2Int[] Directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        const byte Air = 255;
        const int LookAhead = 10;
        int finalizedRow = -3;
        const uint BenchSalt = 0x68bc21eb, TntSalt = 0x02e5be93;

        public MineGenerator(GameBalance balance, int seed) { this.balance = balance; this.seed = seed; }

        public Cell Sample(int x, int row)
        {
            if (Mathf.Abs(x) > balance.worldWidth / 2 || row < -2) return default;
            while (finalizedRow < row)
            {
                int next = finalizedRow + 1;
                while (terrain.Count <= next + LookAhead + 2) AddTerrainRow(terrain.Count - 2);
                GrowRow(next); finalizedRow = next;
            }
            byte material = terrain[row + 2][x + balance.worldWidth / 2];
            if (material == Air) return default;
            BlockKind kind = (BlockKind)material;
            return new Cell(kind, (int)(Hash(x, row, 619) % 4),
                row >= balance.Block(BlockKind.Deepstone).minimumDepth, row == SurfaceRow(x));
        }

        void AddTerrainRow(int row)
        {
            int half = balance.worldWidth / 2;
            var cells = new byte[half * 2 + 1];
            for (int x = -half; x <= half; x++)
            {
                byte kind = Air;
                if (TerrainExists(x, row))
                {
                    kind = (byte)(row < SurfaceRow(x) + 3 ? BlockKind.Dirt : row >= balance.Block(BlockKind.Deepstone).minimumDepth ? BlockKind.Deepstone : BlockKind.Stone);
                    if (IsWorkbench(x, row)) kind = (byte)(balance.rulesVersion>=2 && UnitHash(x,row,0x74db8a31)<balance.upgradeWorkbenchFraction?BlockKind.UpgradeWorkbench:BlockKind.Workbench);
                    else if (IsIsolated(x, row, TntSalt, balance.tntMinimumDepth, balance.tntChance, balance.tntSpacing) && !NearWorkbench(x, row)) kind = (byte)BlockKind.Tnt;
                }
                if(balance.rulesVersion>=2)
                {
                    if(TryLakeCell(x,row,out byte liquid))kind=liquid;
                    else if(kind==(byte)BlockKind.Stone || kind==(byte)BlockKind.Deepstone)
                        if(row>=balance.lavaMinimumDepth && UnitHash(x,row,0x18b3a5c)<balance.lavaPocketChance)kind=(byte)BlockKind.Lava;
                }
                if(TryStructureCell(x,row,out byte structure))kind=structure;
                cells[x + half] = kind;
            }
            terrain.Add(cells);
        }

        public static void VeinRange(BlockKind kind, out int minimum, out int maximum)
        {
            minimum = kind == BlockKind.Coal || kind==BlockKind.Redstone ? 3 : kind == BlockKind.Diamond || kind==BlockKind.Emerald ? 1 : 2;
            maximum = kind switch { BlockKind.Coal=>10,BlockKind.Gold or BlockKind.Redstone=>9,BlockKind.Diamond or BlockKind.Lapis=>7,BlockKind.Emerald=>3,_=>8 };
        }
        // Truncated geometric distribution: each extra block is 32% less likely.
        public static double VeinWeight(int size, int minimum) => System.Math.Pow(.68, size - minimum);
        static double MeanVeinSize(int minimum, int maximum)
        {
            double total = 0, weighted = 0;
            for (int size = minimum; size <= maximum; size++) { double w = VeinWeight(size, minimum); total += w; weighted += size * w; }
            return weighted / total;
        }
        void GrowRow(int row)
        {
            if (row < balance.Block(BlockKind.Coal).minimumDepth) return;
            int half = balance.worldWidth / 2;
            // Alternate scan direction so one side does not systematically win conflicts.
            bool reverse = (Hash(0, row, 317) & 1) != 0;
            for (int i = 0; i <= half * 2; i++)
            {
                int x = reverse ? half - i : i - half;
                byte cell = terrain[row + 2][x + half];
                if (cell != (byte)BlockKind.Stone && cell != (byte)BlockKind.Deepstone) continue;
                double roll = UnitHash(x, row, 173);
                foreach(BlockKind kind in Progression.VeinOres)
                {
                    VeinRange(kind, out int min, out int max);
                    roll -= balance.OreChance(kind, row) / MeanVeinSize(min, max);
                    if (roll >= 0) continue;
                    GrowVein(x, row, kind, min, max); break;
                }
            }
        }
        void GrowVein(int x, int row, BlockKind kind, int minimum, int maximum)
        {
            var random = new System.Random(unchecked((int)Hash(x, row, 991)));
            double sum = 0;
            for (int n = minimum; n <= maximum; n++) sum += VeinWeight(n, minimum);
            double roll = random.NextDouble() * sum;
            int target = maximum;
            for (int n = minimum; n <= maximum; n++) { roll -= VeinWeight(n, minimum); if (roll < 0) { target = n; break; } }
            frontier.Clear(); vein.Clear(); visited.Clear();
            var anchor = new Vector2Int(x, row); frontier.Add(anchor); visited.Add(anchor);
            while (frontier.Count > 0 && vein.Count < target)
            {
                int index = random.Next(frontier.Count); var point = frontier[index]; frontier.RemoveAt(index);
                if (!CanGrow(point, row, kind)) continue;
                vein.Add(point);
                foreach (var direction in Directions)
                {
                    var next = point + direction;
                    if (next.y >= row && next.y < row + LookAhead && Mathf.Abs(next.x) <= balance.worldWidth / 2 && visited.Add(next)) frontier.Add(next);
                }
            }
            // Never leave a truncated or disconnected deposit beside caves/features.
            if (vein.Count != target) return;
            foreach (var point in vein) terrain[point.y + 2][point.x + balance.worldWidth / 2] = (byte)kind;
        }
        bool CanGrow(Vector2Int point, int anchorRow, BlockKind kind)
        {
            if (point.y < anchorRow || point.y < balance.Block(kind).minimumDepth) return false;
            int half = balance.worldWidth / 2;
            byte cell = terrain[point.y + 2][point.x + half];
            if (cell != (byte)BlockKind.Stone && cell != (byte)BlockKind.Deepstone) return false;
            foreach (var direction in Directions)
            {
                var neighbor = point + direction;
                if (Mathf.Abs(neighbor.x) <= half && neighbor.y >= -2 && neighbor.y + 2 < terrain.Count && terrain[neighbor.y + 2][neighbor.x + half] == (byte)kind) return false;
            }
            return true;
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
            => row >= minimumDepth && chance > 0 && UnitHash(x, row, salt) < (salt==BenchSalt?balance.WorkbenchChanceAt(row):chance) && TerrainExists(x, row);

        bool IsWorkbench(int x, int row)
            => IsIsolated(x, row, BenchSalt, balance.workbenchMinimumDepth, balance.WorkbenchChanceAt(row), balance.workbenchSpacing);

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

        public bool TryLakeCell(int x,int row,out byte kind)
        {
            kind=Air;if(row<balance.waterMinimumDepth-3)return false;
            int band=Mathf.Max(0,row/32),center=band*32+10+(int)(Hash(0,band,0x27ca912)%12);
            double roll=UnitHash(0,band,0x17bce19);
            bool water=roll<balance.waterLakeChance,mixed=roll>=balance.waterLakeChance+balance.lavaLakeChance;
            if(roll>=balance.waterLakeChance+balance.lavaLakeChance+balance.mixedLakeChance)return false;
            if(!water && center<balance.lavaMinimumDepth)return false;
            int half=balance.worldWidth/2,margin=Mathf.Max(0,half-6),cx=(int)(Hash(0,band,0x31c8ab2)%(uint)(margin*2+1))-margin;
            int radius=2+(int)(Hash(0,band,0x15a12d3)%3),dx=x-cx,dy=row-center;
            if(Mathf.Abs(dx)>radius+1 || dy< -3 || dy>1)return false;
            // Closed stone basin, finite volume, air above a flat liquid surface.
            if(dy==1 || Mathf.Abs(dx)==radius+1){kind=(byte)(row>=balance.Block(BlockKind.Deepstone).minimumDepth?BlockKind.Deepstone:BlockKind.Stone);return true;}
            if(dy>=-1)kind=(byte)(water?BlockKind.Water:BlockKind.Lava);
            else if(mixed && dy==-3 && Mathf.Abs(dx)<=1)kind=(byte)BlockKind.Water;
            return true;
        }

        public readonly struct Structure
        {
            public readonly int x,row,radius;public readonly bool geode,twoChests;
            public Structure(int x,int row,int radius,bool geode,bool twoChests){this.x=x;this.row=row;this.radius=radius;this.geode=geode;this.twoChests=twoChests;}
        }
        public bool TryStructure(int band,out Structure structure)
        {
            structure=default;if(band<0)return false;
            double roll=UnitHash(0,band,0x123abb19);
            if(roll>=balance.dungeonChance+balance.geodeChance)return false;
            bool geode=roll>=balance.dungeonChance;
            var random=new System.Random(unchecked((int)Hash(0,band,0x71c49b3)));
            int margin=Mathf.Max(0,balance.worldWidth/2-5);
            int x=random.Next(-margin,margin+1),row=band*64+random.Next(band==0?36:10,54);
            structure=new Structure(x,row,geode?random.Next(3,5):2,geode,random.Next(2)==0);return true;
        }
        bool TryStructureCell(int x,int row,out byte kind)
        {
            kind=Air;if(row<25)return false;
            if(!TryStructure(row/64,out var s))return false;
            int dx=x-s.x,dy=row-s.row;
            if(Mathf.Abs(dx)>s.radius || Mathf.Abs(dy)>s.radius)return false;
            if(!s.geode)
            {
                if(Mathf.Abs(dx)==2 || Mathf.Abs(dy)==2)kind=(byte)(dx==2 && dy==2 || !(dx==-2 && dy==-2) && UnitHash(x,row,728)<.42?BlockKind.MossyCobblestone:BlockKind.Cobblestone);
                else if(dy==1 && dx==0)kind=(byte)BlockKind.Spawner;
                else if(dy==1 && (dx==-1 || dx==1 && s.twoChests))kind=(byte)BlockKind.Chest;
                return true;
            }
            float distance=dx*dx+dy*dy;
            if(distance>(s.radius+.3f)*(s.radius+.3f))return false;
            if(distance>=(s.radius-1f)*(s.radius-1f))kind=(byte)BlockKind.Amethyst;
            else if(dy>=0 && dx*dx+(dy+1)*(dy+1)>=(s.radius-1f)*(s.radius-1f) && (dx==0 || UnitHash(x,row,628)>.2))kind=(byte)BlockKind.AmethystCluster;
            return true;
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
