using System;
using UnityEngine;

namespace PickaxeFall
{
    public sealed class ResourceInventory
    {
        public static readonly BlockKind[] Materials =
        {
            BlockKind.Dirt, BlockKind.Stone, BlockKind.Coal, BlockKind.Copper,
            BlockKind.Iron, BlockKind.Gold, BlockKind.Diamond, BlockKind.Deepstone
        };
        readonly int[] counts = new int[Enum.GetValues(typeof(BlockKind)).Length];
        bool dirty;
        const string Prefix = "inventory.v1.";
        public int this[BlockKind kind] => counts[(int)kind];
        public long Total { get { long sum = 0; foreach (var kind in Materials) sum += this[kind]; return sum; } }

        public static bool IsMaterial(BlockKind kind) => kind >= BlockKind.Dirt && kind <= BlockKind.Deepstone;

        public void Load()
        {
            foreach (var kind in Materials) counts[(int)kind] = Mathf.Max(0, PlayerPrefs.GetInt(Prefix + kind, 0));
            dirty = false;
        }

        public void Add(BlockKind kind)
        {
            if (!IsMaterial(kind)) return;
            if (counts[(int)kind] < int.MaxValue) counts[(int)kind]++;
            dirty = true;
        }

        public void Save()
        {
            if (!dirty) return;
            foreach (var kind in Materials) PlayerPrefs.SetInt(Prefix + kind, counts[(int)kind]);
            PlayerPrefs.Save(); dirty = false;
        }
    }
}
