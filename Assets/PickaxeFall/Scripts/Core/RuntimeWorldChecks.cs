#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public static class RuntimeWorldChecks
    {
        public static IEnumerator Run(GameController game,VisualElement root,Action<bool,string> check)
        {
            game.ResetProgress();yield return null;game.Economy.Profile.dashUnlocked=true;game.BeginProbeRun(0,8127,1);game.Clock.Restore(180);new MiningPolicy().Tick(game);check(game.Pickaxe.AbilityActive&&game.Energy.Value<60,"active bot uses downward dash before plunge is bought");game.FinishRun();yield return new WaitForSeconds(1);game.ResetProgress();yield return null;game.Economy.Profile.bridgeStone=1000;game.Economy.Profile.bridgeIron=50;game.Economy.Profile.portalObsidian=10;
            check(game.SelectMap(MapId.Desert)&&game.CurrentMap==MapId.Desert,"constructed desert selectable");game.BeginProbeRun(4,8127,1);
            var block=game.World.DebugSetCell(-10,8,BlockKind.Malachite);long before=game.Inventory[BlockKind.Malachite];block.DestroyBlock(game.Pickaxe);check(game.Inventory[BlockKind.Malachite]==before+1&&game.Economy.Profile.discovered[(int)BlockKind.Malachite],"new ore collected and discovered");
            var chest=game.World.DebugSetCell(-10,10,BlockKind.Chest);long bag=game.Inventory.Total;chest.DestroyBlock(game.Pickaxe);long awarded=game.Inventory.Total;chest.DestroyBlock(game.Pickaxe);check(awarded>bag&&game.Inventory.Total==awarded,"chest exactly once");check(root.Q("treasure-panel")!=null&&!game.Paused,"treasure is nonblocking");
            var snap=game.CaptureRun();game.RestoreRun(snap);check(game.CurrentMap==MapId.Desert&&game.Paused&&game.World.CellAt(-10,10)==null,"desert restores without chest duplication");game.TogglePause();game.FinishRun();yield return new WaitForSeconds(1);game.NewRun();
            check(game.SelectMap(MapId.Nether),"portal opens nether");game.BeginProbeRun(4,8127,1);var spawner=game.World.DebugSetCell(-10,10,BlockKind.Spawner);
            spawner.DestroyBlock(game.Pickaxe,DamageOrigin.Explosion);check(game.World.CaptureTrials().Length==0&&!spawner.IsBroken,"explosion cannot activate trial");
            spawner.DestroyBlock(game.Pickaxe);spawner.DestroyBlock(game.Pickaxe);check(game.World.CaptureTrials()[0].hits==1,"first direct hit starts trial");snap=game.CaptureRun();game.RestoreRun(snap);check(game.CurrentMap==MapId.Nether&&game.World.CaptureTrials()[0].hits==1,"trial progress survives resume");game.TogglePause();spawner=game.World.CellAt(-10,10);
            bag=game.Inventory.Total;for(int hit=0;hit<3;hit++){game.Clock.Advance();spawner.DestroyBlock(game.Pickaxe);}awarded=game.Inventory.Total;
            check(spawner.IsBroken&&awarded>bag&&game.World.CaptureTrials()[0].done,"four hits finish trial");spawner.DestroyBlock(game.Pickaxe);check(game.Inventory.Total==awarded,"trial reward exactly once");
            var other=game.World.DebugSetCell(-9,11,BlockKind.Spawner);other.DestroyBlock(game.Pickaxe);int wear=game.Pickaxe.Durability;bag=game.Inventory.Total;for(int i=0;i<721;i++)game.Clock.Advance();game.World.TickTrials();check(other.IsBroken&&game.Pickaxe.Durability==wear&&game.Inventory.Total==bag,"failed trial no penalty or reward");
            game.TogglePause();snap=game.CaptureRun();float time=game.Clock.Now;game.SimulateMiningStep();check(game.Clock.Now==time,"pause freezes world and trials");game.TogglePause();
            game.FinishRun();yield return new WaitForSeconds(1);game.NewRun();game.ResetProgress();yield return null;
            check(game.CurrentMap==MapId.Mine&&game.Economy.Profile.mapRecords[1]==0&&!game.Economy.Profile.discovered[(int)BlockKind.Malachite],"manual reset clears worlds and collection");
            game.SelectHubTab(HubTab.Inventory);check(root.Q<Button>("sell-Malachite").resolvedStyle.display==DisplayStyle.None,"unfound absent from bag");game.Economy.Collect(BlockKind.Malachite);game.SelectHubTab(HubTab.Inventory);yield return null;check(root.Q<Button>("sell-Malachite").resolvedStyle.display!=DisplayStyle.None,"discovered visible in bag");game.Economy.TrySellAll();game.SelectHubTab(HubTab.Inventory);check(game.Economy.Profile.discovered[(int)BlockKind.Malachite],"sold item remains known");
            game.ResetProgress();game.SelectHubTab(HubTab.Mine);
        }
    }
}
#endif
