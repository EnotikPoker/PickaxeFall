#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed class RuntimeJournalRunner : MonoBehaviour
    {
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string text, string trace, LogType type) { if(type is LogType.Error or LogType.Exception) Application.Quit(1); }
        IEnumerator Start()
        {
            yield return null;
            yield return RuntimeJournalChecks.Run(GetComponent<GameController>(), GetComponent<UIDocument>().rootVisualElement, (ok, message) => { if(!ok) throw new Exception(message); });
            Application.Quit(0);
        }
    }
    public static class RuntimeJournalChecks
    {
        static void Press(VisualElement root, string name)
        {
            var button = root.Q<Button>(name);
            if (button == null || !button.enabledSelf || button.userData is not Action action) throw new Exception("Journal button missing: " + name);
            action();
        }
        static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.temporaryCachePath, "journal-086-" + name + ".png"));
            yield return null;
        }
        public static IEnumerator Run(GameController game, VisualElement root, Action<bool, string> check)
        {
            game.NewRun(); game.Settings.SetTutorialSeen(true); game.Settings.SetTutorialHints(int.MaxValue);
            var p = game.Economy.Profile;
            Array.Clear(p.discovered, 0, p.discovered.Length); Array.Clear(p.resources, 0, p.resources.Length); Array.Clear(p.lifetimeMined, 0, p.lifetimeMined.Length);
            game.Economy.Collect(BlockKind.Stone, 2); game.Economy.TrySell(BlockKind.Stone, 2); p = game.Economy.Profile;
            Press(root, "open-journal"); yield return null; yield return null;
            check(root.Q("mineral-Stone") != null && !root.Q("mineral-Stone").ClassListContains("mineral-unknown"), "sold specimen remains visible");
            check(root.Q("mineral-Diamond").ClassListContains("mineral-unknown"), "unfound mineral is a silhouette");
            yield return Capture("collection");
            Press(root, "mineral-Stone"); yield return null;
            check(root.Q("mineral-details") != null && root.Q<Label>(className: "journal-record-value").text.Contains("мон"), "specimen detail shows real sale price");
            Press(root, "mineral-back"); Press(root, "catalog-map-1"); yield return null;
            check(root.Q("mineral-SmokyQuartz") != null && root.Q("mineral-Quartz") == null, "desert quartz stays distinct");
            Press(root, "catalog-map-2"); yield return null;
            check(root.Q("mineral-Quartz") != null && root.Q("mineral-SmokyQuartz") == null, "Nether catalog has correct minerals");
            Press(root, "journal-tab-1"); yield return null; yield return null;
            check(root.Query(className: "daily-card").ToList().Count == 3 && !root.Q<Button>("daily-claim-0").enabledSelf, "three daily cards and disabled incomplete reward");
            long day = p.journal.day;
            p.journal.tasks[0].progress = p.journal.tasks[0].target;
            long before = p.coins, reward = p.journal.tasks[0].reward;
            Press(root, "journal-tab-1"); yield return null;
            check(root.Q<Button>("daily-claim-0").enabledSelf, "completed daily reward enabled");
            yield return Capture("tasks");
            Press(root, "daily-claim-0"); yield return null;
            check(game.Wallet == before + reward && !root.Q<Button>("daily-claim-0").enabledSelf, "reward claimed once via UI");
            check(!game.ClaimDaily(0, day) && game.Wallet == before + reward, "second daily command cannot duplicate coins");
            check(new ProfileStore(game.ProfileDirectory).Load().journal.tasks[0].claimed, "claimed flag survives disk reload");
            Press(root, "journal-tab-2"); yield return null;
            check(root.Query(className: "journal-record").ToList().Count >= 12, "personal statistics render");
            yield return Capture("records");
            Press(root, "journal-close");
            foreach (string language in new[] { "en", "ru" })
            {
                game.ChangeLanguage(language); yield return null;
                Press(root, "settings-close"); Press(root, "open-journal");
                for (int tab = 0; tab < 3; tab++)
                {
                    Press(root, "journal-tab-" + tab); yield return null;
                    if (language == "en") root.Q("journal-book").Query<TextElement>().ForEach(t => check(!System.Text.RegularExpressions.Regex.IsMatch(t.text ?? "", "[А-Яа-яЁё]"), "journal English: " + t.text));
                }
                Press(root, "journal-close");
            }
            // A stationary tip contact costs wear immediately; leaving the surface
            // does not erase the remaining heat. Resume must keep that exact heat.
            Array.Clear(game.Economy.Profile.upgrades, 0, Progression.KeyCount);
            game.BeginProbeRun(4, 8127, 1); var tool = game.SelectedTool;
            for (int y = 49; y <= 52; y++) for (int x = -3; x <= 3; x++) game.World.DebugClearCell(x, y);
            game.World.Liquids.Reset(null); game.World.Liquids.DebugSet(0, 50, BlockKind.Lava);
            tool.DebugPlace(new Vector2(.65f, -50.5f), Vector2.zero, 0);
            float immersion = game.World.Liquids.Immersion(tool, BlockKind.Lava);
            check(immersion > 0 && immersion < .95f, "test uses partial lava contact");
            int durability = tool.Durability; tool.PhysicsTick();
            check(tool.Durability == durability - 2, "a single tip contact burns immediately");
            var snap = tool.Capture(); check(snap.lavaHeat > 1 && snap.lavaWear > 0, "burn heat and fractional wear captured");
            tool.Restore(snap); check(tool.Capture().lavaHeat == snap.lavaHeat && tool.Durability == snap.durability, "restore does not reset or duplicate burn");
            game.World.Liquids.DebugSet(0, 50, BlockKind.Lava, 0);
            int afterContact = tool.Durability;
            for (int i = 0; i < 90; i++) tool.PhysicsTick();
            check(tool.Durability <= afterContact - 4 && tool.Capture().lavaHeat == 0, "heat causes bounded damage after leaving lava");
            int cooled = tool.Durability; for (int i = 0; i < 90; i++) tool.PhysicsTick();
            check(tool.Durability == cooled, "cooled tool takes no phantom damage");
            // Heat resistance applies to both contact and lingering wear.
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.HeatShield).Index] = 10;
            game.BeginProbeRun(4, 8127, 1); tool = game.SelectedTool; game.World.Liquids.Reset(null);
            tool.DebugPlace(new Vector2(.65f, -50.5f), Vector2.zero, 0); game.World.Liquids.DebugSet(0, 50, BlockKind.Lava);
            durability = tool.Durability; tool.PhysicsTick();
            check(tool.Durability == durability - 1, "heat shield halves initial burn");
            long finishedBefore = game.Economy.Profile.journal.completedRuns;
            game.FinishRun(); game.FinishRun(); yield return new WaitForSeconds(1);
            check(game.Economy.Profile.journal.completedRuns == finishedBefore + 1, "run completion is counted once");
            game.NewRun();
            Debug.Log("PICKAXEFALL_JOURNAL_RUNTIME_OK: catalog, dailies, rewards, statistics, localization and partial lava burns.");
        }
    }
}
#endif
