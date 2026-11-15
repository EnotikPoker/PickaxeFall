#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public static class RuntimeProfileChecks
    {
        static void Press(VisualElement root,string name)
        {
            var button=root.Q<Button>(name);
            if(button==null||!button.enabledSelf||button.userData is not Action action)throw new Exception("Profile UI button unavailable: "+name);
            action();
        }
        public static IEnumerator Run(GameController game,VisualElement root,Action<bool,string> check)
        {
            check(game.Economy.Profile.profileLevel==1&&game.Economy.Profile.version==8,"new profile starts level one");
            game.BeginProbeRun(0,8127,1);yield return null;
            check(root.Q("run-energy").parent.resolvedStyle.display==DisplayStyle.None,"energy panel hidden before ability purchase");
            game.FinishRun();yield return new WaitForSeconds(1);game.NewRun();
            Press(root,"open-profile");check(!root.Q<Button>("profile-level-up").enabledSelf,"level up disabled without required mining and skills");
            game.Economy.Profile.coins=100000;game.Economy.Profile.upgrades[1]=3;game.Economy.Profile.upgrades[5]=3;
            Array.Copy(ProfileRules.MiningRequirements[0],game.Economy.Profile.lifetimeMined,ResourceInventory.SlotCount);
            Press(root,"open-profile");Press(root,"profile-level-up");
            check(game.Economy.Profile.profileLevel==2&&game.Wallet==99750,"profile UI buys level with different valid pickaxe tiers");
            Press(root,"tab-shop");Press(root,"shop-category-1");
            check(root.Q<Button>("buy-Fortune-0").resolvedStyle.display==DisplayStyle.None&&root.Q<Button>("reveal-10").enabledSelf,"eligible skill presents free lock action");
            check(!root.Q<Button>("reveal-16").enabledSelf,"future skill lock stays unavailable");
            using(var click=ClickEvent.GetPooled()){var skill=root.Q("upgrade-Fortune-0");click.target=skill;skill.SendEvent(click);}yield return new WaitForSeconds(.8f);
            check(game.Economy.Profile.revealed[10]&&root.Q("lock-10").resolvedStyle.display==DisplayStyle.None,"lock animation finishes and stays open; revealed="+game.Economy.Profile.revealed[10]+"; display="+root.Q("lock-10").resolvedStyle.display);
            check(new ProfileStore(game.ProfileDirectory).Load().revealed[10],"revealed skill persisted");
            foreach(var kind in ResourceInventory.Materials)game.Economy.Profile.resources[(int)kind]=100;
            using(var click=ClickEvent.GetPooled()){var skill=root.Q("upgrade-Fortune-0");click.target=skill;skill.SendEvent(click);}
            check(game.Economy.Profile.goalKind==GoalKind.Upgrade&&root.Q("goal-pin-10").resolvedStyle.display!=DisplayStyle.None,"card tap pins goal with red pushpin");
            Press(root,"tab-inventory");
            check(game.Economy.Profile.goalKind==GoalKind.Upgrade&&root.Q<Button>("sell-surplus").enabledSelf,"goal persists and enables surplus sale");
            Press(root,"sell-surplus");Press(root,"confirm-sell-surplus");yield return new WaitForSeconds(.3f);
            var recipe=game.Economy.ReservationCost();foreach(var kind in ResourceInventory.Materials)check(game.Inventory[kind]==recipe.Resources[(int)kind],"surplus retains exact material "+kind);
            Press(root,"tab-mine");Press(root,"open-maps");Press(root,"map-page-1");
            game.Economy.Profile.resources[1]=250;game.Economy.Profile.resources[4]=7;
            Press(root,"map-page-1");Press(root,"contribute-Bridge");Press(root,"confirm-construction");yield return new WaitForSeconds(1.1f);
            check(game.Economy.Profile.bridgeStone==250&&game.Economy.Profile.bridgeIron==7&&game.Inventory[BlockKind.Stone]==0&&game.Inventory[BlockKind.Iron]==0,"bridge UI accepts and animates partial contribution");
            Press(root,"map-page-2");game.Economy.Profile.resources[20]=10;Press(root,"map-page-2");Press(root,"contribute-Portal");Press(root,"confirm-construction");yield return new WaitForSeconds(1.1f);
            check(game.Economy.Profile.portalObsidian==10&&root.Q("map-enter-mine")==null&&root.Q<Button>("contribute-Portal")==null&&root.Q<Button>("map-enter-2")!=null,"portal built permanently and offers entry");
            var persisted=new ProfileStore(game.ProfileDirectory).Load();
            check(persisted.bridgeStone==250&&persisted.bridgeIron==7&&persisted.portalObsidian==10,"partial bridge and complete portal survive reload");
            game.SelectHubTab(HubTab.Mine);
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.Fortune).Index]=25;
            game.BeginProbeRun(0,RuntimeSmokeTest.EffectSeed(UpgradeKind.Fortune,.5),1);
            long mined=game.Economy.Profile.lifetimeMined[(int)BlockKind.Coal],coal=game.Inventory[BlockKind.Coal];
            game.World.DebugSetCell(-10,5,BlockKind.Coal).DestroyBlock(game.Pickaxe);
            check(game.Economy.Profile.lifetimeMined[(int)BlockKind.Coal]==mined+1&&game.Inventory[BlockKind.Coal]==coal+2,"fortune awards two resources but only one mined block");
            var counts=(long[])game.Economy.Profile.lifetimeMined.Clone();
            game.World.DebugSetCell(-10,6,BlockKind.Chest).DestroyBlock(game.Pickaxe);
            for(int i=0;i<counts.Length;i++)check(counts[i]==game.Economy.Profile.lifetimeMined[i],"chest grants no lifetime mining "+i);
            game.TogglePause();var save=new ProfileStore(game.ProfileDirectory).Load();
            check(save.run!=null&&save.lifetimeMined[(int)BlockKind.Coal]==mined+1,"run snapshot and mining counters saved together");
            game.RestoreRun(save.run);check(game.Economy.Profile.lifetimeMined[(int)BlockKind.Coal]==mined+1,"restoring run cannot duplicate mining credit");
            game.ResetProgress();yield return null;
            Press(root,"tab-shop");Press(root,"shop-category-0");Press(root,"tab-mine");
            check(game.Economy.Profile.profileLevel==1&&game.Economy.Profile.bridgeStone==0&&game.Economy.Profile.portalObsidian==0,"UI reset clears profile progression and constructions");
        }
    }
}
#endif
