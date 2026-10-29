using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    public class MiningWorld : MonoBehaviour
    {
        readonly Dictionary<int, List<MineBlock>> rows = new();
        readonly Stack<MineBlock> pool = new();
        readonly List<int> retiredRows = new();
        readonly Queue<Vector2> explosions = new();
        bool detonating;
        GameController game;
        GameBalance balance;
        int seed, nextRow;
        Transform walls;
        public int ActiveBlocks { get; private set; }
        public GameBalance Balance => balance;

        public void Initialize(GameController owner, GameBalance config)
        {
            game = owner; balance = config;
            walls = new GameObject("Shaft boundaries").transform;
            walls.SetParent(transform);
            for (int i = 0; i < 2; i++)
            {
                var wall = new GameObject("Boundary " + i).AddComponent<BoxCollider2D>();
                wall.transform.SetParent(walls); wall.transform.localPosition = new Vector3((i == 0 ? -1 : 1) * (balance.worldWidth / 2 + 1), 0, 0);
                wall.size = new Vector2(1, 180);
            }
        }

        public void ResetWorld(int runSeed)
        {
            foreach (var row in rows.Values) foreach (var block in row) Return(block);
            rows.Clear(); explosions.Clear(); nextRow = -2; seed = runSeed; ActiveBlocks = 0;
            Stream(0);
        }

        public void Stream(float depth)
        {
            int row = Mathf.Max(0, Mathf.FloorToInt(depth));
            while (nextRow < row + balance.rowsAhead) GenerateRow(nextRow++);
            retiredRows.Clear();
            foreach (var pair in rows)
                if (pair.Key < row - balance.rowsBehind) retiredRows.Add(pair.Key);
            foreach (int key in retiredRows)
            {
                foreach (var block in rows[key])
                {
                    if (!block.IsBroken) ActiveBlocks--;
                    Return(block);
                }
                rows.Remove(key);
            }
            walls.position = new Vector3(0, -depth, 0);
        }

        void GenerateRow(int row)
        {
            var blocks = new List<MineBlock>(balance.worldWidth);
            var random = new System.Random(unchecked(seed ^ row * 73856093));
            int benchColumn = row > 0 && row % balance.workbenchInterval == 0 ? random.Next(-3, 4) : int.MinValue;
            for (int column = 0; column < balance.worldWidth; column++)
            {
                int x = column - balance.worldWidth / 2;
                int surface = SurfaceRow(x);
                if (row < surface) continue;
                BlockKind kind = balance.ChooseBlock(random, row);
                if (row < surface + 3) kind = BlockKind.Dirt;
                bool bench = x == benchColumn || (row > 0 && row % balance.workbenchInterval == 0 && column % 9 == 2);
                if (bench) kind = BlockKind.Workbench;
                else if (row > surface + 3 && IsCave(x, row)) continue;
                else if (row >= balance.tntMinimumDepth && random.NextDouble() < balance.tntChance) kind = BlockKind.Tnt;
                else if (row >= surface + 3)
                {
                    // Nearby cells share a seed, producing ore pockets rather than isolated dots.
                    var veinRandom = new System.Random(unchecked(seed ^ Mathf.FloorToInt(x / 2f) * 19349663 ^ (row / 2) * 83492791));
                    kind = balance.ChooseBlock(veinRandom, row);
                }
                MineBlock block;
                if (pool.Count > 0) block = pool.Pop();
                else
                {
                    var obj = new GameObject("Mine block"); obj.transform.SetParent(transform);
                    block = obj.AddComponent<MineBlock>();
                }
                bool deep = row >= balance.Block(BlockKind.Deepstone).minimumDepth;
                block.Setup(this, kind, x, row, random.Next(4), deep, row == surface);
                blocks.Add(block); ActiveBlocks++;
            }
            rows.Add(row, blocks);
        }

        public void Break(MineBlock block)
        {
            ActiveBlocks--;
            game.OnBlockBroken(block);
            block.gameObject.SetActive(false);
            if (block.Kind == BlockKind.Tnt)
            {
                explosions.Enqueue(block.transform.position);
                DetonateQueued();
            }
            // Retain the cell until its row retires, preventing a broken cell from respawning.
        }

        public int SurfaceRow(int x)
        {
            if (Mathf.Abs(x) <= 2) return 0;
            return Mathf.FloorToInt(Mathf.PerlinNoise((x + 500) * 0.16f, (seed % 100003) * 0.013f) * 5) - 2;
        }

        bool IsCave(int x, int row)
        {
            float noise = Mathf.PerlinNoise((x + 1000) * 0.29f + seed % 997,
                row * 0.23f + (seed / 997) % 997);
            return noise < balance.caveChance + 0.15f;
        }

        void DetonateQueued()
        {
            if (detonating) return;
            detonating = true;
            try
            {
                while (explosions.Count > 0)
                {
                    Vector2 center = explosions.Dequeue();
                    game.Effects.Explosion(center, balance.explosionRadius);
                    foreach (var collider in Physics2D.OverlapCircleAll(center, balance.explosionRadius))
                    {
                        var block = collider.GetComponent<MineBlock>();
                        if (block && !block.IsBroken && block.Kind != BlockKind.Workbench) block.Hit(int.MaxValue);
                    }
                    var body = game.Pickaxe.Body;
                    Vector2 away = body.position - center;
                    if (body.simulated && away.magnitude < balance.explosionRadius + 1.5f)
                        body.AddForce((away.normalized + Vector2.up * 0.6f) * (body.mass * 3), ForceMode2D.Impulse);
                }
            }
            finally { detonating = false; }
        }

        public MineBlock CellAt(int x, int row)
        {
            if (!rows.TryGetValue(row, out var cells)) return null;
            foreach (var block in cells)
                if (!block.IsBroken && Mathf.RoundToInt(block.transform.position.x) == x) return block;
            return null;
        }

        public int LayoutFingerprint()
        {
            int hash = 17;
            foreach (var row in rows)
                foreach (var block in row.Value)
                    hash = unchecked(hash * 31 + (int)block.Kind * 997 + block.Row * 41 + Mathf.RoundToInt(block.transform.position.x));
            return hash;
        }

        void Return(MineBlock block)
        {
            block.gameObject.SetActive(false); pool.Push(block);
        }
    }
}
