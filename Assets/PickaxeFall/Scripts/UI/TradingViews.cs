using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        readonly Label[] bagCounts = new Label[8];
        readonly List<UpgradeCard> upgradeCards = new();
        readonly List<Button> categoryButtons = new(), tierButtons = new();
        Label bagValue, bagGoal, selectedToolName;
        Button sellBag;
        VisualElement shopCards, tierSelector, soonPanel;
        int shopCategory, shopTier;
        UpgradeKey pinned = new(UpgradeKind.Damage);
        float nextTrade;

        void BuildInventory(VisualElement parent)
        {
            BoldText(parent, "Твой рюкзак", "title");
            bagValue = Text(parent, "", "description"); bagValue.name = "bag-value";
            bagGoal = Text(parent, "", "goal-note"); bagGoal.name = "bag-goal";
            var grid = Box(parent, "bag-grid");
            for (int i = 0; i < 8; i++)
            {
                var kind = (BlockKind)i;
                var card = Button(grid, "", () => OpenSale(kind), "resource-card inset"); card.name = "sell-" + kind;
                Icon(card, PixelArt.Block(kind, 0, kind == BlockKind.Deepstone), "bag-icon");
                BoldText(card, kind == BlockKind.Deepstone ? "Глубинный сланец" : GameBalance.BlockName(kind), "bag-name");
                bagCounts[i] = BoldText(card, "0", "bag-count"); bagCounts[i].name = "count-" + kind;
                Text(card, Progression.SellPrices[i] + " мон. за шт.", "bag-price");
            }
            sellBag = Button(parent, "ПРОДАТЬ ВЕСЬ РЮКЗАК", OpenSellAll, "stone-button primary"); sellBag.name = "sell-bag";
            Text(parent, "Нажми на ресурс, чтобы продать выбранное количество или весь запас этого вида.", "footnote");
        }
        void RefreshInventory()
        {
            bool quote = game.Economy.TryInventoryValue(out long total);
            bagValue.text = quote ? $"Предметов: {Short(game.Inventory.Total)}\nМожно продать за {Exact(total)} монет" : "Большой запас: продавай ресурсы частями.";
            sellBag.SetEnabled(quote && total > 0 && game.CanTrade);
            for (int i = 0; i < 8; i++) bagCounts[i].text = Short(game.Inventory[(BlockKind)i]);
            int next = game.Economy.Profile.Level(pinned) + 1;
            if (next > Progression.MaxLevel(pinned.Kind)) bagGoal.text = "Выбранное улучшение прокачано полностью.";
            else
            {
                var cost = Progression.Cost(pinned, next);
                string tool = pinned.Kind <= UpgradeKind.Durability ? " · " + game.Balance.pickaxes[pinned.Tier].title : "";
                bagGoal.text = $"Цель: {Progression.Title(pinned.Kind)}{tool}\nОставь для уровня {next}: {ResourceRecipe(cost)}";
            }
        }
        public static bool TrySaleQuantity(string value, long available, out long quantity)
        {
            return long.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out quantity) && quantity > 0 && quantity <= available;
        }
        VisualElement OpenModal(string heading)
        {
            modal.Clear(); Display(modal, true);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("modal-scroll"); modal.Add(scroll);
            var card = Box(scroll.contentContainer, "modal-card"); BoldText(card, heading, "title"); return card;
        }
        void OpenSale(BlockKind kind)
        {
            if (!game.CanTrade) return;
            long available = game.Inventory[kind]; var card = OpenModal(GameBalance.BlockName(kind));
            Text(card, $"В рюкзаке: {Exact(available)}\nЦена: {Progression.SellPrices[(int)kind]} монет за шт.", "description");
            Text(card, "Сколько продать?", "section-label");
            var input = new TextField { value = available > 0 ? "1" : "0", maxLength = 19, keyboardType = TouchScreenKeyboardType.NumberPad, name = "sale-quantity" };
            input.AddToClassList("quantity-input"); card.Add(input);
            var quote = BoldText(card, "", "sale-quote");
            var confirm = Button(card, "ПРОДАТЬ", () =>
            {
                if (Time.unscaledTime < nextTrade || !TrySaleQuantity(input.value, game.Inventory[kind], out long count)) return;
                nextTrade = Time.unscaledTime + 0.25f; if (game.Sell(kind, count)) CloseModal();
            }, "stone-button primary"); confirm.name = "sale-confirm";
            void UpdateQuote()
            {
                bool valid = TrySaleQuantity(input.value, game.Inventory[kind], out long count);
                long money = 0;
                if (valid) { try { money = checked(count * Progression.SellPrices[(int)kind]); checked { _ = money + game.Wallet; } } catch (OverflowException) { valid = false; } }
                quote.text = valid ? $"Ты получишь {Exact(money)} монет" : "Укажи доступное целое количество."; confirm.SetEnabled(valid);
            }
            input.RegisterValueChangedCallback(_ => UpdateQuote()); UpdateQuote();
            var all = Button(card, "ПРОДАТЬ ВЕСЬ ЭТОТ ВИД", () =>
            {
                if (Time.unscaledTime < nextTrade) return;
                nextTrade = Time.unscaledTime + 0.25f; if (game.Sell(kind, game.Inventory[kind])) CloseModal();
            }, "stone-button secondary"); all.name = "sale-all-kind"; all.SetEnabled(available > 0);
            Button(card, "ОТМЕНА", () => CloseModal(), "stone-button secondary");
        }
        void OpenSellAll()
        {
            if (!game.CanTrade || !game.Economy.TryInventoryValue(out long value) || value == 0) return;
            var card = OpenModal("Продать весь рюкзак?");
            Text(card, $"Предметов: {Exact(game.Inventory.Total)}\nВыручка: {Exact(value)} монет\n\nБудут проданы и материалы для улучшений. Продажу нельзя отменить.", "description");
            Button(card, "ПРОДАТЬ ВСЁ", () => { if (Time.unscaledTime < nextTrade) return; nextTrade = Time.unscaledTime + 0.25f; if (game.SellAll()) CloseModal(); }, "stone-button primary").name = "sale-confirm-all";
            Button(card, "ОСТАВИТЬ РЕСУРСЫ", () => CloseModal(), "stone-button secondary");
        }
        void BuildShop(VisualElement parent)
        {
            BoldText(parent, "Лавка улучшений", "title");
            Text(parent, "Первые уровни дешёвые. Каждый следующий требует больше монет и материалов.", "description");
            var categories = Box(parent, "shop-categories"); string[] names = { "КИРКИ", "ДОБЫЧА", "УДАЧА", "СКОРО" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i; var b = Button(categories, names[i], () => { shopCategory = index; RebuildShop(); }, "stone-button category"); b.name = "shop-category-" + i; categoryButtons.Add(b);
            }
            tierSelector = Box(parent, "tier-selector");
            for (int i = 0; i < 5; i++)
            {
                int tier = i;
                var b = Button(tierSelector, "", () => { shopTier = tier; RebuildShop(); }, "stone-button tier-button"); b.tooltip = game.Balance.pickaxes[i].title; b.name = "shop-tier-" + i;
                Icon(b, PixelArt.Pickaxe(game.Balance.pickaxes[i].color), "tier-icon"); tierButtons.Add(b);
            }
            selectedToolName = BoldText(parent, "", "selected-tool"); shopCards = Box(parent, "shop-cards");
            soonPanel = Box(parent, "soon-panel");
            BoldText(soonPanel, "Новые карты и кирки", "title");
            Text(soonPanel, "В следующем обновлении. Пока все пять видов кирок выпадают в рулетке, а их характеристики улучшаются здесь.", "description");
            RebuildShop();
        }
        void RebuildShop()
        {
            shopCards.Clear(); upgradeCards.Clear();
            Display(tierSelector, shopCategory == 0); Display(selectedToolName, shopCategory == 0); Display(soonPanel, shopCategory == 3);
            if (shopCategory == 0) { AddCard(new(UpgradeKind.Damage, shopTier)); AddCard(new(UpgradeKind.Durability, shopTier)); }
            else if (shopCategory == 1) foreach (var kind in new[] { UpgradeKind.Fortune, UpgradeKind.TntFind, UpgradeKind.Careful }) AddCard(new(kind));
            else if (shopCategory == 2) foreach (var kind in new[] { UpgradeKind.TeamChance, UpgradeKind.TeamSize, UpgradeKind.DoubleBench, UpgradeKind.DoubleBlast, UpgradeKind.LastChance }) AddCard(new(kind));
            RefreshShop();
        }
        void AddCard(UpgradeKey key) => upgradeCards.Add(new UpgradeCard(this, shopCards, key));
        void RefreshShop()
        {
            for (int i = 0; i < categoryButtons.Count; i++) categoryButtons[i].EnableInClassList("active", i == shopCategory);
            for (int i = 0; i < tierButtons.Count; i++) tierButtons[i].EnableInClassList("active", i == shopTier);
            selectedToolName.text = game.Balance.pickaxes[shopTier].title;
            foreach (var card in upgradeCards) card.Refresh();
        }
        string ResourceRecipe(UpgradeCost cost, bool owned = false)
        {
            var text = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                if (cost.Resources[i] == 0) continue;
                if (text.Length > 0) text.Append(" · ");
                text.Append(GameBalance.BlockName((BlockKind)i)).Append(' ');
                if (owned) text.Append(Short(game.Inventory[(BlockKind)i])).Append('/');
                text.Append(Exact(cost.Resources[i]));
            }
            return text.ToString();
        }
        string UpgradeEffect(UpgradeKey key, int level)
        {
            if (key.Kind == UpgradeKind.Damage) return Progression.Number(game.Balance.pickaxes[key.Tier].damage + level * 0.5) + " урона";
            if (key.Kind == UpgradeKind.Durability) return (game.Balance.pickaxes[key.Tier].durability + level * 2) + " прочности";
            if (key.Kind == UpgradeKind.TeamSize) return (2 + level) + " кирки";
            return Progression.Number(level * (key.Kind == UpgradeKind.Fortune ? 1 : 0.5)) + "%";
        }
        sealed class UpgradeCard
        {
            readonly GameHud hud; readonly UpgradeKey key;
            readonly VisualElement box; readonly Label heading, effect, cost, resources, reason; readonly Button buy;
            public UpgradeCard(GameHud owner, VisualElement parent, UpgradeKey upgrade)
            {
                hud = owner; key = upgrade; box = Box(parent, "upgrade-card inset"); box.name = "upgrade-" + key.Kind + "-" + key.Tier;
                heading = hud.BoldText(box, "", "upgrade-title");
                Text(box, Progression.Description(key.Kind), "description"); effect = hud.BoldText(box, "", "upgrade-effect");
                cost = Text(box, "", "upgrade-price"); resources = Text(box, "", "upgrade-resources"); reason = Text(box, "", "upgrade-reason");
                buy = hud.Button(box, "УЛУЧШИТЬ", () =>
                {
                    if (Time.unscaledTime < hud.nextTrade) return;
                    hud.nextTrade = Time.unscaledTime + 0.25f; hud.pinned = key; hud.game.Buy(key);
                }, "stone-button secondary"); buy.name = "buy-" + key.Kind + "-" + key.Tier;
                box.RegisterCallback<ClickEvent>(_ => { hud.pinned = key; hud.RefreshShop(); });
            }
            public void Refresh()
            {
                int level = hud.game.Economy.Profile.Level(key), max = Progression.MaxLevel(key.Kind);
                heading.text = Progression.Title(key.Kind) + $" · {level}/{max}";
                effect.text = hud.UpgradeEffect(key, level) + (level < max ? " → " + hud.UpgradeEffect(key, level + 1) : " · МАКСИМУМ");
                box.EnableInClassList("pinned", hud.pinned.Index == key.Index);
                bool available = hud.game.Economy.CanBuy(key, out string why) && hud.game.CanTrade;
                buy.SetEnabled(available); buy.text = level < max ? "УЛУЧШИТЬ" : "ПРОКАЧАНО";
                if (level >= max) { cost.text = resources.text = ""; reason.text = "Максимальный уровень"; return; }
                var price = Progression.Cost(key, level + 1);
                cost.text = $"Цена: {Exact(price.Coins)} монет · есть {Short(hud.game.Wallet)}";
                resources.text = "Есть / нужно: " + hud.ResourceRecipe(price, true);
                reason.text = available ? "Нажми на карточку, чтобы оставить её целью в рюкзаке." : why;
                reason.EnableInClassList("missing", !available);
            }
        }
    }
}
