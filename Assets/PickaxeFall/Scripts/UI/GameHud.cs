using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public enum HubTab { Mine, Inventory, Shop }

    public sealed partial class GameHud : IDisposable
    {
        readonly GameController game;
        readonly VisualElement root, safe, window, mineView, inventoryView, shopView, controls, sceneInfo, tabs, resultArea, roulette, pauseOverlay, modal, playfield;
        readonly Label depth, wallet, best, title, description, heroName, heroDetail, status, toast, result;
        readonly Button action, pause, sound;
        readonly Button[] tabButtons = new Button[3];
        readonly VisualElement[] slots;
        readonly ToolStatus[] toolStatuses = new ToolStatus[5];
        readonly ResourceGrid runGrid, miningGrid;
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
                Rect all = root.worldBound, top = sceneInfo.worldBound, bottom = controls.worldBound;
                if (all.height <= 0 || bottom.yMin <= top.yMax) return new Rect(0.04f, 0.24f, 0.92f, 0.53f);
                return new Rect(top.xMin / all.width, 1 - bottom.yMin / all.height, top.width / all.width,
                    Mathf.Max(0.2f, (bottom.yMin - top.yMax) / all.height));
            }
        }

        public GameHud(GameController owner, VisualElement document, StyleSheet style)
        {
            game = owner; root = document; root.styleSheets.Add(style); root.AddToClassList("screen"); root.pickingMode = PickingMode.Ignore;
            var regular = Resources.Load<Font>("Fonts/Monocraft"); bold = Resources.Load<Font>("Fonts/Monocraft-Bold");
            if (regular) root.style.unityFontDefinition = FontDefinition.FromFont(regular);
            safe = Box(root, "safe"); safe.pickingMode = PickingMode.Ignore;
            var header = Box(safe, "header"); var brand = Box(header, "brand-row");
            BoldText(brand, "PICKAXE FALL", "brand");
            var buttons = Box(brand, "header-buttons");
            sound = Button(buttons, "НАСТРОЙКИ", OpenSettings, "stone-button small-button settings-button"); sound.name = "open-settings";
            pause = Button(buttons, "", game.TogglePause, "stone-button small-button"); pause.tooltip = "Пауза";
            var pauseGlyph = Box(pause, "pause-glyph"); pauseGlyph.pickingMode = PickingMode.Ignore;
            Box(pauseGlyph, "pause-bar").pickingMode = PickingMode.Ignore; Box(pauseGlyph, "pause-bar").pickingMode = PickingMode.Ignore;
            var stats = Box(header, "stats-row");
            depth = Stat(stats, "ГЛУБИНА", "0 м"); wallet = Stat(stats, "МОНЕТЫ", "0"); best = Stat(stats, "РЕКОРД", "0 м");
            sceneInfo = Box(safe, "scene-info"); Text(sceneInfo, "БЕЗДОННАЯ ШАХТА", "micro light"); status = Text(sceneInfo, "ПОВЕРХНОСТЬ", "micro light");
            window = Box(safe, "hub-window");
            mineView = Box(window, "inventory-panel mine-panel"); mineView.name = "mine-page";
            title = BoldText(mineView, "Спуск в шахту", "title");
            description = Text(mineView, "Каждый спуск — новая шахта. Добычу можно продать в рюкзаке.", "description");
            roulette = Box(mineView, "roulette"); slots = new VisualElement[game.Balance.pickaxes.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = Box(roulette, "tool-slot inset"); Icon(slots[i], PixelArt.Pickaxe(game.Balance.pickaxes[i].color), "slot-icon");
                BoldText(slots[i], $"{Progression.Number(game.Balance.RollChance(i) * 100)}%", "slot-odds");
            }
            var hero = Box(mineView, "hero"); heroIcon = Icon(hero, PixelArt.Pickaxe(game.Balance.pickaxes[0].color), "hero-icon inset");
            var info = Box(hero, "hero-info"); heroName = BoldText(info, "Пять видов кирок", "hero-name"); heroDetail = Text(info, "", "hero-detail");
            resultArea = Box(mineView, "result-area"); result = Text(resultArea, "", "result");
            runGrid = new ResourceGrid(resultArea, "run-resources", true);
            action = Button(mineView, "КРУТИТЬ РУЛЕТКУ", MainAction, "stone-button primary"); action.name = "main-action";
            Text(mineView, "Верстак — улучшение и ремонт. TNT — взрыв от удара.", "footnote");
            inventoryView = Page(window, "inventory-page"); BuildInventory(inventoryView);
            shopView = Page(window, "shop-page"); BuildShop(shopView);
            playfield = Box(safe, "playfield"); playfield.pickingMode = PickingMode.Ignore;
            controls = Box(safe, "mining-panel"); var toolRow = Box(controls, "tool-status-row");
            for (int i = 0; i < 5; i++) toolStatuses[i] = new ToolStatus(this, toolRow, i);
            miningGrid = new ResourceGrid(controls, "mining-resources", true);
            tabs = Box(safe, "tabs"); string[] captions = { "ШАХТА", "РЮКЗАК", "МАГАЗИН" };
            for (int i = 0; i < 3; i++)
            {
                var tab = (HubTab)i; tabButtons[i] = Button(tabs, captions[i], () => game.SelectHubTab(tab), "stone-button tab");
                tabButtons[i].name = "tab-" + tab.ToString().ToLowerInvariant();
            }
            toast = BoldText(root, "", "toast"); toast.pickingMode = PickingMode.Ignore; Display(toast, false);
            pauseOverlay = Box(root, "pause-overlay"); var pausePanel = Box(pauseOverlay, "modal-card");
            BoldText(pausePanel, "Пауза", "title"); Text(pausePanel, "Добыча и улучшения сохраняются.", "description");
            Button(pausePanel, "ПРОДОЛЖИТЬ", game.TogglePause, "stone-button primary");
            modal = Box(root, "modal-overlay"); modal.name = "trade-modal"; Display(modal, false);
            game.Changed += Refresh; Refresh();
        }
        public void SelectTab(HubTab tab) { CloseModal(); CurrentTab = tab; Refresh(); }
        public void Tick()
        {
            SetText(depth, game.Depth + " м"); SetText(wallet, Short(game.Wallet)); SetText(best, game.BestDepth + " м");
            string nextOre = "";
            foreach (var kind in new[] { BlockKind.Copper, BlockKind.Iron, BlockKind.Gold, BlockKind.Diamond })
                if (game.Depth < game.Balance.Block(kind).minimumDepth) { nextOre = GameBalance.BlockName(kind).ToUpperInvariant() + ": " + (game.Balance.Block(kind).minimumDepth - game.Depth) + " М"; break; }
            SetText(status, nextOre.Length > 0 ? nextOre : "РУБЕЖ: " + ((game.Depth / 50 + 1) * 50) + " М");
            if (game.Phase == RunPhase.Mining || game.Phase == RunPhase.Breaking)
            {
                for (int i = 0; i < 5; i++) toolStatuses[i].Update(game.Pickaxes[i], i < game.RunToolCount);
                miningGrid.Update(kind => game.OreCounts[(int)kind]);
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
            bool active = game.Phase == RunPhase.Mining || game.Phase == RunPhase.Breaking;
            Display(window, !active); Display(tabs, !active); Display(controls, active); Display(playfield, active);
            Display(mineView, CurrentTab == HubTab.Mine); window.EnableInClassList("mine-window", CurrentTab == HubTab.Mine); Display(inventoryView.GetFirstAncestorOfType<ScrollView>(), CurrentTab == HubTab.Inventory); Display(shopView.GetFirstAncestorOfType<ScrollView>(), CurrentTab == HubTab.Shop);
            Display(pause, game.Phase == RunPhase.Mining); Display(pauseOverlay, game.Paused);
            sound.text = "НАСТРОЙКИ";
            for (int i = 0; i < 3; i++) { tabButtons[i].EnableInClassList("active", (int)CurrentTab == i); tabButtons[i].SetEnabled(game.CanTrade); }
            action.SetEnabled(game.Phase != RunPhase.Spinning);
            Display(resultArea, game.Phase == RunPhase.Finished); Display(roulette, game.Phase != RunPhase.Finished);
            if (CurrentTab == HubTab.Inventory) RefreshInventory();
            if (CurrentTab == HubTab.Shop) RefreshShop();
            switch (game.Phase)
            {
                case RunPhase.Ready:
                    title.text = "Спуск в шахту"; description.text = "Крути рулетку, добывай и улучшай снаряжение.";
                    heroName.text = "Пять видов кирок";
                    var preview = new RunUpgrades(game.Economy.Profile);
                    heroDetail.text = $"Отряд: {Progression.Number(preview.Chance(UpgradeKind.TeamChance) * 100)}% · {preview.TeamSize} кирки при удаче";
                    action.text = "КРУТИТЬ РУЛЕТКУ";
                    foreach (var slot in slots) slot.RemoveFromClassList("selected"); break;
                case RunPhase.Spinning:
                    title.text = "Испытываем удачу"; description.text = "Вероятности каждого типа указаны под кирками."; action.text = "ВРАЩАЕТСЯ…"; break;
                case RunPhase.Equipped:
                    title.text = "Готов к спуску?"; description.text = "Кирка готова. Самое время для нового рекорда.";
                    ShowRouletteTier(game.Pickaxe.Tier); action.text = "В ШАХТУ ↓"; break;
                case RunPhase.Finished:
                    title.text = $"Экспедиция: {game.Depth} м"; description.text = "Добыча в рюкзаке. Продай лишнее и усиль кирки.";
                    result.text = $"Собрано на {Short(game.PotentialSaleValue)} монет\nРазрушено блоков: {game.BlocksBroken}";
                    runGrid.Update(kind => game.OreCounts[(int)kind]);
                    ShowRouletteTier(game.Pickaxe.Tier); action.text = "ЕЩЁ ОДИН ЗАБЕГ"; break;
            }
            Tick();
        }
        public void ShowRouletteTier(int tier)
        {
            shownTier = tier; var stats = game.Balance.pickaxes[tier];
            for (int i = 0; i < slots.Length; i++) slots[i].EnableInClassList("selected", i == tier);
            heroIcon.style.backgroundImage = Background.FromSprite(PixelArt.Pickaxe(stats.color)); heroName.text = stats.title;
            int damage = game.Phase == RunPhase.Finished ? game.Pickaxe.DamageHalfUnits : game.PreviewDamageHalfUnits(tier);
            int durability = game.Phase == RunPhase.Finished ? game.Pickaxe.MaxDurability : game.PreviewDurability(tier);
            heroDetail.text = $"Урон {Progression.Number(damage * 0.5)} · Прочность {durability}";
        }
        public void Toast(string message) { toast.text = message; toastUntil = Time.unscaledTime + 2.3f; Display(toast, true); }
        public bool CloseModal() { if (modal == null || modal.style.display == DisplayStyle.None) return false; Display(modal, false); modal.Clear(); FinishSettings(); return true; }
        void MainAction() { if (game.Phase == RunPhase.Ready) game.Spin(); else if (game.Phase == RunPhase.Equipped) game.Drop(); else if (game.Phase == RunPhase.Finished) game.NewRun(); }
        public void Dispose() => game.Changed -= Refresh;
        static void Display(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        static void SetText(Label label, string value) { if (label.text != value) label.text = value; }
        static VisualElement Box(VisualElement parent, string classes) { var item = new VisualElement(); AddClasses(item, classes); parent.Add(item); return item; }
        static Label Text(VisualElement parent, string text, string classes) { var item = new Label(text); AddClasses(item, classes); parent.Add(item); return item; }
        Label BoldText(VisualElement parent, string text, string classes) { var item = Text(parent, text, classes); if (bold) item.style.unityFontDefinition = FontDefinition.FromFont(bold); return item; }
        static VisualElement Icon(VisualElement parent, Sprite sprite, string classes) { var icon = Box(parent, classes); icon.style.backgroundImage = Background.FromSprite(sprite); return icon; }
        Button Button(VisualElement parent, string text, Action click, string classes)
        {
            var button = new Button(click) { text = text };
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
        public static string Short(long n) => n >= 1000000000 ? Progression.Number(n / 1000000000d) + " млрд" : n >= 1000000 ? Progression.Number(n / 1000000d) + " млн" : n >= 10000 ? Progression.Number(n / 1000d) + " тыс" : n.ToString();
        static string Exact(long n) => n.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));

        sealed class ToolStatus
        {
            readonly VisualElement box, icon, fill;
            readonly Label value;
            int tier = -1, hp = -1, max = -1;
            public ToolStatus(GameHud hud, VisualElement parent, int index)
            {
                box = Box(parent, "tool-status"); icon = Icon(box, PixelArt.Pickaxe(hud.game.Balance.pickaxes[0].color), "tool-status-icon");
                value = hud.BoldText(box, "", "tool-health"); value.name = "health-" + index;
                fill = Box(Box(box, "health-track"), "health-fill");
            }
            public void Update(FallingPickaxe tool, bool visible)
            {
                Display(box, visible); if (!visible) return;
                if (tier != tool.Tier) { tier = tool.Tier; icon.style.backgroundImage = Background.FromSprite(PixelArt.Pickaxe(tool.Stats.color)); }
                int current = tool.IsAlive ? tool.Durability : 0;
                if (hp == current && max == tool.MaxDurability) return;
                hp = current; max = tool.MaxDurability; value.text = hp + "/" + max;
                fill.style.width = Length.Percent(100f * hp / max); box.style.opacity = hp > 0 ? 1 : 0.4f;
                fill.style.backgroundColor = (Color)(hp > max / 4 ? new Color32(107, 162, 52, 255) : new Color32(193, 74, 43, 255));
            }
        }
        sealed class ResourceGrid
        {
            readonly Label[] counts = new Label[8]; readonly long[] previous = new long[8];
            public ResourceGrid(VisualElement parent, string name, bool compact)
            {
                var grid = Box(parent, compact ? "resource-grid compact" : "resource-grid"); grid.name = name;
                for (int i = 0; i < 8; i++)
                {
                    var kind = (BlockKind)i; previous[i] = -1; var slot = Box(grid, "resource-slot inset");
                    Icon(slot, PixelArt.Block(kind, 0, kind == BlockKind.Deepstone), "resource-icon");
                    counts[i] = Text(slot, "0", "stack-count"); counts[i].name = "count-" + kind;
                    if (!compact) Text(slot, kind == BlockKind.Deepstone ? "Сланец" : GameBalance.BlockName(kind), "resource-name");
                }
            }
            public void Update(Func<BlockKind, long> quantity)
            {
                for (int i = 0; i < 8; i++) { long n = quantity((BlockKind)i); if (n == previous[i]) continue; previous[i] = n; counts[i].text = Short(n); }
            }
        }
    }
}
