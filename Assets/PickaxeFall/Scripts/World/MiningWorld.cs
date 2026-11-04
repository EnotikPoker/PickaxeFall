using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    public class MiningWorld : MonoBehaviour
    {
        struct CellState { public BlockKind kind; public int health; public bool broken;public float litUntil; }
        readonly Dictionary<int, List<MineBlock>> rows = new();
        readonly Dictionary<Vector2Int, CellState> changes = new();
        readonly Stack<MineBlock> pool = new();
        readonly List<int> retiredRows = new(), orderedRows = new();
        readonly HashSet<int> wantedRows = new();
        readonly Queue<(Vector2 center, FallingPickaxe source, bool ore)> explosions = new();
        static readonly Vector2Int[] ShrapnelDirections = { Vector2Int.down, Vector2Int.left, Vector2Int.up, Vector2Int.right };
        readonly BoxCollider2D[] walls = new BoxCollider2D[2];
        readonly List<Collider2D> blastColliders = new();
        bool detonating;
        float wallTopHint = 24, wallBottomHint = -48;
        GameController game;
        GameBalance balance;
        MineGenerator generator;
        public int ActiveBlocks { get; private set; }
        public int LoadedRows => rows.Count;
        public GameBalance Balance => balance;
        public bool CanMine => game.Phase == RunPhase.Mining && !game.Paused;
        public float Now=>game.Clock.Now;
        public IEnumerable<MineBlock> LoadedBlocks { get {foreach(var list in rows.Values)foreach(var block in list)if(!block.IsBroken)yield return block;} }
        public void SetBalance(GameBalance config)=>balance=config;

        public void Initialize(GameController owner, GameBalance config)
        {
            game = owner; balance = config;
            for (int i = 0; i < 2; i++)
            {
                walls[i] = new GameObject("Boundary " + i).AddComponent<BoxCollider2D>(); walls[i].transform.SetParent(transform);
                walls[i].transform.position = new Vector3((i == 0 ? -1 : 1) * (balance.worldWidth / 2 + 1), 0, 0);
            }
        }
        public void ResetWorld(int runSeed,CellSnapshot[] saved=null)
        {
            foreach (var row in rows.Values) foreach (var block in row) Return(block);
            rows.Clear(); changes.Clear(); explosions.Clear(); ActiveBlocks = 0;
            if(saved!=null)foreach(var cell in saved)changes[new Vector2Int(cell.x,cell.row)]=new CellState {kind=cell.kind,health=cell.health,broken=cell.broken,litUntil=cell.litUntil};
            generator = new MineGenerator(balance, runSeed); Stream(0);
        }
        public void Stream(float depth) { wallTopHint = -depth + balance.spawnHeight + 12; wallBottomHint = -depth - 12; wantedRows.Clear(); WantWindow(depth); ApplyWindows(); }
        public void StreamTools(IReadOnlyList<FallingPickaxe> tools)
        {
            wantedRows.Clear(); wallTopHint = float.NegativeInfinity; wallBottomHint = float.PositiveInfinity;
            foreach (var tool in tools) if (tool.IsAlive)
            {
                float y = tool.Body.position.y; wallTopHint = Mathf.Max(wallTopHint, y + 12); wallBottomHint = Mathf.Min(wallBottomHint, y - 12);
                WantWindow(-y);
            }
            if (wantedRows.Count == 0) WantWindow(0);
            ApplyWindows();
        }
        void WantWindow(float depth)
        {
            int center = Mathf.Max(0, Mathf.FloorToInt(depth));
            for (int row = Mathf.Max(-2, center - balance.rowsBehind); row < center + balance.rowsAhead; row++) wantedRows.Add(row);
        }
        void ApplyWindows()
        {
            // Fill visible gaps when the entire span fits the same fixed five-window budget.
            // Widely separated debug/late-game tools still cannot allocate an unbounded shaft.
            int spanMin = int.MaxValue, spanMax = int.MinValue;
            foreach (int row in wantedRows) { spanMin = Mathf.Min(spanMin, row); spanMax = Mathf.Max(spanMax, row); }
            if ((long)spanMax - spanMin + 1 <= 5L * (balance.rowsAhead + balance.rowsBehind + 2))
                for (int row = spanMin; row <= spanMax; row++) wantedRows.Add(row);
            retiredRows.Clear();
            foreach (var pair in rows) if (!wantedRows.Contains(pair.Key)) retiredRows.Add(pair.Key);
            foreach (int row in retiredRows)
            {
                foreach (var block in rows[row]) { if (!block.IsBroken) ActiveBlocks--; Return(block); }
                rows.Remove(row);
            }
            orderedRows.Clear(); foreach (int row in wantedRows) if (!rows.ContainsKey(row)) orderedRows.Add(row);
            orderedRows.Sort(); foreach (int row in orderedRows) GenerateRow(row);
            int min = int.MaxValue, max = int.MinValue;
            foreach (int row in wantedRows) { min = Mathf.Min(min, row); max = Mathf.Max(max, row); }
            foreach (var wall in walls)
            {
                float top = Mathf.Max(-min + 15, wallTopHint), bottom = Mathf.Min(-max - 15, wallBottomHint);
                var p = wall.transform.position; p.y = (top + bottom) * 0.5f;
                wall.transform.position = p; wall.size = new Vector2(1, top - bottom);
            }
        }
        void GenerateRow(int row)
        {
            var blocks = new List<MineBlock>(balance.worldWidth);
            for (int column = 0; column < balance.worldWidth; column++)
            {
                int x = column - balance.worldWidth / 2; var key = new Vector2Int(x, row); var cell = generator.Sample(x, row);
                bool modified = changes.TryGetValue(key, out var state);
                if (modified && state.broken || !modified && !cell.exists) continue;
                MineBlock block;
                if (pool.Count > 0) block = pool.Pop();
                else { var obj = new GameObject("Mine block"); obj.transform.SetParent(transform); block = obj.AddComponent<MineBlock>(); }
                block.Setup(this, modified ? state.kind : cell.kind, x, row, cell.variant, cell.deep, cell.surface);
                if (modified){block.RestoreHealth(state.health);if(state.litUntil>0)block.SetLit(state.litUntil);}
                blocks.Add(block); ActiveBlocks++;
            }
            rows.Add(row, blocks);
        }
        public void Remember(MineBlock block) => changes[new Vector2Int(block.Column, block.Row)] = new CellState
            { kind = block.Kind, health = block.HealthHalfUnits, broken = block.IsBroken,litUntil=block.LitUntil };
        public CellSnapshot[] CaptureChanges()
        {
            var result=new List<CellSnapshot>();
            foreach(var pair in changes)result.Add(new CellSnapshot {x=pair.Key.x,row=pair.Key.y,kind=pair.Value.kind,health=pair.Value.health,broken=pair.Value.broken,litUntil=pair.Value.litUntil});
            result.Sort((a,b)=>a.row==b.row?a.x.CompareTo(b.x):a.row.CompareTo(b.row));return result.ToArray();
        }
        public bool BlocksLight(int x,int row)
        {
            if(changes.TryGetValue(new Vector2Int(x,row),out var state))return !state.broken && state.kind!=BlockKind.AmethystCluster;
            var cell=generator.Sample(x,row);return cell.exists && cell.kind!=BlockKind.AmethystCluster;
        }
        MineBlock EnsureCell(int x,int row)
        {
            if(Mathf.Abs(x)>balance.worldWidth/2 || row< -2)return null;
            if(!rows.ContainsKey(row))GenerateRow(row);return CellAt(x,row);
        }
        public void ActivateRedstone(int x,int row,FallingPickaxe source)
        {
            if(!CanMine)return;
            var first=EnsureCell(x,row);if(!first || first.Kind!=BlockKind.Redstone || first.IsLit)return;
            var queue=new Queue<Vector2Int>();var seen=new HashSet<Vector2Int>();var tnt=new List<MineBlock>();
            queue.Enqueue(new Vector2Int(x,row));
            while(queue.Count>0 && seen.Count<128)
            {
                var at=queue.Dequeue();if(!seen.Add(at))continue;
                var block=EnsureCell(at.x,at.y);if(!block || block.Kind!=BlockKind.Redstone)continue;
                block.SetLit(Now+8);
                foreach(var offset in ShrapnelDirections)
                {
                    var next=at+offset;var neighbor=EnsureCell(next.x,next.y);if(!neighbor)continue;
                    if(neighbor.Kind==BlockKind.Redstone && !seen.Contains(next))queue.Enqueue(next);
                    else if(neighbor.Kind==BlockKind.Tnt && !tnt.Contains(neighbor))tnt.Add(neighbor);
                }
            }
            // Finish propagation before blasts remove any of the connected ore.
            foreach(var block in tnt)if(!block.IsBroken)block.DestroyBlock(source);
        }
        public void Break(MineBlock block, FallingPickaxe source, DamageOrigin origin)
        {
            bool wasTnt = block.Kind == BlockKind.Tnt;
            Vector2 position = block.transform.position;
            bool createTnt = game.OnBlockBroken(block, source, origin);
            ActiveBlocks--; Remember(block); block.gameObject.SetActive(false);
            if (wasTnt || createTnt)
            {
                source?.CancelAbility(); explosions.Enqueue((position, source, !wasTnt)); DetonateQueued();
            }
        }
        public void DamageShrapnelNeighbour(int x, int row, int damage, FallingPickaxe source, int strike)
        {
            for (int i = 0; i < ShrapnelDirections.Length; i++)
            {
                var offset = ShrapnelDirections[(strike + i) % ShrapnelDirections.Length];
                var block = CellAt(x + offset.x, row + offset.y);
                if (!block || !ResourceInventory.IsMaterial(block.Kind)) continue;
                block.HitHalfUnits(damage, source, DamageOrigin.Shrapnel); return;
            }
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
                    var blast = explosions.Dequeue();
                    bool doubled = game.RollEffect(UpgradeKind.DoubleBlast);
                    if (doubled) game.Benefits.DoubleBlasts++;
                    float radius = (blast.ore ? Mathf.Min(1.5f, balance.explosionRadius) : balance.explosionRadius) * (doubled ? Mathf.Sqrt(2) : 1);
                    game.Effects.Explosion(blast.center, radius);
                    game.PhysicsScene.OverlapCircle(blast.center,radius,new ContactFilter2D().NoFilter(),blastColliders);
                    foreach (var collider in blastColliders)
                    {
                        var block = collider.GetComponent<MineBlock>();
                        if (block && !block.IsBroken && block.Kind != BlockKind.Workbench) block.DestroyBlock(blast.source, true);
                    }
                    foreach (var tool in game.Pickaxes)
                    {
                        var body = tool.Body; Vector2 away = body.position - blast.center;
                        if (tool.IsAlive && body.simulated && away.magnitude < radius + 1.5f)
                            body.AddForce((away.normalized + Vector2.up * 0.6f) * (body.mass * 3), ForceMode2D.Impulse);
                    }
                }
            }
            finally { detonating = false; }
        }
        public MineBlock CellAt(int x, int row)
        {
            if (!rows.TryGetValue(row, out var cells)) return null;
            foreach (var block in cells) if (!block.IsBroken && block.Column == x) return block;
            return null;
        }
        public int LayoutFingerprint()
        {
            int hash = 17;
            var keys = new List<int>(rows.Keys); keys.Sort();
            foreach (int row in keys) foreach (var block in rows[row])
                if (!block.IsBroken) hash = unchecked(hash * 31 + (int)block.Kind * 997 + block.Row * 41 + block.Column);
            return hash;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public MineBlock DebugSetCell(int x, int row, BlockKind kind)
        {
            if (!rows.TryGetValue(row, out var cells)) { cells = new List<MineBlock>(); rows.Add(row, cells); }
            var block = CellAt(x, row);
            if (!block)
            {
                var obj = new GameObject("Test cell"); obj.transform.SetParent(transform); block = obj.AddComponent<MineBlock>(); cells.Add(block); ActiveBlocks++;
            }
            block.Setup(this, kind, x, row, 0); Remember(block); return block;
        }
#endif
        void Return(MineBlock block) { block.gameObject.SetActive(false); pool.Push(block); }
    }
}
