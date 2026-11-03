using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        sealed class LootFloat
        {
            public VisualElement element,icon;public Label count;
            public Vector2 origin;public float started;public bool active;
        }
        sealed class PendingLoot { public Vector2 position;public long count;public BlockKind kind; }
        readonly Dictionary<int,PendingLoot> pendingLoot=new();
        readonly List<LootFloat> lootFloats=new();
        VisualElement lootLayer;
        float nextLootFlush, resultsAt;
        RunPhase presentedPhase=(RunPhase)(-1);
        public void ShowLoot(Vector2 position,BlockKind kind,int quantity,int tool)
        {
            if(!game.Settings.LootMessages || quantity<=0)return;
            int key=(tool+1)*8+(int)kind;
            if(!pendingLoot.TryGetValue(key,out var value))pendingLoot[key]=value=new PendingLoot {kind=kind};
            value.position=position;value.count+=quantity;
        }
        void StartResults(){resultsAt=Time.unscaledTime;runGrid.Update(_=>0);}
        void TickPresentation()
        {
            if(game.Phase==RunPhase.Ready || !game.Settings.LootMessages)
            {
                pendingLoot.Clear();foreach(var item in lootFloats){item.active=false;Display(item.element,false);}
            }
            if(pendingLoot.Count>0 && Time.unscaledTime>=nextLootFlush)
            {
                nextLootFlush=Time.unscaledTime+.075f;
                foreach(var value in pendingLoot.Values)SpawnLoot(value);
                pendingLoot.Clear();
            }
            foreach(var item in lootFloats)
            {
                if(!item.active)continue;
                float duration=game.Settings.ReducedEffects ? .5f : .9f;
                float t=Mathf.Clamp01((Time.unscaledTime-item.started)/duration);
                if(t>=1){item.active=false;Display(item.element,false);continue;}
                float ease=1-(1-t)*(1-t);item.element.style.left=item.origin.x;
                item.element.style.top=item.origin.y-ease*(game.Settings.ReducedEffects?28:85);
                item.element.style.opacity=t<.45f?1:(1-t)/.55f;
            }
            if(game.Phase!=RunPhase.Finished)return;
            float elapsed=Time.unscaledTime-resultsAt;
            float slide=game.Settings.ReducedEffects?1:Mathf.Clamp01(elapsed/.45f);
            float eased=1-Mathf.Pow(1-slide,3);
            mineView.style.translate=new Translate(0,Length.Percent((1-eased)*100),0);mineView.style.opacity=eased;
            runGrid.Update(kind=>
            {
                long target=game.OreCounts[(int)kind];float t=game.Settings.ReducedEffects?1:Mathf.Clamp01((elapsed-.2f-(int)kind*.065f)/1.1f);
                return t>=1?target:(long)(target*(double)(1-Mathf.Pow(1-t,3)));
            });
        }
        void SpawnLoot(PendingLoot value)
        {
            if(!Camera.main || root.worldBound.width<=0)return;
            if(lootLayer==null){lootLayer=Box(root,"loot-layer");root.Insert(1,lootLayer);lootLayer.pickingMode=PickingMode.Ignore;}
            LootFloat item=lootFloats.Find(x=>!x.active);
            int maximum=game.Settings.ReducedEffects?8:20;
            if(item==null && lootFloats.Count<maximum)
            {
                item=new LootFloat();item.element=Box(lootLayer,"loot-float");item.element.pickingMode=PickingMode.Ignore;
                item.count=BoldText(item.element,"","loot-number");item.count.pickingMode=PickingMode.Ignore;
                item.icon=Icon(item.element,PixelArt.Block(value.kind,0,value.kind==BlockKind.Deepstone),"loot-icon");item.icon.pickingMode=PickingMode.Ignore;
                lootFloats.Add(item);
            }
            if(item==null){item=lootFloats[0];foreach(var candidate in lootFloats)if(candidate.started<item.started)item=candidate;}
            Vector3 screen=Camera.main.WorldToScreenPoint(value.position);
            var bounds=root.worldBound;
            item.origin=new Vector2(Mathf.Clamp(screen.x/Screen.width*bounds.width-40,8,bounds.width-160),Mathf.Clamp((1-screen.y/Screen.height)*bounds.height-30,80,bounds.height-100));
            item.count.text="+"+Short(value.count);item.icon.style.backgroundImage=Background.FromSprite(PixelArt.Block(value.kind,0,value.kind==BlockKind.Deepstone));
            item.started=Time.unscaledTime;item.active=true;Display(item.element,true);
            item.element.BringToFront();
        }
    }
}
