using System;

namespace PickaxeFall
{
    public sealed class ResourceInventory
    {
        public static readonly BlockKind[] Materials = { BlockKind.Dirt, BlockKind.Stone, BlockKind.Coal, BlockKind.Copper,
            BlockKind.Iron, BlockKind.Gold, BlockKind.Diamond, BlockKind.Deepstone,
            BlockKind.Emerald,BlockKind.Lapis,BlockKind.Redstone,BlockKind.Amethyst,BlockKind.Obsidian,BlockKind.Sand,BlockKind.Sandstone,BlockKind.DarkQuartzite,BlockKind.Malachite,BlockKind.Turquoise,BlockKind.Garnet,BlockKind.SmokyQuartz,BlockKind.Netherrack,BlockKind.Basalt,BlockKind.Blackstone,BlockKind.Quartz,BlockKind.Sulfur,BlockKind.Cinnabar,BlockKind.FireOpal,BlockKind.AncientDebris,BlockKind.SoulCrystal };
        public const int SlotCount = 39; // Append-only block IDs; non-resource slots remain reserved.
        readonly EconomyService economy;
        public ResourceInventory(EconomyService owner) { economy = owner; }
        public long this[BlockKind kind] => IsMaterial(kind) ? economy.Profile.resources[(int)DropKind(kind)] : 0;
        public long Total
        {
            get { long sum = 0; foreach (var kind in Materials) { if (long.MaxValue - sum < this[kind]) return long.MaxValue; sum += this[kind]; } return sum; }
        }
        public static bool IsMaterial(BlockKind kind) => kind >= BlockKind.Dirt && kind <= BlockKind.Deepstone || kind >= BlockKind.Emerald && kind <= BlockKind.MossyCobblestone || kind==BlockKind.Obsidian || kind>=BlockKind.Sand && kind<=BlockKind.SoulCrystal || kind==BlockKind.NetherBrick;
        public static bool IsResource(BlockKind kind) => Array.IndexOf(Materials,kind)>=0;
        public static BlockKind DropKind(BlockKind kind) => kind switch
        { BlockKind.NetherBrick=>BlockKind.Basalt, BlockKind.Cobblestone or BlockKind.MossyCobblestone => BlockKind.Stone, BlockKind.AmethystCluster => BlockKind.Amethyst, _ => kind };
        public void Add(BlockKind kind, long quantity = 1) => economy.Collect(kind, quantity);
        public void Save() => economy.Save();
    }
}
