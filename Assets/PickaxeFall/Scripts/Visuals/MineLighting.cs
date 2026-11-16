using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    // A low-resolution light field with opaque-cell occlusion. No extra physics
    // colliders or gameplay RNG; the headless lab does not create this component.
    public sealed class MineLighting:MonoBehaviour
    {
        struct Node {public int index,distance;public Node(int i,int d){index=i;distance=d;} }
        GameController game;Material material;Mesh mesh;Texture2D texture;
        Color32[] field,pixels;bool[] solid;int[] visited;int stamp,width,height,step,x0,row0;
        readonly Queue<Node> queue=new();float next;
        public void Initialize(GameController owner)
        {
            game=owner;var shader=Resources.Load<Shader>("Lighting/MineLight");
            material=new Material(shader);var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=6;
            mesh=new Mesh {name="Mine illumination quad"};mesh.vertices=new[] {new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(.5f,.5f),new Vector3(-.5f,.5f)};
            mesh.uv=new[] {Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[] {0,1,2,0,2,3};mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        }
        void LateUpdate()
        {
            if(!game.ReadyToPresent || !Camera.main || Time.unscaledTime<next)return;
            next=Time.unscaledTime+(game.Settings.ReducedEffects ? .1f : .05f);
            var camera=Camera.main;float fullHeight=camera.orthographicSize*2,fullWidth=fullHeight*camera.aspect;
            step=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(fullWidth/120,fullHeight/120)));
            int w=Mathf.CeilToInt(fullWidth/step)+4,h=Mathf.CeilToInt(fullHeight/step)+4;
            if(w!=width || h!=height)
            {
                width=w;height=h;if(texture)Destroy(texture);
                texture=new Texture2D(w,h,TextureFormat.RGBA32,false,true){name="Occluded mine light",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                field=new Color32[w*h];pixels=new Color32[w*h];solid=new bool[w*h];visited=new int[w*h];material.mainTexture=texture;
            }
            x0=Mathf.FloorToInt(camera.transform.position.x-fullWidth*.5f)-2*step;
            row0=Mathf.FloorToInt(-camera.transform.position.y-fullHeight*.5f)-2*step;
            transform.position=new Vector3(x0+width*step*.5f,-row0-height*step*.5f,-2);transform.localScale=new Vector3(width*step,height*step,1);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int row=row0+y*step,i=y*width+x;float depth=Mathf.Clamp01(row/45f);
                solid[i]=game.World.BlocksLight(x0+x*step,row);
                field[i]=Ambient(game.CurrentMap,depth,solid[i]);
            }
            foreach(var tool in game.Pickaxes)if(tool.IsAlive)Light(tool.Body.position,7,new Color(1,.94f,.82f));
            int sources=0;
            foreach(var block in game.World.LoadedBlocks)
            {
                if(++sources>50000)break;
                if(block.IsLit)Light(block.transform.position,4,new Color(1,.27f,.2f));
                else if(block.Kind==BlockKind.AmethystCluster)Light(block.transform.position,4,new Color(.82f,.55f,1));
                else if(block.Kind==BlockKind.SoulCrystal)Light(block.transform.position,4,new Color(.35f,.9f,1));
                else if(block.Kind==BlockKind.FireOpal)Light(block.transform.position,2,new Color(1,.47f,.16f));
                else if(block.Kind==BlockKind.Spawner)Light(block.transform.position,2.5f,new Color(.48f,.78f,.94f));
            }
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(game.World.Liquids.KindAt(x0+x*step,row0+y*step)==BlockKind.Lava)Light(new Vector2(x0+x*step,-row0-y*step-.5f),3,new Color(1,.46f,.1f));
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)pixels[(height-1-y)*width+x]=Readable(field[y*width+x],game.Settings.MineBrightness);
            texture.SetPixels32(pixels);texture.Apply(false,false);
        }
        public static Color Ambient(MapId map,float depth,bool solid)
        {
            Color tint=map==MapId.Desert?new Color(.86f,.83f,.8f):map==MapId.Nether?new Color(.85f,.8f,.81f):new Color(.8f,.83f,.86f);
            if(solid)tint*=.96f;
            return Color.Lerp(Color.white,tint,Mathf.Clamp01(depth));
        }
        public static Color32 Readable(Color32 value,float brightness)
        {
            float gain=float.IsNaN(brightness)||float.IsInfinity(brightness)?1:Mathf.Clamp(brightness,1,1.3f);
            return new Color(Mathf.Clamp01(Mathf.Max(.75f,value.r/255f)*gain),Mathf.Clamp01(Mathf.Max(.75f,value.g/255f)*gain),Mathf.Clamp01(Mathf.Max(.75f,value.b/255f)*gain),1);
        }
        void Light(Vector2 position,float radius,Color color)
        {
            int sx=Mathf.FloorToInt((position.x-x0)/step),sy=Mathf.FloorToInt((-position.y-row0)/step);
            if(sx<0 || sx>=width || sy<0 || sy>=height)return;
            stamp++;queue.Clear();int start=sy*width+sx;visited[start]=stamp;queue.Enqueue(new Node(start,0));
            float range=radius/step;
            while(queue.Count>0)
            {
                var node=queue.Dequeue();if(node.distance>range)continue;
                float strength=1-node.distance/(range+1);var emitted=(Color32)(color*strength);var old=field[node.index];
                field[node.index]=new Color32((byte)Mathf.Max(old.r,emitted.r),(byte)Mathf.Max(old.g,emitted.g),(byte)Mathf.Max(old.b,emitted.b),255);
                if(node.distance>0 && solid[node.index])continue;
                int x=node.index%width,y=node.index/width;
                if(x>0)Push(node.index-1,node.distance+1);if(x+1<width)Push(node.index+1,node.distance+1);
                if(y>0)Push(node.index-width,node.distance+1);if(y+1<height)Push(node.index+width,node.distance+1);
            }
        }
        void Push(int index,int distance){if(visited[index]==stamp)return;visited[index]=stamp;queue.Enqueue(new Node(index,distance));}
        void OnDestroy(){if(material)Destroy(material);if(mesh)Destroy(mesh);if(texture)Destroy(texture);}
    }
}
