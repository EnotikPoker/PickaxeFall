#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    // Development-only previews use a fresh cache profile, never the player's persistent save.
    public class RuntimePreview : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;
            var game = GetComponent<GameController>(); var root = GetComponent<UIDocument>().rootVisualElement;
            game.Economy.Profile.coins = 250;
            for (int i = 0; i < 8; i++) game.Economy.Profile.resources[i] = (8 - i) * 7;
            if(game.TestMode=="desert"||game.TestMode=="nether"||game.TestMode=="temple"||game.TestMode=="fortress"||game.TestMode=="mineshaft"||game.TestMode=="treasure")
            {
                var map=game.TestMode=="nether"||game.TestMode=="fortress"?MapId.Nether:game.TestMode=="mineshaft"?MapId.Mine:MapId.Desert;
                game.Economy.Profile.bridgeStone=1000;game.Economy.Profile.bridgeIron=50;game.Economy.Profile.portalObsidian=10;game.SelectMap(map);
                int seed=8127,cx=0,cy=map==MapId.Nether?120:95;
                if(game.TestMode=="temple"||game.TestMode=="fortress"||game.TestMode=="mineshaft")for(seed=1;seed<1000;seed++){var g=new MineGenerator(game.Balance,seed);if(g.TryFeature(0,out cx,out cy,out _))break;}
                game.BeginProbeRun(4,seed,1);game.Pickaxe.DebugPlace(new Vector2(cx,-cy+1),Vector2.zero);game.Pickaxe.Body.gravityScale=0;game.Pickaxe.Body.simulated=false;game.StepMiningWorld();Camera.main.GetComponent<MineCamera>().SnapToTools();
                if(game.TestMode=="treasure")game.World.DebugSetCell(0,cy,BlockKind.Chest).DestroyBlock(game.Pickaxe);
            }
            else if(game.TestMode=="mapmine")Submit(root.Q<Button>("open-maps"));
            else if(game.TestMode=="pinned")
            {
                game.Economy.Profile.profileLevel=3;System.Array.Fill(game.Economy.Profile.revealed,true);
                game.Economy.TrySetGoal(GoalKind.Upgrade,new UpgradeKey(UpgradeKind.Fortune).Index);
                game.SelectHubTab(HubTab.Shop);Submit(root.Q<Button>("shop-category-1"));
            }
            else if(game.TestMode=="profile")
            {
                game.Economy.Profile.profileLevel=3;game.Economy.Profile.lifetimeMined[1]=2800;game.Economy.Profile.lifetimeMined[3]=220;game.Economy.Profile.lifetimeMined[4]=75;game.Economy.Profile.lifetimeMined[5]=10;
                Submit(root.Q<Button>("open-profile"));
            }
            else if(game.TestMode=="bridge"||game.TestMode=="portal")
            {
                game.Economy.Profile.bridgeStone=650;game.Economy.Profile.bridgeIron=30;game.Economy.Profile.portalObsidian=10;
                Submit(root.Q<Button>("open-maps"));Submit(root.Q<Button>(game.TestMode=="bridge"?"map-page-1":"map-page-2"));
            }
            else if(game.TestMode=="locks")
            {
                game.Economy.Profile.profileLevel=2;game.SelectHubTab(HubTab.Shop);Submit(root.Q<Button>("shop-category-1"));
            }
            else if(game.TestMode=="surplus")
            {
                game.Economy.TrySetGoal(GoalKind.Bridge);game.Economy.Profile.resources[1]=1200;game.Economy.Profile.resources[4]=70;game.SelectHubTab(HubTab.Inventory);Submit(root.Q<Button>("sell-surplus"));
            }
            else if (game.TestMode == "team")
            {
                foreach (var key in Progression.Keys) game.Economy.Profile.upgrades[key.Index] = Progression.MaxLevel(key.Kind);
                game.BeginProbeRun(0, 8127, 5);
            }
            else if (game.TestMode == "results" || game.TestMode == "benefits")
            {
                game.BeginProbeRun(0, 8127, 1); yield return new WaitForSeconds(2); game.FinishRun(); yield return new WaitForSeconds(1);
                if (game.TestMode == "benefits") Submit(root.Q<Button>("show-benefits"));
            }
            else if(game.TestMode=="tutorial"){Submit(root.Q<Button>("open-settings"));Submit(root.Q<Button>("settings-tutorial"));}
            else if(game.TestMode=="reset"){Submit(root.Q<Button>("open-settings"));Submit(root.Q<Button>("settings-reset-progress"));}
            else if(game.TestMode=="loot"){game.BeginProbeRun(0,8127,1);yield return new WaitForSeconds(.8f);game.World.DebugSetCell(-10,8,BlockKind.Copper).DestroyBlock(game.Pickaxe);}
            else if(game.TestMode=="dungeon" || game.TestMode=="geode")
            {
                MineGenerator.Structure found=default;int seed=1;
                for(;seed<1000;seed++){var gen=new MineGenerator(game.Balance,seed);if(gen.TryStructure(0,out found) && found.geode==(game.TestMode=="geode"))break;}
                game.BeginProbeRun(4,seed,1);game.Pickaxe.DebugPlace(new Vector2(found.x,-found.row+1),Vector2.zero);game.Pickaxe.Body.gravityScale=0;game.Pickaxe.Body.simulated=false;
                game.StepMiningWorld();Camera.main.GetComponent<MineCamera>().SnapToTools();
            }
            else if(game.TestMode=="liquids" || game.TestMode=="benches")
            {
                game.BeginProbeRun(4,8127,1);game.Pickaxe.DebugPlace(new Vector2(0,-43),Vector2.zero);game.Pickaxe.Body.gravityScale=0;game.Pickaxe.Body.simulated=false;game.StepMiningWorld();
                if(game.TestMode=="benches")
                {game.World.DebugSetCell(-2,45,BlockKind.Workbench);game.World.DebugSetCell(2,45,BlockKind.UpgradeWorkbench);}
                else
                {
                    for(int row=42;row<=47;row++)for(int x=-4;x<=4;x++)game.World.DebugSetCell(x,row,BlockKind.Stone).DestroyBlock(null);
                    for(int row=43;row<=47;row++){game.World.DebugSetCell(-4,row,BlockKind.Stone);game.World.DebugSetCell(4,row,BlockKind.Stone);}
                    for(int x=-4;x<=4;x++)game.World.DebugSetCell(x,47,BlockKind.Stone);
                    for(int x=-3;x<=3;x++)game.World.Liquids.DebugSet(x,46,x<0?BlockKind.Water:BlockKind.Lava);
                    game.World.Liquids.DebugSet(-1,44,BlockKind.Water);game.World.Liquids.DebugSet(-1,45,BlockKind.Water);
                }
                Camera.main.GetComponent<MineCamera>().SnapToTools();
            }
            else if(game.TestMode=="ores")
            {
                game.BeginProbeRun(4,8127,1);game.Pickaxe.DebugPlace(new Vector2(0,-92),Vector2.zero);game.Pickaxe.Body.gravityScale=0;game.Pickaxe.Body.simulated=false;game.StepMiningWorld();
                foreach(var kind in new[] {BlockKind.Emerald,BlockKind.Lapis,BlockKind.Redstone,BlockKind.Amethyst,BlockKind.AmethystCluster})game.World.DebugSetCell((int)kind-12,94,kind);
                game.World.ActivateRedstone(0,94,game.Pickaxe);Camera.main.GetComponent<MineCamera>().SnapToTools();
            }
            else if (game.TestMode == "controls") game.BeginProbeRun(0, 8127, 3);
            else if (game.TestMode == "abilities") { game.SelectHubTab(HubTab.Shop); yield return null; Submit(root.Q<Button>("shop-category-3")); }
            else if (game.TestMode == "mine") { }
            else if (game.TestMode == "settings") Submit(root.Q<Button>("open-settings"));
            else if (game.TestMode == "shop" || game.TestMode == "luck")
            {
                game.SelectHubTab(HubTab.Shop);
                if (game.TestMode == "luck") { yield return null; Submit(root.Q<Button>("shop-category-2")); }
            }
            else
            {
                game.SelectHubTab(HubTab.Inventory);
                if (game.TestMode == "sale") { yield return null; Submit(root.Q<Button>("sell-Stone")); }
            }
            yield return new WaitForSecondsRealtime(game.TestMode == "loot" ? .2f : game.TestMode == "team" ? 5 : 1);
            string file = Path.Combine(Application.temporaryCachePath, "pickaxefall-preview-" + game.TestMode + ".png");
