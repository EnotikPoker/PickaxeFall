#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    // Rendered integration checks use an isolated profile, like the other runtime checks.
    public sealed class RuntimeVisualChecks : MonoBehaviour
    {
        int checks;
        void Check(bool value,string message){checks++;if(!value)throw new Exception("Visual refresh: "+message);}
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)Application.Quit(1);}
        static void Tap(VisualElement target)=>RuntimeInputLightingChecks.Tap(target);
        static IEnumerator Layout(){yield return null;yield return null;yield return null;}
        static IEnumerator Screenshot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("/tmp/pickaxefall-refresh-"+name+".png");
            yield return null;yield return null;
        }
        void CheckWorldBackgrounds(Camera camera,Transform cave,SpriteRenderer sky,Vector3 skyPosition,Vector3 skyScale,
            System.Collections.Generic.Dictionary<string,(Vector3 position,Vector3 scale)> poses,int map)
        {
            Check(sky.transform.position==skyPosition&&sky.transform.localScale==skyScale,"surface panorama is fixed in world coordinates");
            var tiles=cave.GetComponentsInChildren<SpriteRenderer>();
            foreach(var tile in tiles)
            {
                if(poses.TryGetValue(tile.name,out var previous))
                    Check(tile.transform.position==previous.position&&tile.transform.localScale==previous.scale,"cave tile never moves or rescales: "+tile.name);
                else poses.Add(tile.name,(tile.transform.position,tile.transform.localScale));
                Check(tile.sprite==PixelArt.CaveWall((MapId)map),"cave tile uses the selected location artwork");
            }
            for(int y=0;y<=8;y++)for(int x=0;x<=4;x++)
            {
                Vector3 point=camera.ViewportToWorldPoint(new Vector3(x/4f,y/8f,14));if(point.y>0)continue;
                bool covered=false;
                foreach(var tile in tiles)
                {
                    var bounds=tile.bounds;
                    if(point.x>=bounds.min.x-.001f&&point.x<=bounds.max.x+.001f&&point.y>=bounds.min.y-.001f&&point.y<=bounds.max.y+.001f){covered=true;break;}
                }
                Check(covered,"static cave tiles cover the visible underground area");
            }
        }
        IEnumerator CheckBackgrounds(GameController game,VisualElement root)
        {
            var camera=Camera.main;var framing=camera.GetComponent<MineCamera>();
            var cave=GameObject.Find("Static cave backgrounds").transform;
            var sky=GameObject.Find("Sky, clouds and distant forest").GetComponent<SpriteRenderer>();
            for(int map=0;map<3;map++)
            {
                game.FinishRun();game.NewRun();Check(game.SelectMap((MapId)map),"select background map "+map);
                game.BeginProbeRun(4,8127,1);yield return Layout();
                Vector3 skyPosition=sky.transform.position,skyScale=sky.transform.localScale;
                var poses=new System.Collections.Generic.Dictionary<string,(Vector3 position,Vector3 scale)>();
                Check(Mathf.Abs(sky.bounds.size.x-80)<.01f&&Mathf.Abs(sky.bounds.size.y-40)<.01f,"surface illustration retains its original world dimensions "+map);
                yield return Screenshot("world-static-surface-"+map);
                float startY=camera.transform.position.y;
                var reference=game.World.DebugSetCell(-12,40,BlockKind.Obsidian);
                var tile=cave.Find("Cave background 0,0");
                Vector3 sample=tile.InverseTransformPoint(new Vector3(reference.transform.position.x,reference.transform.position.y,tile.position.z));
                // Real physics and smooth following, including horizontal movement and a dash.
                game.Pickaxe.DebugPlace(new Vector2(8,-40),new Vector2(-2,-4));game.StepMiningWorld();
                for(int frame=0;frame<40;frame++)
                {
                    yield return new WaitForEndOfFrame();
                    CheckWorldBackgrounds(camera,cave,sky,skyPosition,skyScale,poses,map);
                    Vector3 worldSample=tile.TransformPoint(sample),blockPosition=reference.transform.position;
                    Check(Mathf.Abs(worldSample.x-blockPosition.x)<.001f&&Mathf.Abs(worldSample.y-blockPosition.y)<.001f,"the same background pixel remains beside the same block");
                }
                Check(camera.transform.position.y<startY-10,"camera actually descended during the check "+map);
                game.Energy.Ensure(game.Energy.Maximum);
                Check(game.TryUseAbility(AbilityKind.Dash,Vector2.right),"dash started during background check "+map);
                for(int frame=0;frame<20;frame++){yield return new WaitForEndOfFrame();CheckWorldBackgrounds(camera,cave,sky,skyPosition,skyScale,poses,map);}
                yield return Screenshot("world-static-descent-"+map);
                Tap(root.Q("toggle-hud"));yield return Layout();
                CheckWorldBackgrounds(camera,cave,sky,skyPosition,skyScale,poses,map);
                Check(root.ClassListContains("hud-hidden"),"HUD was hidden while checking background anchors");
                Tap(root.Q("toggle-hud"));
                game.Pickaxe.Body.simulated=false;
                foreach(float depth in new[]{179f,181f,359f,361f,10000f,179f})
                {
                    game.Pickaxe.DebugPlace(new Vector2(-7,-depth),Vector2.zero);game.StepMiningWorld();framing.SnapToTools();yield return Layout();
                    CheckWorldBackgrounds(camera,cave,sky,skyPosition,skyScale,poses,map);
                    Check(cave.childCount<50,"offscreen cave tiles are unloaded");
                }
                yield return Screenshot("world-static-tile-boundary-"+map);
                game.BeginProbeRun(4,8127,2);
                game.Pickaxes[0].Body.simulated=game.Pickaxes[1].Body.simulated=false;
                game.Pickaxes[0].DebugPlace(new Vector2(-7,-155),Vector2.zero);game.Pickaxes[1].DebugPlace(new Vector2(7,-205),Vector2.zero);
                game.StepMiningWorld();framing.SnapToTools();yield return Layout();
                CheckWorldBackgrounds(camera,cave,sky,skyPosition,skyScale,poses,map);
            }
        }
        IEnumerator Start()
        {
            yield return null;
            var game=GetComponent<GameController>();var root=GetComponent<UIDocument>().rootVisualElement;
            game.Settings.SetTutorialSeen(true);game.Settings.SetTutorialHints(int.MaxValue);game.NewRun();
            for(int map=0;map<3;map++)
            {
                var id=(MapId)map;
                var wall=PixelArt.CaveWall(id);var landscape=PixelArt.WorldLandscape(id);
                string prefix=map==0?"Mine":map==1?"Desert":"Nether";
                Check(wall==Resources.Load<Sprite>("Backgrounds/"+prefix+"CaveWall"),prefix+" supplied cave wall");
                Check(landscape==Resources.Load<Sprite>("Backgrounds/"+prefix+"Landscape"),prefix+" supplied landscape");
                Check(landscape.texture.width==256&&landscape.texture.height==128,prefix+" current 256px landscape, not the old mine image");
                Tap(root.Q("open-maps"));Tap(root.Q("map-page-"+map));yield return Layout();
                var scene=root.Q("construction-scene");var board=scene.Q(className:"construction-board");
                Rect frame=scene.contentRect,bounds=board.worldBound;
                Debug.Log($"PICKAXEFALL_MAP_FRAME: {prefix}; frame={frame}; board={bounds}; styleHeight={scene.resolvedStyle.height}; border={scene.resolvedStyle.borderTopWidth},{scene.resolvedStyle.borderBottomWidth}");
                yield return Screenshot(prefix.ToLowerInvariant()+"-map");
                Check(Mathf.Abs(frame.width-frame.height*2)<4,"2:1 illustration frame "+prefix);
                Check(Mathf.Abs(bounds.width-frame.width)<3&&Mathf.Abs(bounds.height-frame.height)<4,"full illustration without gaps "+prefix);
                Check(root.Q<Button>("map-page-"+map).worldBound.width>0,"visible route navigation "+prefix);
                Tap(root.Q("map-page-1"));yield return Layout();
                Check(root.Q("contribute-Bridge")!=null,"construction available before bridge completion");
                Tap(root.Q(className:"modal-card").Query<Button>().Last());
            }
            game.Economy.Profile.bridgeStone=1000;game.Economy.Profile.bridgeIron=50;game.Economy.Profile.portalObsidian=10;
            Tap(root.Q("open-maps"));Tap(root.Q("map-page-1"));yield return Layout();
            Check(root.Q("contribute-Bridge")==null,"completed bridge hides contribution button");
            Check(root.Q(className:"map-materials").resolvedStyle.display==DisplayStyle.None,"completed bridge hides recipe");
            yield return Screenshot("bridge-complete");
            Tap(root.Q("map-page-2"));yield return Layout();yield return Screenshot("portal-complete");
            Tap(root.Q("map-page-1"));Tap(root.Q("map-enter-1"));yield return Layout();
            Check(game.CurrentMap==MapId.Desert,"completed destination can be selected");
            game.Economy.Profile.dashUnlocked=true;game.Economy.Profile.plungeUnlocked=true;
            game.BeginProbeRun(4,8127,1);yield return Layout();
            // Freeze physics, preserving real UI frames and a fractional internal energy value.
            game.Pickaxe.Body.simulated=false;game.Energy.TrySpend(10.25f);game.TogglePause();yield return Layout();
            var label=root.Q<Label>("run-energy");
            Check(label.text==L.F("ЭНЕРГИЯ {0} / {1}",Mathf.FloorToInt(game.Energy.Value),Mathf.FloorToInt(game.Energy.Maximum)),"energy shows integers");
            Check(Mathf.Abs(game.Energy.Value-Mathf.Round(game.Energy.Value))>.01f,"fractional energy retained in simulation");
            game.TogglePause();yield return Layout();var toggle=root.Q<Button>("toggle-hud");
            Check(toggle.resolvedStyle.display!=DisplayStyle.None,"toggle available during run");
            float oldHeight=game.GameplayViewport.height;int abilities=game.Benefits.Abilities;
            RuntimeInputLightingChecks.TouchEvent(toggle,0,TouchPhase.Began);RuntimeInputLightingChecks.TouchEvent(toggle,0,TouchPhase.Ended);
            yield return Layout();
            Check(root.ClassListContains("hud-hidden"),"pointer tap hides HUD");
            Check(game.Benefits.Abilities==abilities,"toggle does not fire an ability");
            Check(root.Q(className:"header").resolvedStyle.display==DisplayStyle.None,"header hidden");
            Check(root.Q(className:"mining-panel").resolvedStyle.display==DisplayStyle.None,"tool panel hidden");
            Check(game.GameplayViewport.height>oldHeight,"camera uses expanded playfield");
            Check(toggle.worldBound.width>=40&&toggle.worldBound.height>=40,"restore button remains usable");
            yield return Screenshot("hud-hidden");Tap(toggle);yield return Layout();
            Check(!root.ClassListContains("hud-hidden")&&root.Q(className:"header").resolvedStyle.display!=DisplayStyle.None,"restore HUD");
            yield return Screenshot("hud-visible");
            Tap(toggle);game.FinishRun();yield return new WaitForSecondsRealtime(1);
            Check(!root.ClassListContains("hud-hidden"),"end of run restores HUD");
            Check(toggle.resolvedStyle.display==DisplayStyle.None,"hub hides toggle");
            game.ChangeLanguage("en");yield return Layout();Tap(root.Q("settings-close"));
            game.BeginProbeRun(0,8127,1);yield return Layout();
            Check(root.Q<Button>("toggle-hud").tooltip=="Hide HUD","English tooltip");
            yield return CheckBackgrounds(game,root);
            Debug.Log("PICKAXEFALL_VISUAL_REFRESH_OK: "+checks+" checks; fixed world background anchors during physics, dashes, HUD changes, zoom, deep streaming and tile crossings; maps and energy precision.");
            Application.Quit(0);
        }
    }
}
#endif
