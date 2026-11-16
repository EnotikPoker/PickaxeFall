using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    // Own exactly one pointer pipeline. No Clickable manipulator on these targets.
    // Down mode counts each finger once; release mode activates a menu command once.
    internal sealed class ReliablePress
    {
        readonly VisualElement target;
        readonly Action command;
        readonly bool onDown;
        readonly Action<bool> visual;
        readonly HashSet<int> held=new();
        bool keyHeld;Rect pressBounds;
        public ReliablePress(VisualElement target,Action command,bool onDown,Action<bool> visual=null)
        {
            this.target=target;this.command=command;this.onDown=onDown;
            this.visual=visual??(value=>target.EnableInClassList("press-held",value));
            if(target is Button button)button.clickable=null;
            target.RegisterCallback<PointerDownEvent>(Down);
            target.RegisterCallback<PointerUpEvent>(Up);
            target.RegisterCallback<PointerCancelEvent>(e=>Release(e.pointerId));
            target.RegisterCallback<PointerCaptureOutEvent>(e=>Release(e.pointerId));
            target.RegisterCallback<DetachFromPanelEvent>(_=>Cancel());
            if(!onDown)
            {
                target.RegisterCallback<KeyDownEvent>(e=>{if((e.keyCode==KeyCode.Space||e.keyCode==KeyCode.Return)&&target.enabledInHierarchy){keyHeld=true;this.visual(true);e.StopPropagation();}});
                target.RegisterCallback<KeyUpEvent>(e=>{if((e.keyCode==KeyCode.Space||e.keyCode==KeyCode.Return)&&keyHeld){keyHeld=false;this.visual(false);if(target.enabledInHierarchy)command();e.StopPropagation();}});
                target.RegisterCallback<BlurEvent>(_=>Cancel());
                target.RegisterCallback<NavigationSubmitEvent>(e=>{if(target.enabledInHierarchy)command();e.StopPropagation();});
            }
        }
        bool Inside(Vector3 position)=>target.worldBound.Contains(new Vector2(position.x,position.y));
        void Down(PointerDownEvent e)
        {
            if(!target.enabledInHierarchy || (e.pointerType!=UnityEngine.UIElements.PointerType.touch&&e.button!=0) || !Inside(e.position) || held.Contains(e.pointerId) || !onDown&&held.Count>0)return;
            pressBounds=target.worldBound;held.Add(e.pointerId);target.CapturePointer(e.pointerId);visual(true);e.StopPropagation();
            if(onDown)command();
        }
        void Up(PointerUpEvent e)
        {
            bool activate=held.Contains(e.pointerId)&&!onDown&&target.enabledInHierarchy&&pressBounds.Contains((Vector2)e.position);
            Release(e.pointerId);e.StopPropagation();if(activate)command();
        }
        void Release(int pointer)
        {
            if(!held.Remove(pointer))return;
            if(target.HasPointerCapture(pointer))target.ReleasePointer(pointer);
            visual(held.Count>0||keyHeld);
        }
        public void Cancel()
        {
            var pointers=new List<int>(held);held.Clear();keyHeld=false;
            foreach(int pointer in pointers)if(target.HasPointerCapture(pointer))target.ReleasePointer(pointer);
            visual(false);
        }
    }
    public sealed partial class GameHud
    {
        readonly List<ReliablePress> reliablePresses=new();
        bool cancellingPresses;
        public void CancelUiPresses()
        {
            if(cancellingPresses)return;cancellingPresses=true;
            try
            {
                CancelGesture();CancelSpawnerTouches();foreach(var press in reliablePresses)press.Cancel();
                var panel=root.panel;if(panel==null)return;
                for(int id=0;id<PointerId.maxPointers;id++)
                    if(panel.GetCapturingElement(id) is VisualElement element && (element==root||root.Contains(element)))element.ReleasePointer(id);
            }
            finally{cancellingPresses=false;}
        }
    }
}
