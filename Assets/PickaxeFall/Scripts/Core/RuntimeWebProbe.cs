#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
namespace PickaxeFall
{
    public partial class GameController
    {
        [Serializable] sealed class ProbeState
        {
            public string phase,language,error;public bool soundSuspended;public float mouseX,mouseY;public int ticks,depth;public long coins;public bool frozen,adBusy,paused,rewarded;
            public double afterSeconds,runSeconds,nextAd;public long rewardValue;public int hits;public float remaining;
            public List<ProbeButton> buttons=new();public List<string> russian=new();
        }
        [Serializable] sealed class ProbeButton {public string name,text;public float x,y,width,height;}
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern void PF_Probe(string json);
#endif
        // Development player only. No cheats, test intents or probe bridge in public builds.
        public void WebProbe(string command)
        {
            try
            {
                if(!initialized)
                {
                    var loading=new ProbeState{phase="Loading",language=L.Language};
                    foreach(var doc in UnityEngine.Object.FindObjectsByType<UIDocument>())ReadProbeButtons(doc.rootVisualElement,loading);
                    PublishProbe(JsonUtility.ToJson(loading));return;
                }
                if(command=="prepare")
                {
                    Settings.SetTutorialSeen(true);Settings.SetTutorialHints(int.MaxValue);Settings.Save();
                    Economy.Profile.coins=100000;foreach(var kind in ResourceInventory.Materials){Economy.Profile.resources[(int)kind]=100;Economy.Profile.discovered[(int)kind]=true;}
                    Economy.Profile.profileLevel=5;Economy.Profile.bridgeStone=1000;Economy.Profile.bridgeIron=50;Economy.Profile.portalObsidian=10;Economy.Profile.dashUnlocked=true;Economy.Profile.plungeUnlocked=true;
                    SaveProgress();hud.CloseModal();Notify();
                }
                else if(command=="run")BeginProbeRun(4,8127,1);
                else if(command=="spawner"){BeginProbeRun(4,8127,1);World.DebugSetCell(-10,10,BlockKind.Spawner).DestroyBlock(SelectedTool);}
                else if(command=="long-ad"){Platform.Ads.runSeconds=300;Platform.Ads.afterSeconds=120;}
                else if(command=="finish"){GiveResource(BlockKind.Stone,10,SelectedTool,Vector2.zero);FinishRun();}
                else if(command=="pause")TogglePause();
                else if(command=="save")SaveProgress();
                var state=new ProbeState{mouseX=Input.mousePosition.x,mouseY=Input.mousePosition.y,soundSuspended=Audio.IsSuspended,phase=Phase.ToString(),language=L.Language,ticks=Clock.Ticks,depth=Depth,coins=Wallet,frozen=WorldFrozen,paused=Paused,adBusy=Platform.Busy,afterSeconds=Platform.Ads.afterSeconds,runSeconds=Platform.Ads.runSeconds,nextAd=Platform.Ads.nextRunAd,rewarded=Platform.Ads.rewarded,rewardValue=Platform.Ads.rewardValue,hits=World.ActiveTrial?.hits??-1,remaining=World.ActiveTrial?.remaining??-1};
                var root=GetComponent<UIDocument>().rootVisualElement;var all=root.worldBound;
                ReadProbeButtons(root,state);
                var target=root.Q("spawner-target");if(target!=null&&target.enabledInHierarchy){var r=target.worldBound;state.buttons.Add(new ProbeButton{name="spawner-target",x=r.center.x/all.width,y=r.center.y/all.height,width=r.width/all.width,height=r.height/all.height});}
                root.Query<TextElement>().ForEach(text=>{if(text.resolvedStyle.display!=DisplayStyle.None&&text.visible&&text.text!="Русский"&&System.Text.RegularExpressions.Regex.IsMatch(text.text??"","[А-Яа-яЁё]"))state.russian.Add(text.text);});
                PublishProbe(JsonUtility.ToJson(state));
            }
            catch(Exception e){PublishProbe(JsonUtility.ToJson(new ProbeState{error=e.Message}));}
        }
        static void ReadProbeButtons(VisualElement root,ProbeState state)
        {
            var all=root.worldBound;root.Query<Button>().ForEach(button=>{var r=button.worldBound;if(r.width>0&&r.height>0&&button.resolvedStyle.display!=DisplayStyle.None&&button.visible)state.buttons.Add(new ProbeButton{name=button.name,text=button.text,x=r.center.x/all.width,y=r.center.y/all.height,width=r.width/all.width,height=r.height/all.height});});
        }
        static void PublishProbe(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PF_Probe(json);
#else
            Debug.Log("PICKAXEFALL_WEB_PROBE: "+json);
#endif
        }
    }
}
#endif
