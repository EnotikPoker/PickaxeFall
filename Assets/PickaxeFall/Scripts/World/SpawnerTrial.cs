using System;
using System.Collections.Generic;
using UnityEngine;
namespace PickaxeFall
{
    [Serializable] public sealed class SpawnerState {public int x,row,hits;public float until;public bool done;public int[] lastHits={-1,-1,-1,-1,-1};}
    public partial class MiningWorld
    {
        readonly Dictionary<Vector2Int,SpawnerState> trials=new();
        public IEnumerable<SpawnerState> Trials=>trials.Values;
        public SpawnerState[] CaptureTrials(){var list=new List<SpawnerState>();foreach(var t in trials.Values)list.Add(new SpawnerState{x=t.x,row=t.row,hits=t.hits,until=t.until,done=t.done,lastHits=(int[])t.lastHits.Clone()});list.Sort((a,b)=>a.row==b.row?a.x.CompareTo(b.x):a.row.CompareTo(b.row));return list.ToArray();}
        public void RestoreTrials(SpawnerState[] saved){trials.Clear();if(saved!=null)foreach(var t in saved)trials[new Vector2Int(t.x,t.row)]=new SpawnerState{x=t.x,row=t.row,hits=t.hits,until=t.until,done=t.done,lastHits=(int[])t.lastHits.Clone()};}
        public void TrialHit(MineBlock block,FallingPickaxe source,DamageOrigin origin)
        {
            if(!CanMine || block.IsBroken || source==null || !source.IsAlive || origin==DamageOrigin.Explosion || origin==DamageOrigin.Shrapnel)return;
            var key=new Vector2Int(block.Column,block.Row);
            if(!trials.TryGetValue(key,out var state)){state=new SpawnerState{x=key.x,row=key.y,until=Now+12};trials.Add(key,state);game.TrialEvent(false);}
            if(state.done || Now>=state.until || state.lastHits[source.ToolIndex]==game.Clock.Ticks)return;
            state.lastHits[source.ToolIndex]=game.Clock.Ticks;
            state.hits++;
            if(state.hits>=4){state.done=true;block.RetireTrial();game.AwardTrial(state.x,state.row,source);}
        }
        public void RetireTrial(MineBlock block){ActiveBlocks--;Remember(block);}
        public void TickTrials(){foreach(var state in trials.Values)if(!state.done && Now>=state.until){state.done=true;var block=CellAt(state.x,state.row);if(block)block.RetireTrial();else changes[new Vector2Int(state.x,state.row)]=new CellState{kind=BlockKind.Spawner,broken=true};game.TrialEvent(true);}}
    }
}
