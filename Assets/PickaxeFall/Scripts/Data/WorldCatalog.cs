using System;
using UnityEngine;
namespace PickaxeFall
{
    public enum MapId { Mine, Desert, Nether }
    public static class WorldCatalog
    {
        public static string Name(MapId map)=>map==MapId.Desert?"Пустыня":map==MapId.Nether?"Ад":"Бездонная шахта";
        public static bool Open(PlayerProfile p,MapId map)=>map==MapId.Mine || map==MapId.Desert && ProfileRules.Complete(p,GoalKind.Bridge) || map==MapId.Nether && ProfileRules.Complete(p,GoalKind.Portal);
        public static int Deep(MapId map)=>map==MapId.Desert?90:map==MapId.Nether?110:75;
        public static BlockKind Terrain(MapId map,int row,int surface=0)=>map==MapId.Desert?(row<surface+5?BlockKind.Sand:row<90?BlockKind.Sandstone:BlockKind.DarkQuartzite):map==MapId.Nether?(row<35?BlockKind.Netherrack:row<110?BlockKind.Basalt:BlockKind.Blackstone):(row<surface+3?BlockKind.Dirt:row<75?BlockKind.Stone:BlockKind.Deepstone);
        public static bool Host(BlockKind kind)=>kind==BlockKind.Stone || kind==BlockKind.Deepstone || kind==BlockKind.Sandstone || kind==BlockKind.DarkQuartzite || kind==BlockKind.Netherrack || kind==BlockKind.Basalt || kind==BlockKind.Blackstone;
        public static bool IsNewOre(BlockKind k)=>k>=BlockKind.Malachite&&k<=BlockKind.SmokyQuartz || k>=BlockKind.Quartz&&k<=BlockKind.SoulCrystal;
        public static readonly BlockKind[] Ores={BlockKind.Coal,BlockKind.Copper,BlockKind.Iron,BlockKind.Gold,BlockKind.Diamond,BlockKind.Emerald,BlockKind.Lapis,BlockKind.Redstone,BlockKind.Malachite,BlockKind.Turquoise,BlockKind.Garnet,BlockKind.SmokyQuartz,BlockKind.Amethyst,BlockKind.Quartz,BlockKind.Sulfur,BlockKind.Cinnabar,BlockKind.FireOpal,BlockKind.AncientDebris,BlockKind.SoulCrystal};
        public static double OreChance(MapId map,BlockKind k,int row)
        {
            if(map==MapId.Desert){if(row<5)return 0;if(row<90)return k switch{BlockKind.Copper=>.08,BlockKind.Malachite=>.05,BlockKind.Turquoise=>.035,BlockKind.Gold=>.015,_=>0};return k switch{BlockKind.Gold=>.08,BlockKind.Garnet=>.005,BlockKind.SmokyQuartz=>.007,BlockKind.Amethyst=>.006,_=>0};}
            if(row<35)return k switch{BlockKind.Quartz=>.09,BlockKind.Sulfur=>.06,_=>0};
            if(row<110)return k switch{BlockKind.Quartz=>.06,BlockKind.Cinnabar=>.04,BlockKind.FireOpal=>.012,_=>0};
            return k switch{BlockKind.FireOpal=>.02,BlockKind.AncientDebris=>.002,BlockKind.SoulCrystal=>.0007,_=>0};
        }
        public static int Health(BlockKind k)=>k switch{
            BlockKind.Sand=>2,BlockKind.Sandstone=>9,BlockKind.DarkQuartzite=>15,BlockKind.Malachite=>15,BlockKind.Turquoise=>18,BlockKind.Garnet=>28,BlockKind.SmokyQuartz=>26,
            BlockKind.Netherrack=>8,BlockKind.Basalt=>16,BlockKind.Blackstone=>24,BlockKind.Quartz=>12,BlockKind.Sulfur=>13,BlockKind.Cinnabar=>22,BlockKind.FireOpal=>30,BlockKind.AncientDebris=>65,BlockKind.SoulCrystal=>80,
            BlockKind.Copper=>14,BlockKind.Gold=>24,BlockKind.Amethyst=>20,BlockKind.NetherBrick=>16,BlockKind.Timber=>2,_=>1};
        public static long Price(BlockKind k)=>k switch{BlockKind.Sand=>2,BlockKind.Sandstone=>4,BlockKind.DarkQuartzite=>6,BlockKind.Malachite=>24,BlockKind.Turquoise=>40,BlockKind.Garnet=>160,BlockKind.SmokyQuartz=>110,BlockKind.Netherrack=>4,BlockKind.Basalt=>7,BlockKind.Blackstone=>10,BlockKind.Quartz=>24,BlockKind.Sulfur=>20,BlockKind.Cinnabar=>80,BlockKind.FireOpal=>180,BlockKind.AncientDebris=>400,BlockKind.SoulCrystal=>800,_=>0};
        public static string FoundIn(BlockKind k)=>k>=BlockKind.Netherrack&&k<=BlockKind.SoulCrystal?"Ад":k>=BlockKind.Sand&&k<=BlockKind.SmokyQuartz?"Пустыня":"Шахта";
        public static void AddRecipe(UpgradeCost cost,UpgradeKey key,int level)
        {
            int max=Progression.MaxLevel(key.Kind);float p=level/(float)max;
            if(p>.5f){cost.Resources[(int)BlockKind.Sandstone]+=4+(int)((p-.5f)*16);BlockKind ore=key.Kind switch{UpgradeKind.Damage or UpgradeKind.ImpactPower or UpgradeKind.Shrapnel=>BlockKind.Malachite,UpgradeKind.Durability or UpgradeKind.Careful or UpgradeKind.LastChance=>BlockKind.Turquoise,UpgradeKind.Fortune or UpgradeKind.TntFind or UpgradeKind.DoubleBlast=>BlockKind.Garnet,>=UpgradeKind.EnergyCapacity and <=UpgradeKind.PlungeDepth=>BlockKind.SmokyQuartz,_=>BlockKind.Malachite};cost.Resources[(int)ore]+=1+(int)((p-.5f)*4);}
            if(p>.8f){cost.Resources[(int)BlockKind.Basalt]+=4+(int)((p-.8f)*40);BlockKind ore=key.Kind switch{UpgradeKind.Damage or UpgradeKind.ImpactPower or UpgradeKind.Shrapnel=>BlockKind.FireOpal,UpgradeKind.Fortune or UpgradeKind.TntFind or UpgradeKind.DoubleBlast=>BlockKind.Cinnabar,UpgradeKind.DoubleBench or UpgradeKind.TeamChance or UpgradeKind.TeamSize=>BlockKind.Sulfur,_=>BlockKind.Quartz};cost.Resources[(int)ore]+=1+(int)((p-.8f)*10);}
            if(level==max && key.Kind==UpgradeKind.Damage && key.Tier==4)cost.Resources[(int)BlockKind.AncientDebris]++;
            if(level==max && key.Kind==UpgradeKind.TeamSize)cost.Resources[(int)BlockKind.SoulCrystal]++;
        }
    }
}
