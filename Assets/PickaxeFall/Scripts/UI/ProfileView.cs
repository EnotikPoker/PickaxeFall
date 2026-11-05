using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        Button profileButton;
        int notifiedProfileLevel;
        void CommitProfile(Func<bool> operation,Action success=null)
        {
            if(!game.CanTrade)return;
            if(!operation()){Toast(game.Economy.LastError);return;}
            Refresh();success?.Invoke();
        }
        void TickProfile()
        {
            var p=game.Economy.Profile;
            Display(profileButton,game.CanTrade);
            profileButton.text="УР. "+p.profileLevel;
            bool ready=game.Economy.CanLevelUp(out _);
            profileButton.EnableInClassList("level-ready",ready);
            if(p.resetNotice&&game.ReadyToPresent&&!ModalOpen)
            {
                var card=OpenModal("Новый старт · 0.6.0");
                Text(card,"В этой версии прогресс всех игроков начинается заново: появились уровни профиля, новые условия открытия навыков и строительство карт. Старое сохранение оставлено в резервной копии. Настройки сохранены.","description");
                Button(card,"НАЧАТЬ НОВОЕ ПРИКЛЮЧЕНИЕ",()=>{
                    if(!game.Economy.TryAcknowledgeReset()){Toast(game.Economy.LastError);return;}
                    CloseModal();Refresh();
                },"stone-button primary").name="acknowledge-season-reset";
            }
            if(ready&&game.CanTrade&&!p.resetNotice&&notifiedProfileLevel!=p.profileLevel)
            {notifiedProfileLevel=p.profileLevel;Toast("ДОСТУПЕН УРОВЕНЬ ПРОФИЛЯ "+(p.profileLevel+1));}
        }
        void OpenProfile()
        {
            if(!game.CanTrade)return;
            var p=game.Economy.Profile;var card=OpenModal("Профиль · уровень "+p.profileLevel);
            Text(card,"Накопленная добыча не расходуется. Продажа и строительство не уменьшают её. Для повышения нужны также навыки и монеты.","description");
            if(p.profileLevel<5)
            {
                int gate=p.profileLevel-1;
                BoldText(card,"Следующий уровень: "+(p.profileLevel+1),"section-label");
                BoldText(card,"Добыто за всё время","section-label");
                foreach(var material in ResourceInventory.Materials)
                {
                    int i=(int)material;long required=ProfileRules.MiningRequirements[gate][i];if(required==0)continue;
                    RecipeLine(card,material,p.lifetimeMined[i],required);
                }
                BoldText(card,"Обязательные навыки","section-label");
                foreach(var r in ProfileRules.Skills[gate])
                {
                    int current=0;for(int t=0;t<(r.AnyTier?5:1);t++)current=Math.Max(current,p.Level(new(r.Kind,t)));
                    var row=Box(card,"requirement-row");
                    BoldText(row,Progression.Title(r.Kind)+(r.AnyTier?" · любой тип":"")+": "+current+" / "+r.Level,r.Met(p)?"description cost-enough":"description cost-short");
                    var requirement=r;
                    Button(row,"К НАВЫКУ",()=>JumpToSkill(new(requirement.Kind)),"stone-button small-button").name="profile-requirement-"+r.Kind;
                }
                long coins=ProfileRules.LevelCoins[gate];
                BoldText(card,"Цена перехода: "+Exact(coins)+" монет · есть "+Short(p.coins),p.coins>=coins?"description cost-enough":"description cost-short");
                bool can=game.Economy.CanLevelUp(out string reason);
                var buy=Button(card,"ПОВЫСИТЬ ДО "+(p.profileLevel+1),()=>CommitProfile(game.Economy.TryLevelUp,()=>{OpenProfile();Toast("НОВЫЙ УРОВЕНЬ ПРОФИЛЯ!");}),"stone-button primary "+(can?"buy-ready":"buy-blocked"));
                buy.name="profile-level-up";buy.SetEnabled(can);
                if(!can)Text(card,reason,"description cost-short");
                BoldText(card,"Откроется на следующем уровне","section-label");
                for(int i=10;i<27;i++)if(ProfileRules.RequiredLevel(i)==p.profileLevel+1)
                    Text(card,"• "+(i==25?"Рывок":i==26?"Пробой":Progression.Title(ProfileRules.Key(i).Kind)),"description");
            }
            else BoldText(card,"Максимальный уровень профиля достигнут!","upgrade-effect cost-enough");
            BoldText(card,"Вся накопленная добыча","section-label");
            foreach(var material in ResourceInventory.Materials)RecipeLine(card,material,p.lifetimeMined[(int)material],0);
            Button(card,"ЗАКРЫТЬ",()=>CloseModal(),"stone-button secondary");
        }
        void RecipeLine(VisualElement parent,BlockKind kind,long have,long need)
        {
            var row=Box(parent,"recipe-item profile-material");
            Icon(row,PixelArt.ResourceIcon(kind),"recipe-icon");
            BoldText(row,GameBalance.BlockName(kind)+" · "+Exact(have)+(need>0?" / "+Exact(need):""),need==0||have>=need?"recipe-amount cost-enough":"recipe-amount cost-short");
        }
        void JumpToSkill(UpgradeKey key)
        {
            CloseModal();CurrentTab=HubTab.Shop;shopTier=key.Tier;
            shopCategory=key.Kind<=UpgradeKind.Durability?0:
                key.Kind==UpgradeKind.Fortune||key.Kind==UpgradeKind.TntFind||key.Kind==UpgradeKind.Careful||key.Kind==UpgradeKind.ImpactPower||key.Kind==UpgradeKind.Shrapnel?1:
                key.Kind>=UpgradeKind.EnergyCapacity&&key.Kind<=UpgradeKind.PlungeDepth?3:2;
            RebuildShop();Refresh();
            root.schedule.Execute(()=>root.Q<ScrollView>("shop-page-scroll")?.ScrollTo(root.Q("upgrade-"+key.Kind+"-"+key.Tier))).ExecuteLater(50);
        }
        void PinGoal(GoalKind kind,int index=0)=>CommitProfile(()=>game.Economy.TrySetGoal(kind,index),()=>Toast("ЦЕЛЬ СОХРАНЕНА · "+game.Economy.GoalTitle));

        sealed class SkillLock
        {
            readonly GameHud hud;readonly int index;readonly VisualElement panel,lockArt,left,right;readonly Label label;readonly Button release;
            bool animating;
            public SkillLock(GameHud owner,VisualElement card,int skill)
            {
                hud=owner;index=skill;panel=Box(card,"skill-lock");panel.name="lock-"+skill;
                left=Box(panel,"chain chain-left");right=Box(panel,"chain chain-right");
                for(int i=0;i<6;i++){Box(left,"chain-link");Box(right,"chain-link");}
                lockArt=Box(panel,"lock-art");Box(lockArt,"lock-shackle");Box(lockArt,"lock-body");Box(lockArt,"lock-keyhole");
                label=hud.BoldText(panel,"","lock-label");
                release=hud.Button(panel,"",Reveal,"stone-button lock-button");release.name="reveal-"+skill;
                card.RegisterCallback<ClickEvent>(e=>{
                    // Explicit buttons retain their own action (especially pinning a future goal).
                    var target=e.target as VisualElement;
                    if(target is Button || target?.GetFirstAncestorOfType<Button>()!=null)return;
                    var profile=hud.game.Economy.Profile;
                    if(profile.profileLevel>=ProfileRules.RequiredLevel(index)&&!ProfileRules.Revealed(profile,index))Reveal();
                });
            }
            public void Refresh()
            {
                var p=hud.game.Economy.Profile;bool revealed=ProfileRules.Revealed(p,index),eligible=p.profileLevel>=ProfileRules.RequiredLevel(index);
                Display(panel,!revealed||animating);panel.EnableInClassList("lock-ready",eligible);
                label.text=eligible?"ВЕТВЬ ДОСТУПНА":"УРОВЕНЬ ПРОФИЛЯ "+ProfileRules.RequiredLevel(index);
                release.text=eligible?"СНЯТЬ ЗАМОК · БЕСПЛАТНО":"ОТКРОЕТСЯ НА УРОВНЕ "+ProfileRules.RequiredLevel(index);
                release.SetEnabled(eligible&&!revealed&&hud.game.CanTrade);
            }
            void Reveal()
            {
                if(!hud.game.CanTrade||animating)return;
                if(!hud.game.Economy.TryReveal(index)){hud.Toast(hud.game.Economy.LastError);return;}
                animating=true;hud.Refresh();
                float started=Time.unscaledTime,duration=hud.game.Settings.ReducedEffects?.15f:.65f;
                IVisualElementScheduledItem timer=null;
                timer=panel.schedule.Execute(()=>{
                    float t=Mathf.Clamp01((Time.unscaledTime-started)/duration);
                    lockArt.style.translate=new Translate(0,t*t*100,0);
                    left.style.translate=new Translate(-t*90,t*t*60,0);right.style.translate=new Translate(t*90,t*t*60,0);
                    panel.style.opacity=1-t;
                    if(t>=1){timer.Pause();animating=false;Display(panel,false);}
                }).Every(16);
            }
        }
    }
}
