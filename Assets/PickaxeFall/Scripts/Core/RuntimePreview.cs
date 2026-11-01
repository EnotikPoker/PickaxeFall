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
            if (game.TestMode == "team")
            {
                foreach (var key in Progression.Keys) game.Economy.Profile.upgrades[key.Index] = Progression.MaxLevel(key.Kind);
                game.BeginProbeRun(0, 8127, 5);
            }
            else if (game.TestMode == "results" || game.TestMode == "benefits")
            {
                game.BeginProbeRun(0, 8127, 1); yield return new WaitForSeconds(2); game.FinishRun(); yield return new WaitForSeconds(1);
                if (game.TestMode == "benefits") Submit(root.Q<Button>("show-benefits"));
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
            yield return new WaitForSecondsRealtime(game.TestMode == "team" ? 5 : 1);
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
