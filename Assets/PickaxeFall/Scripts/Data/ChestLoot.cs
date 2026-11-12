using System;
using System.Collections.Generic;
namespace PickaxeFall
{
    public readonly struct LootStack {public readonly BlockKind kind;public readonly int count;public LootStack(BlockKind kind,int count){this.kind=kind;this.count=count;}}
    public static class ChestLoot
    {
        public static LootStack[] Roll(GameBalance balance,int seed,int x,int row,bool trial=false,bool temple=false,bool extra=false)
        {
            var random=new Random(unchecked(seed^(x*73856093)^(row*19349663)^0x23bfa71^(trial?0x17aa:0)));
            var groups=new List<BlockKind>[] {new(),new(),new(),new()};
            MapId map=balance.map;
            groups[0].Add(map==MapId.Mine?(row>=75?BlockKind.Deepstone:BlockKind.Stone):balance.TerrainAt(row));
            foreach(var k in map==MapId.Mine?Progression.VeinOres:WorldCatalog.Ores){if(balance.OreChance(k,row)<=0)continue;int group=k==BlockKind.Diamond||k==BlockKind.Emerald||k==BlockKind.AncientDebris||k==BlockKind.SoulCrystal?3:k==BlockKind.Gold||k==BlockKind.Garnet||k==BlockKind.SmokyQuartz||k==BlockKind.Amethyst||k==BlockKind.FireOpal?2:1;groups[group].Add(k);}
            if(map==MapId.Desert)groups[2].Add(BlockKind.Obsidian);
            var amounts=new SortedDictionary<BlockKind,int>();int[] weights=trial?new[]{20,45,30,5}:new[]{40,40,17,3};
            for(int i=0,n=trial?random.Next(3,5):random.Next(2,5);i<n;i++){
                int total=0;for(int g=0;g<4;g++)if(groups[g].Count>0)total+=weights[g];int roll=random.Next(total),choice=0;
                for(int g=0;g<4;g++){if(groups[g].Count==0)continue;roll-=weights[g];if(roll<0){choice=g;break;}}
                var kind=groups[choice][random.Next(groups[choice].Count)];int count=choice==0?random.Next(6,16):choice==1?random.Next(2,7):choice==2?random.Next(1,4):1;
                amounts.TryGetValue(kind,out int old);amounts[kind]=old+count;
            }
            if(extra){var bonusRandom=new Random(unchecked(seed^(x*73856093)^(row*19349663)^0x5e21bc));var pool=new List<BlockKind>();for(int g=0;g<3;g++)pool.AddRange(groups[g].FindAll(k=>k!=BlockKind.Obsidian));if(pool.Count>0){var k=pool[bonusRandom.Next(pool.Count)];int count=groups[0].Contains(k)?bonusRandom.Next(6,16):groups[1].Contains(k)?bonusRandom.Next(2,7):bonusRandom.Next(1,4);amounts.TryGetValue(k,out int old);amounts[k]=old+count;}}
            if(temple){amounts.TryGetValue(BlockKind.Obsidian,out int old);amounts[BlockKind.Obsidian]=old+random.Next(1,3);}
            var result=new List<LootStack>();foreach(var pair in amounts)result.Add(new LootStack(pair.Key,pair.Value));return result.ToArray();
        }
    }
}
