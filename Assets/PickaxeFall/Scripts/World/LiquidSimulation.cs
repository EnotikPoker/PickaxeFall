using System;
using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    [Serializable] public sealed class LiquidCellSnapshot
    {
        public int x,row,amount;
        public BlockKind kind;
    }
    // Finite-volume grid simulation, 10 Hz of game time. No extra physics world,
    // source blocks or offline advancement. Sorted traversal makes worker order irrelevant.
    public sealed class LiquidSimulation
    {
        struct Cell { public BlockKind kind;public int amount; }
        readonly MiningWorld world;
        readonly GameController game;
        readonly Dictionary<Vector2Int,Cell> cells=new();
        readonly Dictionary<Vector2Int,SpriteRenderer> visuals=new();
        readonly Stack<SpriteRenderer> pool=new();
        readonly List<Vector2Int> ordered=new(),retired=new();
        public LiquidSimulation(MiningWorld world,GameController game){this.world=world;this.game=game;}
        public void Reset(LiquidCellSnapshot[] saved)
        {
            cells.Clear();foreach(var pair in visuals){pair.Value.gameObject.SetActive(false);pool.Push(pair.Value);}visuals.Clear();
            if(saved!=null)foreach(var item in saved)cells[new Vector2Int(item.x,item.row)]=new Cell {kind=item.kind,amount=item.amount};
        }
        public void Register(int x,int row,BlockKind kind)
        {
            var key=new Vector2Int(x,row);if(!cells.ContainsKey(key))cells[key]=new Cell {kind=kind,amount=8};
        }
        public int Amount(int x,int row)=>cells.TryGetValue(new Vector2Int(x,row),out var c)?c.amount:0;
        public BlockKind KindAt(int x,int row)=>cells.TryGetValue(new Vector2Int(x,row),out var c)&&c.amount>0?c.kind:(BlockKind)(-1);
        public LiquidCellSnapshot[] Capture()
        {
            var list=new List<LiquidCellSnapshot>();foreach(var pair in cells)list.Add(new LiquidCellSnapshot {x=pair.Key.x,row=pair.Key.y,kind=pair.Value.kind,amount=pair.Value.amount});
            list.Sort((a,b)=>a.row==b.row?a.x.CompareTo(b.x):a.row.CompareTo(b.row));return list.ToArray();
        }
        public bool Touches(FallingPickaxe tool,BlockKind kind)=>Immersion(tool,kind)>0.00001f;
        public float Immersion(FallingPickaxe tool,BlockKind kind)
        {
            var bounds=tool.HitBounds;float area=0;
            for(int row=Mathf.FloorToInt(-bounds.max.y);row<=Mathf.FloorToInt(-bounds.min.y);row++)
            for(int x=Mathf.CeilToInt(bounds.min.x-.5f);x<=Mathf.FloorToInt(bounds.max.x+.5f);x++)
            {
                if(!cells.TryGetValue(new Vector2Int(x,row),out var c)||c.amount<=0||c.kind!=kind)continue;
                area+=tool.LiquidOverlapArea(new Rect(x-.5f,-row-1,1,c.amount/8f));
            }
            return Mathf.Clamp01(area/Mathf.Max(.0001f,tool.OpaqueArea));
        }
        public void Tick()
        {
            if(game.Balance.rulesVersion<2)return;
            if(game.Clock.Ticks%6==0)
            {
                ordered.Clear();foreach(var pair in cells)if(pair.Value.amount>0 && world.IsRowLoaded(pair.Key.y))ordered.Add(pair.Key);
                ordered.Sort((a,b)=>a.y==b.y?a.x.CompareTo(b.x):b.y.CompareTo(a.y));
                foreach(var key in ordered)
                {
                    if(!cells.TryGetValue(key,out var c)||c.amount<=0)continue;
                    foreach(var d in Neighbors)Mix(key,key+d);
                    if(!cells.TryGetValue(key,out c)||c.amount<=0)continue;
                    Transfer(key,key+Vector2Int.up,8);
                    int direction=((game.Clock.Ticks/6+key.x+key.y)&1)==0?1:-1;
                    Spread(key,key+new Vector2Int(direction,0));Spread(key,key-new Vector2Int(direction,0));
                }
            }
            if(!game.IsSimulation && game.Clock.Ticks%6==0)Draw();
        }
        static readonly Vector2Int[] Neighbors={Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
        void Mix(Vector2Int a,Vector2Int b)
        {
            if(!world.IsRowLoaded(b.y)||!cells.TryGetValue(a,out var ca)||ca.amount<=0||!cells.TryGetValue(b,out var cb)||cb.amount<=0||ca.kind==cb.kind)return;
            var lava=ca.kind==BlockKind.Lava?a:b;var water=ca.kind==BlockKind.Water?a:b;
            if(!world.PlaceObsidian(lava.x,lava.y))return;
            var l=cells[lava];l.amount=0;cells[lava]=l;var w=cells[water];w.amount--;cells[water]=w;
            game.Benefits.ObsidianCreated++;
        }
        void Spread(Vector2Int from,Vector2Int to)
        {
            if(!cells.TryGetValue(from,out var a)||a.amount<2)return;
            cells.TryGetValue(to,out var b);if(b.amount>0 && b.kind!=a.kind){Mix(from,to);return;}
            int amount=Math.Min(2,(a.amount-b.amount)/2);if(amount>0)Transfer(from,to,amount);
        }
        void Transfer(Vector2Int from,Vector2Int to,int maximum)
        {
            if(!world.CanLiquidOccupy(to.x,to.y)||!cells.TryGetValue(from,out var a)||a.amount<=0)return;
            cells.TryGetValue(to,out var b);if(b.amount>0 && b.kind!=a.kind){Mix(from,to);return;}
            int move=Math.Min(maximum,Math.Min(a.amount,8-b.amount));if(move<=0)return;
            a.amount-=move;b.amount+=move;b.kind=a.kind;cells[from]=a;cells[to]=b;
        }
        public void RefreshVisuals(){if(!game.IsSimulation)Draw();}
        void Draw()
        {
            retired.Clear();foreach(var pair in visuals)if(!world.IsRowLoaded(pair.Key.y)||!cells.TryGetValue(pair.Key,out var c)||c.amount<=0)retired.Add(pair.Key);
            foreach(var key in retired){var visual=visuals[key];visual.gameObject.SetActive(false);pool.Push(visual);visuals.Remove(key);}
            foreach(var pair in cells)
            {
                var key=pair.Key;var c=pair.Value;if(c.amount<=0||!world.IsRowLoaded(key.y))continue;
                if(!visuals.TryGetValue(key,out var visual))
                {
                    if(pool.Count>0)visual=pool.Pop();else {var obj=new GameObject("Finite liquid");obj.transform.SetParent(world.transform);visual=obj.AddComponent<SpriteRenderer>();visual.sortingOrder=5;}
                    visuals[key]=visual;visual.gameObject.SetActive(true);
                }
                float height=c.amount/8f;visual.sprite=PixelArt.Block(c.kind,0);visual.transform.position=new Vector3(key.x,-key.y-1+height*.5f,0);visual.transform.localScale=new Vector3(1,height,1);
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || BALANCE_LAB
        public void DebugSet(int x,int row,BlockKind kind,int amount=8)=>cells[new Vector2Int(x,row)]=new Cell {kind=kind,amount=amount};
#endif
    }
}
