using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        bool tutorialOpen,tutorialChecked,tutorialResume,tutorialLibrary;
        int tutorialStep,pendingHints,contextHintIndex;
        HubTab tutorialReturnTab;
        VisualElement tutorialOverlay,tutorialFocus,contextHint;
        static readonly string[] TutorialTitles={"Кирка выбирается рулеткой","Всё добытое — твоё","Рюкзак и продажа","Запас на улучшения","Улучшения кирок","Уровень профиля","Рывок и пробой","Верстаки","Вода и лава","Сундуки","Спавнер","Другие локации","Навыки для локаций","Сохранение"};
        string TutorialBody(int index)=>index switch {
            0=>"Крути рулетку. Шанс каждой кирки указан под её значком. После выбора нажми «Начать спуск».",
            1=>"Кирка падает и разбивает блоки. Удары тратят прочность. Когда сломается последняя кирка, добыча останется в рюкзаке.",
            2=>"Нажми на ресурс в рюкзаке, чтобы продать нужное количество. Внизу можно продать весь рюкзак с подтверждением.",
            3=>"Нажми на карточку навыка: красная кнопка отметит цель. «Продать лишнее» сохранит материалы на выбранное число будущих ступеней.",
            4=>"У каждого вида кирки свои урон и прочность. На карточке видно, что даст покупка. Зелёным отмечено то, на что уже хватает ресурсов.",
            5=>"Для нового уровня нужны монеты, добытые за всё время блоки и указанные навыки. Счётчик добычи не расходуется.",
            6=>"Рывок покупается на втором уровне профиля, пробой — на третьем. Свайп толкает выбранную кирку в сторону, нажатие пробивает блоки вниз. Энергия восстанавливается сама.",
            7=>"Любой верстак полностью чинит кирку. Улучшающий верстак ещё и повышает её вид. Купленные улучшения получившегося вида сохраняются.",
            8=>"Вода замедляет кирку и снижает её урон. Лава тоже замедляет, но ещё тратит прочность — даже при касании кончиком. Встреча воды и лавы даёт обсидиан.",
            9=>"Разбей сундук, чтобы забрать находки. Содержимое появится сбоку и сразу попадёт в рюкзак. Награды зависят от карты и глубины.",
            10=>"Удар по спавнеру запускает испытание: всего четыре прямых попадания за 12 секунд. Успех даст добычу. За неудачу отдельного штрафа нет.",
            11=>"В разделе «Карты» внеси материалы в мост или портал. Можно строить частями. Готовая локация остаётся открытой навсегда.",
            12=>"«Землекоп» даёт больше земли и песка. «Подводник» уменьшает штраф воды, «Термозащита» — износ в лаве. «Искатель сокровищ» добавляет добычу из сундуков.",
            _=>"Игра сохраняет прогресс сама. Незавершённый спуск после запуска ждёт на паузе. Повторить обучение и изменить эффекты можно в настройках."
        };
        void TickTutorial()
        {
            if(game.Economy.Profile.resetNotice || !game.ReadyToPresent || game.TestMode.Length>0)return;
            if(!tutorialChecked){tutorialChecked=true;if(!game.Settings.TutorialSeen)BeginTutorial();}
            if(tutorialOpen || !game.Settings.TutorialSeen)return;
            if(contextHint!=null&&!game.CanTrade&&!game.Paused){game.Settings.SetTutorialHints(game.Settings.TutorialHints&~(1<<contextHintIndex));DismissHint();}
            var p=game.Economy.Profile;
            if(CurrentTab==HubTab.Inventory&&game.Inventory.Total>0)pendingHints|=1<<2;
            if(p.goalKind!=GoalKind.None)pendingHints|=1<<3;
            if(CurrentTab==HubTab.Shop)pendingHints|=1<<4;
            if(game.Economy.CanLevelUp(out _))pendingHints|=1<<5;
            if(p.dashUnlocked||p.plungeUnlocked)pendingHints|=1<<6;
            if(game.WorkbenchesUsed>0)pendingHints|=1<<7;
            if(game.SelectedTool!=null&&game.SelectedTool.IsAlive&&(game.SelectedTool.InWater||game.World.Liquids.Touches(game.SelectedTool,BlockKind.Lava)))pendingHints|=1<<8;
            if(game.Benefits.Chests>0)pendingHints|=1<<9;
            if(game.Benefits.Trials+game.Benefits.FailedTrials>0)pendingHints|=1<<10;
            if(root.Q("construction-scene")!=null)pendingHints|=1<<11;
            if(p.Level(new(UpgradeKind.EarthDigger))+p.Level(new(UpgradeKind.Underwater))+p.Level(new(UpgradeKind.HeatShield))+p.Level(new(UpgradeKind.TreasureHunter))>0)pendingHints|=1<<12;
            if(contextHint!=null || ModalOpen || (!game.CanTrade&&!game.Paused))return;
            for(int i=2;i<13;i++)if((pendingHints&(1<<i))!=0&&(game.Settings.TutorialHints&(1<<i))==0){ShowContextHint(i);break;}
        }
        void ShowContextHint(int index)
        {
            contextHintIndex=index;game.Settings.SetTutorialHints(game.Settings.TutorialHints|(1<<index));game.Settings.Save();
            contextHint=Box(root,"context-hint");contextHint.name="context-hint";
            BoldText(contextHint,TutorialTitles[index],"context-hint-title");Text(contextHint,TutorialBody(index),"context-hint-body");
            var row=Box(contextHint,"settings-row");
            Button(row,"ПОНЯТНО",DismissHint,"stone-button compact-button");
            Button(row,"БЕЗ ПОДСКАЗОК",()=>{game.Settings.SetTutorialHints(int.MaxValue);game.Settings.Save();DismissHint();},"stone-button compact-button");
        }
        void DismissHint(){contextHint?.RemoveFromHierarchy();contextHint=null;}
        void BeginTutorial()
        {
            if(tutorialOpen)return;
            tutorialLibrary=settingsOpen;
            if(tutorialLibrary){game.Settings.SetTutorialHints(0);game.Settings.Save();}
            DismissHint();bool resume=settingsOpen&&resumeAfterSettings;resumeAfterSettings=false;CloseModal();
            tutorialResume=resume||game.Phase==RunPhase.Mining&&!game.Paused;
            if(game.Phase==RunPhase.Mining&&!game.Paused)game.TogglePause();
            CancelGesture();tutorialReturnTab=CurrentTab;tutorialStep=0;tutorialOpen=true;tutorialChecked=true;
            if(tutorialOverlay==null){tutorialOverlay=Box(root,"tutorial-overlay");tutorialOverlay.name="tutorial-overlay";}
            DrawTutorial();
        }
        void DrawTutorial()
        {
            tutorialOverlay.Clear();Display(tutorialOverlay,true);tutorialOverlay.BringToFront();int count=tutorialLibrary?TutorialTitles.Length:2;
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("tutorial-scroll");tutorialOverlay.Add(scroll);
            var card=Box(scroll.contentContainer,"modal-card tutorial-card");
            Text(card,$"ОБУЧЕНИЕ · {tutorialStep+1} / {count}","section-label");BoldText(card,TutorialTitles[tutorialStep],"title");
            var art=Box(card,"tutorial-art");Icon(art,PixelArt.Pickaxe(tutorialStep==0?3:0),"tutorial-icon");
            Text(card,TutorialBody(tutorialStep),"tutorial-body");var row=Box(card,"tutorial-actions");
            var back=Button(row,"НАЗАД",()=>{tutorialStep--;DrawTutorial();},"stone-button tutorial-nav");back.SetEnabled(tutorialStep>0);back.name="tutorial-back";
            Button(row,tutorialStep==count-1?"ИГРАТЬ":"ДАЛЕЕ",()=>{if(tutorialStep==count-1)EndTutorial();else{tutorialStep++;DrawTutorial();}},"stone-button primary tutorial-nav").name="tutorial-next";
            Button(card,"ПРОПУСТИТЬ",()=>{game.Settings.SetTutorialHints(int.MaxValue);EndTutorial();},"stone-button tutorial-skip").name="tutorial-skip";
        }
        void EndTutorial()
        {
            if(!tutorialOpen)return;if(tutorialLibrary)game.Settings.SetTutorialHints(int.MaxValue);tutorialOpen=false;Display(tutorialOverlay,false);game.Settings.SetTutorialSeen(true);game.Settings.Save();
            if(game.CanTrade)CurrentTab=tutorialReturnTab;
            if(tutorialResume&&game.Phase==RunPhase.Mining&&game.Paused)game.TogglePause();tutorialResume=false;Refresh();
        }
        public void ProgressReset(){DismissHint();game.Settings.SetTutorialHints(0);tutorialChecked=false;CloseModal();Toast("ПРОГРЕСС СБРОШЕН");}
    }
}
