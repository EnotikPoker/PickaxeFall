using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    public class MiningWorld : MonoBehaviour
    {
        readonly Dictionary<int, List<MineBlock>> rows = new();
        readonly Stack<MineBlock> pool = new();
        readonly List<int> retiredRows = new();
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
            rows.Clear(); nextRow = 0; seed = runSeed; ActiveBlocks = 0;
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
                BlockKind kind = balance.ChooseBlock(random, row);
                bool bench = x == benchColumn || (row > 0 && row % balance.workbenchInterval == 0 && column % 9 == 2);
                if (bench) kind = BlockKind.Workbench;
                else if (row > 3 && random.NextDouble() < balance.caveChance) continue;
                MineBlock block;
                if (pool.Count > 0) block = pool.Pop();
                else
                {
                    var obj = new GameObject("Mine block"); obj.transform.SetParent(transform);
                    block = obj.AddComponent<MineBlock>();
                }
                block.Setup(this, kind, x, row, row == 0 ? 0 : random.Next(1, 4));
                blocks.Add(block); ActiveBlocks++;
            }
            rows.Add(row, blocks);
        }

        public void Break(MineBlock block)
        {
            ActiveBlocks--;
            game.OnBlockBroken(block);
            block.gameObject.SetActive(false);
            // Retain the cell until its row retires, preventing a broken cell from respawning.
        }

        void Return(MineBlock block)
        {
            block.gameObject.SetActive(false); pool.Push(block);
        }
    }
}
