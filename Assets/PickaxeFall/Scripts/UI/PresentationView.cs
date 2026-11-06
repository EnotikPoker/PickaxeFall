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
            public Vector2 worldOrigin; public float side, lane, started; public bool active;
        }
        sealed class PendingLoot { public Vector2 position;public long count;public BlockKind kind; }
        readonly Dictionary<int,PendingLoot> pendingLoot=new();
        readonly List<LootFloat> lootFloats=new();
        VisualElement lootLayer;
        float nextLootFlush, resultsAt;
        int lootSequence;
        const float LootWidth = 112, LootHeight = 38;
        RunPhase presentedPhase=(RunPhase)(-1);
        public void ShowLoot(Vector2 position,BlockKind kind,int quantity,int tool)
        {
            if(!game.Settings.LootMessages || quantity<=0)return;
            int key=(tool+1)*ResourceInventory.SlotCount+(int)kind;
            if(!pendingLoot.TryGetValue(key,out var value))pendingLoot[key]=value=new PendingLoot {kind=kind};
            value.position=position;value.count+=quantity;
        }
        void StartResults(){resultsAt=Time.unscaledTime;runGrid.Update(_=>0,kind=>game.OreCounts[(int)kind]>0);}
        void TickPresentation()
        {
            if(game.Phase==RunPhase.Ready || !game.Settings.LootMessages)
            {
                pendingLoot.Clear();foreach(var item in lootFloats){item.active=false;Display(item.element,false);}
            }
            if(pendingLoot.Count>0 && Time.unscaledTime>=nextLootFlush)
            {
                nextLootFlush=Time.unscaledTime+.12f;
                foreach(var value in pendingLoot.Values)SpawnLoot(value);
                pendingLoot.Clear();
            }
            foreach(var item in lootFloats)
            {
                if(!item.active)continue;
                float duration=game.Settings.ReducedEffects ? .5f : .8f;
                float t=Mathf.Clamp01((Time.unscaledTime-item.started)/duration);
                if(t>=1){item.active=false;Display(item.element,false);continue;}
                PlaceLoot(item,t);
                item.element.style.opacity=t<.4f?1:(1-t)/.6f;
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
            },kind=>game.OreCounts[(int)kind]>0);
        }
        void SpawnLoot(PendingLoot value)
        {
            if(!Camera.main || root.worldBound.width<=0)return;
            if(lootLayer==null){lootLayer=Box(root,"loot-layer");root.Insert(1,lootLayer);lootLayer.pickingMode=PickingMode.Ignore;}
            LootFloat item=lootFloats.Find(x=>!x.active);
            int maximum=game.Settings.ReducedEffects?6:12;
            if(item==null && lootFloats.Count<maximum)
            {
                item=new LootFloat();item.element=Box(lootLayer,"loot-float");item.element.pickingMode=PickingMode.Ignore;
                item.count=BoldText(item.element,"","loot-number");item.count.pickingMode=PickingMode.Ignore;
                item.icon=Icon(item.element,PixelArt.ResourceIcon(value.kind),"loot-icon");item.icon.pickingMode=PickingMode.Ignore;
                lootFloats.Add(item);
            }
            if(item==null){item=lootFloats[0];foreach(var candidate in lootFloats)if(candidate.started<item.started)item=candidate;}
            item.worldOrigin=value.position;item.side=(lootSequence&1)==0?1:-1;item.lane=(lootSequence++/2)%3;
            item.count.text="+"+Short(value.count);item.icon.style.backgroundImage=Background.FromSprite(PixelArt.ResourceIcon(value.kind));
            item.started=Time.unscaledTime;item.active=true;Display(item.element,true);
            PlaceLoot(item,0);
            item.element.BringToFront();
        }

        Vector2 LootPoint(Vector2 world)
        {
            Vector3 screen=Camera.main.WorldToViewportPoint(world);
            return new Vector2(screen.x*root.worldBound.width,(1-screen.y)*root.worldBound.height);
        }
        void PlaceLoot(LootFloat item,float t)
        {
            if(!Camera.main)return;
            var bounds=root.worldBound;var view=game.GameplayViewport;
            float left=view.xMin*bounds.width+8,right=view.xMax*bounds.width-8;
            float top=(1-view.yMax)*bounds.height+8,bottom=(1-view.yMin)*bounds.height-8;
            // Follow the original world point through camera motion, then arc to a side.
            Vector2 point=LootPoint(item.worldOrigin);
            float blockHalf=Mathf.Abs(LootPoint(item.worldOrigin+Vector2.right*.85f).x-point.x);
            float drift=game.Settings.ReducedEffects?0:28*(1-(1-t)*(1-t));
            float x=point.x+(item.side>0?blockHalf+18+drift:-blockHalf-18-drift-LootWidth);
            if(x<left || x+LootWidth>right)
                x=point.x+(item.side>0?-blockHalf-18-drift-LootWidth:blockHalf+18+drift);
            float y=point.y-LootHeight*.5f-item.lane*36-(game.Settings.ReducedEffects?0:46*t);
            x=Mathf.Clamp(x,left,Mathf.Max(left,right-LootWidth));
            y=Mathf.Clamp(y,top,Mathf.Max(top,bottom-LootHeight));
            // Keep every living tool readable, including groups and moving cameras.
            foreach(var tool in game.Pickaxes)
            {
                if(!tool.IsAlive)continue;
                Vector2 center=LootPoint(tool.Body.position);
                float half=Mathf.Abs(LootPoint(tool.Body.position+Vector2.right*.85f).x-center.x)+10;
                var exclusion=new Rect(center.x-half,center.y-half,half*2,half*2);
                if(!new Rect(x,y,LootWidth,LootHeight).Overlaps(exclusion))continue;
                float outsideRight=exclusion.xMax+8,outsideLeft=exclusion.xMin-LootWidth-8;
                if(item.side>0 && outsideRight+LootWidth<=right)x=outsideRight;
                else if(outsideLeft>=left)x=outsideLeft;
                else if(outsideRight+LootWidth<=right)x=outsideRight;
                else y=Mathf.Clamp(exclusion.yMin-LootHeight-8,top,Mathf.Max(top,bottom-LootHeight));
            }
            // Dense groups may leave no safe screen space; fade rather than cover a tool.
            bool overlaps=false;
            foreach(var tool in game.Pickaxes)
            {
                if(!tool.IsAlive)continue;
                Vector2 center=LootPoint(tool.Body.position);
                float half=Mathf.Abs(LootPoint(tool.Body.position+Vector2.right*.85f).x-center.x)+8;
                if(new Rect(x,y,LootWidth,LootHeight).Overlaps(new Rect(center.x-half,center.y-half,half*2,half*2)))overlaps=true;
            }
            item.element.style.visibility=overlaps?Visibility.Hidden:Visibility.Visible;
            item.element.style.left=x;item.element.style.top=y;
        }
    }
}
