using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        bool tutorialOpen, tutorialChecked, tutorialResume;
        int tutorialStep;
        HubTab tutorialReturnTab;
        VisualElement tutorialOverlay, tutorialFocus;
        static readonly string[] TutorialTitles={"Добро пожаловать!","Испытай удачу","Кирка и шахта","Управляй падением","Энергия и отряд","Находки под землёй","Продай добычу","Усиль снаряжение","Удача и приёмы","Играй в своём темпе"};
        static readonly string[] TutorialBodies={
            "Крути рулетку, отправляй кирку в новую шахту и собирай ресурсы. После каждого забега продавай лишнее и улучшай снаряжение. Можно пропустить обучение и вернуться к нему в настройках.",
            "Проценты под пятью кирками — реальные шансы выпадения. Золотая встречается чаще железной, но железная сильнее и прочнее. Выпавшую кирку отправляет вниз кнопка «В шахту».",
            "Кирка сама падает, вращается и отскакивает. Сильное касание наносит урон, на блоке растут трещины. Каждый удар тратит прочность; лежание блоки не добывает. Когда сломается последняя кирка, забег завершится.",
            "Свайп влево, вправо, вверх или вниз — рывок за 40 энергии: первый материал пробивается сразу. Короткое нажатие на шахту — пробой двух блоков вниз за 50 энергии. Пробитие тоже расходует прочность.",
            "Энергия общая: в начале 100, обычные результативные удары выбранной кирки возвращают по 2. Если выпал отряд, выбирай кирку её значком внизу. Жесты действуют на выбранную; она отмечена тонким жёлтым контуром. Одна кирка не нуждается в обводке.",
            "Чем глубже, тем прочнее порода и ценнее руда. Верстак полностью чинит ударившую кирку и повышает её тип. TNT взрывается от удара, разрушает соседние блоки и тоже приносит добычу. Верстаки защищены от взрывов. Двойной верстак повышает тип на две ступени и заполняет энергию.",
            "Земля, камень и все руды остаются в рюкзаке. Монеты появляются только после продажи. Нажми на материал: можно ввести количество или продать весь вид. Кнопка внизу продаёт весь рюкзак с подтверждением. Оставляй материалы для улучшений!",
            "Урон и прочность покупаются отдельно для каждого вида кирки. Нужны и монеты, и материалы. Зелёные числа означают, что хватает, красные — чего недостаёт. Зелёная кнопка разрешает покупку. Каждая следующая ступень дороже предыдущей.",
            "Общие навыки дают больше руды, отряд и дополнительные кирки, TNT из добытой руды, двойной верстак, усиленный взрыв, экономию прочности и спасение последней кирки. Мощный удар усиливает сильные столкновения, осколочный повреждает соседей. Последний шанс срабатывает только для последней кирки и один раз за забег. Приёмы улучшают энергию и пробитие. Размер отряда работает после открытия его шанса. Новые карты и типы кирок пока в разработке.",
            "Пауза останавливает спуск. В настройках есть звук, частота кадров до 240 и по экрану, уменьшение эффектов, подсказки добычи и повтор обучения. Прогресс сохраняется автоматически; сброс требует отдельного подтверждения. После забега дождись подсчёта добычи и возвращайся за новым рекордом!"};
        void TickTutorial()
        {
            if(tutorialChecked || !game.ReadyToPresent || game.TestMode.Length>0)return;
            tutorialChecked=true;if(!game.Settings.TutorialSeen)BeginTutorial();
        }
        void BeginTutorial()
        {
            if(tutorialOpen)return;
            bool resume=settingsOpen && resumeAfterSettings;resumeAfterSettings=false;CloseModal();
            tutorialResume=resume || game.Phase==RunPhase.Mining && !game.Paused;
            if(game.Phase==RunPhase.Mining && !game.Paused)game.TogglePause();
            CancelGesture();tutorialReturnTab=CurrentTab;tutorialStep=0;tutorialOpen=true;tutorialChecked=true;
            if(tutorialOverlay==null){tutorialOverlay=Box(root,"tutorial-overlay");tutorialOverlay.name="tutorial-overlay";}
            DrawTutorial();
        }
        void DrawTutorial()
        {
            tutorialFocus?.RemoveFromClassList("tutorial-focus");tutorialFocus=null;
            if(game.CanTrade)
            {
                CurrentTab=tutorialStep==6?HubTab.Inventory:tutorialStep==7||tutorialStep==8?HubTab.Shop:HubTab.Mine;Refresh();
                tutorialFocus=root.Q(tutorialStep==6?"tab-inventory":tutorialStep==7||tutorialStep==8?"tab-shop":tutorialStep==1?"main-action":"open-settings");
                tutorialFocus?.AddToClassList("tutorial-focus");
            }
            tutorialOverlay.Clear();Display(tutorialOverlay,true);tutorialOverlay.BringToFront();
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("tutorial-scroll");tutorialOverlay.Add(scroll);
            var card=Box(scroll.contentContainer,"modal-card tutorial-card");
            Text(card,$"ОБУЧЕНИЕ · {tutorialStep+1} / {TutorialTitles.Length}","section-label");
            BoldText(card,TutorialTitles[tutorialStep],"title");
            var art=Box(card,"tutorial-art");
            if(tutorialStep==5)
            {
                Icon(art,PixelArt.Block(BlockKind.Coal,0),"tutorial-icon");Icon(art,PixelArt.Block(BlockKind.Workbench,0),"tutorial-icon");Icon(art,PixelArt.Block(BlockKind.Tnt,0),"tutorial-icon");
            }
            else if(tutorialStep==6||tutorialStep==7){Icon(art,PixelArt.Block(BlockKind.Stone,0),"tutorial-icon");Icon(art,PixelArt.Block(BlockKind.Copper,0),"tutorial-icon");}
            else Icon(art,PixelArt.Pickaxe(tutorialStep==1?3:0),"tutorial-icon");
            if(tutorialStep==3)BoldText(art,"← ↑ ↓ →","tutorial-arrows");
            Text(card,TutorialBodies[tutorialStep],"tutorial-body");
            var buttons=Box(card,"tutorial-actions");
            var back=Button(buttons,"НАЗАД",()=>{tutorialStep--;DrawTutorial();},"stone-button tutorial-nav");back.SetEnabled(tutorialStep>0);back.name="tutorial-back";
            Button(buttons,tutorialStep==TutorialTitles.Length-1?"ИГРАТЬ":"ДАЛЕЕ",()=>{if(tutorialStep==TutorialTitles.Length-1)EndTutorial();else{tutorialStep++;DrawTutorial();}},"stone-button primary tutorial-nav").name="tutorial-next";
            Button(card,"ПРОПУСТИТЬ ОБУЧЕНИЕ",EndTutorial,"stone-button tutorial-skip").name="tutorial-skip";
        }
        void EndTutorial()
        {
            if(!tutorialOpen)return;tutorialOpen=false;tutorialFocus?.RemoveFromClassList("tutorial-focus");
            Display(tutorialOverlay,false);game.Settings.SetTutorialSeen(true);game.Settings.Save();
            if(game.CanTrade)CurrentTab=tutorialReturnTab;
            if(tutorialResume && game.Phase==RunPhase.Mining && game.Paused)game.TogglePause();
            tutorialResume=false;Refresh();
        }
        public void ProgressReset(){tutorialChecked=false;CloseModal();Toast("ПРОГРЕСС СБРОШЕН");}
    }
}
