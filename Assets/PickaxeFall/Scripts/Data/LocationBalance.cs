using System;
namespace PickaxeFall
{
    [Serializable] public sealed class OreBand
    {
        public BlockKind kind;public double chance;
        public OreBand(BlockKind kind,double chance){this.kind=kind;this.chance=chance;}
    }
    [Serializable] public sealed class LocationLayer
    {
        public int startDepth;public BlockKind terrain;public OreBand[] ores=Array.Empty<OreBand>();
        public LocationLayer(int start,BlockKind terrain,OreBand[] ores=null){startDepth=start;this.terrain=terrain;if(ores!=null)this.ores=ores;}
    }
    [Serializable] public sealed class LocationBalance
    {
        public LocationLayer[] layers;
        public int hardnessStart,hardnessDoubling,surfaceThickness;
        public float waterChance,lavaChance,lavaPocketMultiplier=1;
        public int[] healthOverrides=new int[43];
        public LocationLayer Layer(int row){var selected=layers[0];foreach(var layer in layers)if(row>=layer.startDepth)selected=layer;return selected;}
        public BlockKind Terrain(int row,int surface)=>surfaceThickness>0?(row<surface+surfaceThickness?layers[0].terrain:row<layers[2].startDepth?layers[1].terrain:layers[2].terrain):Layer(row).terrain;
        public double OreChance(BlockKind kind,int row){foreach(var ore in Layer(row).ores)if(ore.kind==kind)return ore.chance;return 0;}
        public void Validate()
        {
            if(layers==null||layers.Length!=3||healthOverrides?.Length!=43||hardnessStart<0||hardnessDoubling<20||surfaceThickness<0||!Finite(waterChance)||!Finite(lavaChance)||waterChance+lavaChance>1||!Finite(lavaPocketMultiplier))throw new InvalidOperationException("Invalid location balance.");
            int previous=-1;foreach(var layer in layers){if(layer==null||layer.startDepth<=previous||!ResourceInventory.IsResource(layer.terrain)||layer.ores==null)throw new InvalidOperationException("Invalid location layer.");previous=layer.startDepth;double sum=0;foreach(var ore in layer.ores){if(ore==null||!Progression.IsOre(ore.kind)||double.IsNaN(ore.chance)||ore.chance<0||ore.chance>1)throw new InvalidOperationException("Invalid location ore.");sum+=ore.chance;}if(sum>1)throw new InvalidOperationException("Location ores exceed 100%.");}
            foreach(int health in healthOverrides)if(health<0)throw new InvalidOperationException("Negative block health.");
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=0;
        public static LocationBalance[] Defaults()
        {
            OreBand[] Ores(MapId map,int row){var list=new System.Collections.Generic.List<OreBand>();foreach(var k in WorldCatalog.Ores){double chance=WorldCatalog.OreChance(map,k,row);if(chance>0)list.Add(new OreBand(k,chance));}return list.ToArray();}
            var mine=new LocationBalance{layers=new[]{new LocationLayer(0,BlockKind.Dirt),new LocationLayer(3,BlockKind.Stone),new LocationLayer(75,BlockKind.Deepstone)},surfaceThickness=3,hardnessStart=60,hardnessDoubling=70,waterChance=.16f,lavaChance=.20f};
            var desert=new LocationBalance{layers=new[]{new LocationLayer(0,BlockKind.Sand),new LocationLayer(5,BlockKind.Sandstone,Ores(MapId.Desert,5)),new LocationLayer(90,BlockKind.DarkQuartzite,Ores(MapId.Desert,90))},surfaceThickness=5,hardnessStart=80,hardnessDoubling=80,waterChance=.07f,lavaChance=.20f};
            desert.healthOverrides[(int)BlockKind.Copper]=14;desert.healthOverrides[(int)BlockKind.Gold]=24;desert.healthOverrides[(int)BlockKind.Amethyst]=20;
            var nether=new LocationBalance{layers=new[]{new LocationLayer(0,BlockKind.Netherrack,Ores(MapId.Nether,0)),new LocationLayer(35,BlockKind.Basalt,Ores(MapId.Nether,35)),new LocationLayer(110,BlockKind.Blackstone,Ores(MapId.Nether,110))},hardnessStart=100,hardnessDoubling=90,lavaChance=.68f,lavaPocketMultiplier=9};
            return new[]{mine,desert,nether};
        }
    }
    public partial class GameBalance
    {
        public LocationBalance[] locations=LocationBalance.Defaults();
        public LocationBalance Location=>locations[(int)map];
        public int DeepDepth=>rulesVersion>=3?Location.layers[2].startDepth:Block(BlockKind.Deepstone).minimumDepth;
        public BlockKind TerrainAt(int row,int surface=0)=>Location.Terrain(row,surface);
    }
}
