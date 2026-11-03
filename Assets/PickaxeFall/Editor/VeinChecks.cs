using System;
using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class VeinChecks
    {
        public static void Validate(GameBalance balance)
        {
            const int maps=16, depth=360;
            int half=balance.worldWidth/2,width=half*2+1,veins=0,irregular=0;
            var sizes=new int[5,11];long shallowCoal=0,deepCoal=0,shallowCells=0,deepCells=0,diamonds=0,diamondCells=0;
            var directions=new[] { Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down };
            for(int seed=0;seed<maps;seed++)
            {
                var generator=new MineGenerator(balance,8127+seed*104729);
                var cells=new MineGenerator.Cell[depth,width];var seen=new bool[depth,width];
                for(int row=0;row<depth;row++)for(int col=0;col<width;col++)
                {
                    var cell=cells[row,col]=generator.Sample(col-half,row);
                    if(!cell.exists)continue;
                    if(row>=20 && row<balance.Block(BlockKind.Deepstone).minimumDepth-10){shallowCells++;if(cell.kind==BlockKind.Coal)shallowCoal++;}
                    if(row>=balance.Block(BlockKind.Deepstone).minimumDepth+15){deepCells++;if(cell.kind==BlockKind.Coal)deepCoal++;}
                    if(row>=balance.Block(BlockKind.Diamond).minimumDepth){diamondCells++;if(cell.kind==BlockKind.Diamond)diamonds++;}
                    if(Progression.IsOre(cell.kind) && row<balance.Block(cell.kind).minimumDepth)throw new Exception("Ore above depth gate");
                    if(cell.deep!=(row>=balance.Block(BlockKind.Deepstone).minimumDepth))throw new Exception("Ore background does not match layer");
                }
                for(int row=0;row<depth;row++)for(int col=0;col<width;col++)
                {
                    var cell=cells[row,col];if(seen[row,col] || !cell.exists || !Progression.IsOre(cell.kind))continue;
                    var queue=new Queue<Vector2Int>();queue.Enqueue(new Vector2Int(col,row));seen[row,col]=true;
                    int count=0,minX=col,maxX=col,minY=row,maxY=row;bool cropped=false;
                    while(queue.Count>0)
                    {
                        var at=queue.Dequeue();count++;minX=Math.Min(minX,at.x);maxX=Math.Max(maxX,at.x);minY=Math.Min(minY,at.y);maxY=Math.Max(maxY,at.y);cropped|=at.y==depth-1;
                        foreach(var direction in directions)
                        {
                            var next=at+direction;
                            if(next.x<0 || next.x>=width || next.y<0 || next.y>=depth || seen[next.y,next.x])continue;
                            var other=cells[next.y,next.x];if(!other.exists || other.kind!=cell.kind)continue;
                            seen[next.y,next.x]=true;queue.Enqueue(next);
                        }
                    }
                    if(cropped)continue;
                    MineGenerator.VeinRange(cell.kind,out int minimum,out int maximum);
                    if(count<minimum || count>maximum)throw new Exception($"Connected {cell.kind} deposit has {count} blocks; expected {minimum}–{maximum}");
                    sizes[(int)cell.kind-2,count]++;veins++;
                    if(count!=(maxX-minX+1)*(maxY-minY+1))irregular++;
                }
                // A fresh generator queried from the deepest row must reproduce every cell.
                var reverse=new MineGenerator(balance,8127+seed*104729);
                for(int row=depth-1;row>=0;row-=11)for(int col=width-1;col>=0;col--)
                {
                    var actual=reverse.Sample(col-half,row);var expected=cells[row,col];
                    if(actual.exists!=expected.exists || actual.kind!=expected.kind || actual.variant!=expected.variant || actual.deep!=expected.deep)throw new Exception("Veins depend on query order");
                }
            }
            for(BlockKind kind=BlockKind.Coal;kind<=BlockKind.Diamond;kind++)
            {
                MineGenerator.VeinRange(kind,out int minimum,out int maximum);
                for(int n=minimum;n<maximum;n++)if(MineGenerator.VeinWeight(n+1,minimum)>=MineGenerator.VeinWeight(n,minimum))throw new Exception("Large veins must be less likely");
                if(sizes[(int)kind-2,minimum]<=sizes[(int)kind-2,maximum])throw new Exception("Observed small veins should outnumber largest veins: "+kind);
                var histogram=new List<string>();for(int n=minimum;n<=maximum;n++)histogram.Add(n+":"+sizes[(int)kind-2,n]);
                Debug.Log("PICKAXEFALL_VEIN_SIZES: "+kind+" "+string.Join(", ",histogram));
            }
            double shallow=shallowCoal/(double)shallowCells,deep=deepCoal/(double)deepCells,diamond=diamonds/(double)diamondCells;
            if(deep>=shallow*.65 || diamond>=.006 || diamond<=0 || irregular<veins/3)throw new Exception("Ore density/shape balance failed");
            Debug.Log($"PICKAXEFALL_VEINS_OK: {maps} maps, {veins} connected deposits, {irregular} irregular shapes; coal shallow={shallow:P2} deep={deep:P2}, diamonds={diamond:P3}; exact size bounds and reverse queries.");
        }
    }
}
