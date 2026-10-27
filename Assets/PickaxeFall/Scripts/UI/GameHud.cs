using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed class GameHud : IDisposable
    {
        readonly GameController game;
        readonly VisualElement root, safe, card, controls, healthTrack, healthFill, heroIcon, roulette, pauseOverlay;
        readonly Label depth, wallet, best, title, description, eyebrow, heroName, heroDetail, status, toast, healthText, result;
        readonly Button action, pause, sound;
        readonly VisualElement[] slots;
        float toastUntil;
        Rect previousSafeArea;
        int lastWidth, lastHeight;

        public GameHud(GameController owner, VisualElement document, StyleSheet style)
        {
            game = owner; root = document; root.styleSheets.Add(style); root.AddToClassList("screen"); root.pickingMode = PickingMode.Ignore;
            safe = Box(root, "safe"); safe.pickingMode = PickingMode.Ignore;
            var header = Box(safe, "header");
            var brandRow = Box(header, "brand-row");
            Text(brandRow, "PICKAXE", "brand"); Text(brandRow, "FALL", "brand-accent");
            var headerButtons = Box(brandRow, "header-buttons");
            sound = Button(headerButtons, "ЗВУК", () => { game.Effects.ToggleSound(); Refresh(); }, "small-button");
            pause = Button(headerButtons, "II", game.TogglePause, "small-button");
            var stats = Box(header, "stats-row");
            depth = Stat(stats, "ГЛУБИНА", "0 м"); wallet = Stat(stats, "ДОБЫЧА", "0"); best = Stat(stats, "РЕКОРД", "0 м");
            var sceneInfo = Box(safe, "scene-info"); Text(sceneInfo, "↓  БЕЗДОННАЯ ШАХТА", "micro");
            status = Text(sceneInfo, "ПОВЕРХНОСТЬ / 000", "micro muted");
            var air = Box(safe, "air"); air.pickingMode = PickingMode.Ignore;
            toast = Text(root, "", "toast"); toast.style.display = DisplayStyle.None;

            card = Box(safe, "card");
            eyebrow = Text(card, "01 / НОВАЯ ЭКСПЕДИЦИЯ", "eyebrow");
            title = Text(card, "Удача. Гравитация.\nИ немного алмазов.", "title");
            description = Text(card, "Получи кирку и отправь её в глубину.\nКаждый удар приближает тебя к новой руде.", "description");
            roulette = Box(card, "roulette"); slots = new VisualElement[game.Balance.pickaxes.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i] = Box(roulette, "slot");
                var image = Box(slot, "slot-icon"); image.style.backgroundImage = Background.FromSprite(PixelArt.Pickaxe(game.Balance.pickaxes[i].color));
                float total = 0; foreach (var tier in game.Balance.pickaxes) total += tier.weight;
                Text(slot, $"{game.Balance.pickaxes[i].weight / total * 100:0.#}%", "slot-odds");
            }
            var hero = Box(card, "hero"); heroIcon = Box(hero, "hero-icon");
            var heroInfo = Box(hero, "hero-info"); heroName = Text(heroInfo, "Пять кирок. Один шанс.", "hero-name");
            heroDetail = Text(heroInfo, "Редкость определяет силу и прочность", "hero-detail");
            result = Text(card, "", "result");
            action = Button(card, "КРУТИТЬ РУЛЕТКУ  →", MainAction, "primary");
            Text(card, "БЕЗ СТАВОК · КАЖДЫЙ ЗАБЕГ БЕСПЛАТНЫЙ", "card-footnote");

            controls = Box(safe, "controls");
            var toolRow = Box(controls, "tool-row"); Text(toolRow, "ПРОЧНОСТЬ", "micro"); healthText = Text(toolRow, "100 / 100", "micro mint");
            healthTrack = Box(controls, "health-track"); healthFill = Box(healthTrack, "health-fill");
            var steer = Box(controls, "steer");
            Button(steer, "←  ВЛЕВО", () => game.Pickaxe.Nudge(-1), "steer-button");
            Text(steer, "НАПРАВЛЯЙ\nПАДЕНИЕ", "steer-hint");
            Button(steer, "ВПРАВО  →", () => game.Pickaxe.Nudge(1), "steer-button");
            Text(controls, "ВЕРСТАК = УЛУЧШЕНИЕ + ПОЛНЫЙ РЕМОНТ", "card-footnote");
            Text(safe, "КОПАЙ ГЛУБЖЕ. ПАДАЙ ДАЛЬШЕ.", "footer");

            pauseOverlay = Box(root, "pause-overlay");
            var pauseCard = Box(pauseOverlay, "pause-card"); Text(pauseCard, "ПАУЗА", "eyebrow");
            Text(pauseCard, "Шахта подождёт.", "title");
            Button(pauseCard, "ПРОДОЛЖИТЬ  →", game.TogglePause, "primary");
            game.Changed += Refresh; Refresh();
        }

        public void Tick()
        {
            depth.text = $"{game.Depth} м"; wallet.text = game.Coins.ToString();
            status.text = $"{(game.Depth < 3 ? "ПОВЕРХНОСТЬ" : game.Depth < 22 ? "КАМЕННЫЙ СЛОЙ" : game.Depth < 55 ? "РУДНЫЙ СЛОЙ" : "ГЛУБИННЫЙ СЛАНЕЦ")} / {game.Depth:000}";
            if (game.Pickaxe && game.Pickaxe.MaxDurability > 0)
            {
                healthText.text = $"{game.Pickaxe.Durability} / {game.Pickaxe.MaxDurability}";
                healthFill.style.width = Length.Percent(100f * game.Pickaxe.Durability / game.Pickaxe.MaxDurability);
                healthFill.style.backgroundColor = (Color)(game.Pickaxe.Durability > game.Pickaxe.MaxDurability * 0.25f ? new Color32(110, 237, 190, 255) : new Color32(245, 146, 103, 255));
            }
            if (Time.unscaledTime > toastUntil) toast.style.display = DisplayStyle.None;
            Rect safeArea = Screen.safeArea;
            if (safeArea != previousSafeArea || lastWidth != Screen.width || lastHeight != Screen.height)
            {
                previousSafeArea = safeArea; lastWidth = Screen.width; lastHeight = Screen.height;
                float scale = 1080f / Mathf.Max(1, Screen.width);
                // USS uses reference-resolution pixels; cover cutouts and gesture areas.
                safe.style.paddingTop = 32 + (Screen.height - safeArea.yMax) * scale;
                safe.style.paddingBottom = 16 + safeArea.y * scale;
                safe.style.paddingLeft = 40 + safeArea.xMin * scale;
                safe.style.paddingRight = 40 + (Screen.width - safeArea.xMax) * scale;
            }
        }

        void Refresh()
        {
            if (!game.Pickaxe) return;
            bool mining = game.Phase == RunPhase.Mining;
            card.style.display = mining ? DisplayStyle.None : DisplayStyle.Flex;
            controls.style.display = mining ? DisplayStyle.Flex : DisplayStyle.None;
            pause.style.display = mining ? DisplayStyle.Flex : DisplayStyle.None;
            pauseOverlay.style.display = game.Paused ? DisplayStyle.Flex : DisplayStyle.None;
            best.text = $"{game.BestDepth} м";
            sound.text = game.Effects.Muted ? "ТИХО" : "ЗВУК";
            action.SetEnabled(game.Phase != RunPhase.Spinning);
            result.style.display = DisplayStyle.None;
            roulette.style.display = DisplayStyle.Flex;
            switch (game.Phase)
            {
                case RunPhase.Ready:
                    eyebrow.text = "01 / НОВАЯ ЭКСПЕДИЦИЯ"; title.text = "Удача. Гравитация.\nИ немного алмазов.";
                    description.text = "Получи кирку и отправь её в глубину.\nКаждый удар приближает тебя к новой руде.";
                    heroName.text = "Пять кирок. Один шанс."; heroDetail.text = "Редкость определяет силу и прочность";
                    heroName.style.color = (Color)new Color32(115, 239, 193, 255);
                    heroIcon.style.backgroundImage = Background.FromSprite(PixelArt.Pickaxe(game.Balance.pickaxes[4].color));
                    action.text = "КРУТИТЬ РУЛЕТКУ  →";
                    foreach (var slot in slots) slot.RemoveFromClassList("selected");
                    break;
                case RunPhase.Spinning:
                    eyebrow.text = "02 / ИСПЫТЫВАЕМ УДАЧУ"; title.text = "Какая кирка\nдостанется тебе?";
                    description.text = "От деревянной до алмазной.\nСейчас всё решит случай."; action.text = "ВРАЩАЕТСЯ…"; break;
                case RunPhase.Equipped:
                    eyebrow.text = "03 / ГОТОВА К ПАДЕНИЮ"; title.text = "Твоя кирка.\nТвоя глубина.";
                    description.text = "Толкай кирку влево и вправо.\nИщи руду и не пропускай верстаки.";
                    ShowRouletteTier(game.Pickaxe.Tier); action.text = "В ШАХТУ  ↓"; break;
                case RunPhase.Finished:
                    eyebrow.text = "04 / ЭКСПЕДИЦИЯ ЗАВЕРШЕНА"; title.text = $"{game.Depth} метров\nближе к сокровищам.";
                    description.text = "Прочность закончилась.\nНовая кирка — новая история.";
                    roulette.style.display = DisplayStyle.None;
                    ShowRouletteTier(game.Pickaxe.Tier);
                    result.style.display = DisplayStyle.Flex;
                    result.text = $"БЛОКОВ  {game.BlocksBroken}     ДОБЫЧА  +{game.Coins}\nВСЕГО МОНЕТ  {game.Wallet}     РЕКОРД  {game.BestDepth} м\nУГОЛЬ {game.OreCounts[2]}  ·  МЕДЬ {game.OreCounts[3]}  ·  ЖЕЛЕЗО {game.OreCounts[4]}\nЗОЛОТО {game.OreCounts[5]}  ·  АЛМАЗЫ {game.OreCounts[6]}";
                    action.text = "ЕЩЁ ОДИН ЗАБЕГ  ↻"; break;
            }
            Tick();
        }

        public void ShowRouletteTier(int tier)
        {
            var stats = game.Balance.pickaxes[tier];
            for (int i = 0; i < slots.Length; i++) slots[i].EnableInClassList("selected", i == tier);
            heroIcon.style.backgroundImage = Background.FromSprite(PixelArt.Pickaxe(stats.color));
            heroName.text = stats.title; heroName.style.color = stats.color;
            heroDetail.text = $"{stats.rarity}  /  УРОН {stats.damage}  /  ПРОЧНОСТЬ {stats.durability}";
        }

        public void Toast(string message) { toast.text = message; toastUntil = Time.unscaledTime + 1.8f; toast.style.display = DisplayStyle.Flex; }
        void MainAction()
        {
            if (game.Phase == RunPhase.Ready) game.Spin();
            else if (game.Phase == RunPhase.Equipped) game.Drop();
            else if (game.Phase == RunPhase.Finished) game.NewRun();
        }
        public void Dispose() => game.Changed -= Refresh;
        static VisualElement Box(VisualElement parent, string classes)
        {
            var element = new VisualElement(); AddClasses(element, classes); parent.Add(element); return element;
        }
        static Label Text(VisualElement parent, string text, string classes)
        {
            var label = new Label(text); AddClasses(label, classes); parent.Add(label); return label;
        }
        static Button Button(VisualElement parent, string text, Action click, string classes)
        {
            var button = new Button(click) { text = text }; AddClasses(button, classes); parent.Add(button); return button;
        }
        static Label Stat(VisualElement parent, string title, string value)
        {
            var box = Box(parent, "stat"); Text(box, title, "stat-title"); return Text(box, value, "stat-value");
        }
        static void AddClasses(VisualElement element, string classes)
        { foreach (string name in classes.Split(' ')) element.AddToClassList(name); }
    }
}
