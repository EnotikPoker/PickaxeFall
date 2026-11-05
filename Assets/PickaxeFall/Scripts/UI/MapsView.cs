using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        void OpenMaps(int page,float oldStone=-1,float oldIron=-1,float oldObsidian=-1)
        {
            if(!game.CanTrade)return;
            string[] names={"Шахта","Пустыня","Ад"};var card=OpenModal("Карты · "+names[page]);card.AddToClassList("map-card");
            var navigation=Box(card,"map-navigation");
            for(int i=0;i<3;i++){int tab=i;Button(navigation,names[i].ToUpperInvariant(),()=>OpenMaps(tab),"stone-button category"+(page==i?" active":"")).name="map-page-"+i;}
            var p=game.Economy.Profile;
            var scene=Box(card,"construction-scene");scene.name="construction-scene";
            DrawConstruction(scene,page,oldStone<0?p.bridgeStone:oldStone,oldIron<0?p.bridgeIron:oldIron,oldObsidian<0?p.portalObsidian:oldObsidian);
            if(page==0)
            {
                BoldText(card,"Бездонная шахта","section-label");
                Text(card,"Доступна сейчас. Каждый спуск — новая процедурная карта.","description");
                Button(card,"ВЕРНУТЬСЯ В ШАХТУ",()=>{CloseModal();SelectTab(HubTab.Mine);},"stone-button primary").name="map-enter-mine";
            }
            else
            {
                var kind=page==1?GoalKind.Bridge:GoalKind.Portal;
                bool complete=ProfileRules.Complete(p,kind);
                BoldText(card,complete?"ПОСТРОЕНО":"СТРОИТЕЛЬСТВО · "+(page==1?"МОСТ":"ПОРТАЛ"),complete?"upgrade-effect cost-enough":"section-label");
                Text(card,complete?"Карта появится в следующем обновлении. Постройка сохранена!":"Вноси материалы частями. Они расходуются на строительство; монеты не нужны. Сама карта появится в следующем обновлении.","description");
                if(page==1){RecipeLine(card,BlockKind.Stone,p.bridgeStone,1000);RecipeLine(card,BlockKind.Iron,p.bridgeIron,50);}
                else RecipeLine(card,BlockKind.Obsidian,p.portalObsidian,10);
                if(!complete)
                {
                    long total=0;foreach(long n in game.Economy.ConstructionQuote(kind))total+=n;
                    var contribute=Button(card,"ВНЕСТИ РЕСУРСЫ",()=>ConfirmContribution(page),"stone-button primary "+(total>0?"buy-ready":"buy-blocked"));
                    contribute.name="contribute-"+kind;contribute.SetEnabled(total>0);
                    if(total==0)Text(card,"В рюкзаке пока нет нужных материалов.","description cost-short");
                    Button(card,"ВЫБРАТЬ ЦЕЛЬЮ",()=>PinGoal(kind),"stone-button secondary goal-button").name="goal-map-"+page;
                }
                else Text(card,"ВХОД СКОРО","stone-button secondary map-coming-soon");
            }
            Button(card,"ЗАКРЫТЬ",()=>CloseModal(),"stone-button secondary");
        }
        void ConfirmContribution(int page)
        {
            if(!game.CanTrade)return;
            var kind=page==1?GoalKind.Bridge:GoalKind.Portal;var amounts=game.Economy.ConstructionQuote(kind);
            long total=0;foreach(long n in amounts)total+=n;if(total==0)return;
            var card=OpenModal("Внести материалы?");
            Text(card,"Будут списаны из рюкзака и навсегда вложены в "+(page==1?"мост:":"портал:"),"description");
            foreach(var material in ResourceInventory.Materials)if(amounts[(int)material]>0)RecipeLine(card,material,amounts[(int)material],0);
            Button(card,"ПОДТВЕРДИТЬ СТРОИТЕЛЬСТВО",()=>{
                if(Time.unscaledTime<nextTrade)return;nextTrade=Time.unscaledTime+.25f;
                var before=game.Economy.Profile.Copy();
                CommitProfile(()=>game.Economy.TryContribute(kind),()=>{
                    OpenMaps(page,before.bridgeStone,before.bridgeIron,before.portalObsidian);
                    var art=root.Q("construction-scene");FlyConstructionMaterials(art,amounts);
                    if(ProfileRules.Complete(game.Economy.Profile,kind))Toast(page==1?"МОСТ ПОСТРОЕН!":"ПОРТАЛ ПОСТРОЕН!");
                });
            },"stone-button primary").name="confirm-construction";
            Button(card,"ОТМЕНА",()=>OpenMaps(page),"stone-button secondary");
        }
        static VisualElement ScenePiece(VisualElement parent,float x,float y,float w,float h,Color color,Sprite sprite=null)
        {
            var item=new VisualElement();item.pickingMode=PickingMode.Ignore;
            item.style.position=Position.Absolute;item.style.left=Length.Percent(x);item.style.top=Length.Percent(y);item.style.width=Length.Percent(w);item.style.height=Length.Percent(h);
            item.style.backgroundColor=color;if(sprite)item.style.backgroundImage=Background.FromSprite(sprite);
            parent.Add(item);return item;
        }
        void DrawConstruction(VisualElement art,int map,float oldStone,float oldIron,float oldObsidian)
        {
            Color sky=map==2?new Color32(38,15,37,255):new Color32(104,181,215,255);
            art.style.backgroundColor=sky;
            if(map!=2)
            {
                ScenePiece(art,76,10,10,17,new Color32(255,227,126,255));
                ScenePiece(art,7,16,23,7,new Color32(227,242,241,255));ScenePiece(art,37,27,17,5,new Color32(227,242,241,255));
                ScenePiece(art,0,58,24,42,new Color32(142,99,61,255));ScenePiece(art,78,58,22,42,new Color32(183,136,74,255));
                ScenePiece(art,0,53,24,7,map==0?new Color32(95,151,58,255):new Color32(228,195,115,255));
                ScenePiece(art,78,53,22,7,new Color32(228,195,115,255));
                ScenePiece(art,24,82,54,18,new Color32(31,73,88,255));
                if(map==0){ScenePiece(art,40,27,19,31,Color.white,PixelArt.Pickaxe(1));return;}
            }
            else
            {
                for(int i=0;i<8;i++)ScenePiece(art,i*14,65-i%3*9,15,45,new Color32((byte)(83+i%3*10),30,36,255));
                ScenePiece(art,0,89,100,11,new Color32(227,79,26,255));
                ScenePiece(art,8,92,22,3,new Color32(255,178,46,255));ScenePiece(art,72,90,18,3,new Color32(255,178,46,255));
                ScenePiece(art,32,80,37,9,new Color32(55,44,53,255));
            }
            var deck=new List<VisualElement>();var supports=new List<VisualElement>();VisualElement glow=null;
            if(map==1)
            {
                for(int i=0;i<10;i++)
                {
                    deck.Add(ScenePiece(art,24+i*5.4f,53,5.2f,7,Color.white,PixelArt.ResourceIcon(BlockKind.Stone)));
                    supports.Add(ScenePiece(art,24+i*5.4f,44,1.5f,20,new Color32(184,195,193,255)));
                }
            }
            else
            {
                glow=ScenePiece(art,42,27,17,47,new Color32(152,75,215,190));
                Vector2[] positions={new(42,74),new(51,74),new(33,62),new(33,48),new(33,34),new(42,20),new(51,20),new(60,34),new(60,48),new(60,62)};
                foreach(var point in positions)deck.Add(ScenePiece(art,point.x,point.y,9,14,Color.white,PixelArt.ResourceIcon(BlockKind.Obsidian)));
            }
            var profile=game.Economy.Profile;
            float started=Time.unscaledTime,duration=game.Settings.ReducedEffects?.15f:1.0f;
            bool animate=map==1?(oldStone!=profile.bridgeStone||oldIron!=profile.bridgeIron):oldObsidian!=profile.portalObsidian;
            void Draw(float t)
            {
                float primary=map==1?Mathf.Lerp(oldStone,profile.bridgeStone,t)/1000:Mathf.Lerp(oldObsidian,profile.portalObsidian,t)/10;
                float secondary=Mathf.Lerp(oldIron,profile.bridgeIron,t)/50;
                for(int i=0;i<deck.Count;i++)deck[i].style.opacity=Mathf.Clamp01(primary*10-i);
                for(int i=0;i<supports.Count;i++)supports[i].style.opacity=Mathf.Clamp01(secondary*10-i);
                if(glow!=null)glow.style.opacity=primary>=1?(.75f+.25f*Mathf.Sin((Time.unscaledTime-started)*3)):0;
            }
            Draw(animate?0:1);
            if(animate)
            {
                IVisualElementScheduledItem task=null;
                task=art.schedule.Execute(()=>{float t=Mathf.Clamp01((Time.unscaledTime-started)/duration);Draw(t);if(t>=1)task.Pause();}).Every(16);
            }
            if(glow!=null&&profile.portalObsidian==10&&!game.Settings.ReducedEffects)
                art.schedule.Execute(()=>{if(Time.unscaledTime-started>duration)Draw(1);}).Every(80);
        }
        void FlyConstructionMaterials(VisualElement art,long[] amounts)
        {
            if(art==null||game.Settings.ReducedEffects)return;
            int sequence=0;
            foreach(var kind in ResourceInventory.Materials)
            {
                if(amounts[(int)kind]==0)continue;
                var item=Box(art,"construction-flight");Icon(item,PixelArt.ResourceIcon(kind),"recipe-icon");
                BoldText(item,"+"+Short(amounts[(int)kind]),"recipe-amount");
                float start=Time.unscaledTime,offset=sequence++*12;IVisualElementScheduledItem task=null;
                task=item.schedule.Execute(()=>{float t=Mathf.Clamp01((Time.unscaledTime-start)/.85f);item.style.left=Length.Percent(8+42*t);item.style.top=Length.Percent(78-offset-45*t-15*Mathf.Sin(t*Mathf.PI));item.style.opacity=1-t*t;if(t>=1){task.Pause();item.RemoveFromHierarchy();}}).Every(16);
            }
        }
    }
}
