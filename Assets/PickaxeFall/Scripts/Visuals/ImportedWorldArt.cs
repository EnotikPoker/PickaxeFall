using System.Collections.Generic;
using UnityEngine;
namespace PickaxeFall
{
    public static partial class PixelArt
    {
        static readonly (int key,(string prefix,int count) art)[] ImportedWorldCatalog={
            ((int)BlockKind.Amethyst*64+(int)BlockKind.Amethyst, ("Blocks/World/Mine/Amethyst/Amethyst_Amethyst_",4)),
            ((int)BlockKind.Amethyst*64+(int)BlockKind.DarkQuartzite, ("Blocks/World/Desert/Amethyst_DarkQuartzite/Amethyst_DarkQuartzite_",4)),
            ((int)BlockKind.AmethystCluster*64+(int)BlockKind.AmethystCluster, ("Blocks/World/Mine/Cluster/AmethystCluster_AmethystCluster_",4)),
            ((int)BlockKind.AncientDebris*64+(int)BlockKind.Blackstone, ("Blocks/World/Nether/AncientDebris/AncientDebris_Blackstone_",4)),
            ((int)BlockKind.Basalt*64+(int)BlockKind.Basalt, ("Blocks/World/Nether/Basalt/Basalt_Basalt_",4)),
            ((int)BlockKind.Blackstone*64+(int)BlockKind.Blackstone, ("Blocks/World/Nether/Blackstone/Blackstone_Blackstone_",4)),
            ((int)BlockKind.Chest*64+(int)BlockKind.Chest, ("Blocks/World/Structures/Chest/Chest_Chest_",1)),
            ((int)BlockKind.Cinnabar*64+(int)BlockKind.Basalt, ("Blocks/World/Nether/Cinnabar/Cinnabar_Basalt_",4)),
            ((int)BlockKind.Copper*64+(int)BlockKind.Sandstone, ("Blocks/World/Desert/Copper_Sandstone/Copper_Sandstone_",4)),
            ((int)BlockKind.DarkQuartzite*64+(int)BlockKind.DarkQuartzite, ("Blocks/World/Desert/DarkQuartzite/DarkQuartzite_DarkQuartzite_",4)),
            ((int)BlockKind.Emerald*64+(int)BlockKind.Deepstone, ("Blocks/World/Mine/Emerald/Emerald_Deepstone_",4)),
            ((int)BlockKind.FireOpal*64+(int)BlockKind.Basalt, ("Blocks/World/Nether/FireOpal_Basalt/FireOpal_Basalt_",4)),
            ((int)BlockKind.FireOpal*64+(int)BlockKind.Blackstone, ("Blocks/World/Nether/FireOpal_Blackstone/FireOpal_Blackstone_",4)),
            ((int)BlockKind.Garnet*64+(int)BlockKind.DarkQuartzite, ("Blocks/World/Desert/Garnet_DarkQuartzite/Garnet_DarkQuartzite_",4)),
            ((int)BlockKind.Gold*64+(int)BlockKind.DarkQuartzite, ("Blocks/World/Desert/Gold_DarkQuartzite/Gold_DarkQuartzite_",4)),
            ((int)BlockKind.Gold*64+(int)BlockKind.Sandstone, ("Blocks/World/Desert/Gold_Sandstone/Gold_Sandstone_",4)),
            ((int)BlockKind.Lapis*64+(int)BlockKind.Deepstone, ("Blocks/World/Mine/Lapis/Lapis_Deepstone_",4)),
            ((int)BlockKind.Malachite*64+(int)BlockKind.Sandstone, ("Blocks/World/Desert/Malachite_Sandstone/Malachite_Sandstone_",4)),
            ((int)BlockKind.MinecartChest*64+(int)BlockKind.MinecartChest, ("Blocks/World/Structures/Minecart/MinecartChest_MinecartChest_",1)),
            ((int)BlockKind.Netherrack*64+(int)BlockKind.Netherrack, ("Blocks/World/Nether/Netherrack/Netherrack_Netherrack_",4)),
            ((int)BlockKind.Quartz*64+(int)BlockKind.Basalt, ("Blocks/World/Nether/Quartz_Basalt/Quartz_Basalt_",4)),
            ((int)BlockKind.Quartz*64+(int)BlockKind.Netherrack, ("Blocks/World/Nether/Quartz/Quartz_Netherrack_",4)),
            ((int)BlockKind.Rail*64+(int)BlockKind.Rail, ("Blocks/World/Structures/Rail/Rail_Rail_",1)),
            ((int)BlockKind.Redstone*64+(int)BlockKind.Deepstone, ("Blocks/World/Mine/Redstone/Redstone_Deepstone_",4)),
            ((int)BlockKind.Sand*64+(int)BlockKind.Sand, ("Blocks/World/Desert/Sand/Sand_Sand_",4)),
            ((int)BlockKind.Sandstone*64+(int)BlockKind.Sandstone, ("Blocks/World/Desert/Sandstone/Sandstone_Sandstone_",4)),
            ((int)BlockKind.SmokyQuartz*64+(int)BlockKind.DarkQuartzite, ("Blocks/World/Desert/SmokyQuartz/SmokyQuartz_DarkQuartzite_",4)),
            ((int)BlockKind.SoulCrystal*64+(int)BlockKind.Blackstone, ("Blocks/World/Nether/SoulCrystal/SoulCrystal_Blackstone_",4)),
            ((int)BlockKind.Sulfur*64+(int)BlockKind.Netherrack, ("Blocks/World/Nether/Sulfur/Sulfur_Netherrack_",4)),
            ((int)BlockKind.Turquoise*64+(int)BlockKind.Sandstone, ("Blocks/World/Desert/Turquoise_Sandstone/Turquoise_Sandstone_",4)),
        };
        static readonly Dictionary<int,Sprite> importedCache=new();
        static Sprite ImportedWorld(BlockKind kind,int variant,BlockKind host)
        {
            int key=(int)kind*64+(int)host;int cacheKey=key*4+Mathf.Abs(variant%4);if(importedCache.TryGetValue(cacheKey,out var cached))return cached;
            foreach(var entry in ImportedWorldCatalog)if(entry.key==key)return importedCache[cacheKey]=Resources.Load<Sprite>(entry.art.prefix+Mathf.Abs(variant%entry.art.count));
            // UI icons and standalone structures have no surrounding host rock.
            if(host==kind || kind==BlockKind.Chest || kind==BlockKind.MinecartChest || kind==BlockKind.Rail || kind==BlockKind.AmethystCluster)
                foreach(var entry in ImportedWorldCatalog)if(entry.key/64==(int)kind)return importedCache[cacheKey]=Resources.Load<Sprite>(entry.art.prefix+Mathf.Abs(variant%entry.art.count));
            return importedCache[cacheKey]=null;
        }
    }
}
