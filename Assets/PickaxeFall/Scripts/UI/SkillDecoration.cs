using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        static bool ButtonClick(ClickEvent e)=>e.target is Button || (e.target as VisualElement)?.GetFirstAncestorOfType<Button>()!=null;
        void ToggleCardGoal(GoalKind kind,int index)
        {
            var p=game.Economy.Profile;
            CommitProfile(()=>game.Economy.TrySetGoal(p.goalKind==kind&&p.goalIndex==index?GoalKind.None:kind,p.goalKind==kind&&p.goalIndex==index?0:index));
        }
        static VisualElement PaperPin(VisualElement card)
        {
            var pin=Box(card,"paper-pin");pin.pickingMode=PickingMode.Ignore;
            foreach(string piece in new[]{"pin-needle","pin-neck","pin-head","pin-glint"})Box(pin,piece).pickingMode=PickingMode.Ignore;
            return pin;
        }
        static void CardClick(VisualElement card,Action action)
        {
            card.RegisterCallback<ClickEvent>(e=>{if(ButtonClick(e))return;action();e.StopPropagation();});
        }
        void ShowSkillDetails(string title,string body)
        {
            var card=OpenModal(title);Text(card,body,"description");
            Button(card,"ПОНЯТНО",()=>CloseModal(),"stone-button compact-button");
        }
        static string SkillSummary(UpgradeKind kind)=>kind switch{
            UpgradeKind.Underwater=>"Меньше штраф урона в воде.",
            UpgradeKind.HeatShield=>"Меньше износ в лаве.",
            UpgradeKind.TreasureHunter=>"Шанс на дополнительную добычу из сундука.",
            UpgradeKind.EarthDigger=>"Шанс добыть больше земли и песка.",
            UpgradeKind.Damage=>"Больше урона выбранной кирке.",
            UpgradeKind.Durability=>"Больше прочности выбранной кирке.",
            UpgradeKind.Fortune=>"Шанс получить дополнительную руду.",
            UpgradeKind.TeamChance=>"Шанс начать спуск с несколькими кирками.",
            UpgradeKind.TeamSize=>"Больше кирок при выпадении отряда.",
            UpgradeKind.TntFind=>"Добытая руда может вызвать взрыв.",
            UpgradeKind.DoubleBench=>"Шанс дополнительного улучшения на верстаке.",
            UpgradeKind.DoubleBlast=>"Шанс удвоить площадь взрыва.",
            UpgradeKind.Careful=>"Шанс сохранить прочность при ударе.",
            UpgradeKind.LastChance=>"Шанс спасти последнюю кирку один раз за спуск.",
            UpgradeKind.EnergyCapacity=>"Накапливай запас до трёх применений.",
            UpgradeKind.EnergyCharge=>"Восстанавливай энергию быстрее.",
            UpgradeKind.EnergyEfficiency=>"Меньше энергии на каждый приём.",
            UpgradeKind.DashForce=>"Более сильный направленный рывок.",
            UpgradeKind.PlungeDepth=>"Пробивай больше блоков нажатием.",
            UpgradeKind.ImpactPower=>"Больше урона при сильном столкновении.",
            _=>"Сильный удар повреждает соседний блок."
        };

        sealed class SkillLock
        {
            readonly GameHud hud;readonly int index;readonly VisualElement card,panel;readonly Button cover;readonly ChainDrawing drawing;readonly Label note;
            bool animating;
            public bool IsOpen=>ProfileRules.Revealed(hud.game.Economy.Profile,index);
            public SkillLock(GameHud owner,VisualElement parent,int skill)
            {
                hud=owner;card=parent;index=skill;
                panel=Box(card,"skill-lock-layer");panel.name="lock-"+index;
                cover=hud.Button(panel,"",Activate,"skill-lock-cover");cover.name="reveal-"+index;
                drawing=new ChainDrawing {pickingMode=PickingMode.Ignore};drawing.AddToClassList("chain-drawing");cover.Add(drawing);
                string title=index==25?"Рывок":index==26?"Пробой":Progression.Title(ProfileRules.RevealKey(index).Kind);
                hud.BoldText(cover,title,"locked-title").pickingMode=PickingMode.Ignore;
                note=hud.BoldText(cover,"","locked-note");note.pickingMode=PickingMode.Ignore;
                cover.tooltip=title;
                // Stable public query for previews and UI checks.
                cover.AddToClassList("lock-"+index);
            }
            public void Refresh()
            {
                bool eligible=hud.game.Economy.Profile.profileLevel>=ProfileRules.RequiredLevel(index);
                card.EnableInClassList("locked-skill",!IsOpen||animating);
                Display(panel,!IsOpen||animating);cover.SetEnabled(eligible&&!IsOpen&&!animating&&hud.game.CanTrade);
                cover.EnableInClassList("lock-ready",eligible);
                note.text=eligible?"НАЖМИ, ЧТОБЫ ОТКРЫТЬ":"УРОВЕНЬ ПРОФИЛЯ "+ProfileRules.RequiredLevel(index);
                drawing.Ready=eligible;drawing.MarkDirtyRepaint();
            }
            public void Activate()
            {
                if(!hud.game.CanTrade||animating||IsOpen)return;
                if(!hud.game.Economy.TryReveal(index))return;
                animating=true;hud.Refresh();
                float started=Time.unscaledTime,duration=hud.game.Settings.ReducedEffects?.15f:.6f;
                IVisualElementScheduledItem task=null;
                task=cover.schedule.Execute(()=>{
                    float t=Mathf.Clamp01((Time.unscaledTime-started)/duration);
                    drawing.Fall=hud.game.Settings.ReducedEffects?0:t;drawing.MarkDirtyRepaint();
                    cover.style.opacity=1-t;note.style.opacity=1-t;
                    if(t>=1){task.Pause();animating=false;card.RemoveFromClassList("locked-skill");Display(panel,false);hud.Refresh();}
                }).Every(16);
            }
        }
        sealed class ChainDrawing:VisualElement
        {
            public bool Ready;public float Fall;
            public ChainDrawing(){generateVisualContent+=Paint;}
            static void Shape(Painter2D p,Vector2[] points,Color color)
            {
                p.fillColor=color;p.BeginPath();p.MoveTo(points[0]);for(int i=1;i<points.Length;i++)p.LineTo(points[i]);p.ClosePath();p.Fill();
            }
            static Vector2 Transform(Vector2 local,Vector2 center,Vector2 along)=>center+along*local.x+new Vector2(-along.y,along.x)*local.y;
            static void Link(Painter2D p,Vector2 center,Vector2 along,bool edge)
            {
                float half=edge?7:15;Vector2[] points={new(-23,-half+5),new(-18,-half),new(18,-half),new(23,-half+5),new(23,half-5),new(18,half),new(-18,half),new(-23,half-5)};
                void Path(float width,Color color)
                {
                    p.lineWidth=width;p.strokeColor=color;p.BeginPath();p.MoveTo(Transform(points[0],center,along));
                    for(int i=1;i<points.Length;i++)p.LineTo(Transform(points[i],center,along));p.ClosePath();p.Stroke();
                }
                Path(13,new Color32(15,20,24,255));Path(8,edge?new Color32(94,107,116,255):new Color32(145,157,163,255));
                p.lineWidth=2;p.strokeColor=new Color32(214,225,224,255);p.BeginPath();p.MoveTo(Transform(points[0],center,along));p.LineTo(Transform(points[1],center,along));p.LineTo(Transform(points[2],center,along));p.Stroke();
            }
            void Paint(MeshGenerationContext ctx)
            {
                var p=ctx.painter2D;float w=contentRect.width,h=contentRect.height;if(w<=0||h<=0)return;
                for(int diagonal=0;diagonal<2;diagonal++)
                {
                    Vector2 a=new(-30,h*(diagonal==0?.25f:.82f)),b=new(w+30,h*(diagonal==0?.82f:.25f));Vector2 d=(b-a).normalized;
                    Vector2 shift=new((diagonal==0?-1:1)*Fall*w*.7f,Fall*Fall*h*.7f);
                    for(float distance=0;distance<(b-a).magnitude;distance+=35)Link(p,a+d*distance+shift,d,((int)(distance/35)&1)==1);
                }
                Vector2 c=new(w*.5f,h*.535f+Fall*Fall*h);
                Color gold=Ready?new Color32(230,179,66,255):new Color32(130,141,146,255);
                p.lineWidth=14;p.strokeColor=new Color32(20,25,28,255);p.BeginPath();p.MoveTo(c+new Vector2(-23,-13));p.LineTo(c+new Vector2(-23,-42));p.LineTo(c+new Vector2(-15,-50));p.LineTo(c+new Vector2(15,-50));p.LineTo(c+new Vector2(23,-42));p.LineTo(c+new Vector2(23,-13));p.Stroke();
                p.lineWidth=8;p.strokeColor=new Color32(188,199,199,255);p.Stroke();
                Shape(p,new[]{c+new Vector2(-44,-17),c+new Vector2(44,-17),c+new Vector2(44,37),c+new Vector2(35,46),c+new Vector2(-35,46),c+new Vector2(-44,37)},new Color32(21,25,27,255));
                Shape(p,new[]{c+new Vector2(-38,-12),c+new Vector2(38,-12),c+new Vector2(38,32),c+new Vector2(30,39),c+new Vector2(-30,39),c+new Vector2(-38,32)},gold);
                Shape(p,new[]{c+new Vector2(-34,-8),c+new Vector2(34,-8),c+new Vector2(34,-2),c+new Vector2(-34,-2)},Ready?new Color32(255,226,133,255):new Color32(192,204,207,255));
                Shape(p,new[]{c+new Vector2(-7,5),c+new Vector2(7,5),c+new Vector2(10,15),c+new Vector2(4,19),c+new Vector2(7,30),c+new Vector2(-7,30),c+new Vector2(-4,19),c+new Vector2(-10,15)},new Color32(38,35,29,255));
            }
        }
    }
}
