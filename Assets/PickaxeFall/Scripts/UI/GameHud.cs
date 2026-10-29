using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public enum HubTab { Mine, Inventory, Shop }

    public sealed class GameHud : IDisposable
    {
        readonly GameController game;
        readonly VisualElement root, safe, window, mineView, inventoryView, shopView, controls,
            healthFill, heroIcon, roulette, pauseOverlay, tabs, resultArea;
        readonly Label depth, wallet, best, title, description, eyebrow, heroName, heroDetail,
            status, toast, healthText, result, inventoryTotal;
        readonly Button action, pause, sound;
        readonly Button[] tabButtons;
        readonly VisualElement[] slots;
        readonly ResourceGrid runGrid, inventoryGrid, miningGrid;
        float toastUntil;
        Rect previousSafeArea;
        int lastWidth, lastHeight;
        public HubTab CurrentTab { get; private set; }

        public GameHud(GameController owner, VisualElement document, StyleSheet style)
        {
            game = owner; root = document; root.styleSheets.Add(style); root.AddToClassList("screen");
            root.pickingMode = PickingMode.Ignore;
            var font = Resources.Load<Font>("Fonts/Monocraft");
            if (font) root.style.unityFontDefinition = FontDefinition.FromFont(font);
            safe = Box(root, "safe"); safe.pickingMode = PickingMode.Ignore;
            var header = Box(safe, "header");
            var brand = Box(header, "brand-row");
            Text(brand, "PICKAXE FALL", "brand");
            var buttons = Box(brand, "header-buttons");
            sound = Button(buttons, "ЗВУК", () => { game.Effects.ToggleSound(); Refresh(); }, "stone-button small-button");
            pause = Button(buttons, "II", game.TogglePause, "stone-button small-button");
            var stats = Box(header, "stats-row");
            depth = Stat(stats, "ГЛУБИНА", "0 м"); wallet = Stat(stats, "МОНЕТЫ", "0"); best = Stat(stats, "РЕКОРД", "0 м");
            var sceneInfo = Box(safe, "scene-info"); Text(sceneInfo, "↓ БЕЗДОННАЯ ШАХТА", "micro light");
            status = Text(sceneInfo, "ПОВЕРХНОСТЬ", "micro light");
            Box(safe, "air").pickingMode = PickingMode.Ignore;
            toast = Text(root, "", "toast"); Display(toast, false);

            window = Box(safe, "inventory-window");
            var panel = Box(window, "inventory-panel");
            mineView = Box(panel, "page"); mineView.name = "mine-page";
            eyebrow = Text(mineView, "РУЛЕТКА", "eyebrow");
            title = Text(mineView, "Снаряжение шахтёра", "title");
            description = Text(mineView, "Выбери кирку и пополни свой рюкзак.", "description");
            roulette = Box(mineView, "roulette"); slots = new VisualElement[game.Balance.pickaxes.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i] = Box(roulette, "tool-slot inset");
                Icon(slot, PixelArt.Pickaxe(game.Balance.pickaxes[i].color), "slot-icon");
                Text(slot, $"{game.Balance.RollChance(i) * 100:0.#}%", "slot-odds");
            }
            var hero = Box(mineView, "hero");
            heroIcon = Icon(hero, PixelArt.Pickaxe(game.Balance.pickaxes[4].color), "hero-icon inset");
            var info = Box(hero, "hero-info");
            heroName = Text(info, "Пять кирок на выбор", "hero-name");
            heroDetail = Text(info, "У каждой свои сила и прочность", "hero-detail");
            resultArea = Box(mineView, "result-area");
            result = Text(resultArea, "", "result");
            Text(resultArea, "ДОБЫЧА ЗА ЗАБЕГ", "section-label");
            runGrid = new ResourceGrid(resultArea, "run-resources", false);
            action = Button(mineView, "КРУТИТЬ РУЛЕТКУ", MainAction, "stone-button primary"); action.name = "main-action";
            Text(mineView, "Каждый забег — новая шахта", "footnote");

            inventoryView = Box(panel, "page"); inventoryView.name = "inventory-page";
            Text(inventoryView, "СОБРАННЫЕ РЕСУРСЫ", "eyebrow"); Text(inventoryView, "Твой рюкзак", "title");
            Text(inventoryView, "Здесь хранится всё, что удалось добыть.", "description");
            var bagHeader = Box(inventoryView, "bag-header");
            Icon(bagHeader, PixelArt.Chest(), "chest-icon");
            inventoryTotal = Text(bagHeader, "Всего предметов: 0", "bag-total");
            inventoryGrid = new ResourceGrid(inventoryView, "inventory-resources", false);
            Text(inventoryView, "Ресурсы сохраняются между забегами", "footnote");
            Button(inventoryView, "ВЕРНУТЬСЯ В ШАХТУ", () => game.SelectHubTab(HubTab.Mine), "stone-button secondary").name = "inventory-back";

            shopView = Box(panel, "page"); shopView.name = "shop-page";
            Text(shopView, "ТОРГОВАЯ ЛАВКА", "eyebrow"); Text(shopView, "Магазин", "title");
            Text(shopView, "Лавочник ещё расставляет товары.", "description");
            var sign = Box(shopView, "shop-sign");
            Icon(sign, PixelArt.Chest(), "shop-chest"); Text(sign, "СКОРО ОТКРОЕМСЯ", "shop-announcement");
            var shelves = Box(shopView, "shop-shelves");
            ShopShelf(shelves, PixelArt.Pickaxe(game.Balance.pickaxes[4].color), "Кирки");
            ShopShelf(shelves, PixelArt.Block(BlockKind.Workbench, 0), "Улучшения");
            ShopShelf(shelves, PixelArt.Block(BlockKind.Dirt, 0, false, true), "Карты");
            Text(shopView, "Заглядывай сюда после обновления", "footnote");
            Button(shopView, "ВЕРНУТЬСЯ В ШАХТУ", () => game.SelectHubTab(HubTab.Mine), "stone-button secondary").name = "shop-back";

            controls = Box(safe, "mining-panel");
            var toolRow = Box(controls, "tool-row");
            Text(toolRow, "ПРОЧНОСТЬ КИРКИ", "micro"); healthText = Text(toolRow, "", "micro");
            healthFill = Box(Box(controls, "health-track inset"), "health-fill");
            miningGrid = new ResourceGrid(controls, "mining-resources", true);
            Text(controls, "ВЕРСТАК: РЕМОНТ  /  TNT: ВЗРЫВ", "footnote");

            tabs = Box(safe, "tabs"); tabButtons = new Button[3];
            string[] captions = { "ШАХТА", "РЮКЗАК", "МАГАЗИН" };
            for (int i = 0; i < 3; i++)
            {
                var tab = (HubTab)i;
                tabButtons[i] = Button(tabs, captions[i], () => game.SelectHubTab(tab), "stone-button tab");
                tabButtons[i].name = "tab-" + tab.ToString().ToLowerInvariant();
            }
            pauseOverlay = Box(root, "pause-overlay");
            var pausePanel = Box(Box(pauseOverlay, "inventory-window"), "inventory-panel");
            Text(pausePanel, "МЕНЮ ИГРЫ", "eyebrow"); Text(pausePanel, "Пауза", "title");
            Text(pausePanel, "Шахта никуда не убежит.", "description");
            Button(pausePanel, "ПРОДОЛЖИТЬ", game.TogglePause, "stone-button primary");
            game.Changed += Refresh; Refresh();
        }

        public void SelectTab(HubTab tab) { CurrentTab = tab; Refresh(); }

        public void Tick()
        {
            depth.text = $"{game.Depth} м";
            wallet.text = (game.Phase == RunPhase.Mining || game.Phase == RunPhase.Breaking ? game.Coins : game.Wallet).ToString();
            best.text = $"{game.BestDepth} м";
            status.text = game.Depth < 3 ? "ПОВЕРХНОСТЬ" : game.Depth < 55 ? "КАМЕННЫЙ СЛОЙ" : "ГЛУБИННЫЙ СЛАНЕЦ";
            healthText.text = $"{game.Pickaxe.Durability} / {game.Pickaxe.MaxDurability}";
            healthFill.style.width = Length.Percent(100f * game.Pickaxe.Durability / game.Pickaxe.MaxDurability);
            healthFill.style.backgroundColor = (Color)(game.Pickaxe.Durability > game.Pickaxe.MaxDurability * 0.25f
                ? new Color32(113, 172, 51, 255) : new Color32(194, 79, 48, 255));
            runGrid.Update(kind => game.OreCounts[(int)kind]);
            miningGrid.Update(kind => game.OreCounts[(int)kind]);
            inventoryGrid.Update(kind => game.Inventory[kind]);
            inventoryTotal.text = $"Всего предметов: {game.Inventory.Total}";
            if (Time.unscaledTime > toastUntil) Display(toast, false);
            Rect area = Screen.safeArea;
            if (area != previousSafeArea || lastWidth != Screen.width || lastHeight != Screen.height)
            {
                previousSafeArea = area; lastWidth = Screen.width; lastHeight = Screen.height;
                float scale = 1080f / Mathf.Max(1, Screen.width);
                safe.style.paddingTop = 30 + (Screen.height - area.yMax) * scale;
                safe.style.paddingBottom = 18 + area.y * scale;
                safe.style.paddingLeft = 36 + area.xMin * scale;
                safe.style.paddingRight = 36 + (Screen.width - area.xMax) * scale;
            }
        }

        void Refresh()
        {
            if (!game.Pickaxe) return;
            bool active = game.Phase == RunPhase.Mining || game.Phase == RunPhase.Breaking;
            Display(window, !active); Display(tabs, !active); Display(controls, active);
            Display(mineView, CurrentTab == HubTab.Mine);
            Display(inventoryView, CurrentTab == HubTab.Inventory);
            Display(shopView, CurrentTab == HubTab.Shop);
            Display(pause, game.Phase == RunPhase.Mining); Display(pauseOverlay, game.Paused);
            sound.text = game.Effects.Muted ? "ТИХО" : "ЗВУК";
            for (int i = 0; i < tabButtons.Length; i++)
            {
                tabButtons[i].EnableInClassList("active", (int)CurrentTab == i);
                tabButtons[i].SetEnabled(game.Phase != RunPhase.Spinning);
            }
            action.SetEnabled(game.Phase != RunPhase.Spinning);
            Display(resultArea, game.Phase == RunPhase.Finished); Display(roulette, game.Phase != RunPhase.Finished);
            switch (game.Phase)
            {
                case RunPhase.Ready:
                    eyebrow.text = "РУЛЕТКА"; title.text = "Снаряжение шахтёра";
                    description.text = "Выбери кирку и пополни свой рюкзак.";
                    heroName.text = "Пять кирок на выбор"; heroDetail.text = "У каждой свои сила и прочность";
                    heroIcon.style.backgroundImage = Background.FromSprite(PixelArt.Pickaxe(game.Balance.pickaxes[4].color));
                    action.text = "КРУТИТЬ РУЛЕТКУ";
                    foreach (var slot in slots) slot.RemoveFromClassList("selected");
                    break;
                case RunPhase.Spinning:
                    eyebrow.text = "ИСПЫТЫВАЕМ УДАЧУ"; title.text = "Какая кирка выпадет?";
                    description.text = "От деревянной до алмазной."; action.text = "ВРАЩАЕТСЯ..."; break;
                case RunPhase.Equipped:
                    eyebrow.text = "КИРКА ПОЛУЧЕНА"; title.text = "Готов к спуску?";
                    description.text = "Собирай блоки, руду и ищи верстаки.";
                    ShowRouletteTier(game.Pickaxe.Tier); action.text = "В ШАХТУ  ↓"; break;
                case RunPhase.Finished:
                    eyebrow.text = "ЭКСПЕДИЦИЯ ЗАВЕРШЕНА"; title.text = $"{game.Depth} м в глубину";
                    description.text = "Кирка сломалась. Добыча — в рюкзаке.";
                    ShowRouletteTier(game.Pickaxe.Tier);
                    result.text = $"+{game.Coins} монет  /  {game.BlocksBroken} блоков";
                    action.text = "ЕЩЁ ОДИН ЗАБЕГ"; break;
            }
            Tick();
        }

        public void ShowRouletteTier(int tier)
        {
            var stats = game.Balance.pickaxes[tier];
            for (int i = 0; i < slots.Length; i++) slots[i].EnableInClassList("selected", i == tier);
            heroIcon.style.backgroundImage = Background.FromSprite(PixelArt.Pickaxe(stats.color));
            heroName.text = stats.title;
            heroDetail.text = $"{stats.rarity}\nУрон {stats.damage}  /  Прочность {stats.durability}";
        }
        public void Toast(string message) { toast.text = message; toastUntil = Time.unscaledTime + 1.5f; Display(toast, true); }
        void MainAction()
        {
            if (game.Phase == RunPhase.Ready) game.Spin();
            else if (game.Phase == RunPhase.Equipped) game.Drop();
            else if (game.Phase == RunPhase.Finished) game.NewRun();
        }
        public void Dispose() => game.Changed -= Refresh;
        static void Display(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        static VisualElement Box(VisualElement parent, string classes)
        { var element = new VisualElement(); AddClasses(element, classes); parent.Add(element); return element; }
        static Label Text(VisualElement parent, string text, string classes)
        { var label = new Label(text); AddClasses(label, classes); parent.Add(label); return label; }
        static VisualElement Icon(VisualElement parent, Sprite sprite, string classes)
        { var icon = Box(parent, classes); icon.style.backgroundImage = Background.FromSprite(sprite); return icon; }
        static Button Button(VisualElement parent, string text, Action click, string classes)
        { var button = new Button(click) { text = text }; AddClasses(button, classes); parent.Add(button); return button; }
        static Label Stat(VisualElement parent, string title, string value)
        { var box = Box(parent, "stat inset"); Text(box, title, "stat-title"); return Text(box, value, "stat-value"); }
        static void AddClasses(VisualElement element, string classes)
        { foreach (string name in classes.Split(' ')) element.AddToClassList(name); }
        static void ShopShelf(VisualElement parent, Sprite sprite, string title)
        {
            var shelf = Box(parent, "shop-shelf inset"); Icon(shelf, sprite, "shelf-icon");
            Text(shelf, title, "shelf-title"); Text(shelf, "СКОРО", "locked");
        }

        sealed class ResourceGrid
        {
            readonly Label[] counts = new Label[ResourceInventory.Materials.Length];
            readonly VisualElement[] icons = new VisualElement[ResourceInventory.Materials.Length];
            readonly int[] previous = new int[ResourceInventory.Materials.Length];
            public ResourceGrid(VisualElement parent, string name, bool compact)
            {
                var grid = Box(parent, compact ? "resource-grid compact" : "resource-grid"); grid.name = name;
                for (int i = 0; i < counts.Length; i++)
                {
                    var kind = ResourceInventory.Materials[i];
                    previous[i] = -1;
                    var slot = Box(grid, "resource-slot inset"); slot.tooltip = GameBalance.BlockName(kind);
                    icons[i] = Icon(slot, PixelArt.Block(kind, 0, kind == BlockKind.Deepstone), "resource-icon");
                    counts[i] = Text(slot, "0", "stack-count"); counts[i].name = "count-" + kind;
                    if (!compact) Text(slot, kind == BlockKind.Deepstone ? "Сланец" : GameBalance.BlockName(kind), "resource-name");
                }
            }
            public void Update(Func<BlockKind, int> quantity)
            {
                for (int i = 0; i < counts.Length; i++)
                {
                    int value = quantity(ResourceInventory.Materials[i]);
                    if (value == previous[i]) continue;
                    previous[i] = value;
                    string text = value.ToString(); if (counts[i].text != text) counts[i].text = text;
                    icons[i].style.opacity = value > 0 ? 1 : 0.5f;
                }
            }
        }
    }
}
