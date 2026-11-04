using System;
using System.Collections.Generic;

namespace PickaxeFall
{
    [Serializable] public sealed class BenchVisit
    {
        public int tool,depth,tierBefore,tierAfter,durabilityBefore,maximumBefore,restored;
        public float seconds,nextDistance,nextSeconds;
        public bool upgrading,nextReached,terminalCensored;
        public float weight=1;
    }
    public partial class GameController
    {
        public readonly List<BenchVisit> BenchVisits=new();
        void RecordBench(FallingPickaxe tool,MineBlock block,int before,int maximumBefore,int tierBefore)
        {
            for(int i=BenchVisits.Count-1;i>=0;i--)if(BenchVisits[i].tool==tool.ToolIndex)
            {var last=BenchVisits[i];last.nextReached=true;last.nextDistance=block.Row-last.depth;last.nextSeconds=Clock.Now-last.seconds;break;}
            BenchVisits.Add(new BenchVisit {tool=tool.ToolIndex,depth=block.Row,seconds=Clock.Now,upgrading=block.Kind==BlockKind.UpgradeWorkbench,tierBefore=tierBefore,tierAfter=tool.Tier,durabilityBefore=before,maximumBefore=maximumBefore,restored=Math.Max(0,tool.MaxDurability-before)});
        }
    }
}
