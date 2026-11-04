using UnityEngine;

namespace PickaxeFall
{
    public static partial class PixelArt
    {
        static Sprite ExtraBlock(BlockKind kind,int variant)
        {
            const int size=32;var random=new System.Random((int)kind*7919+variant*613);
            if(kind==BlockKind.Chest)return Chest();
            if(kind==BlockKind.Water || kind==BlockKind.Lava || kind==BlockKind.Obsidian)
            {
                var fluid=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float noise=(float)random.NextDouble();Color color;
                    if(kind==BlockKind.Water)color=Color.Lerp(new Color32(25,92,194,180),new Color32(95,184,239,180),y%9==0?.65f:noise*.22f);
                    else if(kind==BlockKind.Lava)color=Color.Lerp(new Color32(166,35,8,255),new Color32(255,190,30,255),Mathf.PerlinNoise(x*.2f+variant,y*.2f));
                    else color=Color.Lerp(new Color32(17,13,29,255),new Color32(89,54,117,255),noise>.88f?.7f:noise*.3f);
                    fluid[y*size+x]=color;
                }
                return Make(size,size,fluid);
            }
            Color[] pixels;
            if(kind==BlockKind.Emerald || kind==BlockKind.Lapis || kind==BlockKind.Redstone)
            {
                var source=Block(BlockKind.Deepstone,variant,true).texture;
                pixels=source.GetPixels();
                Color ore=BlockColor(kind);
                for(int n=0;n<(kind==BlockKind.Redstone?10:7);n++)
                {
                    int x=random.Next(2,size-5),y=random.Next(2,size-5),w=kind==BlockKind.Lapis?4:3;
                    Fill(pixels,size,x,y,w,2,ore*.65f);Fill(pixels,size,x,y+2,w-1,1,ore);
                    Fill(pixels,size,x+1,y+1,1,1,Color.Lerp(ore,Color.white,.4f));
                }
                for(int i=0;i<pixels.Length;i++){var color=pixels[i];color.a=1;pixels[i]=color;}
                return Make(size,size,pixels);
            }
            pixels=new Color[size*size];
            if(kind==BlockKind.AmethystCluster)
            {
                for(int crystal=0;crystal<3;crystal++)
                {
                    int cx=7+crystal*8,height=crystal==1?27:19+variant%4;
                    for(int y=2;y<height;y++)
                    {
                        int half=y<height-6?3:Mathf.Max(0,(height-y)/2);
                        for(int x=cx-half;x<=cx+half;x++)pixels[y*size+x]=x<cx?new Color32(111,62,166,255):x==cx?new Color32(227,196,255,255):new Color32(174,120,221,255);
                    }
                }
                Fill(pixels,size,3,1,25,3,new Color32(120,76,170,255));return Make(size,size,pixels);
            }
            int grid=kind==BlockKind.Amethyst?2:3;var centers=new Vector2[grid*grid];var shades=new float[centers.Length];
            for(int n=0;n<centers.Length;n++)
            {centers[n]=new Vector2((n%grid+.5f)*size/grid+random.Next(-3,4),(n/grid+.5f)*size/grid+random.Next(-3,4));shades[n]=.83f+(float)random.NextDouble()*.3f;}
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float nearest=float.MaxValue,second=float.MaxValue;int which=0;
                for(int n=0;n<centers.Length;n++){float d=(new Vector2(x,y)-centers[n]).sqrMagnitude;if(d<nearest){second=nearest;nearest=d;which=n;}else if(d<second)second=d;}
                Color color;
                if(kind==BlockKind.Amethyst)
                {
                    float facet=shades[which]*(x-centers[which].x+y-centers[which].y>0?1.12f:.91f);
                    color=new Color32(147,98,197,255);color*=facet;
                    if(second-nearest<6)color=Color.Lerp(color,new Color32(219,184,249,255),.5f);
                }
                else
                {
                    bool joint=second-nearest<12;
                    float shade=joint?.45f:shades[which]+(float)random.NextDouble()*.09f;
                    if(!joint && second-nearest<25 && y>centers[which].y)shade*=1.16f;
                    color=new Color32(120,126,123,255);color*=shade;
                    if(kind==BlockKind.MossyCobblestone && ((x*3+y*5+variant*11)%29<8 || Mathf.PerlinNoise(x*.22f+variant,y*.2f)>.6f))color=Color.Lerp(color,new Color32(72,120,46,255),.7f);
                }
                color.a=1;pixels[y*size+x]=color;
            }
            if(kind==BlockKind.Spawner)
            {
                Fill(pixels,size,0,0,32,32,new Color32(18,24,31,255));
                Fill(pixels,size,10,9,12,13,new Color32(35,91,115,255));Fill(pixels,size,14,13,5,6,new Color32(99,202,217,255));
                for(int x=2;x<32;x+=7)Fill(pixels,size,x,1,2,30,new Color32(87,96,106,255));
                for(int y=2;y<32;y+=9)Fill(pixels,size,1,y,30,2,new Color32(68,77,86,255));
                Fill(pixels,size,0,0,32,2,new Color32(111,121,129,255));Fill(pixels,size,0,30,32,2,new Color32(111,121,129,255));
            }
            return Make(size,size,pixels);
        }
    }
}
