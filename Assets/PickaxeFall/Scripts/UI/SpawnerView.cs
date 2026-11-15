using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        VisualElement spawnerOverlay,spawnerCracks;
        Label spawnerCount,spawnerTime,spawnerHelp,blockHealthHint;
        Button spawnerTarget,spawnerStart;
        float healthHintUntil;
        void BuildSpawner()
        {
            spawnerOverlay=Box(root,"spawner-overlay");spawnerOverlay.name="spawner-overlay";
            var card=Box(spawnerOverlay,"modal-card spawner-card");
            BoldText(card,"Сломай спавнер","title");
            spawnerHelp=Text(card,"30 нажатий за 8 секунд. Не успеешь — потеряешь 30% максимальной прочности этой кирки.","spawner-help");
            spawnerTime=BoldText(card,"8,0 с","spawner-count");spawnerTime.name="spawner-time";
            spawnerTarget=Button(card,"",()=>{},"stone-button spawner-target",UiSound.None);spawnerTarget.name="spawner-target";
            Icon(spawnerTarget,PixelArt.Block(BlockKind.Spawner,0),"spawner-image").pickingMode=PickingMode.Ignore;
            spawnerCracks=Box(spawnerTarget,"spawner-cracks");spawnerCracks.pickingMode=PickingMode.Ignore;
            // A single primary pointer only; no auto-repeat or simultaneous finger bonus.
            int pointer=-1;
            spawnerTarget.RegisterCallback<PointerDownEvent>(e=>{
                if(pointer>=0 || !e.isPrimary || e.button!=0)return;
                pointer=e.pointerId;spawnerTarget.CapturePointer(pointer);
                if(game.World.TapTrial()){game.Audio?.MaterialHit(BlockKind.Stone);spawnerTarget.style.scale=new Scale(new Vector3(.96f,.96f,1));}
                e.StopPropagation();
            });
            spawnerTarget.RegisterCallback<PointerUpEvent>(e=>{if(e.pointerId==pointer){pointer=-1;spawnerTarget.ReleasePointer(e.pointerId);spawnerTarget.style.scale=new Scale(Vector3.one);}e.StopPropagation();});
            spawnerTarget.RegisterCallback<PointerCaptureOutEvent>(_=>{pointer=-1;spawnerTarget.style.scale=new Scale(Vector3.one);});
            spawnerCount=BoldText(card,"0 / 30","spawner-count");spawnerCount.name="spawner-count";
            spawnerStart=Button(card,"НАЧАТЬ",()=>game.World.StartTrial(),"stone-button primary");spawnerStart.name="spawner-start";
            Button(card,"ПАУЗА",game.TogglePause,"stone-button secondary").name="spawner-pause";
        }
        void TickSpawner()
        {
            if(spawnerOverlay==null)BuildSpawner();
            var state=game.World.ActiveTrial;bool show=state!=null && game.Phase==RunPhase.Mining && !game.Paused && !settingsOpen;
            Display(spawnerOverlay,show);if(!show)return;
            SetText(spawnerCount,state.hits+" / "+SpawnerState.RequiredTaps);
            SetText(spawnerTime,state.done?(state.won?"ГОТОВО!":"ВРЕМЯ ВЫШЛО"):state.remaining.ToString("0.0")+" с");
            SetText(spawnerHelp,state.done?(state.won?"Награда в рюкзаке.":"Кирка потеряла 30% максимальной прочности."):"30 нажатий за 8 секунд. Не успеешь — потеряешь 30% максимальной прочности этой кирки.");
            Display(spawnerStart,!state.started&&!state.done);spawnerTarget.SetEnabled(state.started&&!state.done);
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
