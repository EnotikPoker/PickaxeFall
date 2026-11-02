#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public static class RuntimePresentationChecks
    {
        static void Press(VisualElement root,string name)
        {
            var button=root.Q<Button>(name);if(button?.userData is Action action)action();else throw new Exception("Missing UI command: "+name);
        }
        public static IEnumerator Run(GameController game,VisualElement root,Action<bool,string> check)
        {
            check(root.Q("mining-resources")==null,"resource totals removed from combat HUD");
            Press(root,"open-settings");Press(root,"settings-fps-120");
            check(game.Settings.FrameRate==120 && Application.targetFrameRate==120,"120 FPS actually applies");
            Press(root,"settings-fps-240");check(game.Settings.FrameRate==240,"240 FPS selectable");
            Press(root,"settings-fps-0");check(Application.targetFrameRate==game.Settings.EffectiveFrameRate,"display-rate option applies detected refresh");
            check(root.Q<Label>("game-version").text.Contains(GameVersion.Name) && root.Q<Button>("telegram-channel").text=="@gamedevmaxon","settings show current version and channel");
            Press(root,"settings-close");
            Array.Clear(game.Economy.Profile.upgrades,0,25);game.BeginProbeRun(0,8127,1);
            check(!game.Pickaxe.SelectionVisible,"one tool has no outline");
            long wallet=game.Wallet;float energy=game.Energy.Value;int layout=game.World.LayoutFingerprint();
            Press(root,"open-settings");Press(root,"settings-tutorial");
            check(game.Paused && root.Q("tutorial-overlay").style.display==DisplayStyle.Flex,"tutorial pauses active run");
            for(int i=0;i<10;i++)Press(root,"tutorial-next");
            check(!game.Paused && game.Settings.TutorialSeen && root.Q("tutorial-overlay").style.display==DisplayStyle.None,"all tutorial pages finish and resume");
            check(game.Wallet==wallet && game.Energy.Value==energy && game.World.LayoutFingerprint()==layout,"tutorial never spends resources or changes mine");
            Press(root,"open-settings");Press(root,"settings-tutorial");Press(root,"tutorial-skip");
            check(!game.Paused && game.Settings.TutorialSeen,"replay can be skipped safely");
            game.BeginProbeRun(0,8127,2);
            check(game.Pickaxes[0].SelectionVisible && !game.Pickaxes[1].SelectionVisible,"only selected group tool outlined");
            game.SelectTool(1);check(game.Pickaxes[1].SelectionVisible && !game.Pickaxes[0].SelectionVisible,"silhouette follows selection");
            game.Pickaxes[0].DebugExhaust();check(!game.Pickaxes[1].SelectionVisible,"last survivor loses redundant outline");
            var ore=game.World.DebugSetCell(-10,10,BlockKind.Copper);ore.DestroyBlock(game.SelectedTool);
            yield return new WaitForSecondsRealtime(.2f);
            var popup=root.Q<Label>(className:"loot-number");
            check(popup!=null && popup.text.StartsWith("+") && root.Q(className:"loot-icon")!=null,"mining produces quantity and resource icon");
            game.FinishRun();yield return new WaitForSecondsRealtime(1.05f);
            long[] resources=(long[])game.OreCounts.Clone();
            yield return new WaitForSecondsRealtime(1.4f);
            var grid=root.Q("run-resources");
            for(int i=0;i<8;i++)check(grid.Q<Label>("count-"+(BlockKind)i).text==GameHud.Short(resources[i]),"animated resource count reaches exact total "+i);
            check(game.OreCounts.SequenceEqual(resources),"results animation cannot award loot twice");
            Array.Clear(game.Economy.Profile.upgrades,0,25);game.Economy.Profile.coins=0;Array.Clear(game.Economy.Profile.resources,0,8);
            game.SelectHubTab(HubTab.Shop);Press(root,"shop-category-0");Press(root,"shop-tier-0");
            var card=root.Q("upgrade-Damage-0");
            check(!card.Q<Button>("buy-Damage-0").enabledSelf && card.Q<Button>("buy-Damage-0").ClassListContains("buy-blocked"),"unaffordable upgrade is visibly red and disabled");
            var price=Progression.Cost(new(UpgradeKind.Damage),1);int material=Array.FindIndex(price.Resources,n=>n>0);
            check(card.Q<Label>("required-"+(BlockKind)material).ClassListContains("cost-short") && card.Q(className:"recipe-icon")!=null,"missing material is red with icon");
            game.Economy.Profile.resources[material]=price.Resources[material];game.SelectHubTab(HubTab.Shop);
            check(card.Q<Label>("required-"+(BlockKind)material).ClassListContains("cost-enough"),"enough material becomes green independently of coins");
            game.Economy.Profile.coins=100000;Array.Fill(game.Economy.Profile.resources,100000L);game.SelectHubTab(HubTab.Shop);
            check(card.Q<Button>("buy-Damage-0").enabledSelf && card.Q<Button>("buy-Damage-0").ClassListContains("buy-ready"),"affordable upgrade is green and enabled");
            Press(root,"open-settings");Press(root,"settings-reset-progress");
            root.Q<TextField>("progress-reset-input").value="нет";check(!root.Q<Button>("progress-reset-confirm").enabledSelf,"reset requires exact confirmation");
            Press(root,"progress-reset-cancel");check(game.Wallet==100000,"cancel reset keeps all progress");
            Press(root,"settings-reset-progress");root.Q<TextField>("progress-reset-input").value="СБРОС";Press(root,"progress-reset-confirm");
            check(game.Wallet==0 && game.Inventory.Total==0 && game.BestDepth==0 && Array.TrueForAll(game.Economy.Profile.upgrades,x=>x==0),"confirmed reset clears all progression");
            check(game.Phase==RunPhase.Ready && !game.Settings.TutorialSeen && game.Settings.FrameRate==0,"reset starts fresh run/tutorial and preserves settings");
            var saved=new ProfileStore(game.ProfileDirectory).Load();check(saved.coins==0 && saved.bestDepth==0 && saved.resources.All(x=>x==0),"reset survives profile reload");
            Debug.Log("PICKAXEFALL_PRESENTATION_OK: tutorial replay/skip, no-spend pause, FPS, recipes, selection, loot, animated results and confirmed reset.");
        }
    }
}
#endif
