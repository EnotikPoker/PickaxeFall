using System;
using System.Collections.Generic;
using UnityEngine;
namespace PickaxeFall
{
    [Serializable] public sealed class SpawnerState
    {
        public const int RequiredTaps=30;
        public const float Duration=8;
        public int x,row,hits,sourceTool;
        public float until,remaining=Duration,resultRemaining;
        public bool done,started,won;
        // Retained for importing saves made before 0.8.1.
        public int[] lastHits={-1,-1,-1,-1,-1};
        public SpawnerState Copy()=>new(){x=x,row=row,hits=hits,sourceTool=sourceTool,until=until,remaining=remaining,resultRemaining=resultRemaining,done=done,started=started,won=won,lastHits=(int[])lastHits.Clone()};
    }
    public partial class MiningWorld
    {
        readonly Dictionary<Vector2Int,SpawnerState> trials=new();
        public IEnumerable<SpawnerState> Trials=>trials.Values;
        public SpawnerState ActiveTrial
        {
            get{foreach(var t in trials.Values)if(!t.done || t.resultRemaining>0)return t;return null;}
        }
        public SpawnerState[] CaptureTrials(){var list=new List<SpawnerState>();foreach(var t in trials.Values)list.Add(t.Copy());list.Sort((a,b)=>a.row==b.row?a.x.CompareTo(b.x):a.row.CompareTo(b.row));return list.ToArray();}
        public void RestoreTrials(SpawnerState[] saved,bool legacy=false,int selectedTool=0)
        {
            trials.Clear();if(saved==null)return;
            bool activeRestored=false;
            foreach(var old in saved)
            {
                var t=old.Copy();
                if(legacy)
                {
                    t.sourceTool=selectedTool;t.started=false;t.remaining=SpawnerState.Duration;t.resultRemaining=0;
                    if(!t.done){t.hits=0;if(activeRestored)continue;activeRestored=true;}
                }
                trials[new Vector2Int(t.x,t.row)]=t;
            }
        }
        public void TrialHit(MineBlock block,FallingPickaxe source,DamageOrigin origin)
        {
            if(!CanMine || block.IsBroken || source==null || !source.IsAlive || origin==DamageOrigin.Explosion || origin==DamageOrigin.Shrapnel)return;
            var key=new Vector2Int(block.Column,block.Row);
            if(trials.ContainsKey(key) || ActiveTrial!=null)return;
            var state=new SpawnerState{x=key.x,row=key.y,sourceTool=source.ToolIndex};trials.Add(key,state);
            foreach(var tool in game.Pickaxes)tool.CancelAbility();
            game.TrialEvent(false);game.TrialChanged();
        }
        public bool StartTrial()
        {
            var t=ActiveTrial;if(t==null || t.done || t.started || game.Paused || game.SystemSuspended)return false;
            t.started=true;game.TrialChanged();return true;
        }
        public bool TapTrial()
        {
            var t=ActiveTrial;if(t==null || !t.started || t.done || t.remaining<=0 || game.Paused || game.SystemSuspended)return false;
            t.hits++;if(t.hits>=SpawnerState.RequiredTaps)CompleteTrial(t,true);return true;
        }
        public void AdvanceTrial(float seconds)
        {
            var t=ActiveTrial;
            if(t==null || seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || game.Paused || game.SystemSuspended)return;
            if(t.done)
            {
                float elapsed=Mathf.Min(seconds,t.resultRemaining);t.resultRemaining=Mathf.Max(0,t.resultRemaining-elapsed);game.Benefits.TrialSeconds+=elapsed;
                if(t.resultRemaining<=.00001f){t.resultRemaining=0;game.TrialChanged();}return;
            }
            if(!t.started)return;
            float delta=Mathf.Min(seconds,t.remaining);game.Benefits.TrialSeconds+=delta;t.remaining=Mathf.Max(0,t.remaining-delta);
            if(t.remaining<=.00001f){t.remaining=0;CompleteTrial(t,false);}
        }
        void CompleteTrial(SpawnerState state,bool success)
        {
            // Retire before awards or damage: those can synchronously save/end a run.
            state.done=true;state.won=success;state.resultRemaining=.8f;
            var block=CellAt(state.x,state.row);if(block)block.RetireTrial();
            else changes[new Vector2Int(state.x,state.row)]=new CellState{kind=BlockKind.Spawner,broken=true};
            var source=game.Pickaxes[state.sourceTool];
            if(success)game.AwardTrial(state.x,state.row,source);
            else{game.TrialEvent(true);if(source.IsAlive)source.LoseTrialDurability();}
            if(game.Phase!=RunPhase.Mining)state.resultRemaining=0;
            game.TrialChanged();
        }
        public void RetireTrial(MineBlock block){ActiveBlocks--;Remember(block);}
        public void TickTrials() { } // Legacy callers; tap trials advance on their own clock.
    }
}
