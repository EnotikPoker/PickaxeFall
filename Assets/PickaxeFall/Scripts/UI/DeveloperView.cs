#if PICKAXEFALL_DEVTOOLS
using System;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        int developerTaps;float developerTapUntil;bool developerResume;
        void RegisterDeveloperAccess(VisualElement version)
        {
            version.RegisterCallback<ClickEvent>(_=>{
                if(Time.unscaledTime>developerTapUntil)developerTaps=0;developerTapUntil=Time.unscaledTime+3;
                if(++developerTaps<5)return;developerTaps=0;
                developerResume=resumeAfterSettings;resumeAfterSettings=false;
                if(game.DeveloperUnlocked){OpenDeveloper();return;}
                var card=OpenModal("Доступ разработчика");var input=new TextField{isPasswordField=true,maxLength=64,name="developer-code"};card.Add(input);
                Button(card,"ОТКРЫТЬ",()=>{if(game.DeveloperAuthenticate(input.value)){input.value="";OpenDeveloper();}else Toast("Код не подошёл.");},"stone-button compact-button");
                Button(card,"ОТМЕНА",DrawSettings,"stone-button compact-button");
            });
        }
        void OpenDeveloper()
        {
            if(!game.DeveloperUnlocked)return;
            if(game.Phase==RunPhase.Mining&&!game.Paused)game.TogglePause();
            var card=OpenModal("PickaxeFall Dev");card.AddToClassList("developer-dialog");Text(card,"Отдельный тестовый профиль. Изменения навыков действуют со следующего спуска.","description");
            var amount=new LongField("Количество"){value=1000};card.Add(amount);
            Button(card,"ДОБАВИТЬ МОНЕТЫ",()=>DevResult(game.DeveloperEdit(p=>p.coins=checked(p.coins+Math.Max(0,amount.value)))),"stone-button compact-button");
            var kinds=new System.Collections.Generic.List<string>();foreach(var k in ResourceInventory.Materials)kinds.Add(GameBalance.BlockName(k));
            var resource=new DropdownField("Ресурс",kinds,0);card.Add(resource);
            Button(card,"ДОБАВИТЬ РЕСУРС",()=>{var kind=ResourceInventory.Materials[resource.index];DevResult(game.DeveloperEdit(p=>{p.resources[(int)kind]=checked(p.resources[(int)kind]+Math.Max(0,amount.value));p.discovered[(int)kind]=true;}));},"stone-button compact-button");
            var profile=new IntegerField("Уровень профиля"){value=game.Economy.Profile.profileLevel};card.Add(profile);
            Button(card,"ПРИМЕНИТЬ УРОВЕНЬ",()=>DevResult(game.DeveloperEdit(p=>p.profileLevel=Mathf.Clamp(profile.value,1,5))),"stone-button compact-button");
            var skillNames=new System.Collections.Generic.List<string>();foreach(var k in Progression.Keys)skillNames.Add(Progression.Title(k.Kind)+(k.Kind<=UpgradeKind.Durability?" · "+game.ShopBalance.pickaxes[k.Tier].title:""));
            var skill=new DropdownField("Навык",skillNames,0);card.Add(skill);var level=new IntegerField("Ступень навыка"){value=0};card.Add(level);
            Button(card,"ПРИМЕНИТЬ НАВЫК",()=>{var key=Progression.Keys[skill.index];DevResult(game.DeveloperEdit(p=>{p.upgrades[key.Index]=Mathf.Clamp(level.value,0,Progression.MaxLevel(key.Kind));p.revealed[key.RevealIndex]=true;}));},"stone-button compact-button");
            Button(card,"ОТКРЫТЬ КАРТЫ И ПРИЁМЫ",()=>DevResult(game.DeveloperEdit(p=>{p.bridgeStone=1000;p.bridgeIron=50;p.portalObsidian=10;p.dashUnlocked=p.plungeUnlocked=true;Array.Fill(p.revealed,true);})),"stone-button compact-button");
            var map=new DropdownField("Карта",new(){"Шахта","Пустыня","Ад"},0);card.Add(map);
            var tier=new DropdownField("Кирка",new(){"Дерево","Камень","Золото","Железо","Алмаз"},0);card.Add(tier);
            var count=new IntegerField("Кирок"){value=1};var seed=new IntegerField("Сид"){value=8127};var depth=new IntegerField("Глубина"){value=0};card.Add(count);card.Add(seed);card.Add(depth);
            Button(card,"НОВЫЙ ТЕСТОВЫЙ СПУСК",()=>{CloseModal();game.DeveloperRun((MapId)map.index,tier.index,Mathf.Clamp(count.value,1,5),seed.value,depth.value);},"stone-button compact-button");
            DevToggle(card,"Бесконечная прочность",game.DeveloperInfiniteWear,v=>game.DeveloperInfiniteWear=v);DevToggle(card,"Бесконечная энергия",game.DeveloperInfiniteEnergy,v=>game.DeveloperInfiniteEnergy=v);DevToggle(card,"Коллайдеры и контакты",game.DeveloperColliders,v=>game.DeveloperColliders=v);
            Button(card,"ВОССТАНОВИТЬ ПРОЧНОСТЬ И ЭНЕРГИЮ",game.DeveloperRefill,"stone-button compact-button");
            Button(card,"СОХРАНИТЬ КОПИЮ ПРОФИЛЯ",()=>DevResult(game.DeveloperRestore(true)),"stone-button compact-button");
            Button(card,"ВОССТАНОВИТЬ КОПИЮ",()=>DevConfirm("Восстановить тестовый профиль?",()=>game.DeveloperRestore(false)),"stone-button compact-button");
            Button(card,"ЧИСТЫЙ ТЕСТОВЫЙ ПРОФИЛЬ",()=>DevConfirm("Удалить тестовый прогресс?",()=>{game.ResetProgress();return true;}),"stone-button compact-button buy-blocked");
            Button(card,"ЗАКРЫТЬ",()=>{CloseModal();if(developerResume&&game.Paused)game.TogglePause();developerResume=false;},"stone-button compact-button");
        }
        void DevConfirm(string title,Func<bool> action){var card=OpenModal(title);Button(card,"ПОДТВЕРДИТЬ",()=>{DevResult(action());OpenDeveloper();},"stone-button compact-button buy-blocked");Button(card,"ОТМЕНА",OpenDeveloper,"stone-button compact-button");}
        void DevResult(bool success)=>Toast(success?"ГОТОВО":game.Economy.LastError??"Не удалось выполнить действие.");
        void DevToggle(VisualElement card,string label,bool value,Action<bool> change){var toggle=new Toggle(label){value=value};card.Add(toggle);toggle.RegisterValueChangedCallback(e=>change(e.newValue));}
    }
}
#endif
