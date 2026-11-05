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
            string[] names={"Шахта","Пустыня","Ад"};
            var card=OpenModal("Маршруты");card.AddToClassList("map-card");
            var navigation=Box(card,"map-navigation");
            for(int i=0;i<3;i++){int tab=i;Button(navigation,names[i].ToUpperInvariant(),()=>OpenMaps(tab),"stone-button map-tab"+(page==i?" active":"")).name="map-page-"+i;}
            var p=game.Economy.Profile;
            var project=Box(card,"map-project");project.name="map-project-"+page;
            var scene=Box(project,"construction-scene");scene.name="construction-scene";
            DrawConstruction(scene,page,oldStone<0?p.bridgeStone:oldStone,oldIron<0?p.bridgeIron:oldIron,oldObsidian<0?p.portalObsidian:oldObsidian);
            if(page==0)
            {
                BoldText(project,"Бездонная шахта","map-title");
                Text(project,"Каждый спуск — новая шахта.","skill-summary");
                Text(project,"ДОСТУПНА","map-status cost-enough");
                Button(card,"ВЕРНУТЬСЯ В ШАХТУ",()=>{CloseModal();SelectTab(HubTab.Mine);},"stone-button compact-button buy-ready").name="map-enter-mine";
            }
            else
            {
                var kind=page==1?GoalKind.Bridge:GoalKind.Portal;bool complete=ProfileRules.Complete(p,kind);
                BoldText(project,page==1?"Мост в пустыню":"Портал в ад","map-title");
                Text(project,complete?"Путь готов. Спуски появятся в следующем обновлении.":"Построй путь к будущей карте.","skill-summary");
                float fraction=page==1?(p.bridgeStone/1000f+p.bridgeIron/50f)*.5f:p.portalObsidian/10f;
                var progress=Box(project,"map-progress-row");
                Text(progress,complete?"ПОСТРОЕНО":"ГОТОВНОСТЬ","map-status"+(complete?" cost-enough":""));
                BoldText(progress,Mathf.RoundToInt(fraction*100)+"%","map-status");
                var track=Box(project,"profile-progress-track");Box(track,"profile-progress-fill").style.width=Length.Percent(fraction*100);
                var requirements=Box(project,"map-materials");
                if(page==1){RecipeLine(requirements,BlockKind.Stone,p.bridgeStone,1000);RecipeLine(requirements,BlockKind.Iron,p.bridgeIron,50);}
                else RecipeLine(requirements,BlockKind.Obsidian,p.portalObsidian,10);
                var pin=PaperPin(project);pin.name="map-goal-pin";Display(pin,!complete&&p.goalKind==kind);
                if(!complete)
                {
                    CardClick(project,()=>{ToggleCardGoal(kind,0);OpenMaps(page);});
                    long total=0;foreach(long n in game.Economy.ConstructionQuote(kind))total+=n;
                    var contribute=Button(card,"ВНЕСТИ МАТЕРИАЛЫ",()=>ConfirmContribution(page),"stone-button compact-button "+(total>0?"buy-ready":"buy-blocked"));
                    contribute.name="contribute-"+kind;contribute.SetEnabled(total>0);
                    Text(card,total>0?"Можно вносить частями.":"Нужные материалы пока не собраны.","profile-note");
                }
            }
            Button(card,"ЗАКРЫТЬ",()=>CloseModal(),"stone-button compact-button");
        }
        void ConfirmContribution(int page)
        {
            if(!game.CanTrade)return;
            var kind=page==1?GoalKind.Bridge:GoalKind.Portal;var amounts=game.Economy.ConstructionQuote(kind);
            long total=0;foreach(long n in amounts)total+=n;if(total==0)return;
            var card=OpenModal("Внести материалы?");card.AddToClassList("profile-dialog");
            foreach(var material in ResourceInventory.Materials)if(amounts[(int)material]>0)RecipeLine(card,material,amounts[(int)material],0);
            Text(card,"Материалы будут вложены в "+(page==1?"мост.":"портал."),"profile-note");
            Button(card,"ПОСТРОИТЬ",()=>{
                if(Time.unscaledTime<nextTrade)return;nextTrade=Time.unscaledTime+.25f;
                var before=game.Economy.Profile.Copy();
                CommitProfile(()=>game.Economy.TryContribute(kind),()=>{
                    OpenMaps(page,before.bridgeStone,before.bridgeIron,before.portalObsidian);
                    FlyConstructionMaterials(root.Q("construction-scene"),amounts);
                    if(ProfileRules.Complete(game.Economy.Profile,kind))Toast(page==1?"МОСТ ПОСТРОЕН!":"ПОРТАЛ ПОСТРОЕН!");
                });
            },"stone-button compact-button buy-ready").name="confirm-construction";
            Button(card,"ОТМЕНА",()=>OpenMaps(page),"stone-button compact-button");
        }
        static VisualElement ArtPiece(VisualElement parent,float x,float y,float w,float h,Color color,Sprite sprite=null)
        {
            var item=new VisualElement {pickingMode=PickingMode.Ignore};
            item.style.position=Position.Absolute;item.style.left=x;item.style.top=y;item.style.width=w;item.style.height=h;
            item.style.backgroundColor=color;if(sprite)item.style.backgroundImage=Background.FromSprite(sprite);parent.Add(item);return item;
        }
        static Color32 RGB(byte r,byte g,byte b)=>new(r,g,b,255);
        void DrawConstruction(VisualElement art,int map,float oldStone,float oldIron,float oldObsidian)
        {
            // All art uses one 480×240 board; block width and height share the same scale.
            // Screen aspect ratio can never stretch a portal or a tile.
            Color sky=map==2?RGB(34,22,42):RGB(118,185,211);art.style.backgroundColor=sky;
            var board=Box(art,"construction-board");board.pickingMode=PickingMode.Ignore;
            art.RegisterCallback<GeometryChangedEvent>(_=>{
                float scale=Mathf.Min(art.contentRect.width/480,art.contentRect.height/240);
                board.style.scale=new Scale(new Vector3(scale,scale,1));
            });
            ArtPiece(board,0,0,480,240,sky);
            if(map!=2)
            {
                ArtPiece(board,392,22,32,32,RGB(255,232,154));
                ArtPiece(board,32,33,68,10,RGB(222,239,228));ArtPiece(board,50,26,31,9,RGB(222,239,228));
                ArtPiece(board,284,48,54,8,RGB(222,239,228));
            }
            if(map==0)
            {
                for(int i=0;i<12;i++)
                {
                    float top=108-Mathf.Abs(5.5f-i)*9;
                    ArtPiece(board,i*44,top,45,156,RGB(105,151,131));
                    ArtPiece(board,i*44,top+24,45,132,RGB(79,123,105));
                }
                for(int y=0;y<4;y++)for(int x=0;x<20;x++)
                    ArtPiece(board,x*24,144+y*24,24,24,Color.white,PixelArt.Block(y<2?BlockKind.Dirt:BlockKind.Stone,x+y*7));
                ArtPiece(board,0,138,480,8,RGB(91,143,57));
                ArtPiece(board,178,100,124,140,RGB(24,33,32));
                ArtPiece(board,190,108,100,132,RGB(17,25,27));
                ArtPiece(board,172,94,12,116,RGB(101,65,38));ArtPiece(board,296,94,12,116,RGB(101,65,38));
                ArtPiece(board,168,88,144,15,RGB(141,97,53));ArtPiece(board,171,90,138,4,RGB(183,133,72));
                for(int y=112;y<209;y+=32){ArtPiece(board,174,y,8,3,RGB(171,121,61));ArtPiece(board,298,y,8,3,RGB(171,121,61));}
                for(int y=180;y<240;y+=14)ArtPiece(board,213,y,55,5,RGB(105,78,52));
                ArtPiece(board,221,173,4,67,RGB(158,163,146));ArtPiece(board,255,173,4,67,RGB(158,163,146));
                ArtPiece(board,196,107,25,31,new Color32(255,197,79,35));
                ArtPiece(board,206,101,2,13,RGB(66,58,36));ArtPiece(board,201,114,12,15,RGB(103,76,34));
                ArtPiece(board,204,117,6,9,RGB(255,212,101));
                return;
            }
            var deck=new List<VisualElement>();var supports=new List<VisualElement>();VisualElement glow=null;
            if(map==1)
            {
                ArtPiece(board,0,148,120,92,RGB(167,117,63));ArtPiece(board,360,148,120,92,RGB(177,125,66));
                ArtPiece(board,0,142,120,10,RGB(231,202,133));ArtPiece(board,360,142,120,10,RGB(231,202,133));
                ArtPiece(board,120,214,240,26,RGB(90,123,121));
                for(int i=0;i<5;i++){ArtPiece(board,8+i*23,173+(i%2)*18,13,6,RGB(146,95,50));ArtPiece(board,370+i*21,186-(i%2)*15,12,5,RGB(153,100,52));}
                for(int i=0;i<10;i++)
                {
                    var segment=ArtPiece(board,120+i*24,148,24,24,Color.white,PixelArt.Block(BlockKind.Stone,i));deck.Add(segment);
                    var rail=ArtPiece(board,120+i*24,123,24,27,new Color32(0,0,0,0));
                    ArtPiece(rail,1,0,4,27,RGB(171,184,173));ArtPiece(rail,1,0,24,4,RGB(205,210,182));
                    supports.Add(rail);
                }
            }
            else
            {
                for(int i=0;i<10;i++)ArtPiece(board,i*52,126+(i%3)*11,54,114,RGB((byte)(72+i%3*9),35,47));
                ArtPiece(board,0,222,480,18,RGB(214,76,31));ArtPiece(board,16,228,105,5,RGB(255,169,57));ArtPiece(board,352,225,81,4,RGB(255,169,57));
                ArtPiece(board,156,168,168,16,RGB(57,55,62));ArtPiece(board,170,184,140,11,RGB(43,44,51));
                const int left=192,top=48,tile=24;
                glow=ArtPiece(board,left+tile,top+tile,tile*2,tile*3,RGB(113,60,155));
                for(int i=0;i<5;i++)ArtPiece(glow,5+(i%2)*20,i*13,12,3,new Color32(212,139,239,95));
                Vector2Int[] positions={new(1,4),new(2,4),new(0,3),new(0,2),new(0,1),new(1,0),new(2,0),new(3,1),new(3,2),new(3,3)};
                foreach(var cell in positions)deck.Add(ArtPiece(board,left+cell.x*tile,top+cell.y*tile,tile,tile,Color.white,PixelArt.ResourceIcon(BlockKind.Obsidian)));
            }
            var profile=game.Economy.Profile;float started=Time.unscaledTime,duration=game.Settings.ReducedEffects?.15f:1;
            bool animate=map==1?(oldStone!=profile.bridgeStone||oldIron!=profile.bridgeIron):oldObsidian!=profile.portalObsidian;
            void Draw(float t)
            {
                float primary=map==1?Mathf.Lerp(oldStone,profile.bridgeStone,t)/1000:Mathf.Lerp(oldObsidian,profile.portalObsidian,t)/10;
                float secondary=Mathf.Lerp(oldIron,profile.bridgeIron,t)/50;
                for(int i=0;i<deck.Count;i++)deck[i].style.opacity=Mathf.Clamp01(primary*10-i);
                for(int i=0;i<supports.Count;i++)supports[i].style.opacity=Mathf.Min(Mathf.Clamp01(primary*10-i),Mathf.Clamp01(secondary*10-i));
                if(glow!=null)glow.style.opacity=primary>=1?(.84f+.16f*Mathf.Sin((Time.unscaledTime-started)*3)):0;
            }
            Draw(animate?0:1);
            if(animate)
            {
                IVisualElementScheduledItem task=null;
                task=art.schedule.Execute(()=>{float t=Mathf.Clamp01((Time.unscaledTime-started)/duration);Draw(t);if(t>=1)task.Pause();}).Every(16);
            }
            if(glow!=null&&profile.portalObsidian==10&&!game.Settings.ReducedEffects)art.schedule.Execute(()=>{if(Time.unscaledTime-started>duration)Draw(1);}).Every(80);
        }
        void FlyConstructionMaterials(VisualElement art,long[] amounts)
        {
            if(art==null||game.Settings.ReducedEffects)return;int sequence=0;
            foreach(var kind in ResourceInventory.Materials)
            {
                if(amounts[(int)kind]==0)continue;
                var item=Box(art,"construction-flight");Icon(item,PixelArt.ResourceIcon(kind),"recipe-icon");BoldText(item,"+"+Short(amounts[(int)kind]),"recipe-amount");
                float start=Time.unscaledTime,offset=sequence++*12;IVisualElementScheduledItem task=null;
                task=item.schedule.Execute(()=>{float t=Mathf.Clamp01((Time.unscaledTime-start)/.85f);item.style.left=Length.Percent(8+42*t);item.style.top=Length.Percent(78-offset-45*t-15*Mathf.Sin(t*Mathf.PI));item.style.opacity=1-t*t;if(t>=1){task.Pause();item.RemoveFromHierarchy();}}).Every(16);
            }
        }
    }
}
