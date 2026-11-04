#if BALANCE_LAB || UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace PickaxeFall
{
    // A repeatable heuristic, not an optimal player. Only loaded/visible terrain is known.
    public class MiningPolicy
    {
        float next=2;
        public void Tick(GameController game)
        {
            if(game.Clock.Now<next || game.SelectedTool==null || !game.SelectedTool.IsAlive)return;
            next=game.Clock.Now+.2f;
            var tool=game.SelectedTool;Vector2 position=tool.Body.position,direction=Vector2.down;float best=0;
            int cx=Mathf.RoundToInt(position.x),cr=Mathf.FloorToInt(-position.y);
            for(int row=cr-3;row<=cr+3;row++)for(int x=cx-3;x<=cx+3;x++)
            {
                var block=game.World.CellAt(x,row);if(!block)continue;
                Vector2 delta=(Vector2)block.transform.position-position;
                if(delta.magnitude<.5f || delta.magnitude>3.2f || Mathf.Abs(delta.x)<=Mathf.Abs(delta.y))continue;
                float score=0;
                if(GameBalance.IsWorkbench(block.Kind))
                {
                    float missing=1-tool.Durability/(float)tool.MaxDurability;
                    if(block.Kind==BlockKind.UpgradeWorkbench && tool.Tier<4)score=150;
                    else if(missing>.15f)score=40+100*missing;
                }
                else if(block.Kind==BlockKind.Chest)score=65;
                else if(Progression.IsOre(block.Kind))score=8+Mathf.Sqrt(game.RunUpgrades.Config.sellPrices[(int)ResourceInventory.DropKind(block.Kind)]);
                if(score<=0)continue;
                int firstX=cx,firstRow=cr;bool reached=true;int walls=0;
                for(float distance=.25f;distance<delta.magnitude;distance+=.25f)
                {
                    var at=position+delta.normalized*distance;int ax=Mathf.RoundToInt(at.x),ar=Mathf.FloorToInt(-at.y);
                    if(ax==firstX && ar==firstRow)continue;firstX=ax;firstRow=ar;
                    if(game.World.Liquids.KindAt(ax,ar)==BlockKind.Lava){reached=false;break;}
                    var obstacle=game.World.CellAt(ax,ar);
                    if(obstacle && obstacle!=block && ++walls>1){reached=false;break;}
                }
                score-=delta.magnitude*2+walls*8;
                if(reached && score>best){best=score;direction=new Vector2(Mathf.Sign(delta.x),0);}
            }
            bool used=game.TryUseAbility(best>0?AbilityKind.Dash:AbilityKind.Plunge,direction);
            if(used)next=game.Clock.Now+.5f;
        }
    }
}
#endif
