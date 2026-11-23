using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public enum HubTab { Mine, Inventory, Shop }

    public sealed partial class GameHud : IDisposable
    {
        static readonly BlockKind[] MilestoneOres = { BlockKind.Coal, BlockKind.Copper, BlockKind.Iron, BlockKind.Gold, BlockKind.Diamond };
        readonly GameController game;
        readonly VisualElement root, safe, header, window, mineView, inventoryView, shopView, controls, sceneInfo, tabs, resultArea, roulette, pauseOverlay, modal, playfield;
        readonly Label depth, wallet, best, title, description, heroName, heroDetail, status, toast, result;
        readonly Button action, pause, sound, hudToggle;
        bool hudHidden;
        readonly Button[] tabButtons = new Button[3];
        readonly VisualElement[] slots;
        readonly ToolStatus[] toolStatuses = new ToolStatus[5];
        readonly ResourceGrid runGrid;
        readonly Font bold;
        VisualElement heroIcon;
        float toastUntil;
        Rect previousSafeArea;
        int lastWidth, lastHeight, shownTier;
        public HubTab CurrentTab { get; private set; }
        public Rect GameplayViewport
        {
            get
            {
                if(hudHidden)
                {
                    Rect screen=root.worldBound,field=playfield.worldBound;
                    if(FiniteBounds(screen)&&FiniteBounds(field))return new Rect((field.xMin-screen.xMin)/screen.width,1-(field.yMax-screen.yMin)/screen.height,field.width/screen.width,field.height/screen.height);
                }
                Rect all = root.worldBound, top = sceneInfo.worldBound, bottom = controls.worldBound;
                if (!FiniteBounds(all) || !FiniteBounds(top) || !FiniteBounds(bottom) || bottom.yMin <= top.yMax) return new Rect(0.04f, 0.24f, 0.92f, 0.53f);
                return new Rect(top.xMin / all.width, 1 - bottom.yMin / all.height, top.width / all.width,
                    Mathf.Max(0.2f, (bottom.yMin - top.yMax) / all.height));
            }
        }

        static bool FiniteBounds(Rect r)=>r.width>0&&r.height>0&&FiniteNumber(r.x)&&FiniteNumber(r.y)&&FiniteNumber(r.width)&&FiniteNumber(r.height);
        static bool FiniteNumber(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);

        public GameHud(GameController owner, VisualElement document, StyleSheet style)
        {
            game = owner;startingRecords=(int[])owner.Economy.Profile.mapRecords.Clone(); root = document; root.styleSheets.Add(style); root.AddToClassList("screen"); root.pickingMode = PickingMode.Ignore;
            var regular = Resources.Load<Font>("Fonts/Monocraft"); bold = Resources.Load<Font>("Fonts/Monocraft-Bold");
            if (regular) root.style.unityFontDefinition = FontDefinition.FromFont(regular);
            safe = Box(root, "safe"); safe.pickingMode = PickingMode.Ignore;
            header = Box(safe, "header"); var brand = Box(header, "brand-row");
            BoldText(brand, "PickaxeFall", "brand");
            var buttons = Box(brand, "header-buttons");
            profileButton=Button(buttons,L.T("УР. 1"),OpenProfile,"stone-button small-button profile-button");profileButton.name="open-profile";
            sound = Button(buttons, L.T("НАСТРОЙКИ"), OpenSettings, "stone-button small-button settings-button"); sound.name = "open-settings";
            pause = Button(buttons, "", game.TogglePause, "stone-button small-button"); pause.tooltip = L.T("Пауза");pause.name="open-pause";pause.AddToClassList("pause-button");
            var pauseGlyph = Box(pause, "pause-glyph"); pauseGlyph.pickingMode = PickingMode.Ignore;
            Box(pauseGlyph, "pause-bar").pickingMode = PickingMode.Ignore; Box(pauseGlyph, "pause-bar").pickingMode = PickingMode.Ignore;
#if PICKAXEFALL_DEVTOOLS
            BuildDeveloperShortcut(buttons);
#endif
            var stats = Box(header, "stats-row");
            depth = Stat(stats, L.T("ГЛУБИНА"), L.T("0 м")); wallet = Stat(stats, L.T("МОНЕТЫ"), "0"); best = Stat(stats, L.T("РЕКОРД"), L.T("0 м"));
            sceneInfo = Box(safe, "scene-info"); Text(sceneInfo, L.T("БЕЗДОННАЯ ШАХТА"), "micro light").name="map-heading"; status = Text(sceneInfo, L.T("ПОВЕРХНОСТЬ"), "micro light");
            window = Box(safe, "hub-window");
            mineView = Box(window, "inventory-panel mine-panel"); mineView.name = "mine-page";
            title = BoldText(mineView, L.T("Спуск в шахту"), "title");
            description = Text(mineView, L.T("Каждый спуск — новая шахта. Добычу можно продать в рюкзаке."), "description");
            roulette = Box(mineView, "roulette"); slots = new VisualElement[game.Balance.pickaxes.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = Box(roulette, "tool-slot inset"); Icon(slots[i], PixelArt.Pickaxe(i), "slot-icon");
                BoldText(slots[i], $"{Progression.Number(game.Balance.RollChance(i) * 100)}%", "slot-odds");
            }
            var hero = Box(mineView, "hero"); heroIcon = Icon(hero, PixelArt.Pickaxe(0), "hero-icon inset");
            var info = Box(hero, "hero-info"); heroName = BoldText(info, L.T("Пять видов кирок"), "hero-name"); heroDetail = Text(info, "", "hero-detail");
            resultArea = Box(mineView, "result-area"); result = Text(resultArea, "", "result");
            runGrid = new ResourceGrid(resultArea, "run-resources", true);BuildPlatformControls();
            action = Button(mineView, L.T("КРУТИТЬ РУЛЕТКУ"), MainAction, "stone-button primary"); action.name = "main-action";action.AddToClassList("reliable-button");
            reliablePresses.Add(new ReliablePress(action,()=>{game.Audio?.Ui(UiSound.Tap);MainAction();},false));
            Button(mineView,L.T("КАРТЫ"),()=>OpenMaps(0),"stone-button secondary").name="open-maps";
            Text(mineView, L.T("Приёмы — в магазине. Верстак чинит, улучшающий повышает тип."), "footnote");
            inventoryView = Page(window, "inventory-page"); BuildInventory(inventoryView);
            shopView = Page(window, "shop-page"); BuildShop(shopView);
            playfield = Box(safe, "playfield"); playfield.pickingMode = PickingMode.Ignore;
            hudToggle=Button(playfield,"",ToggleHud,"stone-button hud-toggle");hudToggle.name="toggle-hud";
            var eye=Box(hudToggle,"hud-eye");eye.pickingMode=PickingMode.Ignore;
            Box(eye,"hud-eye-pupil").pickingMode=PickingMode.Ignore;
            Box(hudToggle,"hud-eye-slash").pickingMode=PickingMode.Ignore;
            // A UI tap must never begin a mining gesture behind the button.
            hudToggle.RegisterCallback<PointerDownEvent>(e=>e.StopPropagation());
            hudToggle.RegisterCallback<PointerUpEvent>(e=>e.StopPropagation());
            controls = Box(safe, "mining-panel"); var toolRow = Box(controls, "tool-status-row");
            for (int i = 0; i < 5; i++) toolStatuses[i] = new ToolStatus(this, toolRow, i);

            tabs = Box(safe, "tabs"); string[] captions = { L.T("ШАХТА"), L.T("РЮКЗАК"), L.T("МАГАЗИН") };
            for (int i = 0; i < 3; i++)
            {
                var tab = (HubTab)i; tabButtons[i] = Button(tabs, captions[i], () => {if(CurrentTab!=tab){game.Audio?.Ui(UiSound.Navigate);game.SelectHubTab(tab);}}, "stone-button tab",UiSound.None);
                tabButtons[i].name = "tab-" + tab.ToString().ToLowerInvariant();
            }
            toast = BoldText(root, "", "toast"); toast.pickingMode = PickingMode.Ignore; Display(toast, false);
            pauseOverlay = Box(root, "pause-overlay"); var pausePanel = Box(pauseOverlay, "modal-card");
            BoldText(pausePanel, L.T("Пауза"), "title"); Text(pausePanel, L.T("Добыча и улучшения сохраняются."), "description");
            Button(pausePanel, L.T("ПРОДОЛЖИТЬ"), game.TogglePause, "stone-button primary");
            Button(pausePanel,L.T("НАСТРОЙКИ"),OpenSettings,"stone-button secondary").name="pause-settings";
            modal = Box(root, "modal-overlay"); modal.name = "trade-modal"; Display(modal, false);
            BuildCombat(); game.Changed += Refresh; Refresh();
        }
        public void SelectTab(HubTab tab) { CloseModal(); CurrentTab = tab; Refresh(); }
        public void Tick()
        {
            TickPlatformControls();
#if PICKAXEFALL_DEVTOOLS
            TickDeveloperShortcut();
#endif
            TickSpawner();TickBlockHealth();TickTreasure();TickCombat(); TickPresentation(); TickProfile(); TickTutorial();
            SetText(depth, game.Depth + L.T(" м")); SetText(wallet, Short(game.Wallet)); SetText(best, game.BestDepth + L.T(" м"));
            string nextOre = "";
            foreach (var kind in game.CurrentMap==MapId.Mine?MilestoneOres:System.Array.Empty<BlockKind>())
                if (game.Depth < game.Balance.Block(kind).minimumDepth) { nextOre = GameBalance.BlockName(kind).ToUpperInvariant() + ": " + (game.Balance.Block(kind).minimumDepth - game.Depth) + L.T(" М"); break; }
            SetText(status, nextOre.Length > 0 ? nextOre : L.T("РУБЕЖ: ") + ((game.Depth / 50 + 1) * 50) + L.T(" М"));
            if (game.Phase == RunPhase.Mining || game.Phase == RunPhase.Breaking)
            {
                for (int i = 0; i < 5; i++) toolStatuses[i].Update(game.Pickaxes[i], i < game.RunToolCount);

            }
            if (Time.unscaledTime > toastUntil) Display(toast, false);
            Rect area = Screen.safeArea;
            if (area != previousSafeArea || lastWidth != Screen.width || lastHeight != Screen.height)
            {
                previousSafeArea = area; lastWidth = Screen.width; lastHeight = Screen.height;
                float scale = 1080f / Mathf.Max(1, Screen.width);
                safe.style.paddingTop = 20 + (Screen.height - area.yMax) * scale;
                safe.style.paddingBottom = 16 + area.y * scale;
                safe.style.paddingLeft = 24 + area.xMin * scale; safe.style.paddingRight = 24 + (Screen.width - area.xMax) * scale;
            }
        }
        void Refresh()
        {
            if (!game.Pickaxe) return;
            root.Q<Label>("map-heading").text= L.T(WorldCatalog.Name(game.CurrentMap).ToUpperInvariant());
            title.text=L.T("Спуск · ")+WorldCatalog.Name(game.CurrentMap);
            if(game.Phase is RunPhase.Ready or RunPhase.Equipped)startingRecords[(int)game.CurrentMap]=game.BestDepth;
            if(presentedPhase!=game.Phase)CancelUiPresses();
            bool newResults=game.Phase==RunPhase.Finished && presentedPhase!=RunPhase.Finished;
            presentedPhase=game.Phase;
            if(newResults)StartResults();
            if(game.Phase!=RunPhase.Finished){mineView.style.translate=new Translate(0,0,0);mineView.style.opacity=1;}
            bool active = game.Phase == RunPhase.Mining || game.Phase == RunPhase.Breaking;
            if(!active)hudHidden=false;
            ApplyHudVisibility(active);
            Display(window, !active); Display(tabs, !active); Display(playfield, active);
            Display(mineView, CurrentTab == HubTab.Mine); window.EnableInClassList("mine-window", CurrentTab == HubTab.Mine); Display(inventoryView.GetFirstAncestorOfType<ScrollView>(), CurrentTab == HubTab.Inventory); Display(shopView.GetFirstAncestorOfType<ScrollView>(), CurrentTab == HubTab.Shop);
            Display(pause, game.Phase == RunPhase.Mining); Display(pauseOverlay, game.Paused); Display(sound,game.CanTrade);
            sound.text = L.T("НАСТРОЙКИ");
            for (int i = 0; i < 3; i++) { tabButtons[i].EnableInClassList("active", (int)CurrentTab == i); tabButtons[i].SetEnabled(game.CanTrade); }
            action.SetEnabled(game.Phase != RunPhase.Spinning);
            Display(resultArea, game.Phase == RunPhase.Finished); Display(roulette, game.Phase != RunPhase.Finished);
            if (CurrentTab == HubTab.Inventory) RefreshInventory();
            if (CurrentTab == HubTab.Shop) RefreshShop();
            switch (game.Phase)
            {
                case RunPhase.Ready:
                    title.text = L.T("Спуск · ")+WorldCatalog.Name(game.CurrentMap); description.text = L.T("Крути рулетку, добывай и улучшай снаряжение.");
                    heroName.text = L.T("Пять видов кирок");
                    var preview = new RunUpgrades(game.Economy.Profile,game.Economy.Config);
                    heroDetail.text = L.T(L.F("Отряд: {0}% · {1} кирки при удаче", (Progression.Number(preview.Chance(UpgradeKind.TeamChance) * 100)), (preview.TeamSize)));
                    action.text = L.T("КРУТИТЬ РУЛЕТКУ");
                    foreach (var slot in slots) slot.RemoveFromClassList("selected"); break;
                case RunPhase.Spinning:
                    title.text = L.T("Выбираем кирку"); description.text = L.T("Вероятности каждого типа указаны под кирками."); action.text = L.T("ВРАЩАЕТСЯ…"); break;
                case RunPhase.Equipped:
                    title.text = L.T("Готов к спуску?"); description.text = L.T("Кирка готова к спуску.");
                    ShowRouletteTier(game.Pickaxe.Tier); action.text = L.T("НАЧАТЬ СПУСК ↓"); break;
                case RunPhase.Finished:
                    SetText(runBenefits, game.Benefits.Summary);
                    title.text = L.T(L.F("Спуск завершён · {0} м", (game.Depth))); description.text = L.T("Добыча в рюкзаке. Продай лишнее и усиль кирки.");
                    result.text = L.T(L.F("Собрано на {0} монет\nРазрушено блоков: {1}", (Short(game.PotentialSaleValue)), (game.BlocksBroken)));

                    ShowRouletteTier(game.Pickaxe.Tier); action.text = L.T("ЕЩЁ ОДИН ЗАБЕГ"); break;
            }
            Tick();
        }
        void ToggleHud()
        {
            if(game.Phase!=RunPhase.Mining&&game.Phase!=RunPhase.Breaking)return;
            CancelGesture();hudHidden=!hudHidden;ApplyHudVisibility(true);
        }
        void ApplyHudVisibility(bool active)
        {
            root.EnableInClassList("hud-hidden",hudHidden);
            Display(header,!hudHidden);Display(sceneInfo,!hudHidden);Display(controls,active&&!hudHidden);Display(hudToggle,active);
            hudToggle.EnableInClassList("hud-is-hidden",hudHidden);
            hudToggle.tooltip=L.T(hudHidden?"Показать интерфейс":"Скрыть интерфейс");
        }
        public void ShowRouletteTier(int tier)
        {
            shownTier = tier; var stats = game.Balance.pickaxes[tier];
            for (int i = 0; i < slots.Length; i++) slots[i].EnableInClassList("selected", i == tier);
            heroIcon.style.backgroundImage = Background.FromSprite(PixelArt.Pickaxe(tier)); heroName.text = L.T(stats.title);
            int damage = game.Phase == RunPhase.Finished ? game.Pickaxe.DamageHalfUnits : game.PreviewDamageHalfUnits(tier);
            int durability = game.Phase == RunPhase.Finished ? game.Pickaxe.MaxDurability : game.PreviewDurability(tier);
            heroDetail.text = L.T(L.F("Урон {0} · Прочность {1}", (Progression.Number(damage * 0.5)), (durability)));
        }
        public void Toast(string message) { toast.text = L.T(message); toastUntil = Time.unscaledTime + 2.3f; Display(toast, true); }
        public bool CloseModal() { CancelUiPresses();if(tutorialOpen){EndTutorial();return true;} if (modal == null || modal.style.display == DisplayStyle.None) return false; Display(modal, false); modal.Clear(); FinishSettings(); return true; }
        void MainAction() { if(game.PlatformFrozen)return;if (game.Phase == RunPhase.Ready) game.Spin(); else if (game.Phase == RunPhase.Equipped) game.Drop(); else if (game.Phase == RunPhase.Finished) game.RepeatRun(); }
        public void Dispose() { CancelUiPresses(); game.Changed -= Refresh; }
        static void Display(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        static void SetText(Label label, string value) { if (label.text != value) label.text = L.T(value); }
        static VisualElement Box(VisualElement parent, string classes) { var item = new VisualElement(); AddClasses(item, classes); parent.Add(item); return item; }
        static Label Text(VisualElement parent, string text, string classes) { var item = new Label(L.T(text)); AddClasses(item, classes); parent.Add(item); return item; }
        Label BoldText(VisualElement parent, string text, string classes) { var item = Text(parent, text, classes); if (bold) item.style.unityFontDefinition = FontDefinition.FromFont(bold); return item; }
        static VisualElement Icon(VisualElement parent, Sprite sprite, string classes) { var icon = Box(parent, classes); icon.style.backgroundImage = Background.FromSprite(sprite); return icon; }
        Button Button(VisualElement parent, string text, Action click, string classes,UiSound cue=UiSound.Tap)
        {
            var button = new Button(()=>{game.Audio?.Ui(cue);click();}) { text = L.T(text) };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            button.userData = click; // Command binding for isolated development previews.
#endif
            if (bold) button.style.unityFontDefinition = FontDefinition.FromFont(bold); AddClasses(button, classes); parent.Add(button); return button; }
        Label Stat(VisualElement parent, string title, string value) { var slot = Box(parent, "stat inset"); Text(slot, title, "stat-title"); return BoldText(slot, value, "stat-value"); }
        static void AddClasses(VisualElement element, string classes) { foreach (string name in classes.Split(' ')) element.AddToClassList(name); }
        static VisualElement Page(VisualElement parent, string name)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = name + "-scroll" }; scroll.AddToClassList("page-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; parent.Add(scroll);
            var body = Box(scroll.contentContainer, "inventory-panel"); body.name = name; return body;
        }
        public static string Short(long n) => n >= 1000000000 ? Progression.Number(n / 1000000000d) + L.T(" млрд") : n >= 1000000 ? Progression.Number(n / 1000000d) + L.T(" млн") : n >= 10000 ? Progression.Number(n / 1000d) + L.T(" тыс") : n.ToString();
        static string Exact(long n) => n.ToString("N0", L.Culture);

        sealed class ToolStatus
        {
            readonly GameHud owner;
            readonly VisualElement box, icon, fill;
            readonly Label value;
            int tier = -1, hp = -1, max = -1;
            public ToolStatus(GameHud hud, VisualElement parent, int index)
            {
                box = Box(parent, "tool-status"); box.name = "tool-select-" + index;
                box.RegisterCallback<ClickEvent>(e => { if (hud.game.ControlsAvailable) hud.game.SelectTool(index); e.StopPropagation(); });
                owner = hud;
                icon = Icon(box, PixelArt.Pickaxe(0), "tool-status-icon");
                value = hud.BoldText(box, "", "tool-health"); value.name = "health-" + index;
                fill = Box(Box(box, "health-track"), "health-fill");
            }
            public void Update(FallingPickaxe tool, bool visible)
            {
                Display(box, visible); if (!visible) return;
                box.EnableInClassList("tool-selected", tool.IsAlive && owner.game.LivingToolCount>1 && owner.game.SelectedToolIndex == tool.ToolIndex);
                if (tier != tool.Tier) { tier = tool.Tier; icon.style.backgroundImage = Background.FromSprite(PixelArt.Pickaxe(tool.Tier)); }
                int current = tool.IsAlive ? tool.Durability : 0;
                if (hp == current && max == tool.MaxDurability) return;
                hp = current; max = tool.MaxDurability; value.text = L.T(hp + "/" + max);
                fill.style.width = Length.Percent(100f * hp / max); box.style.opacity = hp > 0 ? 1 : 0.4f;
                fill.style.backgroundColor = (Color)(hp > max / 4 ? new Color32(107, 162, 52, 255) : new Color32(193, 74, 43, 255));
            }
        }
        sealed class ResourceGrid
        {
            readonly Label[] counts = new Label[ResourceInventory.SlotCount]; readonly long[] previous = new long[ResourceInventory.SlotCount];
            public ResourceGrid(VisualElement parent, string name, bool compact)
            {
                var grid = Box(parent, compact ? "resource-grid compact" : "resource-grid"); grid.name = name;
                foreach(var kind in ResourceInventory.Materials)
                {
                    int i=(int)kind;previous[i] = -1; var slot = Box(grid, "resource-slot inset");
                    Icon(slot, PixelArt.ResourceIcon(kind), "resource-icon");
                    counts[i] = Text(slot, "0", "stack-count"); counts[i].name = "count-" + kind;
                    if (!compact) Text(slot, kind == BlockKind.Deepstone ? L.T("Сланец") : GameBalance.BlockName(kind), "resource-name");
                }
            }
            public void Update(Func<BlockKind, long> quantity,Func<BlockKind,bool> visible=null)
            {
                foreach(var kind in ResourceInventory.Materials){int i=(int)kind;long n=quantity(kind);Display(counts[i].parent,visible!=null?visible(kind):n>0);if(n==previous[i])continue;previous[i]=n;counts[i].text= L.T(Short(n));}
            }
        }
    }
}
