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
        readonly uint[] streams = new uint[17];
        public readonly long[] Attempts = new long[17], Successes = new long[17];
        public readonly int[] Forced = new int[17];
        public void Reset(int seed)
        {
            Array.Clear(Attempts, 0, 17); Array.Clear(Successes, 0, 17);
            for (int i = 0; i < 17; i++)
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
        public int[] Capture(){var result=new int[17];for(int i=0;i<17;i++)result[i]=unchecked((int)streams[i]);return result;}
        public void Restore(int[] states,long[] attempts,long[] successes)
        {
            for(int i=0;i<17;i++){streams[i]=unchecked((uint)states[i]);Attempts[i]=attempts[i];Successes[i]=successes[i];Forced[i]=-1;}
        }
    }
}
