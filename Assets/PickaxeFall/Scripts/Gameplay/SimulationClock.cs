using System;
using UnityEngine;

namespace PickaxeFall
{
    public sealed class SimulationClock
    {
        public bool Manual { get; set; }
        public int Ticks { get; private set; }
        public float Now => Ticks / 60f;
        public float Delta => 1f / 60;
        public void Reset() => Ticks = 0;
        public void Restore(int ticks)=>Ticks=Math.Max(0,ticks);
        public void Advance() => Ticks++;
    }

    // Independent streams keep unrelated upgrades from shifting each other's rolls.
    public sealed class EffectRolls
    {
        readonly uint[] streams = new uint[Progression.KindCount];
        public readonly long[] Attempts = new long[Progression.KindCount], Successes = new long[Progression.KindCount];
        public readonly int[] Forced = new int[Progression.KindCount];
        public void Reset(int seed)
        {
            Array.Clear(Attempts, 0, Progression.KindCount); Array.Clear(Successes, 0, Progression.KindCount);
            for (int i = 0; i < Progression.KindCount; i++)
            {
                uint x=unchecked((uint)(seed^((i+1)*73856093)));x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;x^=x>>16;
                streams[i]=x==0?0x9e3779b9:x;Forced[i]=-1;
            }
        }
        public bool Roll(UpgradeKind kind, double chance)
        {
            if (chance <= 0) return false;
            int i = (int)kind; Attempts[i]++;
            uint x=streams[i];x^=x<<13;x^=x>>17;x^=x<<5;streams[i]=x;
            bool result = Forced[i] >= 0 ? Forced[i] == 1 : x/4294967296.0 < chance;
            if (result) Successes[i]++;
            return result;
        }
        public int[] Capture(){var result=new int[Progression.KindCount];for(int i=0;i<Progression.KindCount;i++)result[i]=unchecked((int)streams[i]);return result;}
        public void Restore(int[] states,long[] attempts,long[] successes)
        {
            for(int i=0;i<Progression.KindCount;i++){streams[i]=unchecked((uint)states[i]);Attempts[i]=attempts[i];Successes[i]=successes[i];Forced[i]=-1;}
        }
    }
}
