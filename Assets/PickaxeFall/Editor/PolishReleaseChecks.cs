using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace PickaxeFall.Editor
{
    public static class PolishReleaseChecks
    {
        static int checks;
        static void Check(bool ok,string label){checks++;if(!ok)throw new Exception("Release 0.8: "+label);}
        public static void Validate(GameBalance source)
        {
            checks=0;Check(Progression.Keys.Length==29&&Progression.KindCount==21&&Progression.RevealCount==31,"stable expanded dimensions");
            for(int i=0;i<Progression.KeyCount;i++){var key=Progression.Keys[i];Check(key.Index==i&&ProfileRules.RevealKey(key.RevealIndex).Index==i,"upgrade/reveal mapping "+i);}
            var p=new PlayerProfile{profileLevel=5,coins=100000000};Array.Fill(p.revealed,true);foreach(var kind in ResourceInventory.Materials)p.resources[(int)kind]=1000000;
            var bank=new EconomyService(p);bank.TrySetGoal(GoalKind.Upgrade,new UpgradeKey(UpgradeKind.HeatShield).Index);
            foreach(int steps in new[]{1,3,5,0}){
                bank.ReserveSteps=steps;var price=bank.ReservationCost();var expected=new long[ResourceInventory.SlotCount];
                for(int level=1;level<=(steps==0?10:steps);level++){var cost=Progression.Cost(new(UpgradeKind.HeatShield),level);for(int i=0;i<expected.Length;i++)expected[i]+=cost.Resources[i];}
                Check(price.Resources.SequenceEqual(expected),"reserve sum "+steps);var quantities=bank.SurplusQuote(out long value);
                long checkValue=0;foreach(var k in ResourceInventory.Materials){Check(quantities[(int)k]+Math.Min(expected[(int)k],bank.Profile.resources[(int)k])==bank.Profile.resources[(int)k],"reserve conservation "+k);checkValue+=quantities[(int)k]*bank.Config.sellPrices[(int)k];}Check(value==checkValue,"reserve quotation "+steps);
            }
            bank.Profile.upgrades[new UpgradeKey(UpgradeKind.HeatShield).Index]=9;bank.ReserveSteps=5;Check(bank.ReservationCost().Resources.SequenceEqual(Progression.Cost(new(UpgradeKind.HeatShield),10).Resources),"reserve caps at max");
            bank.Profile.resources[1]=0;Check(bank.SurplusQuote(out _)[1]==0,"no negative sales");
            foreach(var k in new[]{UpgradeKind.Underwater,UpgradeKind.HeatShield,UpgradeKind.TreasureHunter,UpgradeKind.EarthDigger}){long previous=0;for(int n=1;n<=Progression.MaxLevel(k);n++){long cost=Progression.Cost(new(k),n).Coins;Check(cost>previous,"new skill grows "+k);previous=cost;}p.upgrades[new UpgradeKey(k).Index]=Progression.MaxLevel(k);}
            var max=new RunUpgrades(p);Check(Mathf.Abs(max.WaterDamageMultiplier(.7f)-1)<.0001f&&Mathf.Abs(max.LavaWearMultiplier-.5f)<.0001f,"environment skill caps");Check(Math.Abs(max.Chance(UpgradeKind.TreasureHunter)-.2)<1e-8&&Math.Abs(max.Chance(UpgradeKind.EarthDigger)-.2)<1e-8,"bonus caps");
            for(int seed=1;seed<=500;seed++)foreach(MapId map in Enum.GetValues(typeof(MapId))){var b=UnityEngine.Object.Instantiate(source);b.map=map;try{var plain=ChestLoot.Roll(b,seed,2,150,false,map==MapId.Desert);var extra=ChestLoot.Roll(b,seed,2,150,false,map==MapId.Desert,true);foreach(var stack in plain){int after=extra.FirstOrDefault(x=>x.kind==stack.kind).count;Check(after>=stack.count,"extra cannot remove loot");if(stack.kind is BlockKind.Diamond or BlockKind.Emerald or BlockKind.AncientDebris or BlockKind.SoulCrystal or BlockKind.Obsidian)Check(after==stack.count,"no rare duplication");}}finally{UnityEngine.Object.DestroyImmediate(b);}}
            var paths=AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/PickaxeFall/Resources/Blocks/World"});Check(paths.Length==111,"111 supplied PNGs");foreach(var guid in paths){string path=AssetDatabase.GUIDToAssetPath(guid);var importer=(TextureImporter)AssetImporter.GetAtPath(path);var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);Check(sprite&&sprite.texture.width==32&&sprite.texture.height==32&&importer.filterMode==FilterMode.Point&&importer.spritePixelsPerUnit==32&&!importer.mipmapEnabled&&importer.textureCompression==TextureImporterCompression.Uncompressed,"sprite import "+path);}
            for(int tier=0;tier<5;tier++)Geometry(tier);
            Migration(source);
            Debug.Log("PICKAXEFALL_POLISH_080_OK: "+checks+" checks.");
        }
        static void Geometry(int tier)
        {
            var obj=new GameObject("Geometry validation");try{var hitbox=obj.AddComponent<PolygonCollider2D>();var sprite=PixelArt.Pickaxe(tier);PickaxeGeometry.Apply(hitbox,sprite);var pixels=sprite.texture.GetPixels32();obj.transform.localScale=Vector3.one*PixelArt.PickaxeScale;
                foreach(int degrees in new[]{0,37,90,179}){obj.transform.rotation=Quaternion.Euler(0,0,degrees);Physics2D.SyncTransforms();for(int y=0;y<32;y++)for(int x=0;x<32;x++){Vector2 local=(new Vector2(x+.5f,y+.5f)-sprite.pivot)/sprite.pixelsPerUnit;Vector2 point=obj.transform.TransformPoint(local);bool hit=hitbox.OverlapPoint(point)||hitbox.OverlapPoint(point+Vector2.one*.00001f)||hitbox.OverlapPoint(point-Vector2.one*.00001f);Check(hit==(pixels[y*32+x].a>=128),"opaque collider pixel "+tier+"/"+degrees+"/"+x+"/"+y);}}
            }finally{UnityEngine.Object.DestroyImmediate(obj);}
        }
        static void Migration(GameBalance source)
        {
            var obj=new GameObject("Save fixture"){hideFlags=HideFlags.HideAndDontSave};var balance=UnityEngine.Object.Instantiate(source);string dir=Path.Combine(Application.temporaryCachePath,"polish-migrate-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try{var game=obj.AddComponent<GameController>();game.InitializeSimulation(balance);game.BeginSimulation(3,8127,new int[Progression.KeyCount],0,0);var run=game.CaptureRun();run.version=4;Array.Resize(ref run.levels,25);Array.Resize(ref run.rollStates,17);Array.Resize(ref run.attempts,17);Array.Resize(ref run.successes,17);
                var old=new PlayerProfile{version=7,coins=123456,profileLevel=4,bestDepth=234,bridgeStone=1000,bridgeIron=50,portalObsidian=10,selectedMap=MapId.Nether,mapRecords=new[]{234,123,45},upgrades=new int[25],revealed=new bool[27],activeRunJson=JsonUtility.ToJson(run)};old.resources[6]=3;old.upgrades[0]=7;old.revealed[25]=true;old.goalKind=GoalKind.Upgrade;old.goalIndex=0;
                string json=JsonUtility.ToJson(old),path=Path.Combine(dir,"profile-v2.json");File.WriteAllText(path,json);var loaded=new ProfileStore(dir).Load();Check(loaded.version==8&&loaded.coins==123456&&loaded.profileLevel==4&&loaded.mapRecords.SequenceEqual(old.mapRecords)&&loaded.selectedMap==MapId.Nether&&loaded.upgrades[0]==7&&loaded.upgrades.Length==29&&loaded.revealed[25]&&loaded.revealed.Length==31&&loaded.resources[6]==3&&loaded.goalIndex==0,"v7 full preserved migration");Check(File.ReadAllText(path+".pre-v8.bak")==json,"exact v7 backup");Check(loaded.run!=null&&loaded.run.seed==8127&&loaded.run.levels.Length==29&&loaded.run.rollStates.Length==21&&loaded.run.rollStates.Take(17).SequenceEqual(run.rollStates),"legacy run/RNG preserved");
                game.RestoreRun(loaded.run);Check(game.Paused&&game.SelectedTool.IsAlive,"unfinished run restored");
                // Partial immersion at a corner; check both halves and shallow layers against the actual opaque shape.
                game.NewRun();game.BeginSimulation(0,8127,new int[Progression.KeyCount],0,0);var tool=game.SelectedTool;tool.Body.position=new Vector2(0,10);tool.Body.rotation=37;Physics2D.SyncTransforms();
                float full=tool.LiquidOverlapArea(new Rect(-2,8,4,4));Check(Mathf.Abs(full-tool.OpaqueArea)<.0001f,"opaque area matches immersed polygon");float left=tool.LiquidOverlapArea(new Rect(-2,8,2,4));float right=tool.LiquidOverlapArea(new Rect(0,8,2,4));Check(left>0&&right>0&&Mathf.Abs(left+right-full)<.0001f,"partial liquid overlap on both sides");
                tool.Body.position=new Vector2(.65f,-50.5f);tool.Body.rotation=0;Physics2D.SyncTransforms();game.World.Liquids.DebugSet(0,50,BlockKind.Lava);float partial=game.World.Liquids.Immersion(tool,BlockKind.Lava);Check(partial>0&&partial<.95f,"partial lava contact detected");int durability=tool.Durability;for(int i=0;i<8;i++){game.Clock.Advance();tool.PhysicsTick();}Check(tool.Durability==durability-1,"lava damages partially immersed tip");
                game.World.Liquids.DebugSet(0,50,BlockKind.Water);tool.Body.linearVelocity=new Vector2(5,5);tool.Body.angularVelocity=120;tool.PhysicsTick();Check(tool.InWater&&tool.Body.linearVelocity.x<5&&tool.Body.angularVelocity<120,"water slows translation and spin");
                game.World.Liquids.DebugSet(0,50,BlockKind.Lava);tool.Body.linearVelocity=new Vector2(5,5);tool.Body.angularVelocity=120;tool.PhysicsTick();Check(!tool.InWater&&tool.Body.linearVelocity.x<5&&tool.Body.angularVelocity<120,"lava slows translation and spin");
                tool.Body.position=new Vector2(0,-1000);Physics2D.SyncTransforms();Check(Mathf.Abs(tool.LiquidOverlapArea(new Rect(-2,-1002,4,4))-tool.OpaqueArea)<.0002f,"deep fluid overlap precision");
            }finally{UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(balance);Directory.Delete(dir,true);}
        }
    }
}
