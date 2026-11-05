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
                var card=OpenModal("Новый старт · "+GameVersion.Name);
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
            var p=game.Economy.Profile;var card=OpenModal("Профиль");card.AddToClassList("profile-dialog");
            var banner=Box(card,"profile-banner");
            BoldText(banner,p.profileLevel.ToString(),"profile-rank");
            var headline=Box(banner,"profile-headline");
            BoldText(headline,"УРОВЕНЬ "+p.profileLevel,"profile-heading");
            Text(headline,p.profileLevel<5?"Следующая ступень — "+(p.profileLevel+1):"Все ветви доступны","skill-summary");
            if(p.profileLevel<5)
            {
                int gate=p.profileLevel-1,done=0,total=1;long coins=ProfileRules.LevelCoins[gate];
                foreach(var material in ResourceInventory.Materials){int i=(int)material;if(ProfileRules.MiningRequirements[gate][i]>0){total++;if(p.lifetimeMined[i]>=ProfileRules.MiningRequirements[gate][i])done++;}}
                foreach(var r in ProfileRules.Skills[gate]){total++;if(r.Met(p))done++;}
                if(p.coins>=coins)done++;
                var progressRow=Box(card,"profile-progress-label");
                BoldText(progressRow,"УСЛОВИЯ","section-label");Text(progressRow,done+" / "+total,"skill-summary");
                var track=Box(card,"profile-progress-track");Box(track,"profile-progress-fill").style.width=Length.Percent(100f*done/total);
                BoldText(card,"ДОБЫЧА ЗА ВСЁ ВРЕМЯ","profile-section");
                var materials=Box(card,"profile-requirements");
                foreach(var material in ResourceInventory.Materials)
                {
                    int i=(int)material;long required=ProfileRules.MiningRequirements[gate][i];if(required==0)continue;
                    var tile=Box(materials,"profile-resource");
                    Icon(tile,PixelArt.ResourceIcon(material),"profile-resource-icon");
                    var words=Box(tile,"profile-resource-copy");Text(words,GameBalance.BlockName(material),"profile-resource-name");
                    BoldText(words,Short(p.lifetimeMined[i])+" / "+Short(required),p.lifetimeMined[i]>=required?"profile-resource-count cost-enough":"profile-resource-count cost-short");
                }
                Text(card,"Материалы не расходуются.","profile-note");
                BoldText(card,"ПРОКАЧКА","profile-section");
                foreach(var r in ProfileRules.Skills[gate])
                {
                    int current=0;for(int t=0;t<(r.AnyTier?5:1);t++)current=Math.Max(current,p.Level(new(r.Kind,t)));
                    var requirement=r;
                    var row=Button(card,"",()=>JumpToSkill(new(requirement.Kind)),"profile-skill-row");row.name="profile-requirement-"+r.Kind;
                    Text(row,Progression.Title(r.Kind)+(r.AnyTier?" · любой тип":""),"profile-skill-name");
                    BoldText(row,current+" / "+r.Level,r.Met(p)?"profile-skill-count cost-enough":"profile-skill-count cost-short");
                }
                var price=Box(card,"profile-coin-row");Text(price,"МОНЕТЫ","profile-section");
                BoldText(price,Short(p.coins)+" / "+Exact(coins),p.coins>=coins?"profile-skill-count cost-enough":"profile-skill-count cost-short");
                var unlocks=new Foldout {text="Откроется на уровне "+(p.profileLevel+1),value=false};unlocks.AddToClassList("profile-unlocks");card.Add(unlocks);
                var chips=Box(unlocks,"profile-unlock-chips");
                for(int i=10;i<27;i++)if(ProfileRules.RequiredLevel(i)==p.profileLevel+1)
                    Text(chips,i==25?"Рывок":i==26?"Пробой":Progression.Title(ProfileRules.Key(i).Kind),"profile-unlock-chip");
                bool can=game.Economy.CanLevelUp(out _);
                var buy=Button(card,"ПОВЫСИТЬ ДО "+(p.profileLevel+1),()=>CommitProfile(game.Economy.TryLevelUp,()=>{OpenProfile();Toast("НОВЫЙ УРОВЕНЬ ПРОФИЛЯ!");}),"stone-button compact-button "+(can?"buy-ready":"buy-blocked"));
                buy.name="profile-level-up";buy.SetEnabled(can);
            }
            else Text(card,"Максимальный уровень профиля. Продолжай улучшать навыки и готовить новые маршруты.","description");
            var footer=Box(card,"dialog-footer");
            Button(footer,"СТАТИСТИКА",OpenMiningStatistics,"stone-button compact-button").name="profile-statistics";
            Button(footer,"ЗАКРЫТЬ",()=>CloseModal(),"stone-button compact-button");
        }
        void OpenMiningStatistics()
        {
            var card=OpenModal("Накопленная добыча");card.AddToClassList("profile-dialog");
            foreach(var material in ResourceInventory.Materials)RecipeLine(card,material,game.Economy.Profile.lifetimeMined[(int)material],0);
            Button(card,"НАЗАД К ПРОФИЛЮ",OpenProfile,"stone-button compact-button");
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

    }
}
