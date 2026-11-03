using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        bool settingsOpen,resumeAfterSettings;
        void OpenSettings()
        {
            if(settingsOpen)return;settingsOpen=true;resumeAfterSettings=game.Phase==RunPhase.Mining && !game.Paused;
            if(resumeAfterSettings)game.TogglePause();DrawSettings();
        }
        void DrawSettings()
        {
            var card=OpenModal("Настройки игры");
            Text(card,"PickaxeFall · Версия "+GameVersion.Name,"footnote").name="game-version";
            Button(card,"@gamedevmaxon",()=>Application.OpenURL("https://t.me/gamedevmaxon"),"stone-button channel-button").name="telegram-channel";
            var options=game.Settings;
            var volumeText=BoldText(card,"Громкость: "+Mathf.RoundToInt(options.Volume*100)+"%","section-label");
            var volume=new Slider(0,1){value=options.Volume,name="settings-volume"};volume.AddToClassList("volume-slider");card.Add(volume);
            volume.RegisterValueChangedCallback(e=>{options.SetVolume(e.newValue);volumeText.text="Громкость: "+Mathf.RoundToInt(options.Volume*100)+"%";});
            BoldText(card,"Частота кадров","section-label");var fps=Box(card,"settings-row fps-options");
            foreach(int value in GameSettings.FrameRates)
                Button(fps,value==0?"ПО ЭКРАНУ":value+" FPS",()=>{options.SetFrameRate(value);DrawSettings();},"stone-button settings-choice"+(options.FrameRate==value?" active":"")).name="settings-fps-"+value;
            Text(card,"Выше частота — плавнее движение и больше расход батареи. «По экрану» использует текущую частоту дисплея.","footnote");
            Button(card,"МЕНЬШЕ ЧАСТИЦ: "+(options.ReducedEffects?"ВКЛ":"ВЫКЛ"),()=>{options.SetReducedEffects(!options.ReducedEffects);DrawSettings();},"stone-button settings-switch"+(options.ReducedEffects?" active":"")).name="settings-effects";
            Button(card,"ПОДСКАЗКИ ДОБЫЧИ: "+(options.LootMessages?"ВКЛ":"ВЫКЛ"),()=>{options.SetLootMessages(!options.LootMessages);DrawSettings();},"stone-button settings-switch"+(options.LootMessages?" active":"")).name="settings-loot";
            Text(card,"У кирки появляются +количество и значок ресурса. Одновременная добыча одного вида объединяется, чтобы не закрывать шахту.","footnote");
            Button(card,"ПОВТОРИТЬ ОБУЧЕНИЕ",BeginTutorial,"stone-button settings-switch").name="settings-tutorial";
            Button(card,"НАСТРОЙКИ ПО УМОЛЧАНИЮ",()=>{options.Reset();DrawSettings();},"stone-button settings-switch").name="settings-reset";
            Button(card,"СБРОСИТЬ ПРОГРЕСС",ConfirmProgressReset,"stone-button settings-switch buy-blocked").name="settings-reset-progress";
            Button(card,"ГОТОВО",()=>CloseModal(),"stone-button primary").name="settings-close";
        }
        void ConfirmProgressReset()
        {
            var card=OpenModal("Сбросить весь прогресс?");
            Text(card,"Будут удалены монеты, ресурсы, все улучшения и рекорд глубины. Текущий забег завершится. Настройки останутся. Отменить сброс нельзя.\n\nДля подтверждения введи СБРОС:","description");
            var input=new TextField{name="progress-reset-input",maxLength=5};input.AddToClassList("quantity-input");card.Add(input);
            var confirm=Button(card,"УДАЛИТЬ ПРОГРЕСС",()=>{if(input.value.Trim().ToUpperInvariant()=="СБРОС")game.ResetProgress();},"stone-button primary buy-blocked");confirm.name="progress-reset-confirm";confirm.SetEnabled(false);
            input.RegisterValueChangedCallback(e=>confirm.SetEnabled(e.newValue.Trim().ToUpperInvariant()=="СБРОС"));
            Button(card,"ОТМЕНА",DrawSettings,"stone-button secondary").name="progress-reset-cancel";
        }
        void FinishSettings()
        {
            if(!settingsOpen)return;settingsOpen=false;game.Settings.Save();bool resume=resumeAfterSettings;resumeAfterSettings=false;
            if(resume && game.Paused && game.Phase==RunPhase.Mining)game.TogglePause();
        }
    }
}
