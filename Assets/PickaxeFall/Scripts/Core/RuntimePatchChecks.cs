#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public static class RuntimePatchChecks
    {
        public static IEnumerator Run(GameController game,VisualElement root,Action<bool,string> check)
        {
            game.NewRun();game.SelectMap(MapId.Mine);game.BeginProbeRun(0,8127,1);
            game.SendMessage("OnApplicationFocus",false);check(!game.Paused&&!game.WorldFrozen,"focus loss does not pause");game.SendMessage("OnApplicationFocus",true);
            game.SetApplicationSuspended(true);int ticks=game.Clock.Ticks;game.SimulateMiningStep();check(game.Clock.Ticks==ticks&&!game.Paused,"OS suspension stops simulation without manual menu");game.SetApplicationSuspended(false);check(!game.WorldFrozen,"foreground returns to running state");
            game.TogglePause();game.SetApplicationSuspended(true);game.SetApplicationSuspended(false);check(game.Paused,"manual pause survives OS transition");game.TogglePause();
            var spawner=game.World.DebugSetCell(-10,10,BlockKind.Spawner);spawner.DestroyBlock(game.SelectedTool);yield return null;
            check(root.Q("spawner-overlay").style.display==DisplayStyle.Flex&&!game.Paused&&Time.timeScale==0,"trial overlay freezes physics without pause overlay");
            (root.Q<Button>("spawner-start").userData as Action)();for(int i=0;i<12;i++)game.World.TapTrial();game.World.AdvanceTrial(2);
            game.SetApplicationSuspended(true);float left=game.World.ActiveTrial.remaining;game.World.AdvanceTrial(20);check(game.World.ActiveTrial.remaining==left&&!game.World.TapTrial(),"trial OS suspension freezes time and input");game.SetApplicationSuspended(false);
            game.TogglePause();(root.Q<Button>("pause-settings").userData as Action)();(root.Q<Button>("settings-close").userData as Action)();check(game.Paused&&game.World.ActiveTrial.hits==12,"settings preserves paused trial");game.TogglePause();
            for(int i=12;i<30;i++)game.World.TapTrial();game.World.AdvanceTrial(1);check(!game.TrialOpen&&Time.timeScale==1,"trial success resumes physics");
            // Native collision callbacks are only delivered in play mode.
            for(int tier=0;tier<5;tier++)foreach(int liquid in new[]{0,1,2})
            {
                game.BeginProbeRun(tier,8127,1);var tool=game.SelectedTool;
                for(int row=4;row<=14;row++)for(int x=-4;x<=4;x++)game.World.DebugClearCell(x,row);
                for(int x=-3;x<=3;x++)game.World.DebugSetCell(x,12,BlockKind.Obsidian);
                var block=game.World.CellAt(0,12);int hp=block.HealthHalfUnits;
                tool.DebugPlace(new Vector2(0,-10.7f),new Vector2(0,-4));if(liquid>0)game.World.Liquids.DebugSet(0,11,liquid==1?BlockKind.Water:BlockKind.Lava);
                for(int i=0;i<180&&tool.IsAlive&&game.Phase==RunPhase.Mining;i++)game.SimulateMiningStep();
                bool hit=false;for(int x=-3;x<=3;x++){var b=game.World.CellAt(x,12);if(b==null||b.HealthHalfUnits<hp)hit=true;}
                check(hit,"native obsidian impact tier "+tier+" liquid="+liquid);
            }
            game.NewRun();yield return null;
        }
    }
}
#endif
