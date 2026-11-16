using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        VisualElement spawnerOverlay,spawnerCracks;
        Label spawnerCount,spawnerTime,spawnerHelp,blockHealthHint;
        VisualElement spawnerTarget,spawnerImage;Button spawnerStart;
        float healthHintUntil;
#if UNITY_ANDROID && !UNITY_EDITOR
        readonly System.Collections.Generic.HashSet<int> trialFingers=new();
        int touchFrame=-1;
#endif
        void CancelSpawnerTouches()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            trialFingers.Clear();touchFrame=-1;
#endif
            spawnerImage?.RemoveFromClassList("spawner-hit");
        }
        void ReadSpawnerTouches()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(touchFrame==Time.frameCount)return;touchFrame=Time.frameCount;
            for(int index=0;index<Input.touchCount;index++)
            {
                var touch=Input.GetTouch(index);
                if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled){trialFingers.Remove(touch.fingerId);continue;}
                if(touch.phase!=TouchPhase.Began||trialFingers.Contains(touch.fingerId))continue;
                var screen=new Vector2(touch.position.x,Screen.height-touch.position.y);
                var point=RuntimePanelUtils.ScreenToPanel(root.panel,screen);
                if(!spawnerTarget.worldBound.Contains(point))continue;
                trialFingers.Add(touch.fingerId);
                if(game.World.TapTrial())game.Audio?.MaterialHit(BlockKind.Stone);
            }
            spawnerImage.EnableInClassList("spawner-hit",trialFingers.Count>0);
#endif
        }
        void BuildSpawner()
        {
            spawnerOverlay=Box(root,"spawner-overlay");spawnerOverlay.name="spawner-overlay";
            var card=Box(spawnerOverlay,"modal-card spawner-card");
            BoldText(card,"Сломай спавнер","title");
            spawnerHelp=Text(card,"30 нажатий за 8 секунд. Не успеешь — потеряешь 30% максимальной прочности этой кирки.","spawner-help");
            spawnerTime=BoldText(card,"8,0 с","spawner-count");spawnerTime.name="spawner-time";
            spawnerTarget=Box(card,"stone-button spawner-target");spawnerTarget.name="spawner-target";
            spawnerImage=Icon(spawnerTarget,PixelArt.Block(BlockKind.Spawner,0),"spawner-image");spawnerImage.pickingMode=PickingMode.Ignore;
            spawnerCracks=Box(spawnerImage,"spawner-cracks");spawnerCracks.pickingMode=PickingMode.Ignore;
#if !UNITY_ANDROID || UNITY_EDITOR
            reliablePresses.Add(new ReliablePress(spawnerTarget,()=>{
                if(game.World.TapTrial())game.Audio?.MaterialHit(BlockKind.Stone);
            },true,value=>spawnerImage.EnableInClassList("spawner-hit",value)));
#endif
            spawnerCount=BoldText(card,"0 / 30","spawner-count");spawnerCount.name="spawner-count";
            spawnerStart=Button(card,"НАЧАТЬ",()=>game.World.StartTrial(),"stone-button primary");spawnerStart.name="spawner-start";
            Button(card,"ПАУЗА",game.TogglePause,"stone-button secondary").name="spawner-pause";
        }
        void TickSpawner()
        {
            if(spawnerOverlay==null)BuildSpawner();
            var state=game.World.ActiveTrial;bool show=state!=null && game.Phase==RunPhase.Mining && !game.Paused && !settingsOpen;
            if(spawnerOverlay.style.display!=DisplayStyle.None&&!show)CancelUiPresses();
            Display(spawnerOverlay,show);if(!show)return;
            SetText(spawnerCount,state.hits+" / "+SpawnerState.RequiredTaps);
            SetText(spawnerTime,state.done?(state.won?"ГОТОВО!":"ВРЕМЯ ВЫШЛО"):state.remaining.ToString("0.0")+" с");
            SetText(spawnerHelp,state.done?(state.won?"Награда в рюкзаке.":"Кирка потеряла 30% максимальной прочности."):"30 нажатий за 8 секунд. Не успеешь — потеряешь 30% максимальной прочности этой кирки.");
            Display(spawnerStart,!state.started&&!state.done);if(spawnerTarget.enabledSelf&&(!state.started||state.done))CancelUiPresses();spawnerTarget.SetEnabled(state.started&&!state.done);
            if(state.started&&!state.done&&!game.SystemSuspended)ReadSpawnerTouches();
            int stage=Mathf.Clamp(Mathf.CeilToInt(state.hits/(float)SpawnerState.RequiredTaps*8),0,8);
            Display(spawnerCracks,stage>0);if(stage>0)spawnerCracks.style.backgroundImage=Background.FromSprite(PixelArt.Cracks(stage));
        }
        public void ShowBlockHealth(MineBlock block)
        {
            if(blockHealthHint==null){blockHealthHint=BoldText(root,"","block-health-hint");blockHealthHint.name="block-health-hint";blockHealthHint.pickingMode=PickingMode.Ignore;}
            blockHealthHint.text="Обсидиан · "+Progression.Number(block.Health)+" / "+Progression.Number(block.MaxHealthHalfUnits*.5f);
            healthHintUntil=Time.unscaledTime+1.4f;Display(blockHealthHint,true);
        }
        void TickBlockHealth(){if(blockHealthHint!=null)Display(blockHealthHint,Time.unscaledTime<healthHintUntil && !game.WorldFrozen && game.Phase==RunPhase.Mining);}
    }
}
