using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        MapId catalogMap;
        Button journalShortcut;
        void OpenJournal(int page = 0)
        {
            if (!game.CanTrade) return;
            game.Economy.RefreshDailyTasks();
            var card = OpenModal(L.T("Дневник шахтёра"));
            DecorateJournal(card); card.name = "journal-book";
            var tabs = Box(card, "journal-tabs");
            string[] titles = { L.T("КОЛЛЕКЦИЯ"), L.T("ЗАДАНИЯ"), L.T("РЕКОРДЫ") };
            for (int i = 0; i < titles.Length; i++)
            {
                int index = i;
                var button = Button(tabs, titles[i], () => OpenJournal(index), "stone-button journal-tab");
                button.name = "journal-tab-" + i; button.EnableInClassList("active", i == page);
            }
            if (page == 0) BuildCollection(card);
            else if (page == 1) BuildDailyTasks(card);
            else BuildPersonalRecords(card);
            Button(card, L.T("ЗАКРЫТЬ"), () => CloseModal(), "stone-button compact-button").name = "journal-close";
        }
        void DecorateJournal(VisualElement card)
        {
            card.AddToClassList("journal-book");
            var title = card[0]; title.RemoveFromHierarchy();
            var top = Box(card, "journal-top"); top.Add(title);
            var close = Button(top, "×", () => CloseModal(), "stone-button journal-close");
            close.name = "journal-close-top"; close.tooltip = L.T("ЗАКРЫТЬ");
        }
        void BuildCollection(VisualElement card)
        {
            var profile = game.Economy.Profile;
            var banner = Box(card, "journal-banner");
            BoldText(banner, L.T("КОЛЛЕКЦИЯ МИНЕРАЛОВ"), "journal-heading");
            Text(banner, L.F("Найдено: {0} / {1}", MineralCatalog.FoundCount(profile), ResourceInventory.Materials.Length), "journal-summary");
            Text(card, L.T("Здесь остаются все находки, даже если ты их продал."), "journal-note");
            var maps = Box(card, "journal-tabs");
            foreach (MapId map in Enum.GetValues(typeof(MapId)))
            {
                MapId choice = map;
                var button = Button(maps, WorldCatalog.Name(map), () => { catalogMap = choice; OpenJournal(0); }, "stone-button journal-map-tab");
                button.name = "catalog-map-" + (int)map; button.EnableInClassList("active", catalogMap == map);
            }
            var grid = Box(card, "mineral-grid"); grid.name = "mineral-grid";
            foreach (var kind in ResourceInventory.Materials)
            {
                if (!MineralCatalog.InMap(kind, catalogMap)) continue;
                BlockKind resource = kind;
                bool found = MineralCatalog.Found(profile, kind);
                var tile = Button(grid, "", () => OpenMineral(resource), "mineral-tile");
                tile.name = "mineral-" + kind; tile.EnableInClassList("mineral-unknown", !found);
                Text(tile, ((int)kind + 1).ToString("00"), "mineral-number");
                Icon(tile, PixelArt.ResourceIcon(kind), "mineral-art");
                BoldText(tile, found ? GameBalance.BlockName(kind) : "???", "mineral-name");
                Text(tile, found ? L.T("ОТКРЫТО") : L.T("НЕ НАЙДЕНО"), "mineral-caption");
            }
        }
        void OpenMineral(BlockKind kind)
        {
            if (!game.CanTrade) return;
            var profile = game.Economy.Profile;
            bool found = MineralCatalog.Found(profile, kind);
            var card = OpenModal(found ? GameBalance.BlockName(kind) : L.T("Неизвестный минерал"));
            DecorateJournal(card); card.name = "mineral-details";
            var specimen = Box(card, "mineral-specimen"); specimen.EnableInClassList("mineral-unknown", !found);
            Icon(specimen, PixelArt.ResourceIcon(kind), "mineral-art mineral-large");
            Text(card, WorldCatalog.Name(catalogMap), "journal-eyebrow");
            Text(card, found ? MineralCatalog.Location(kind, catalogMap) : L.T("Исследуй эту карту, чтобы открыть запись."), "journal-body");
            if (found)
            {
                Text(card, MineralCatalog.Note(kind), "journal-body");
                RecordRow(card, L.T("Цена продажи"), Exact(game.Economy.Config.sellPrices[(int)kind]) + L.T(" мон."));
                RecordRow(card, L.T("Добыто блоков"), Exact(profile.lifetimeMined[(int)kind]));
                RecordRow(card, L.T("В рюкзаке"), Exact(game.Inventory[kind]));
            }
            Button(card, L.T("НАЗАД В КОЛЛЕКЦИЮ"), () => OpenJournal(0), "stone-button compact-button").name = "mineral-back";
        }
        static string DailyTitle(DailyTask task) => task.goal switch
        {
            DailyGoal.MineBlocks => L.T("Разработка породы"),
            DailyGoal.MineOre => L.T("Рудная смена"),
            DailyGoal.MineResource => GameBalance.BlockName(task.resource),
            DailyGoal.FinishRuns => L.T("Ещё один спуск"),
            DailyGoal.ReachDepth => L.T("Курс на глубину"),
            _ => L.T("Удачная торговля")
        };
        static string DailyDescription(DailyTask task) => task.goal switch
        {
            DailyGoal.MineBlocks => L.F("Добудь {0} блоков. Бонусные ресурсы не считаются.", task.target),
            DailyGoal.MineOre => L.F("Разбей {0} блоков любой руды.", task.target),
            DailyGoal.MineResource => L.F("Добудь блоков этого вида: {0}.", task.target),
            DailyGoal.FinishRuns => L.F("Заверши {0} спуска. Продолженный спуск считается один раз.", task.target),
            DailyGoal.ReachDepth => L.F("Достигни {0} м на любой карте.", task.target),
            _ => L.F("Продай ресурсы на {0} монет. Награды заданий не считаются.", task.target)
        };
        void BuildDailyTasks(VisualElement card)
        {
            var journal = game.Economy.Profile.journal;
            long shownDay = journal.day;
            var banner = Box(card, "journal-banner");
            BoldText(banner, L.T("ТРИ ДЕЛА НА СЕГОДНЯ"), "journal-heading");
            var timer = Text(banner, "", "journal-summary"); timer.name = "daily-reset-timer";
            Text(card, L.T("Новый список в 00:00 UTC · 03:00 МСК. Награды забирай до смены дня. За пропуск дня ничего не теряешь."), "journal-note");
            void TickTimer()
            {
                if (card.panel == null) return;
                game.Economy.RefreshDailyTasks();
                if (game.Economy.Profile.journal.day != shownDay) { OpenJournal(1); return; }
                long seconds = Math.Max(0, ((shownDay + 1) * DailyTasks.DayMilliseconds - game.Platform.UtcMilliseconds) / 1000);
                timer.text = L.F("До новых заданий: {0}", TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss"));
            }
            card.schedule.Execute(TickTimer).Every(1000);
            for (int i = 0; i < journal.tasks.Length; i++)
            {
                int index = i; var task = journal.tasks[i];
                var tile = Box(card, "daily-card"); tile.name = "daily-card-" + i;
                tile.EnableInClassList("daily-done", task.claimed);
                var head = Box(tile, "daily-head");
                BoldText(head, (i + 1).ToString("00"), "daily-number");
                BoldText(head, DailyTitle(task), "daily-title");
                Text(tile, DailyDescription(task), "journal-body");
                var track = Box(tile, "daily-track");
                Box(track, "daily-fill").style.width = Length.Percent(100f * task.progress / task.target);
                var footer = Box(tile, "daily-footer");
                BoldText(footer, Exact(task.progress) + " / " + Exact(task.target), "daily-progress");
                Text(footer, "+" + Exact(task.reward) + L.T(" мон."), "daily-reward");
                var claim = Button(tile, task.claimed ? L.T("ПОЛУЧЕНО") : task.progress >= task.target ? L.T("ЗАБРАТЬ НАГРАДУ") : L.T("В ПРОЦЕССЕ"),
                    () => { bool success = game.ClaimDaily(index, shownDay); OpenJournal(1); if(success)Celebrate("+" + Exact(task.reward) + L.T(" мон.")); }, "stone-button daily-claim");
                claim.name = "daily-claim-" + i;
                claim.SetEnabled(!task.claimed && task.progress >= task.target);
                claim.EnableInClassList("buy-ready", !task.claimed && task.progress >= task.target);
            }
        }
        void BuildPersonalRecords(VisualElement card)
        {
            var profile = game.Economy.Profile; var journal = profile.journal;
            var banner = Box(card, "journal-banner");
            BoldText(banner, L.T("ЛИЧНЫЕ РЕКОРДЫ"), "journal-heading");
            Text(banner, L.F("Открыто ресурсов: {0} / {1}", MineralCatalog.FoundCount(profile), ResourceInventory.Materials.Length), "journal-summary");
            foreach (MapId map in Enum.GetValues(typeof(MapId))) RecordRow(card, WorldCatalog.Name(map), Exact(profile.mapRecords[(int)map]) + L.T(" м"));
            RecordRow(card, L.T("Всего добыто блоков"), Exact(MineralCatalog.TotalMined(profile)));
            Text(card, L.T("Следующие показатели считаются с версии 0.8.6. Старые рекорды глубины и добыча сохранены."), "journal-note");
            RecordRow(card, L.T("Завершено спусков"), Exact(journal.completedRuns));
            RecordRow(card, L.T("Время под землёй"), Duration(journal.miningTicks));
            RecordRow(card, L.T("Самый долгий спуск"), Duration(journal.longestRunTicks));
            RecordRow(card, L.T("Больше всего блоков за спуск"), Exact(journal.bestRunBlocks));
            RecordRow(card, L.T("Самая дорогая добыча за спуск"), Exact(journal.bestRunValue) + L.T(" мон."));
            RecordRow(card, L.T("Монет от продажи"), Exact(journal.soldCoins));
            RecordRow(card, L.T("Открыто сундуков"), Exact(journal.chests));
            RecordRow(card, L.T("Использовано верстаков"), Exact(journal.workbenches));
            RecordRow(card, L.T("Пройдено испытаний спавнера"), Exact(journal.trialsWon));
            RecordRow(card, L.T("Использовано приёмов"), Exact(journal.abilities));
            RecordRow(card, L.T("Получено ежедневных наград"), Exact(journal.dailyRewardsClaimed));
        }
        static string Duration(long ticks) => $"{ticks / 216000}:{ticks / 3600 % 60:00}:{ticks / 60 % 60:00}";
        void RecordRow(VisualElement parent, string title, string value)
        {
            var row = Box(parent, "journal-record"); Text(row, title, "journal-record-title"); BoldText(row, value, "journal-record-value");
        }
        void TickJournalShortcut()
        {
            if (journalShortcut == null) return;
            int rewards = 0;
            foreach (var task in game.Economy.Profile.journal.tasks) if (!task.claimed && task.progress >= task.target) rewards++;
            journalShortcut.text = rewards > 0 ? L.F("ДНЕВНИК · НАГРАДЫ: {0}", rewards) : L.T("ДНЕВНИК ШАХТЁРА");
        }
    }
}
