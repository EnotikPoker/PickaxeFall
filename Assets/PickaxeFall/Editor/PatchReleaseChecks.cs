using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace PickaxeFall.Editor
{
    public static class PatchReleaseChecks
    {
        static int checks;
        static void Check(bool ok,string text){checks++;if(!ok)throw new Exception("Patch 0.8.1: "+text);}
        public static void Validate(GameBalance source)
        {
            checks=0;Physics2D.simulationMode=SimulationMode2D.Script;
            var root=new GameObject("Patch checks");var config=UnityEngine.Object.Instantiate(source);
            try
            {
                var game=root.AddComponent<GameController>();game.InitializeSimulation(config);
                void Begin(int tier=4,int count=1){game.BeginSimulation(tier,8127,new int[Progression.KeyCount],0,0);if(count>1){var snap=game.CaptureRun();snap.toolCount=count;for(int i=1;i<count;i++){game.Pickaxes[i].Equip(tier,i,i*2);game.Pickaxes[i].Launch();snap.tools[i]=game.Pickaxes[i].Capture();}game.RestoreRun(snap);game.TogglePause();}}
                Begin();var tool=game.SelectedTool;var block=game.World.DebugSetCell(-10,10,BlockKind.Spawner);
                block.DestroyBlock(tool,DamageOrigin.Explosion);Check(!game.TrialOpen,"explosions cannot activate");block.DestroyBlock(tool);Check(game.TrialOpen&&!game.Paused,"trial freezes separately from manual pause");
                int ticks=game.Clock.Ticks;var position=tool.Body.position;float energy=game.Energy.Value;game.SimulateMiningStep();Check(game.Clock.Ticks==ticks&&position==tool.Body.position&&energy==game.Energy.Value,"whole world frozen");
                Check(!game.World.TapTrial()&&game.World.StartTrial()&&!game.World.StartTrial(),"briefing requires explicit start once");
                for(int i=0;i<10;i++)Check(game.World.TapTrial(),"tap "+i);game.World.AdvanceTrial(2);Check(game.World.ActiveTrial.hits==10&&Mathf.Abs(game.World.ActiveTrial.remaining-6)<.0001,"independent timer");
                game.TogglePause();game.World.AdvanceTrial(20);Check(!game.World.TapTrial()&&game.World.ActiveTrial.remaining==6,"manual pause freezes timer/input");
                var saved=JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(game.CaptureRun()));saved.Validate();game.RestoreRun(saved);Check(game.World.ActiveTrial.hits==10&&game.World.ActiveTrial.remaining==6&&game.Paused,"trial save restored");game.TogglePause();
                long inventory=game.Inventory.Total;for(int i=10;i<30;i++)game.World.TapTrial();Check(game.World.ActiveTrial.won&&game.World.ActiveTrial.done&&game.Inventory.Total>inventory,"success rewards");long awarded=game.Inventory.Total;
                Check(!game.World.TapTrial(),"completed trial rejects taps");game.World.AdvanceTrial(1);Check(!game.TrialOpen&&game.Inventory.Total==awarded,"result returns to world once");
                block=game.World.DebugSetCell(-10,11,BlockKind.Spawner);int wear=tool.Durability;block.DestroyBlock(tool);game.World.StartTrial();game.World.AdvanceTrial(8);Check(block.IsBroken&&tool.Durability==wear-Mathf.CeilToInt(tool.MaxDurability*.3f)&&game.Inventory.Total==awarded,"failure charges 30% max once");game.World.AdvanceTrial(10);Check(tool.Durability==wear-Mathf.CeilToInt(tool.MaxDurability*.3f),"no repeated failure damage");
                Begin(0);tool=game.SelectedTool;var snap=game.CaptureRun();snap.tools[0].durability=1;game.RestoreRun(snap);game.TogglePause();block=game.World.DebugSetCell(-10,10,BlockKind.Spawner);block.DestroyBlock(tool);game.World.StartTrial();game.World.AdvanceTrial(8);Check(game.Phase==RunPhase.Finished,"penalty can finish run");
                Begin();snap=game.CaptureRun();snap.version=5;snap.spawners=new[]{new SpawnerState{x=-10,row=10,hits=2,until=12},new SpawnerState{x=-11,row=11,hits=4,done=true,until=12}};game.RestoreRun(snap);Check(game.World.ActiveTrial.hits==0&&!game.World.ActiveTrial.started&&game.World.ActiveTrial.remaining==8&&game.World.Trials.Count(t=>t.done)==1,"legacy unfinished trial migrates without damage");
                // Cooling cannot create solid colliders inside an opaque tool pixel.
                Begin();tool=game.SelectedTool;game.World.DebugClearCell(0,10);game.World.DebugClearCell(1,10);tool.DebugPlace(new Vector2(0,-10.5f),Vector2.zero);
                game.World.Liquids.DebugSet(0,10,BlockKind.Lava);game.World.Liquids.DebugSet(1,10,BlockKind.Water);game.Clock.Restore(6);game.World.Liquids.Tick();Check(game.World.Liquids.IsCooling(0,10)&&game.World.CellAt(0,10)==null&&!game.World.Liquids.Touches(tool,BlockKind.Lava),"cooling waits without lava damage");
                saved=game.CaptureRun();game.RestoreRun(saved);Check(game.World.Liquids.IsCooling(0,10),"pending cooling persists");game.TogglePause();tool.DebugPlace(new Vector2(4,6),Vector2.zero);game.Clock.Restore(12);game.World.Liquids.Tick();Check(game.World.CellAt(0,10)?.Kind==BlockKind.Obsidian&&!game.World.Liquids.IsCooling(0,10),"cooling finishes after tool leaves");
                // A shallow boundary used to request Sandstone-on-Sand and fall back to generated art.
                config.map=MapId.Desert;Begin();
                for(int x=-20;x<=20;x++)for(int row=0;row<=9;row++)
                {
                    var cell=game.World.Generator.Sample(x,row);if(!cell.exists||cell.kind!=BlockKind.Sand&&cell.kind!=BlockKind.Sandstone)continue;
                    var at=game.World.CellAt(x,row);if(!at)continue;var sprite=at.GetComponent<SpriteRenderer>().sprite;
                    Check(AssetDatabase.GetAssetPath(sprite).StartsWith("Assets/PickaxeFall/Resources/Blocks/World/Desert/"),"new boundary texture "+x+"/"+row);
                }
                config.map=MapId.Mine;
                foreach(float rate in new[]{0f,3f,5f})
                {
                    Begin();game.World.DebugSetCell(-10,10,BlockKind.Spawner).DestroyBlock(game.SelectedTool);
                    for(int i=0;i<600&&game.TrialOpen;i++)BalanceLab.LabWorker.SimulateTrial(game,rate);
                    Check(!game.TrialOpen&&game.Benefits.Trials==(rate==5?1:0)&&game.Benefits.FailedTrials==(rate==5?0:1),"explicit lab tap policy "+rate);
                    Check(game.Clock.Ticks==0&&game.Benefits.TrialSeconds>6,"trial consumes session time without mining ticks "+rate);
                }
                Begin(4,3);var partner=game.Pickaxes[1];int partnerWear=partner.Durability,ownWear=game.Pickaxes[0].Durability;
                game.World.DebugSetCell(-10,10,BlockKind.Spawner).DestroyBlock(partner);game.World.StartTrial();game.World.AdvanceTrial(8);
                Check(partner.Durability==partnerWear-Mathf.CeilToInt(partner.MaxDurability*.3f)&&game.Pickaxes[0].Durability==ownWear&&game.Pickaxes[2].Durability==ownWear,"only initiating tool pays penalty");
                var levels=new int[Progression.KeyCount];levels[new UpgradeKey(UpgradeKind.LastChance).Index]=40;
                game.BeginSimulation(0,8127,levels,0,1);tool=game.SelectedTool;snap=game.CaptureRun();snap.tools[0].durability=1;game.RestoreRun(snap);game.TogglePause();game.Rolls.Forced[(int)UpgradeKind.LastChance]=1;
                game.World.DebugSetCell(-10,10,BlockKind.Spawner).DestroyBlock(tool);game.World.StartTrial();game.World.AdvanceTrial(8);
                Check(tool.IsAlive&&game.RescueUsed&&game.Benefits.Rescues==1,"last chance can rescue failed trial");
                Debug.Log("PICKAXEFALL_PATCH_CHECKS_OK: "+checks);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(config);}
        }
    }
}
