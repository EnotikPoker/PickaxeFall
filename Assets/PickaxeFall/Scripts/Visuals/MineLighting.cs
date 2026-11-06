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
            material=new Material(shader);var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=30;
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
                Color ambient=game.CurrentMap==MapId.Desert?new Color(.44f,.37f,.27f,1):game.CurrentMap==MapId.Nether?new Color(.4f,.25f,.3f,1):new Color(.34f,.38f,.46f,1);
                field[i]=Color.Lerp(Color.white,ambient,depth);solid[i]=game.World.BlocksLight(x0+x*step,row);
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
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)pixels[(height-1-y)*width+x]=field[y*width+x];
            texture.SetPixels32(pixels);texture.Apply(false,false);
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
