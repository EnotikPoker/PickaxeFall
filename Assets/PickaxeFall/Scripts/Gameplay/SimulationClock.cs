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
        public void Advance() => Ticks++;
    }

    // Independent streams keep unrelated upgrades from shifting each other's rolls.
    public sealed class EffectRolls
    {
        readonly System.Random[] streams = new System.Random[17];
        public readonly long[] Attempts = new long[17], Successes = new long[17];
        public readonly int[] Forced = new int[17];
        public void Reset(int seed)
        {
            Array.Clear(Attempts, 0, 17); Array.Clear(Successes, 0, 17);
            for (int i = 0; i < 17; i++) { streams[i] = new System.Random(unchecked(seed ^ ((i + 1) * 73856093))); Forced[i] = -1; }
        }
        public bool Roll(UpgradeKind kind, double chance)
        {
            if (chance <= 0) return false;
            int i = (int)kind; Attempts[i]++;
            bool result = Forced[i] >= 0 ? Forced[i] == 1 : streams[i].NextDouble() < chance;
            if (result) Successes[i]++;
            return result;
        }
    }
}
