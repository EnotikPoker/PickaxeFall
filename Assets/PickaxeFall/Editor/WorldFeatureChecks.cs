using System;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class WorldFeatureChecks
    {
        static void Check(bool value,string text){if(!value)throw new Exception("World feature audit: "+text);}
        public static void Validate(GameBalance balance)
        {
            var generator=new MineGenerator(balance,8127);int rooms=0,geodes=0,left=0,right=0;
            for(int band=0;band<4096;band++)if(generator.TryStructure(band,out var s))
            {if(s.geode)geodes++;else rooms++;if(s.x<0)left++;if(s.x>0)right++;Check(Mathf.Abs(s.x)+s.radius<=balance.worldWidth/2,"structure fits world");}
            Check(Math.Abs(rooms/4096.0-balance.dungeonChance)<.02 && Math.Abs(geodes/4096.0-balance.geodeChance)<.02,"rare structure frequency");
            Check(left>100 && right>100 && Math.Abs(left-right)<100,"structures cover both sides");
            for(int type=0;type<2;type++)
            {
                MineGenerator map=null;MineGenerator.Structure s=default;
                for(int seed=1;seed<1000;seed++){var g=new MineGenerator(balance,seed);if(g.TryStructure(0,out s) && s.geode==(type==1)){map=g;break;}}
                Check(map!=null,"find sample structure");
                if(type==0)
                {
                    int moss=0,cobble=0,chests=0,spawners=0;
                    for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
                    {
                        var cell=map.Sample(s.x+dx,s.row+dy);
                        if(Mathf.Abs(dx)==2 || Mathf.Abs(dy)==2)Check(cell.exists && (cell.kind==BlockKind.Cobblestone || cell.kind==BlockKind.MossyCobblestone),"5x5 room wall");
                        if(cell.kind==BlockKind.MossyCobblestone)moss++;if(cell.kind==BlockKind.Cobblestone)cobble++;
                        if(cell.exists && cell.kind==BlockKind.Chest){chests++;Check(dy==1 && Math.Abs(dx)==1,"chest bottom corners");}
                        if(cell.exists && cell.kind==BlockKind.Spawner){spawners++;Check(dx==0 && dy==1,"spawner bottom middle");}
                    }
                    Check(moss>0 && cobble>0 && chests==(s.twoChests?2:1) && spawners==1,"room contents");
                }
                else
                {
                    int shell=0,crystals=0;
                    for(int dy=-s.radius;dy<=s.radius;dy++)for(int dx=-s.radius;dx<=s.radius;dx++)
                    {var cell=map.Sample(s.x+dx,s.row+dy);if(cell.exists && cell.kind==BlockKind.Amethyst)shell++;if(cell.exists && cell.kind==BlockKind.AmethystCluster)crystals++;}
                    Check(shell>=12 && crystals>=1 && !map.Sample(s.x,s.row).exists,"hollow amethyst shell and crystals");
                }
            }
            Check(ResourceInventory.DropKind(BlockKind.Cobblestone)==BlockKind.Stone && ResourceInventory.DropKind(BlockKind.MossyCobblestone)==BlockKind.Stone,"cobblestone drops ordinary stone");
            Check(ResourceInventory.DropKind(BlockKind.AmethystCluster)==BlockKind.Amethyst,"crystal resource mapping");
            Check(balance.pickaxes[4].damage>=balance.pickaxes[3].damage*2 && balance.pickaxes[4].durability>=balance.pickaxes[3].durability*1.5f,"distinct diamond tier");
            Check(balance.OreChance(BlockKind.Emerald,100)<balance.OreChance(BlockKind.Diamond,100),"emerald rarer than diamond");
            foreach(var kind in new[] {BlockKind.Emerald,BlockKind.Lapis,BlockKind.Redstone})Check(balance.OreChance(kind,balance.Block(BlockKind.Deepstone).minimumDepth-1)==0,"deep-only ore");
            var profile=new PlayerProfile {coins=10000};foreach(var kind in ResourceInventory.Materials)profile.resources[(int)kind]=1000;
            var economy=new EconomyService(profile);Check(!economy.AbilityUnlocked(AbilityKind.Dash) && !economy.AbilityUnlocked(AbilityKind.Plunge),"new game actions locked");
            var cost=Progression.AbilityCost(AbilityKind.Dash);Check(economy.TryUnlock(AbilityKind.Dash) && economy.AbilityUnlocked(AbilityKind.Dash) && !economy.AbilityUnlocked(AbilityKind.Plunge) && economy.Profile.coins==10000-cost.Coins,"independent paid unlock");
            long coins=economy.Profile.coins;Check(!economy.TryUnlock(AbilityKind.Dash) && economy.Profile.coins==coins,"no duplicate unlock debit");
            var failing=new EconomyService(new PlayerProfile {coins=10000},_=>throw new System.IO.IOException("test"));foreach(var kind in ResourceInventory.Materials)failing.Profile.resources[(int)kind]=1000;
            Check(!failing.TryUnlock(AbilityKind.Plunge) && !failing.Profile.plungeUnlocked && failing.Profile.coins==10000,"failed unlock is atomic");
            Debug.Log($"PICKAXEFALL_WORLD_FEATURES_OK: 4096 bands; rooms={rooms}, geodes={geodes}, left/right={left}/{right}; room geometry, geodes, resources, diamond advantage and paid unlocks.");
        }
    }
}
