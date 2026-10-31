using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        bool settingsOpen, resumeAfterSettings;
        void OpenSettings()
        {
            if (settingsOpen) return;
            settingsOpen = true;
            resumeAfterSettings = game.Phase == RunPhase.Mining && !game.Paused;
            if (resumeAfterSettings) game.TogglePause();
            DrawSettings();
        }
        void DrawSettings()
        {
            var card = OpenModal("Настройки игры");
            var options = game.Settings;
            var volumeText = BoldText(card, "Громкость: " + Mathf.RoundToInt(options.Volume * 100) + "%", "section-label");
            var volume = new Slider(0, 1) { value = options.Volume, name = "settings-volume" }; volume.AddToClassList("volume-slider"); card.Add(volume);
            volume.RegisterValueChangedCallback(e => { options.SetVolume(e.newValue); volumeText.text = "Громкость: " + Mathf.RoundToInt(options.Volume * 100) + "%"; });
            Text(card, "При 0% звук выключен.", "footnote");
            BoldText(card, "Частота кадров", "section-label"); var fps = Box(card, "settings-row");
            Button(fps, "30 FPS", () => { options.SetFrameRate(30); DrawSettings(); }, "stone-button settings-choice" + (options.FrameRate == 30 ? " active" : "")).name = "settings-fps-30";
            Button(fps, "60 FPS", () => { options.SetFrameRate(60); DrawSettings(); }, "stone-button settings-choice" + (options.FrameRate == 60 ? " active" : "")).name = "settings-fps-60";
            Button(card, "МЕНЬШЕ ЧАСТИЦ: " + (options.ReducedEffects ? "ВКЛ" : "ВЫКЛ"), () => { options.SetReducedEffects(!options.ReducedEffects); DrawSettings(); }, "stone-button settings-switch" + (options.ReducedEffects ? " active" : "")).name = "settings-effects";
            Button(card, "СООБЩЕНИЯ О ДОБЫЧЕ: " + (options.LootMessages ? "ВКЛ" : "ВЫКЛ"), () => { options.SetLootMessages(!options.LootMessages); DrawSettings(); }, "stone-button settings-switch" + (options.LootMessages ? " active" : "")).name = "settings-loot";
            Text(card, "Ресурсы всегда видны внизу экрана. Важные события показываются при любом положении переключателя.", "footnote");
            Button(card, "ПО УМОЛЧАНИЮ", () => { options.Reset(); DrawSettings(); }, "stone-button secondary").name = "settings-reset";
            Button(card, "ГОТОВО", () => CloseModal(), "stone-button primary").name = "settings-close";
        }
        void FinishSettings()
        {
            if (!settingsOpen) return;
            settingsOpen = false; game.Settings.Save();
            bool resume = resumeAfterSettings; resumeAfterSettings = false;
            if (resume && game.Paused && game.Phase == RunPhase.Mining) game.TogglePause();
        }
    }
}
