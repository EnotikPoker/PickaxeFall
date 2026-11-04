using System;

namespace PickaxeFall
{
    public sealed class ResourceInventory
    {
        public static readonly BlockKind[] Materials = { BlockKind.Dirt, BlockKind.Stone, BlockKind.Coal, BlockKind.Copper,
            BlockKind.Iron, BlockKind.Gold, BlockKind.Diamond, BlockKind.Deepstone,
            BlockKind.Emerald,BlockKind.Lapis,BlockKind.Redstone,BlockKind.Amethyst };
        public const int SlotCount = 14; // Existing workbench/TNT IDs 8/9 stay reserved.
        readonly EconomyService economy;
        public ResourceInventory(EconomyService owner) { economy = owner; }
        public long this[BlockKind kind] => IsMaterial(kind) ? economy.Profile.resources[(int)DropKind(kind)] : 0;
        public long Total
        {
            get { long sum = 0; foreach (var kind in Materials) { if (long.MaxValue - sum < this[kind]) return long.MaxValue; sum += this[kind]; } return sum; }
        }
        public static bool IsMaterial(BlockKind kind) => kind >= BlockKind.Dirt && kind <= BlockKind.Deepstone || kind >= BlockKind.Emerald && kind <= BlockKind.MossyCobblestone;
        public static bool IsResource(BlockKind kind) => Array.IndexOf(Materials,kind)>=0;
        public static BlockKind DropKind(BlockKind kind) => kind switch
        { BlockKind.Cobblestone or BlockKind.MossyCobblestone => BlockKind.Stone, BlockKind.AmethystCluster => BlockKind.Amethyst, _ => kind };
        public void Add(BlockKind kind, long quantity = 1) => economy.Collect(kind, quantity);
        public void Save() => economy.Save();
    }
}
