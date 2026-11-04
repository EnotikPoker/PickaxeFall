using System;
using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class ProgressionFeatureChecks
    {
        public static void Validate(GameBalance balance)
        {
            ProfileV5Checks.Validate();
            void Check(bool test,string message){if(!test)throw new Exception("Progression 0.5: "+message);}
            Check(balance.rulesVersion==2,"new runs use current rules");
            Check(balance.benchDepths.Length==balance.benchDensity.Length && balance.benchDepths.Length>1,"density curve");
            for(int i=1;i<balance.benchDepths.Length;i++)Check(balance.benchDepths[i]>balance.benchDepths[i-1] && balance.benchDensity[i]<=balance.benchDensity[i-1] && balance.benchDensity[i]>0,"monotone positive bench density");
            for(int level=0;level<=20;level++)for(int tier=1;tier<5;tier++)
            {Check(balance.pickaxes[tier].damage*2+level*balance.progression.damageSteps[tier]>balance.pickaxes[tier-1].damage*2+level*balance.progression.damageSteps[tier-1],"damage hierarchy");Check(balance.pickaxes[tier].durability+level*balance.progression.durabilitySteps[tier]>balance.pickaxes[tier-1].durability+level*balance.progression.durabilitySteps[tier-1],"durability hierarchy");}
            int upgrades=0,total=0,water=0,lava=0;int[] bands=new int[6];
            for(int seed=0;seed<32;seed++)
            {
                var gen=new MineGenerator(balance,seed*7919+1);
                for(int row=0;row<360;row++)for(int x=-balance.worldWidth/2;x<=balance.worldWidth/2;x++)
                {
                    var cell=gen.Sample(x,row);if(!cell.exists)continue;
                    if(GameBalance.IsWorkbench(cell.kind)){total++;if(cell.kind==BlockKind.UpgradeWorkbench)upgrades++;bands[Math.Min(5,row/60)]++;}
                    if(cell.kind==BlockKind.Water)water++;if(cell.kind==BlockKind.Lava)lava++;
                }
            }
            Check(total>100 && upgrades>0 && upgrades/(float)total>.1f && upgrades/(float)total<.3f,"rare upgrading variant");
            Check(bands[5]<bands[0] && water>0 && lava>0,"depth curve and both finite liquids");
            var old=UnityEngine.Object.Instantiate(balance);old.rulesVersion=1;old.progression=ProgressionBalance.Legacy();
            Check(Math.Abs(old.WorkbenchChanceAt(1000)-old.workbenchChance)<1e-6 && old.progression.energyRegen==2 && old.progression.plungeCost==60,"legacy rules preserved");UnityEngine.Object.DestroyImmediate(old);
            Debug.Log($"PICKAXEFALL_PROGRESSION_050_OK: benches={total}, upgrades={upgrades}, bands={string.Join(",",bands)}, water={water}, lava={lava}; hierarchy and legacy rules.");
        }
    }
}
