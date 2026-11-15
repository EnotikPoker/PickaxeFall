using System.Collections.Generic;
using UnityEngine;
namespace PickaxeFall
{
    public static partial class PixelArt
    {
        static readonly Dictionary<int,Sprite> WorldSprites=new();
        public static Color WorldColor(BlockKind k)=>k switch{
            BlockKind.Sand=>new Color32(229,204,139,255),BlockKind.Sandstone=>new Color32(194,161,102,255),BlockKind.DarkQuartzite=>new Color32(64,58,62,255),
            BlockKind.Malachite=>new Color32(38,175,105,255),BlockKind.Turquoise=>new Color32(57,203,209,255),BlockKind.Garnet=>new Color32(180,35,69,255),BlockKind.SmokyQuartz=>new Color32(166,140,111,255),
            BlockKind.Netherrack=>new Color32(111,45,46,255),BlockKind.Basalt=>new Color32(80,77,84,255),BlockKind.Blackstone=>new Color32(36,32,43,255),
            BlockKind.Quartz=>new Color32(240,222,201,255),BlockKind.Sulfur=>new Color32(224,202,53,255),BlockKind.Cinnabar=>new Color32(219,61,49,255),BlockKind.FireOpal=>new Color32(255,144,38,255),BlockKind.AncientDebris=>new Color32(154,102,77,255),BlockKind.SoulCrystal=>new Color32(84,230,237,255),BlockKind.Timber=>new Color32(122,82,45,255),BlockKind.NetherBrick=>new Color32(62,29,43,255),_=>BlockColor(k)};
        public static Sprite WorldBlock(BlockKind kind,int variant,BlockKind host,bool surface=false)
        {
            var supplied=ImportedWorld(kind,variant,host);if(supplied && !(kind==BlockKind.Sandstone&&variant>=4))return supplied;
            if(kind==BlockKind.Sandstone&&variant>=4)return TempleAccent(variant);
            if(kind<BlockKind.Sand && !(Progression.IsOre(kind)&&host>=BlockKind.Sand))return Block(kind,variant,host==BlockKind.Deepstone,surface);
            if(kind==BlockKind.MinecartChest)return WorldDecoration(kind,variant);
            if(kind==BlockKind.Rail||kind==BlockKind.Timber||kind==BlockKind.NetherBrick)return WorldDecoration(kind,variant);
            int key=(int)kind*512+(int)host*8+variant%4;if(WorldSprites.TryGetValue(key,out var old)&&old)return old;
            const int n=32;var pixels=new Color[n*n];var random=new System.Random(key*7919);
            bool ore=Progression.IsOre(kind);Color basis=WorldColor(ore?host:kind);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){
                float shade=.88f+(float)random.NextDouble()*.2f;
                BlockKind rock=ore?host:kind;
                if(rock==BlockKind.Sandstone && (y%8==0||y%8<2&&x%16==0))shade*=.75f;
                if(rock==BlockKind.DarkQuartzite && (y+x/7)%10==0)shade*=1.24f;
                if(rock==BlockKind.Netherrack && Mathf.PerlinNoise(x*.3f+variant,y*.3f)<.4f)shade*=.65f;
                if(rock==BlockKind.Basalt && x%8<2)shade*=.65f;
                if(rock==BlockKind.Blackstone && (y%9==0 || (x+y/9*4)%13==0))shade*=.65f;
                var color=basis*shade;color.a=1;pixels[y*n+x]=color;
            }
            if(ore){Color mineral=kind>=BlockKind.Sand?WorldColor(kind):kind==BlockKind.Copper?new Color32(224,141,95,255):kind==BlockKind.Gold?new Color32(255,202,83,255):new Color32(162,110,220,255);
                for(int i=0;i<7;i++){int x=random.Next(2,27),y=random.Next(2,27);Fill(pixels,n,x,y,4,3,mineral*.62f);Fill(pixels,n,x,y+2,3,2,mineral);Fill(pixels,n,x+1,y+3,1,1,Color.Lerp(mineral,Color.white,.45f));}
            }
            return WorldSprites[key]=Make(n,n,pixels);
        }
        static Sprite TempleAccent(int variant){int key=200000+variant;if(WorldSprites.TryGetValue(key,out var old)&&old)return old;var p=(Color[])WorldBlock(BlockKind.Sandstone,variant%4,BlockKind.Sandstone).texture.GetPixels().Clone();Fill(p,32,3,3,26,26,new Color32(155,70,36,255));Fill(p,32,6,6,20,20,new Color32(219,151,72,255));Fill(p,32,10,8,12,16,new Color32(137,60,35,255));Fill(p,32,8,12,16,8,new Color32(137,60,35,255));Fill(p,32,13,13,6,6,new Color32(77,120,128,255));return WorldSprites[key]=Make(32,32,p);}
        static Sprite WorldDecoration(BlockKind k,int variant)
        {
            int key=100000+(int)k*4+variant%4;if(WorldSprites.TryGetValue(key,out var old)&&old)return old;
            var p=new Color[1024];const int n=32;
            if(k==BlockKind.MinecartChest){var source=Chest().texture;for(int y=0;y<24;y++)for(int x=4;x<28;x++)p[y*n+x]=source.GetPixel((x-4)*source.width/24,y*source.height/24);Fill(p,n,1,2,30,9,new Color32(82,88,96,255));Fill(p,n,3,0,6,4,new Color32(29,31,35,255));Fill(p,n,23,0,6,4,new Color32(29,31,35,255));Fill(p,n,2,10,28,2,new Color32(166,171,176,255));}
            else if(k==BlockKind.Rail){for(int x=1;x<32;x+=8)Fill(p,n,x,1,3,30,new Color32(104,70,39,255));Fill(p,n,0,6,32,3,new Color32(161,170,175,255));Fill(p,n,0,23,32,3,new Color32(161,170,175,255));}
            else for(int y=0;y<n;y++)for(int x=0;x<n;x++){bool joint=k==BlockKind.NetherBrick?(y%8==0||(x+(y/8%2)*8)%16==0):x%8==0;p[y*n+x]=WorldColor(k)*(joint?.6f:1+(x+y+variant)%5*.025f);}
            return WorldSprites[key]=Make(n,n,p);
        }
        public static Sprite WorldLandscape(MapId map)
        {
            if(map==MapId.Mine)return Landscape();const int w=256,h=128;var p=new Color[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                Color c=map==MapId.Desert?Color.Lerp(new Color32(239,189,117,255),new Color32(111,182,211,255),y/(float)h):Color.Lerp(new Color32(95,36,36,255),new Color32(30,18,32,255),y/(float)h);
                float ridge=25+Mathf.Sin(x*.035f)*10+Mathf.Sin(x*.071f)*7;
                if(y<ridge)c=map==MapId.Desert?new Color32(208,158,85,255):new Color32(55,28,38,255);
                if(y<ridge-12)c=map==MapId.Desert?new Color32(174,121,64,255):new Color32(89,36,39,255);
                if(map==MapId.Desert && (x-205)*(x-205)+(y-95)*(y-95)<64)c=new Color32(255,231,160,255);
                p[y*w+x]=c;
            }return Make(w,h,p);
        }
    }
}
