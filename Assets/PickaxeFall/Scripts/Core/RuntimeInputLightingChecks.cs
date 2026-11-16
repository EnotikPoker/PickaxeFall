#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public sealed class RuntimeInputLightingChecks:MonoBehaviour
    {
        int assertions;
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception)Application.Quit(1);}
        void Check(bool ok,string label){assertions++;if(!ok)throw new Exception("Input/light 0.8.2: "+label);}
        public static void TouchEvent(VisualElement target,int finger,TouchPhase phase,Vector2? point=null)
        {
            var touch=new Touch{fingerId=finger,phase=phase,position=point??target.worldBound.center,pressure=1,maximumPossiblePressure=1,tapCount=1};
            if(phase==TouchPhase.Began){using var e=PointerDownEvent.GetPooled(touch,EventModifiers.None);e.target=target;target.SendEvent(e);}
            else if(phase==TouchPhase.Ended){using var e=PointerUpEvent.GetPooled(touch,EventModifiers.None);e.target=target;target.SendEvent(e);}
            else if(phase==TouchPhase.Canceled){using var e=PointerCancelEvent.GetPooled(touch,EventModifiers.None);e.target=target;target.SendEvent(e);}
            else {using var e=PointerMoveEvent.GetPooled(touch,EventModifiers.None);e.target=target;target.SendEvent(e);}
        }
        public static void Tap(VisualElement target,int finger=0)
        {
            if(target==null)throw new Exception("Missing input target");
            if(target is Button button && button.clickable!=null)
            {
                // Standard menus use their public keyboard/navigation input path.
                using var submit=NavigationSubmitEvent.GetPooled();submit.target=target;target.SendEvent(submit);
            }
            else {TouchEvent(target,finger,TouchPhase.Began);TouchEvent(target,finger,TouchPhase.Ended);}
        }
        IEnumerator Start()
        {
            yield return null;var game=GetComponent<GameController>();var root=GetComponent<UIDocument>().rootVisualElement;
            game.Settings.SetTutorialSeen(true);game.Settings.SetTutorialHints(int.MaxValue);game.NewRun();
            game.BeginProbeRun(4,8127,1);game.World.DebugSetCell(-10,10,BlockKind.Spawner).DestroyBlock(game.SelectedTool);yield return null;yield return null;
            var start=root.Q<Button>("spawner-start");
            Tap(start);yield return null;
            Check(game.World.ActiveTrial.started,"start through UI event dispatcher");
            var target=root.Q("spawner-target");Rect bounds=target.worldBound;
            TouchEvent(target,0,TouchPhase.Began);Check(game.World.ActiveTrial.hits==1,"first touch");
            TouchEvent(target,1,TouchPhase.Began);Check(game.World.ActiveTrial.hits==2,"secondary finger while primary held");
            TouchEvent(target,1,TouchPhase.Moved);TouchEvent(target,0,TouchPhase.Stationary);Check(game.World.ActiveTrial.hits==2,"motion and holding do not add");
            TouchEvent(target,1,TouchPhase.Ended);TouchEvent(target,0,TouchPhase.Ended);
            Check(target.worldBound==bounds,"hit area stays still");
            TouchEvent(target,0,TouchPhase.Began,bounds.min-Vector2.one*10);TouchEvent(target,0,TouchPhase.Moved,bounds.center);TouchEvent(target,0,TouchPhase.Ended);Check(game.World.ActiveTrial.hits==2,"drag in from outside does not count");
            TouchEvent(target,0,TouchPhase.Began);TouchEvent(target,0,TouchPhase.Canceled);Tap(target);Check(game.World.ActiveTrial.hits==4,"cancel releases finger identity");
            TouchEvent(target,0,TouchPhase.Began);game.SendMessage("OnApplicationFocus",false);Tap(target);Check(game.World.ActiveTrial.hits==6,"focus loss clears held UI pointers");TouchEvent(target,0,TouchPhase.Ended);game.SendMessage("OnApplicationFocus",true);
            for(int i=6;i<30;i++)Tap(target,i%2);Check(game.World.ActiveTrial.done&&game.World.ActiveTrial.won,"thirty real UI touches complete trial");
            Tap(target);Check(game.World.ActiveTrial.hits==30,"completion rejects extra touch");game.World.AdvanceTrial(1);yield return null;
            var input=root.Q("main-action");
            // Exact reported sequence, with pointer events on the repaired main action and navigation events on standard menus.
            game.Economy.Profile.bridgeStone=1000;game.Economy.Profile.bridgeIron=50;game.Economy.Profile.portalObsidian=10;
            for(int cycle=0;cycle<100;cycle++)
            {
                game.BeginProbeRun(0,8127+cycle,1);game.FinishRun();yield return new WaitForSecondsRealtime(.9f);yield return null;
                Check(game.Phase==RunPhase.Finished,"finished before repeat "+cycle);Tap(input);yield return null;
                Check(game.Phase==RunPhase.Ready,"repeat -> ready "+cycle);Tap(root.Q("open-maps"));yield return null;
                int map=cycle%3;Tap(root.Q("map-page-"+map));yield return null;
                Tap(root.Q(map==0?"map-enter-mine":"map-enter-"+map));yield return null;
                Check(game.CurrentMap==(MapId)map&&game.Phase==RunPhase.Ready,"map selected "+cycle);
                if(cycle%10==0){TouchEvent(input,0,TouchPhase.Began);game.SendMessage("OnApplicationFocus",false);game.SendMessage("OnApplicationFocus",true);TouchEvent(input,0,TouchPhase.Ended);Check(game.Phase==RunPhase.Ready,"interrupted press does not activate "+cycle);}
                Tap(input);Check(game.Phase==RunPhase.Spinning,"roulette starts after repeat/map "+cycle);
                Tap(input);Check(game.Phase==RunPhase.Spinning,"double tap cannot restart spin "+cycle);
                if(cycle==0)
                {
                    game.SetApplicationSuspended(true);game.AdvanceRoulette(8);Check(game.Phase==RunPhase.Spinning,"suspension freezes roulette");game.SetApplicationSuspended(false);
                    yield return new WaitForSecondsRealtime(2.5f);Check(game.Phase==RunPhase.Equipped,"roulette progresses with native frames");
                }
                else game.AdvanceRoulette(3);
                int tier=game.Pickaxe.Tier;game.AdvanceRoulette(8);Check(game.Phase==RunPhase.Equipped&&game.Pickaxe.Tier==tier,"spin completes once "+cycle);
                Check(!input.ClassListContains("press-held"),"main button releases visual "+cycle);
            }
            game.NewRun();input.Focus();
            using(var submit=NavigationSubmitEvent.GetPooled()){submit.target=input;input.SendEvent(submit);}
            Check(game.Phase==RunPhase.Spinning,"main action supports navigation submit");game.AdvanceRoulette(3);game.NewRun();
            using(var key=KeyDownEvent.GetPooled(new Event{type=EventType.KeyDown,keyCode=KeyCode.Space})){key.target=input;input.SendEvent(key);}
            Check(game.Phase==RunPhase.Ready,"space press waits for release");
            using(var key=KeyUpEvent.GetPooled(new Event{type=EventType.KeyUp,keyCode=KeyCode.Space})){key.target=input;input.SendEvent(key);}
            Check(game.Phase==RunPhase.Spinning,"main action supports space release");game.AdvanceRoulette(3);game.NewRun();
            var mouse=new Event{type=EventType.MouseDown,button=0,mousePosition=input.worldBound.center};
            using(var down=PointerDownEvent.GetPooled(mouse)){down.target=input;input.SendEvent(down);}mouse.type=EventType.MouseUp;
            using(var up=PointerUpEvent.GetPooled(mouse)){up.target=input;input.SendEvent(up);}
            Check(game.Phase==RunPhase.Spinning,"main action supports mouse");game.AdvanceRoulette(3);game.NewRun();game.SelectMap(MapId.Mine);
            // Repeated impacts on an unbroken, immovable floor, not a direct Hit() call.
            float originalWear=game.Balance.lavaWearPerSecond;int originalHealth=game.Balance.Block(BlockKind.Obsidian).health;game.Balance.lavaWearPerSecond=0;game.Balance.Block(BlockKind.Obsidian).health=100000;
            for(int sceneIndex=0;sceneIndex<4;sceneIndex++)foreach(float rotation in new[]{0f,37f,90f,179f})
            {
                int liquid=Mathf.Min(sceneIndex,2);
                game.BeginProbeRun(4,8127,1);game.Balance.lavaWearPerSecond=0;game.Balance.Block(BlockKind.Obsidian).health=100000;var tool=game.SelectedTool;
                for(int row=5;row<=15;row++)for(int x=-12;x<=12;x++)game.World.DebugClearCell(x,row);
                for(int x=-12;x<=12;x++){var b=game.World.DebugSetCell(x,13,BlockKind.Obsidian);b.RestoreHealth(int.MaxValue);}
                for(int row=5;row<=13;row++){game.World.DebugSetCell(-13,row,BlockKind.Obsidian);game.World.DebugSetCell(13,row,BlockKind.Obsidian);}
                if(sceneIndex==3)for(int row=10;row<=12;row++)game.World.DebugSetCell(2,row,BlockKind.Obsidian);
                tool.DebugPlace(new Vector2(sceneIndex==3?.9f:0,-11.7f),new Vector2(.6f,-1),rotation);
                int previous=0,lastStrike=0,maxGap=0;
                for(int step=0;step<600&&tool.IsAlive;step++)
                {
                    if(liquid>0)for(int row=10;row<=12;row++)for(int x=-12;x<=12;x++)game.World.Liquids.DebugSet(x,row,liquid==1?BlockKind.Water:BlockKind.Lava);
                    game.SimulateMiningStep();
                    if(tool.NaturalStrikes>previous){if(previous>0)maxGap=Mathf.Max(maxGap,step-lastStrike);lastStrike=step;previous=tool.NaturalStrikes;}

                }
                if(previous>0)maxGap=Mathf.Max(maxGap,599-lastStrike);
                Check(maxGap<=72,"no prolonged resting liquid "+liquid+" scene="+sceneIndex+" rotation "+rotation+" gap="+maxGap/60f);
                Check(tool.NaturalStrikes>=3,"repeated native impacts liquid "+liquid+" rotation "+rotation+" hits="+tool.NaturalStrikes);
                Debug.Log("PICKAXEFALL_REBOUND_TRACE: liquid="+liquid+"; scene="+sceneIndex+"; rotation="+rotation+"; hits="+tool.NaturalStrikes+"; maxGap="+maxGap/60f);
            }
            game.Balance.lavaWearPerSecond=originalWear;game.Balance.Block(BlockKind.Obsidian).health=originalHealth;game.NewRun();
            // Rendered field bounds and brightness setting, with dedicated in-game screenshots.
            foreach(MapId map in Enum.GetValues(typeof(MapId)))foreach(int depth in new[]{0,75,128,250})
            {
                game.SelectMap(map);game.BeginProbeRun(4,8127,1);var tool=game.SelectedTool;
                tool.DebugPlace(new Vector2(0,-depth+2),Vector2.zero);tool.Body.simulated=false;game.StepMiningWorld();Camera.main.GetComponent<MineCamera>().SnapToTools();game.TogglePause();yield return new WaitForSecondsRealtime(.12f);
                var lighting=game.GetComponentInChildren<MineLighting>();var renderer=lighting.GetComponent<MeshRenderer>();var texture=(Texture2D)renderer.sharedMaterial.mainTexture;
                Check(renderer.sortingOrder<tool.GetComponent<SpriteRenderer>().sortingOrder,"light does not cover tool "+map+"/"+depth);
                foreach(var pixel in texture.GetPixels32())Check(pixel.r>=191&&pixel.g>=191&&pixel.b>=191,"readable field "+map+"/"+depth);
                if(depth==128)Capture(Camera.main,Path.Combine(Application.temporaryCachePath,"lighting-082-"+map+".png"));
                game.NewRun();
            }
            game.SelectMap(MapId.Mine);game.NewRun();Tap(root.Q("open-settings"));yield return null;
            var slider=root.Q<Slider>("settings-mine-brightness");slider.value=1.2f;Check(Mathf.Abs(game.Settings.MineBrightness-1.2f)<.001,"brightness slider applies");Tap(root.Q("settings-close"));game.Settings.Reset();
            Debug.Log("PICKAXEFALL_INPUT_LIGHTING_OK: "+assertions+" checks; 100 finished-repeat-map-spin cycles; real pointer dispatch, secondary fingers, cancellation, native wet impacts, readable lighting.");Application.Quit(0);
        }
        static void Capture(Camera camera,string path)
        {
            var old=camera.targetTexture;var target=RenderTexture.GetTemporary(540,960,16);var previous=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;var image=new Texture2D(540,960,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,540,960),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.Destroy(image);}
            finally{camera.targetTexture=old;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
    }
}
#endif
