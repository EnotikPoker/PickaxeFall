using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace PickaxeFall.Editor
{
    public static class WorldReleaseChecks
    {
        static int checks;
        static void Check(bool ok,string why){checks++;if(!ok)throw new Exception("World 0.7: "+why);}
        public static void Validate(GameBalance source)
        {
            checks=0;var balance=UnityEngine.Object.Instantiate(source);
            try{
                Check(balance.locations[1].Terrain(2,-2)==BlockKind.Sand&&balance.locations[1].Terrain(3,-2)==BlockKind.Sandstone,"surface thickness follows terrain");
                Check(balance.locations[0].Terrain(1,-2)==BlockKind.Stone,"mine thickness follows terrain");
                foreach(MapId map in Enum.GetValues(typeof(MapId))){balance.map=map;var first=new MineGenerator(balance,8127);var second=new MineGenerator(balance,8127);
                    for(int row=300;row>=0;row-=3)for(int x=-20;x<=20;x+=2){var a=first.Sample(x,row);var b=second.Sample(x,row);Check(a.exists==b.exists&&a.kind==b.kind,"reverse streaming "+map);}
                    for(int seed=1;seed<=8;seed++){var gen=new MineGenerator(balance,seed);for(int row=0;row<250;row++)for(int x=-20;x<=20;x++){
                        var cell=gen.Sample(x,row);if(!cell.exists)continue;
                        if(map!=MapId.Mine && Progression.IsOre(cell.kind))Check(balance.OreChance(cell.kind,row)>0,"ore belongs to layer "+map+" "+cell.kind+" "+row);
                        if(map==MapId.Desert && row<5)Check(!Progression.IsOre(cell.kind),"no ore in desert surface");
                        if(gen.IsStructureCell(x,row))Check(!WorldCatalog.IsNewOre(cell.kind)&&cell.kind!=BlockKind.Gold&&cell.kind!=BlockKind.Copper,"ore cannot overwrite structure");
                        if(map==MapId.Nether)Check(cell.kind!=BlockKind.Water,"no natural nether water");
                    }}
                    var loot1=ChestLoot.Roll(balance,8127,3,150);var loot2=ChestLoot.Roll(balance,8127,3,150);Check(loot1.Length==loot2.Length,"deterministic loot count");
                    for(int i=0;i<loot1.Length;i++)Check(loot1[i].kind==loot2[i].kind&&loot1[i].count==loot2[i].count&&ResourceInventory.IsResource(loot1[i].kind),"deterministic valid loot");
                    for(int band=0;band<8;band++)if(first.TryFeature(band,out int cx,out int cy,out int r)){Check(cx-r>=-20&&cx+r<=20&&cy%64>10,"structure fits band");}
                }
                foreach(var kind in ResourceInventory.Materials){Check(Progression.Default.sellPrices[(int)kind]>0,"positive sell price "+kind);Check(PixelArt.ResourceIcon(kind)!=null,"resource icon "+kind);}
                var inventory=new EconomyService(new PlayerProfile());Check(!inventory.Profile.discovered[(int)BlockKind.Garnet],"unknown hidden");inventory.Collect(BlockKind.Garnet);Check(inventory.Profile.discovered[(int)BlockKind.Garnet]&&inventory.TrySellAll()&&inventory.Profile.discovered[(int)BlockKind.Garnet],"discovery survives sale");
                Check(!inventory.TrySelectMap(MapId.Desert),"locked map rejects entry");inventory.Profile.bridgeStone=1000;inventory.Profile.bridgeIron=50;Check(inventory.TrySelectMap(MapId.Desert),"bridge unlocks desert");
                foreach(var key in Progression.Keys){var max=Progression.MaxLevel(key.Kind);var early=Progression.Cost(key,1);if(max>3)Check(early.Resources[(int)BlockKind.Sandstone]==0&&early.Resources[(int)BlockKind.Basalt]==0,"early accessible "+key.Kind);var late=Progression.Cost(key,max);Check(late.Resources[(int)BlockKind.Sandstone]>0&&late.Resources[(int)BlockKind.Basalt]>0,"late worlds matter "+key.Kind);}
                Check(WorldCatalog.OreChance(MapId.Nether,BlockKind.SoulCrystal,150)<WorldCatalog.OreChance(MapId.Nether,BlockKind.AncientDebris,150)&&WorldCatalog.OreChance(MapId.Nether,BlockKind.AncientDebris,150)<source.OreChance(BlockKind.Diamond,150),"rare hierarchy");
                for(int i=0;i<3;i++)Check(Resources.Load<AudioClip>("Audio/Music/"+new[]{"Mine","Desert","Nether"}[i]),"music included");
                foreach(var key in new[]{"stone","sand","wood","metal","crystal","break","shatter","dash","explosion","bench","chest","reward","trial","error","click","roll","water","lava"})Check(Resources.LoadAll<AudioClip>("Audio/Sfx/"+key).Length>0,"sound included "+key);
                string dir=Path.Combine(Application.temporaryCachePath,"world-migrate-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
                try{var old=new PlayerProfile{version=6,upgrades=new int[25],revealed=new bool[27],coins=12345,bestDepth=87,profileLevel=4,bridgeStone=1000,bridgeIron=50,portalObsidian=10,resources=new long[21],lifetimeMined=new long[21]};old.resources[6]=7;old.lifetimeMined[1]=4567;old.upgrades[0]=8;string json=JsonUtility.ToJson(old);File.WriteAllText(Path.Combine(dir,"profile-v2.json"),json);var loaded=new ProfileStore(dir).Load();Check(loaded.version==8&&loaded.coins==12345&&loaded.profileLevel==4&&loaded.upgrades[0]==8&&loaded.resources[6]==7&&loaded.mapRecords[0]==87&&loaded.portalObsidian==10&&loaded.discovered[6]&&loaded.discovered[1],"v6 preserved migration");Check(File.ReadAllText(Path.Combine(dir,"profile-v2.json.pre-v8.bak"))==json,"exact migration backup");Check(new ProfileStore(dir).Load().coins==12345,"migration once");}finally{Directory.Delete(dir,true);}
                var config=new ProgressionBalance();var oldRows=new int[63];oldRows[21+4]=17;config.teamMaterials=oldRows;config.ExpandSlots();Check(config.teamMaterials[ResourceInventory.SlotCount+4]==17,"flat recipe row stride migration");
                Debug.Log("PICKAXEFALL_WORLDS_070_OK: "+checks+" checks.");
            }finally{UnityEngine.Object.DestroyImmediate(balance);}
        }
    }
}
