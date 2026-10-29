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
        int nextRow;
        MineGenerator generator;
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
            rows.Clear(); explosions.Clear(); nextRow = -2; ActiveBlocks = 0;
            generator = new MineGenerator(balance, runSeed);
            Stream(0);
        }

        public void Stream(float depth)
        {
            int row = Mathf.Max(0, Mathf.FloorToInt(depth));
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
            nextRow = Mathf.Max(nextRow, row - balance.rowsBehind);
            while (nextRow < row + balance.rowsAhead) GenerateRow(nextRow++);
            walls.position = new Vector3(0, -depth, 0);
        }

        void GenerateRow(int row)
        {
            var blocks = new List<MineBlock>(balance.worldWidth);
            for (int column = 0; column < balance.worldWidth; column++)
            {
                int x = column - balance.worldWidth / 2;
                var cell = generator.Sample(x, row);
                if (!cell.exists) continue;
                MineBlock block;
                if (pool.Count > 0) block = pool.Pop();
                else
                {
                    var obj = new GameObject("Mine block"); obj.transform.SetParent(transform);
                    block = obj.AddComponent<MineBlock>();
                }
                block.Setup(this, cell.kind, x, row, cell.variant, cell.deep, cell.surface);
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

        public int SurfaceRow(int x) => generator.SurfaceRow(x);

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
