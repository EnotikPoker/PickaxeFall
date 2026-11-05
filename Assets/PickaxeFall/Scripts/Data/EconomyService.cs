using System;

namespace PickaxeFall
{
    public sealed class EconomyService
    {
        readonly Action<PlayerProfile> persist, resetPersist;
        public PlayerProfile Profile { get; private set; }
        public string LastError { get; private set; }
        public ProgressionBalance Config { get; }
        public EconomyService(PlayerProfile profile, Action<PlayerProfile> save = null, Action<PlayerProfile> reset = null, ProgressionBalance config=null) { Config=config??Progression.Default;Config.Validate(); profile.Validate(); Profile = profile; persist = save;resetPersist=reset??save; }

        bool Commit(PlayerProfile next)
        {
            try { next.Validate(); persist?.Invoke(next); Profile = next; LastError = null; return true; }
            catch (Exception) { LastError = "Не удалось сохранить. Покупка или продажа отменена."; return false; }
        }
        public bool TryResetProgress()
        {
            var empty=new PlayerProfile();
            try { resetPersist?.Invoke(empty);Profile=empty;LastError=null;return true; }
            catch(Exception){LastError="Не удалось сохранить сброс. Прогресс не изменён.";return false;}
        }
        public void Save() => persist?.Invoke(Profile);
        public bool Collect(BlockKind kind, long quantity = 1)
        {
            kind=ResourceInventory.DropKind(kind);
            if (!ResourceInventory.IsResource(kind) || quantity < 1) return false;
            try { Profile.resources[(int)kind] = checked(Profile.resources[(int)kind] + quantity); return true; }
            catch (OverflowException) { LastError = "Слишком много ресурсов."; return false; }
        }
        public bool CanBuy(UpgradeKey key, out string reason)
        {
            reason = null;
            if (!key.IsValid) { reason = "Неизвестное улучшение"; return false; }
            int level = Profile.Level(key);
            if (level >= Progression.MaxLevel(key.Kind)) { reason = "Максимальный уровень"; return false; }
            if(key.Kind==UpgradeKind.DashForce && !Profile.dashUnlocked || key.Kind==UpgradeKind.PlungeDepth && !Profile.plungeUnlocked ||
                key.Kind>=UpgradeKind.EnergyCapacity && key.Kind<=UpgradeKind.EnergyEfficiency && !Profile.dashUnlocked && !Profile.plungeUnlocked)
            {reason="Сначала открой нужный приём в разделе «Приёмы»";return false;}
            if (key.Kind == UpgradeKind.TeamSize && Profile.Level(new(UpgradeKind.TeamChance)) == 0)
            { reason = "Сначала открой «Отряд шахтёров»"; return false; }
            var cost = Progression.Cost(key, level + 1,Config);
            if (Profile.coins < cost.Coins) { reason = "Не хватает монет"; return false; }
            for (int i = 0; i < ResourceInventory.SlotCount; i++) if (Profile.resources[i] < cost.Resources[i]) { reason = "Не хватает ресурсов"; return false; }
            return true;
        }
        public bool AbilityUnlocked(AbilityKind kind)=>kind==AbilityKind.Dash?Profile.dashUnlocked:Profile.plungeUnlocked;
        public bool CanUnlock(AbilityKind kind,out string reason)
        {
            reason=null;
            if(kind!=AbilityKind.Dash && kind!=AbilityKind.Plunge){reason="Неизвестная способность";return false;}
            if(AbilityUnlocked(kind)){reason="Способность уже открыта";return false;}
            var cost=Progression.AbilityCost(kind,Config);
            if(Profile.coins<cost.Coins){reason="Не хватает монет";return false;}
            foreach(var material in ResourceInventory.Materials)if(Profile.resources[(int)material]<cost.Resources[(int)material]){reason="Не хватает ресурсов";return false;}
            return true;
        }
        public bool TryUnlock(AbilityKind kind)
        {
            if(!CanUnlock(kind,out string reason)){LastError=reason;return false;}
            var next=Profile.Copy();var cost=Progression.AbilityCost(kind,Config);next.coins-=cost.Coins;
            foreach(var material in ResourceInventory.Materials)next.resources[(int)material]-=cost.Resources[(int)material];
            if(kind==AbilityKind.Dash)next.dashUnlocked=true;else next.plungeUnlocked=true;
            return Commit(next);
        }
        public bool TryBuy(UpgradeKey key)
        {
            if (!CanBuy(key, out string reason)) { LastError = reason; return false; }
            var next = Profile.Copy(); var cost = Progression.Cost(key, next.Level(key) + 1,Config);
            next.coins -= cost.Coins;
            for (int i = 0; i < ResourceInventory.SlotCount; i++) next.resources[i] -= cost.Resources[i];
            next.upgrades[key.Index]++;
            return Commit(next);
        }
        public bool TrySell(BlockKind kind, long quantity)
        {
            if (!ResourceInventory.IsResource(kind) || quantity < 1 || quantity > Profile.resources[(int)kind])
            { LastError = "Укажи целое количество от 1 до имеющегося."; return false; }
            var next = Profile.Copy();
            try { next.coins = checked(next.coins + checked(quantity * Config.sellPrices[(int)kind])); }
            catch (OverflowException) { LastError = "Слишком большая сумма."; return false; }
            next.resources[(int)kind] -= quantity; return Commit(next);
        }
        public long InventoryValue()
        {
            long sum = 0;
            checked { for (int i = 0; i < ResourceInventory.SlotCount; i++) sum += Profile.resources[i] * Config.sellPrices[i]; }
            return sum;
        }
        public bool TryInventoryValue(out long value)
        {
            try { value = InventoryValue(); return true; }
            catch (OverflowException) { value = 0; return false; }
        }
        public bool TrySellAll()
        {
            var next = Profile.Copy();
            try { long value = InventoryValue(); if (value == 0) { LastError = "Рюкзак пуст."; return false; } next.coins = checked(next.coins + value); }
            catch (OverflowException) { LastError = "Слишком большая сумма."; return false; }
            Array.Clear(next.resources, 0, next.resources.Length); return Commit(next);
        }
    }
}
