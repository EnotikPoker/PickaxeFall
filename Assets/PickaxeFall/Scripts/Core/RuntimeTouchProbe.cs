#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    // Isolated verification APK only. No test intent or input telemetry in releases.
    [DefaultExecutionOrder(-200)]
    public sealed class RuntimeTouchProbe:MonoBehaviour
    {
        GameController owner;
        void Update()
        {
            if(owner!=null&&owner.TestMode=="touch-edge"&&owner.World.ActiveTrial is {started:true,done:false,hits:29} state)
                foreach(var touch in Input.touches)if(touch.phase==TouchPhase.Began)state.remaining=.000001f;
            foreach(var touch in Input.touches)if(touch.phase==TouchPhase.Began)Debug.Log("RAW_TOUCH_DOWN: "+touch.fingerId);
        }
        IEnumerator Start()
        {
            Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None);
            yield return null;var game=owner=GetComponent<GameController>();var root=GetComponent<UIDocument>().rootVisualElement;
            root.RegisterCallback<PointerDownEvent>(e=>Debug.Log($"TOUCH_OS_DOWN: id={e.pointerId}; button={e.button}; type={e.pointerType}; primary={e.isPrimary}; target={(e.target as VisualElement)?.name}"),TrickleDown.TrickleDown);
            game.Settings.SetTutorialSeen(true);game.Settings.SetTutorialHints(int.MaxValue);game.BeginProbeRun(4,8127,1);
            game.World.DebugSetCell(-10,10,BlockKind.Spawner).DestroyBlock(game.SelectedTool);
            int count=-1;bool? started=null;
            while(true)
            {
                yield return new WaitForSecondsRealtime(.05f);
                var trial=game.World.ActiveTrial;if(trial==null)continue;
                if(started!=trial.started)
                {
                    started=trial.started;if(trial.started&&game.TestMode=="touch-edge")trial.hits=29;var target=root.Q(trial.started?"spawner-target":"spawner-start");var r=target.worldBound;var all=root.worldBound;
                    Debug.Log("PICKAXEFALL_TOUCH_TARGET: "+(trial.started?"tap":"start")+"; x="+Mathf.RoundToInt(r.center.x/all.width*Screen.width)+"; y="+Mathf.RoundToInt(r.center.y/all.height*Screen.height));
                }
                if(count!=trial.hits){count=trial.hits;Debug.Log("PICKAXEFALL_TOUCH_COUNT: "+count+"; done="+trial.done+"; remaining="+trial.remaining);}
            }
        }
    }
}
#endif
