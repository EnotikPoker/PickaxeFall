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
        static readonly string[] TutorialTitles={L.T("Кирка выбирается рулеткой"),L.T("Всё добытое — твоё"),L.T("Рюкзак и продажа"),L.T("Запас на улучшения"),L.T("Улучшения кирок"),L.T("Уровень профиля"),L.T("Рывок и пробой"),L.T("Верстаки"),L.T("Вода и лава"),L.T("Сундуки"),L.T("Спавнер"),L.T("Другие локации"),L.T("Навыки для локаций"),L.T("Сохранение")};
        string TutorialBody(int index)=>index switch {
            0=>L.T("Крути рулетку. Шанс каждой кирки указан под её значком. После выбора нажми «Начать спуск»."),
            1=>L.T("Кирка падает и разбивает блоки. Удары тратят прочность. Когда сломается последняя кирка, добыча останется в рюкзаке."),
            2=>L.T("Нажми на ресурс в рюкзаке, чтобы продать нужное количество. Внизу можно продать весь рюкзак с подтверждением."),
            3=>L.T("Нажми на карточку навыка: красная кнопка отметит цель. «Продать лишнее» сохранит материалы на выбранное число будущих ступеней."),
            4=>L.T("У каждого вида кирки свои урон и прочность. На карточке видно, что даст покупка. Зелёным отмечено то, на что уже хватает ресурсов."),
            5=>L.T("Для нового уровня нужны монеты, добытые за всё время блоки и указанные навыки. Счётчик добычи не расходуется."),
            6=>L.T("Рывок покупается на втором уровне профиля, пробой — на третьем. Свайп толкает выбранную кирку в сторону, нажатие пробивает блоки вниз. Энергия восстанавливается сама."),
            7=>L.T("Любой верстак полностью чинит кирку. Улучшающий верстак ещё и повышает её вид. Купленные улучшения получившегося вида сохраняются."),
            8=>L.T("Вода замедляет кирку и снижает её урон. Лава тоже замедляет, но ещё тратит прочность — даже при касании кончиком. Встреча воды и лавы даёт обсидиан."),
            9=>L.T("Разбей сундук, чтобы забрать находки. Содержимое появится сбоку и сразу попадёт в рюкзак. Награды зависят от карты и глубины."),
            10=>L.T("Коснись спавнера киркой: спуск остановится. Сделай 30 нажатий за 8 секунд и получи награду. Не успеешь — кирка потеряет 30% максимальной прочности. Пауза сохраняет время и нажатия."),
            11=>L.T("В разделе «Карты» внеси материалы в мост или портал. Можно строить частями. Готовая локация остаётся открытой навсегда."),
            12=>L.T("«Землекоп» даёт больше земли и песка. «Подводник» уменьшает штраф воды, «Термозащита» — износ в лаве. «Искатель сокровищ» добавляет добычу из сундуков."),
            _=>L.T("Игра сохраняет прогресс сама. Незавершённый спуск после запуска ждёт на паузе. Повторить обучение и изменить эффекты можно в настройках.")
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
            Button(row,L.T("ПОНЯТНО"),DismissHint,"stone-button compact-button");
            Button(row,L.T("БЕЗ ПОДСКАЗОК"),()=>{game.Settings.SetTutorialHints(int.MaxValue);game.Settings.Save();DismissHint();},"stone-button compact-button");
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
            Text(card,L.F("ОБУЧЕНИЕ · {0} / {1}", (tutorialStep+1), (count)),"section-label");BoldText(card,TutorialTitles[tutorialStep],"title");
            var art=Box(card,"tutorial-art");Icon(art,PixelArt.Pickaxe(tutorialStep==0?3:0),"tutorial-icon");
            Text(card,TutorialBody(tutorialStep),"tutorial-body");var row=Box(card,"tutorial-actions");
            var back=Button(row,L.T("НАЗАД"),()=>{tutorialStep--;DrawTutorial();},"stone-button tutorial-nav");back.SetEnabled(tutorialStep>0);back.name="tutorial-back";
            Button(row,tutorialStep==count-1?L.T("ИГРАТЬ"):L.T("ДАЛЕЕ"),()=>{if(tutorialStep==count-1)EndTutorial();else{tutorialStep++;DrawTutorial();}},"stone-button primary tutorial-nav").name="tutorial-next";
            Button(card,L.T("ПРОПУСТИТЬ"),()=>{game.Settings.SetTutorialHints(int.MaxValue);EndTutorial();},"stone-button tutorial-skip").name="tutorial-skip";
        }
        void EndTutorial()
        {
            if(!tutorialOpen)return;if(tutorialLibrary)game.Settings.SetTutorialHints(int.MaxValue);tutorialOpen=false;Display(tutorialOverlay,false);game.Settings.SetTutorialSeen(true);game.Settings.Save();
            if(game.CanTrade)CurrentTab=tutorialReturnTab;
            if(tutorialResume&&game.Phase==RunPhase.Mining&&game.Paused)game.TogglePause();tutorialResume=false;Refresh();
        }
        public void ProgressReset(){DismissHint();game.Settings.SetTutorialHints(0);tutorialChecked=false;CloseModal();Toast(L.T("ПРОГРЕСС СБРОШЕН"));}
    }
}
