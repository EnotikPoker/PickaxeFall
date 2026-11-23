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
            string[] names={L.T("Шахта"),L.T("Пустыня"),L.T("Ад")};
            var card=OpenModal(L.T("Маршруты"));card.AddToClassList("map-card");
            Text(card,L.T("Выбери, куда отправиться в следующий спуск."),"map-intro");
            var navigation=Box(card,"map-navigation");
            for(int i=0;i<3;i++){int tab=i;Button(navigation,names[i].ToUpperInvariant(),()=>OpenMaps(tab),"stone-button map-tab"+(page==i?" active":"")).name="map-page-"+i;}
            var p=game.Economy.Profile;
            var project=Box(card,"map-project");project.name="map-project-"+page;
            var scene=Box(project,"construction-scene");scene.name="construction-scene";
            DrawConstruction(scene,page,oldStone<0?p.bridgeStone:oldStone,oldIron<0?p.bridgeIron:oldIron,oldObsidian<0?p.portalObsidian:oldObsidian);
            if(page==0)
            {
                BoldText(project,L.T("Бездонная шахта"),"map-title");
                Text(project,L.T("Каждый спуск — новая шахта."),"skill-summary");
                Text(project,L.T("ДОСТУПНА"),"map-status cost-enough");
                Button(card,L.T("ВЕРНУТЬСЯ В ШАХТУ"),()=>{if(game.SelectMap(MapId.Mine)){CloseModal();SelectTab(HubTab.Mine);}else Toast(L.T("Заверши выбор текущей кирки перед сменой карты"));},"stone-button compact-button buy-ready").name="map-enter-mine";
            }
            else
            {
                var kind=page==1?GoalKind.Bridge:GoalKind.Portal;bool complete=ProfileRules.Complete(p,kind);
                BoldText(project,page==1?L.T("Мост в пустыню"):L.T("Портал в ад"),"map-title");
                Text(project,complete?(page==1?L.T("Песчанник, кварцит и сокровища храмов."):L.T("Лава, крепости и редчайшие минералы.")):L.T("Построй путь к новой карте."),"skill-summary");
                float fraction=page==1?(p.bridgeStone/1000f+p.bridgeIron/50f)*.5f:p.portalObsidian/10f;
                var progress=Box(project,"map-progress-row");Display(progress,!complete);
                Text(progress,complete?L.T("ПОСТРОЕНО"):L.T("ГОТОВНОСТЬ"),"map-status"+(complete?" cost-enough":""));
                BoldText(progress,Mathf.RoundToInt(fraction*100)+"%","map-status");
                var track=Box(project,"profile-progress-track");Display(track,!complete);Box(track,"profile-progress-fill").style.width=Length.Percent(fraction*100);
                var requirements=Box(project,"map-materials");Display(requirements,!complete);
                if(page==1){RecipeLine(requirements,BlockKind.Stone,p.bridgeStone,1000);RecipeLine(requirements,BlockKind.Iron,p.bridgeIron,50);}
                else RecipeLine(requirements,BlockKind.Obsidian,p.portalObsidian,10);
                var pin=PaperPin(project);pin.name="map-goal-pin";Display(pin,!complete&&p.goalKind==kind);
                if(complete){Text(project,L.T("ПУТЬ ОТКРЫТ"),"map-status cost-enough");Button(card,L.T("ВЫБРАТЬ · ")+names[page].ToUpperInvariant(),()=>{if(game.SelectMap((MapId)page)){CloseModal();SelectTab(HubTab.Mine);}else Toast(L.T("Заверши выбор текущей кирки перед сменой карты"));},"stone-button compact-button buy-ready").name="map-enter-"+page;Text(card,L.T("Рекорд: ")+p.mapRecords[page]+L.T(" м · ")+(page==1?L.T("Сложнее шахты"):L.T("Высокая опасность лавы")),"profile-note");}
                if(!complete)
                {
                    CardClick(project,()=>{ToggleCardGoal(kind,0);OpenMaps(page);});
                    long total=0;foreach(long n in game.Economy.ConstructionQuote(kind))total+=n;
                    var contribute=Button(card,L.T("ВНЕСТИ МАТЕРИАЛЫ"),()=>ConfirmContribution(page),"stone-button compact-button "+(total>0?"buy-ready":"buy-blocked"));
                    contribute.name="contribute-"+kind;contribute.SetEnabled(total>0);
                    Text(card,total>0?L.T("Можно вносить частями."):L.T("Нужные материалы пока не собраны."),"profile-note");
                }
            }
            Button(card,L.T("ЗАКРЫТЬ"),()=>CloseModal(),"stone-button compact-button");
        }
        void ConfirmContribution(int page)
        {
            if(!game.CanTrade)return;
            var kind=page==1?GoalKind.Bridge:GoalKind.Portal;var amounts=game.Economy.ConstructionQuote(kind);
            long total=0;foreach(long n in amounts)total+=n;if(total==0)return;
            var card=OpenModal(L.T("Внести материалы?"));card.AddToClassList("profile-dialog");
            foreach(var material in ResourceInventory.Materials)if(amounts[(int)material]>0)RecipeLine(card,material,amounts[(int)material],0);
            Text(card,L.T("Материалы будут вложены в ")+(page==1?L.T("мост."):L.T("портал.")),"profile-note");
            Button(card,L.T("ПОСТРОИТЬ"),()=>{
                if(Time.unscaledTime<nextTrade)return;nextTrade=Time.unscaledTime+.25f;
                var before=game.Economy.Profile.Copy();
                CommitProfile(()=>game.Economy.TryContribute(kind),()=>{
                    OpenMaps(page,before.bridgeStone,before.bridgeIron,before.portalObsidian);
                    FlyConstructionMaterials(root.Q("construction-scene"),amounts);
                    if(ProfileRules.Complete(game.Economy.Profile,kind)){game.Audio?.Ui(UiSound.Construction);Celebrate(page==1?L.T("МОСТ ПОСТРОЕН"):L.T("ПОРТАЛ ПОСТРОЕН"));}
                });
            },"stone-button compact-button buy-ready").name="confirm-construction";
            Button(card,L.T("ОТМЕНА"),()=>OpenMaps(page),"stone-button compact-button");
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
            // Fit the entire 2:1 illustration, then size the frame to it: no side gaps or stretched tiles.
            Color sky=map==2?RGB(34,22,42):RGB(118,185,211);art.style.backgroundColor=sky;
            var board=Box(art,"construction-board");board.pickingMode=PickingMode.Ignore;
            art.RegisterCallback<GeometryChangedEvent>(_=>{
                float scale=art.contentRect.width/480;
                art.style.height=240*scale+art.resolvedStyle.borderTopWidth+art.resolvedStyle.borderBottomWidth;
                board.style.scale=new Scale(new Vector3(scale,scale,1));
            });
            ArtPiece(board,0,0,480,240,Color.white,PixelArt.WorldLandscape((MapId)map));
            if(map==0)
            {
                // A small entrance in the foreground leaves the landscape readable.
                ArtPiece(board,0,206,480,34,RGB(68,86,48));
                ArtPiece(board,150,196,180,44,RGB(76,79,62));
                ArtPiece(board,166,182,148,58,RGB(89,94,73));
                ArtPiece(board,182,170,116,70,RGB(102,107,84));
                ArtPiece(board,208,182,64,58,RGB(17,25,26));
                ArtPiece(board,200,178,8,62,RGB(109,74,43));ArtPiece(board,272,178,8,62,RGB(109,74,43));
                ArtPiece(board,196,172,88,10,RGB(167,118,64));ArtPiece(board,198,173,84,2,RGB(208,162,96));
                ArtPiece(board,217,218,4,22,RGB(147,155,153));ArtPiece(board,259,218,4,22,RGB(147,155,153));
                for(int y=223;y<240;y+=8)ArtPiece(board,213,y,54,3,RGB(134,94,56));
                ArtPiece(board,211,187,18,25,new Color32(255,197,79,32));
                ArtPiece(board,216,192,6,9,RGB(255,211,110));
                return;
            }
            var deck=new List<VisualElement>();var supports=new List<VisualElement>();VisualElement glow=null;
            if(map==1)
            {
                var plains=ArtPiece(board,0,0,208,240,Color.clear);plains.style.overflow=Overflow.Hidden;
                ArtPiece(plains,0,0,480,240,Color.white,PixelArt.WorldLandscape(MapId.Mine));
                for(int i=0;i<16;i++)
                {
                    float x=208+i*4;
                    var blend=ArtPiece(board,x,0,4,240,Color.clear);blend.style.overflow=Overflow.Hidden;blend.style.opacity=1-i/15f;
                    ArtPiece(blend,-x,0,480,240,Color.white,PixelArt.WorldLandscape(MapId.Mine));
                }
                ArtPiece(board,0,148,120,92,RGB(115,80,54));ArtPiece(board,360,148,120,92,RGB(177,125,66));
                ArtPiece(board,0,142,120,10,RGB(90,146,54));ArtPiece(board,360,142,120,10,RGB(231,202,133));
                ArtPiece(board,120,172,240,68,RGB(40,63,60));
                ArtPiece(board,120,220,240,20,RGB(65,115,132));
                for(int i=0;i<5;i++){ArtPiece(board,8+i*23,173+(i%2)*18,13,6,RGB(90,66,48));ArtPiece(board,370+i*21,186-(i%2)*15,12,5,RGB(153,100,52));}
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