#if !UNITY_ANDROID
            file = "/tmp/pickaxefall-preview-" + game.TestMode + ".png";
#endif
#if !UNITY_ANDROID
            ScreenCapture.CaptureScreenshot(file);
#endif
            if (game.TestMode == "controls" || game.TestMode == "team")
            {
                var field = root.Q("gesture-field").worldBound; var tool = root.Q("tool-select-2").worldBound; float scale = Screen.width / root.worldBound.width;
                Debug.Log(FormattableString.Invariant($"PICKAXEFALL_CONTROL_LAYOUT: field={field.x * scale:F1},{field.y * scale:F1},{field.width * scale:F1},{field.height * scale:F1}; tool2={tool.center.x * scale:F1},{tool.center.y * scale:F1}"));
            }
            Debug.Log("PICKAXEFALL_PREVIEW_READY: " + game.TestMode + "; " + file);
            int frames = 0; float seconds = 0, slowest = 0;
            while (seconds < 10) { yield return null; float dt = Time.unscaledDeltaTime; seconds += dt; frames++; slowest = Mathf.Max(slowest, dt); }
            if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--exit-after-preview"))Application.Quit(0);
            Debug.Log($"PICKAXEFALL_DEVICE_PERFORMANCE: mode={game.TestMode}; averageFPS={frames / seconds:F1}; slowestFrameMs={slowest * 1000:F1}; blocks={game.World.ActiveBlocks}");
        }
        static void Submit(Button button)
        {
            if (button == null) throw new Exception("Preview button missing");
            if (button.userData is Action command) command(); else throw new Exception("Preview command binding missing");
        }
    }
}
#endif
