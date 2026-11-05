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
        readonly Label[] bagCounts = new Label[ResourceInventory.SlotCount];
        readonly List<AbilityCard> abilityCards=new();
        readonly List<UpgradeCard> upgradeCards = new();
        readonly List<Button> categoryButtons = new(), tierButtons = new();
        Label bagValue, bagGoal, selectedToolName;
        Button sellBag, sellSurplus;
        VisualElement shopCards, tierSelector, soonPanel, bagGoalResources;
        int shopCategory, shopTier;
        UpgradeKey pinned => game.Economy.Profile.goalKind==GoalKind.Upgrade?ProfileRules.Key(game.Economy.Profile.goalIndex):new(UpgradeKind.Damage);
        float nextTrade;

        void BuildInventory(VisualElement parent)
        {
            BoldText(parent, "Твой рюкзак", "title");
            bagValue = Text(parent, "", "description"); bagValue.name = "bag-value";
            bagGoal = Text(parent, "", "goal-note"); bagGoal.name = "bag-goal";
            bagGoalResources=Box(parent,"recipe-resources");bagGoalResources.name="goal-materials";
            var grid = Box(parent, "bag-grid");
            foreach(var kind in ResourceInventory.Materials)
            {
                int i=(int)kind;
                var card = Button(grid, "", () => OpenSale(kind), "resource-card inset"); card.name = "sell-" + kind;
                Icon(card, PixelArt.ResourceIcon(kind), "bag-icon");
                BoldText(card, kind == BlockKind.Deepstone ? "Глубинный сланец" : GameBalance.BlockName(kind), "bag-name");
                bagCounts[i] = BoldText(card, "0", "bag-count"); bagCounts[i].name = "count-" + kind;
                Text(card, game.Economy.Config.sellPrices[i] + " мон. за шт.", "bag-price");
            }
            sellSurplus=Button(parent,"ПРОДАТЬ ЛИШНЕЕ",OpenSellSurplus,"stone-button primary");sellSurplus.name="sell-surplus";
            Button(parent,"СНЯТЬ ЦЕЛЬ",()=>PinGoal(GoalKind.None),"stone-button secondary").name="clear-goal";
            sellBag = Button(parent, "ПРОДАТЬ ВЕСЬ РЮКЗАК", OpenSellAll, "stone-button primary"); sellBag.name = "sell-bag";
            Text(parent, "Нажми на ресурс, чтобы продать выбранное количество или весь запас этого вида.", "footnote");
        }
        void RefreshInventory()
        {
            bool quote = game.Economy.TryInventoryValue(out long total);
            bagValue.text = quote ? $"Предметов: {Short(game.Inventory.Total)}\nМожно продать за {Exact(total)} монет" : "Большой запас: продавай ресурсы частями.";
            sellBag.SetEnabled(quote && total > 0 && game.CanTrade);
            foreach(var kind in ResourceInventory.Materials)bagCounts[(int)kind].text=Short(game.Inventory[kind]);
            bagGoal.text=game.Economy.GoalTitle;
            bagGoalResources.Clear();
            if(game.Economy.Profile.goalKind!=GoalKind.None){
                var target=game.Economy.GoalCost();
                if(target.Coins>0)bagGoal.text+="\nЦена покупки: "+Exact(target.Coins)+" монет";
                foreach(var material in ResourceInventory.Materials)if(target.Resources[(int)material]>0)RecipeLine(bagGoalResources,material,game.Inventory[material],target.Resources[(int)material]);
            }
            long surplus=0;try{game.Economy.SurplusQuote(out surplus);checked{_=game.Wallet+surplus;}}catch(OverflowException){surplus=0;}
            bool hasGoal=game.Economy.Profile.goalKind!=GoalKind.None;
            sellSurplus.SetEnabled(hasGoal&&surplus>0&&game.CanTrade);
            sellSurplus.text=!hasGoal?"ВЫБЕРИ ЦЕЛЬ ДЛЯ ПРОДАЖИ":surplus>0?"ПРОДАТЬ ЛИШНЕЕ · "+Short(surplus)+" МОН.":"ЛИШНИХ РЕСУРСОВ НЕТ";
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
            Text(card, $"В рюкзаке: {Exact(available)}\nЦена: {game.Economy.Config.sellPrices[(int)kind]} монет за шт.", "description");
            Text(card, "Сколько продать?", "section-label");
            var input = new TextField { value = available > 0 ? "1" : "0", maxLength = 19, keyboardType = TouchScreenKeyboardType.NumberPad, name = "sale-quantity" };
            input.AddToClassList("quantity-input"); card.Add(input);
            var quote = BoldText(card, "", "sale-quote");
            var confirm = Button(card, "ПРОДАТЬ", () =>
            {
                if (Time.unscaledTime < nextTrade || !TrySaleQuantity(input.value, game.Inventory[kind], out long count)) return;
                nextTrade = Time.unscaledTime + 0.25f; ConfirmTargetSale(kind,count);
            }, "stone-button primary"); confirm.name = "sale-confirm";
            void UpdateQuote()
            {
                bool valid = TrySaleQuantity(input.value, game.Inventory[kind], out long count);
                long money = 0;
                if (valid) { try { money = checked(count * game.Economy.Config.sellPrices[(int)kind]); checked { _ = money + game.Wallet; } } catch (OverflowException) { valid = false; } }
                quote.text = valid ? $"Ты получишь {Exact(money)} монет" : "Укажи доступное целое количество."; confirm.SetEnabled(valid);
            }
            input.RegisterValueChangedCallback(_ => UpdateQuote()); UpdateQuote();
            var all = Button(card, "ПРОДАТЬ ВЕСЬ ЭТОТ ВИД", () =>
            {
                if (Time.unscaledTime < nextTrade) return;
                nextTrade = Time.unscaledTime + 0.25f; ConfirmTargetSale(kind,game.Inventory[kind]);
            }, "stone-button secondary"); all.name = "sale-all-kind"; all.SetEnabled(available > 0);
            Button(card, "ОТМЕНА", () => CloseModal(), "stone-button secondary");
        }
        void OpenSellAll()
        {
            if (!game.CanTrade || !game.Economy.TryInventoryValue(out long value) || value == 0) return;
            var card = OpenModal("Продать весь рюкзак?");
            ReservationWarning(card,null,0);
            Text(card, $"Предметов: {Exact(game.Inventory.Total)}\nВыручка: {Exact(value)} монет\n\nБудут проданы и материалы для улучшений. Продажу нельзя отменить.", "description");
            Button(card, "ПРОДАТЬ ВСЁ", () => { if (Time.unscaledTime < nextTrade) return; nextTrade = Time.unscaledTime + 0.25f; if (game.SellAll()) CloseModal(); }, "stone-button primary").name = "sale-confirm-all";
            Button(card, "ОСТАВИТЬ РЕСУРСЫ", () => CloseModal(), "stone-button secondary");
        }
        void BuildShop(VisualElement parent)
        {
            BoldText(parent, "Лавка улучшений", "title");
            Text(parent, "Первые уровни дешёвые. Каждый следующий требует больше монет и материалов.", "description");
            var categories = Box(parent, "shop-categories"); string[] names = { "КИРКИ", "ДОБЫЧА", "УДАЧА", "ПРИЁМЫ", "СКОРО" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i; var b = Button(categories, names[i], () => { shopCategory = index; RebuildShop(); }, "stone-button category"); b.name = "shop-category-" + i; categoryButtons.Add(b);
            }
            tierSelector = Box(parent, "tier-selector");
            for (int i = 0; i < 5; i++)
            {
                int tier = i;
                var b = Button(tierSelector, "", () => { shopTier = tier; RebuildShop(); }, "stone-button tier-button"); b.tooltip = game.ShopBalance.pickaxes[i].title; b.name = "shop-tier-" + i;
                Icon(b, PixelArt.Pickaxe(i), "tier-icon"); tierButtons.Add(b);
            }
            selectedToolName = BoldText(parent, "", "selected-tool");
            toolHitCounts = Text(parent, "", "description"); shopCards = Box(parent, "shop-cards");
            soonPanel = Box(parent, "soon-panel");
            BoldText(soonPanel, "Новые карты и кирки", "title");
            Text(soonPanel, "В следующем обновлении. Пока все пять видов кирок выпадают в рулетке, а их характеристики улучшаются здесь.", "description");
            RebuildShop();
        }
        void RebuildShop()
        {
            shopCards.Clear(); upgradeCards.Clear();abilityCards.Clear();
            Display(tierSelector, shopCategory == 0); Display(selectedToolName, shopCategory == 0); Display(toolHitCounts, shopCategory == 0); Display(soonPanel, shopCategory == 4);
            if (shopCategory == 0) { AddCard(new(UpgradeKind.Damage, shopTier)); AddCard(new(UpgradeKind.Durability, shopTier)); }
            else if (shopCategory == 1) foreach (var kind in new[] { UpgradeKind.Fortune, UpgradeKind.TntFind, UpgradeKind.Careful, UpgradeKind.ImpactPower, UpgradeKind.Shrapnel }) AddCard(new(kind));
            else if (shopCategory == 2) foreach (var kind in new[] { UpgradeKind.TeamChance, UpgradeKind.TeamSize, UpgradeKind.DoubleBench, UpgradeKind.DoubleBlast, UpgradeKind.LastChance }) AddCard(new(kind));
            else if (shopCategory == 3)
            {
                foreach(var kind in new[] {AbilityKind.Dash,AbilityKind.Plunge})abilityCards.Add(new AbilityCard(this,shopCards,kind));
                foreach (var kind in new[] { UpgradeKind.EnergyCapacity, UpgradeKind.EnergyCharge, UpgradeKind.EnergyEfficiency, UpgradeKind.DashForce, UpgradeKind.PlungeDepth }) AddCard(new(kind));
            }
            RefreshShop();
        }
        void AddCard(UpgradeKey key) => upgradeCards.Add(new UpgradeCard(this, shopCards, key));
        void RefreshShop()
        {
            for (int i = 0; i < categoryButtons.Count; i++) categoryButtons[i].EnableInClassList("active", i == shopCategory);
            for (int i = 0; i < tierButtons.Count; i++) tierButtons[i].EnableInClassList("active", i == shopTier);
            selectedToolName.text = game.ShopBalance.pickaxes[shopTier].title;
            if (shopCategory == 0)
            {
                int damage = game.PreviewDamageHalfUnits(shopTier);
                bool canUpgrade = game.Economy.Profile.Level(new(UpgradeKind.Damage, shopTier)) < Progression.MaxLevel(UpgradeKind.Damage);
                int nextDamage = canUpgrade ? damage + Progression.DamageStepHalfUnits(shopTier,game.Economy.Config) : damage;
                toolHitCounts.text = "Ударов на глубине появления" + (canUpgrade ? " сейчас → после +урона:" : ":") + "\nКамень " + HitComparison(BlockKind.Stone, damage, nextDamage) + " · Медь " + HitComparison(BlockKind.Copper, damage, nextDamage) + " · Железо " + HitComparison(BlockKind.Iron, damage, nextDamage) + " · Алмаз " + HitComparison(BlockKind.Diamond, damage, nextDamage);
            }
            foreach (var card in upgradeCards) card.Refresh();
            foreach(var card in abilityCards)card.Refresh();
        }
        Label toolHitCounts;
        string HitComparison(BlockKind kind, int damage, int nextDamage) => Hits(kind, damage) + (damage == nextDamage ? "" : "→" + Hits(kind, nextDamage));
        int Hits(BlockKind kind, int damage)
        {
            int hp = game.ShopBalance.BlockHealthHalfUnits(kind, game.ShopBalance.Block(kind).minimumDepth);
            return (hp + damage - 1) / damage;
        }
        string ResourceRecipe(UpgradeCost cost, bool owned = false)
        {
            var text = new StringBuilder();
            for (int i = 0; i < ResourceInventory.SlotCount; i++)
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
            if (key.Kind == UpgradeKind.Damage) return Progression.Number(game.ShopBalance.pickaxes[key.Tier].damage + level * Progression.DamageStepHalfUnits(key.Tier,game.Economy.Config) * 0.5) + " урона";
            if (key.Kind == UpgradeKind.Durability) return (game.ShopBalance.pickaxes[key.Tier].durability + level * Progression.DurabilityStep(key.Tier,game.Economy.Config)) + " прочности";
            if (key.Kind == UpgradeKind.TeamSize) return (2 + level) + " кирки";
            return key.Kind switch
            {
                UpgradeKind.EnergyCapacity => (game.Economy.Config.energyCapacity + game.Economy.Config.capacityStep * level) + " энергии",
                UpgradeKind.EnergyCharge => (game.Economy.Config.energyRegen + game.Economy.Config.regenStep * level).ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) + " в секунду",
                UpgradeKind.EnergyEfficiency => "−" + Progression.Number(level*game.Economy.Config.efficiencyStep*100) + "% расхода",
                UpgradeKind.DashForce => Progression.Number(game.Economy.Config.dashSpeed + game.Economy.Config.dashSpeedStep * level) + " м/с",
                UpgradeKind.PlungeDepth => (1 + level) + " блоков",
                UpgradeKind.ImpactPower => "+" + Progression.Number(level * game.Economy.Config.impactStep * 0.5) + " урона сильного удара",
                UpgradeKind.Shrapnel => Progression.Number(level * game.Economy.Config.shrapnelStep * 0.5) + " соседу",
                _ => Progression.Number(level * Progression.ChanceStep(key.Kind,game.Economy.Config) * 100) + "%"
            };
        }
        sealed class UpgradeCard
        {
            readonly GameHud hud; readonly UpgradeKey key;
            readonly SkillLock skillLock;
            readonly VisualElement box, resources; readonly Label heading, effect, cost, reason; readonly Button buy;
            public UpgradeCard(GameHud owner, VisualElement parent, UpgradeKey upgrade)
            {
                hud = owner; key = upgrade; box = Box(parent, "upgrade-card inset"); box.name = "upgrade-" + key.Kind + "-" + key.Tier;
                heading = hud.BoldText(box, "", "upgrade-title");
                Text(box, Progression.Description(key.Kind,hud.game.Economy.Config), "description"); effect = hud.BoldText(box, "", "upgrade-effect");
                cost = Text(box, "", "upgrade-price"); resources = Box(box, "recipe-resources"); reason = Text(box, "", "upgrade-reason");
                buy = hud.Button(box, "УЛУЧШИТЬ", () =>
                {
                    if (Time.unscaledTime < hud.nextTrade) return;
                    hud.nextTrade = Time.unscaledTime + 0.25f; hud.game.Buy(key);
                }, "stone-button secondary"); buy.name = "buy-" + key.Kind + "-" + key.Tier;
                hud.Button(box,"ВЫБРАТЬ ЦЕЛЬЮ",()=>hud.PinGoal(GoalKind.Upgrade,key.Index),"stone-button secondary goal-button").name="goal-upgrade-"+key.Index;
                skillLock=new SkillLock(hud,box,key.Index);
            }
            public void Refresh()
            {
                int level = hud.game.Economy.Profile.Level(key), max = Progression.MaxLevel(key.Kind);
                heading.text = Progression.Title(key.Kind) + $" · {level}/{max}";
                effect.text = hud.UpgradeEffect(key, level) + (level < max ? " → " + hud.UpgradeEffect(key, level + 1) : " · МАКСИМУМ");
                box.EnableInClassList("pinned", hud.game.Economy.Profile.goalKind==GoalKind.Upgrade && hud.pinned.Index == key.Index);
                skillLock.Refresh();
                Display(buy,ProfileRules.Revealed(hud.game.Economy.Profile,key.Index));
                bool available = hud.game.Economy.CanBuy(key, out string why) && hud.game.CanTrade;
                buy.SetEnabled(available); buy.text = level>=max ? "ПРОКАЧАНО" : available ? "УЛУЧШИТЬ" : "НЕДОСТУПНО";
                buy.EnableInClassList("buy-ready",available);buy.EnableInClassList("buy-blocked",!available && level<max);
                resources.Clear();
                if (level >= max) { cost.text = ""; reason.text = "Максимальный уровень"; return; }
                var price = Progression.Cost(key, level + 1,hud.game.Economy.Config);
                cost.text = $"Цена: {Exact(price.Coins)} монет · есть {Short(hud.game.Wallet)}";
                cost.EnableInClassList("cost-enough",hud.game.Wallet>=price.Coins);cost.EnableInClassList("cost-short",hud.game.Wallet<price.Coins);
                for(int i=0;i<ResourceInventory.SlotCount;i++)
                {
                    if(price.Resources[i]==0)continue;
                    var kind=(BlockKind)i;bool enough=hud.game.Inventory[kind]>=price.Resources[i];
                    var item=Box(resources,"recipe-item");item.tooltip=GameBalance.BlockName(kind);
                    Icon(item,PixelArt.ResourceIcon(kind),"recipe-icon");
                    var amount=hud.BoldText(item,$"{Short(hud.game.Inventory[kind])} / {Exact(price.Resources[i])}",enough?"recipe-amount cost-enough":"recipe-amount cost-short");amount.name="required-"+kind;
                }
                reason.text = available ? "Нажми на карточку, чтобы оставить её целью в рюкзаке." : why;
                reason.EnableInClassList("missing", !available);
            }
        }
    }
}
