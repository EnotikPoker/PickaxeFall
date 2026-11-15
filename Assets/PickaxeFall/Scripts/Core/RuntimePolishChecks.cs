#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public static class RuntimePolishChecks
    {
        static void Press(VisualElement root,string name){if(root.Q<Button>(name)?.userData is Action action)action();else throw new Exception("Missing command "+name);}
        public static IEnumerator Run(GameController game,VisualElement root,Action<bool,string> check)
        {
            game.NewRun();var p=game.Economy.Profile;p.profileLevel=5;p.coins=100000000;Array.Fill(p.revealed,true);foreach(var kind in ResourceInventory.Materials){p.resources[(int)kind]=100000;p.discovered[(int)kind]=true;}
            game.SelectHubTab(HubTab.Shop);Press(root,"shop-category-1");
            check(root.Q("upgrade-EarthDigger-0")!=null&&root.Q("upgrade-Underwater-0")!=null&&root.Q("upgrade-HeatShield-0")!=null,"new gathering/defence cards rendered");
            foreach(var kind in new[]{UpgradeKind.EarthDigger,UpgradeKind.Underwater,UpgradeKind.HeatShield}){Press(root,"buy-"+kind+"-0");yield return new WaitForSecondsRealtime(.3f);check(game.Economy.Profile.Level(new(kind))==1,"new skill purchased "+kind);}
            check(root.Q("upgrade-Underwater-0").Q<Label>(className:"upgrade-title").text.StartsWith("Подводник"),"underwater skill human title");
            Press(root,"shop-category-2");Press(root,"buy-TreasureHunter-0");yield return new WaitForSecondsRealtime(.3f);check(game.Economy.Profile.Level(new(UpgradeKind.TreasureHunter))==1,"treasure skill purchased");
            game.Economy.TrySetGoal(GoalKind.Upgrade,new UpgradeKey(UpgradeKind.EarthDigger).Index);game.SelectHubTab(HubTab.Inventory);Press(root,"reserve-5");
            check(game.Settings.ReserveSteps==5&&game.Economy.ReserveSteps==5&&root.Q<Button>("reserve-5").ClassListContains("active"),"reserve horizon selected and shown");
            var reserve=game.Economy.ReservationCost();Press(root,"sell-surplus");Press(root,"confirm-sell-surplus");yield return new WaitForSecondsRealtime(.3f);
            foreach(var kind in ResourceInventory.Materials)check(game.Inventory[kind]==reserve.Resources[(int)kind],"five-step sale retains "+kind);
            game.Economy.Profile.bridgeStone=1000;game.Economy.Profile.bridgeIron=50;game.Economy.Profile.portalObsidian=10;game.SelectHubTab(HubTab.Mine);Press(root,"open-maps");
            foreach(int page in new[]{1,2}){Press(root,"map-page-"+page);check(root.Q(className:"map-materials").style.display==DisplayStyle.None&&root.Q(className:"map-progress-row").style.display==DisplayStyle.None&&root.Q(className:"profile-progress-track").style.display==DisplayStyle.None,"completed map hides requirements and percentage "+page);}
            Press(root,"map-enter-2");check(game.CurrentMap==MapId.Nether&&game.Phase==RunPhase.Ready,"completed route enters map");
            game.SelectMap(MapId.Mine);
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.EarthDigger).Index]=20;game.BeginProbeRun(0,8127,1);game.Rolls.Forced[(int)UpgradeKind.EarthDigger]=1;
            foreach(var kind in new[]{BlockKind.Dirt,BlockKind.Sand})foreach(var origin in new[]{DamageOrigin.Physical,DamageOrigin.Explosion,DamageOrigin.Shrapnel}){
                long before=game.Inventory[kind],mined=game.Economy.Profile.lifetimeMined[(int)kind];var block=game.World.DebugSetCell(-10,10,kind);block.DestroyBlock(game.SelectedTool,origin);int expected=origin==DamageOrigin.Physical?2:1;
                check(game.Inventory[kind]==before+expected&&game.Economy.Profile.lifetimeMined[(int)kind]==mined+1,"earth bonus applies only to direct hits "+kind+"/"+origin);
            }
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.HeatShield).Index]=10;game.BeginProbeRun(0,8127,1);var tool=game.SelectedTool;tool.DebugPlace(new Vector2(.65f,6.5f),new Vector2(5,5));tool.Body.rotation=0;Physics2D.SyncTransforms();game.World.Liquids.DebugSet(0,-7,BlockKind.Lava);
            int wear=tool.Durability;for(int i=0;i<30;i++){game.Clock.Advance();tool.PhysicsTick();}
            check(tool.Durability==wear-2&&game.Benefits.LavaWearSaved>1.9f,"heat shield halves lava wear even at partial contact");
            game.FinishRun();game.NewRun();
            game.Economy.Profile.mapRecords[0]=10;game.BeginProbeRun(0,8127,1);game.SelectedTool.DebugPlace(new Vector2(0,-10.1f),Vector2.zero);game.FinishRun();yield return new WaitForSecondsRealtime(1);
            check(!root.Q<Label>(className:"toast").text.StartsWith("НОВЫЙ РЕКОРД"),"equal depth is not a new record");
            game.BeginProbeRun(0,8127,1);game.SelectedTool.DebugPlace(new Vector2(0,-11.1f),Vector2.zero);game.FinishRun();yield return new WaitForSecondsRealtime(1);
            check(root.Q<Label>(className:"toast").text.StartsWith("НОВЫЙ РЕКОРД"),"strictly deeper run celebrates record");
            game.Economy.Profile.mapRecords[0]=200;game.Economy.Profile.mapRecords[1]=3;game.SelectMap(MapId.Desert);game.BeginProbeRun(0,8127,1);game.SelectedTool.DebugPlace(new Vector2(0,-4.1f),Vector2.zero);game.FinishRun();yield return new WaitForSecondsRealtime(1);
            check(root.Q<Label>(className:"toast").text.StartsWith("НОВЫЙ РЕКОРД"),"record celebration is independent for each map");
            game.SelectMap(MapId.Mine);game.NewRun();game.Settings.SetReserveSteps(3);game.Economy.ReserveSteps=3;game.Settings.Save();
        }
    }
}
#endif
