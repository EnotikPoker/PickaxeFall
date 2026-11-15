using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    // Exact union of opaque texels. Horizontal runs are merged vertically;
    // every path is convex and transparent corners never become solid.
    public static class PickaxeGeometry
    {
        static readonly Dictionary<Sprite,Vector2[][]> cache=new();
        public static Vector2[][] Paths(Sprite sprite)
        {
            if(cache.TryGetValue(sprite,out var found))return found;
            var pixels=sprite.texture.GetPixels32();var rect=sprite.rect;int w=(int)rect.width,h=(int)rect.height;
            var rectangles=new List<RectInt>();var previous=new Dictionary<(int,int),int>();
            for(int y=0;y<h;y++)
            {
                var current=new Dictionary<(int,int),int>();
                for(int x=0;x<w;)
                {
                    if(pixels[((int)rect.y+y)*sprite.texture.width+(int)rect.x+x].a<128){x++;continue;}
                    int start=x;while(x<w&&pixels[((int)rect.y+y)*sprite.texture.width+(int)rect.x+x].a>=128)x++;
                    var key=(start,x);int index;
                    if(previous.TryGetValue(key,out index)){var r=rectangles[index];r.height++;rectangles[index]=r;}
                    else{index=rectangles.Count;rectangles.Add(new RectInt(start,y,x-start,1));}
                    current[key]=index;
                }
                previous=current;
            }
            var paths=new Vector2[rectangles.Count][];Vector2 pivot=sprite.pivot;float ppu=sprite.pixelsPerUnit;
            for(int i=0;i<paths.Length;i++)
            {
                var r=rectangles[i];paths[i]=new[]{(new Vector2(r.xMin,r.yMin)-pivot)/ppu,(new Vector2(r.xMax,r.yMin)-pivot)/ppu,(new Vector2(r.xMax,r.yMax)-pivot)/ppu,(new Vector2(r.xMin,r.yMax)-pivot)/ppu};
            }
            return cache[sprite]=paths;
        }
        static readonly Dictionary<Sprite,Vector2[][]> contourCache=new();
        public static Vector2[][] Contours(Sprite sprite)
        {
            if(contourCache.TryGetValue(sprite,out var found))return found;
            int w=(int)sprite.rect.width,h=(int)sprite.rect.height;var pixels=sprite.texture.GetPixels32();
            bool Solid(int x,int y)=>x>=0&&y>=0&&x<w&&y<h&&pixels[((int)sprite.rect.y+y)*sprite.texture.width+(int)sprite.rect.x+x].a>=128;
            var edges=new Dictionary<Vector2Int,List<Vector2Int>>();
            void Edge(int x,int y,int xx,int yy){var a=new Vector2Int(x,y);if(!edges.TryGetValue(a,out var list))edges[a]=list=new List<Vector2Int>();list.Add(new Vector2Int(xx,yy));}
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(Solid(x,y)){
                if(!Solid(x,y-1))Edge(x,y,x+1,y);if(!Solid(x+1,y))Edge(x+1,y,x+1,y+1);
                if(!Solid(x,y+1))Edge(x+1,y+1,x,y+1);if(!Solid(x-1,y))Edge(x,y+1,x,y);
            }
            var paths=new List<Vector2[]>();
            while(edges.Count>0)
            {
                Vector2Int first=default;foreach(var key in edges.Keys){first=key;break;}
                var points=new List<Vector2Int>();var current=first;var direction=Vector2Int.zero;int guard=0;
                do{
                    if(++guard>w*h*8)throw new System.InvalidOperationException("Invalid sprite boundary");
                    points.Add(current);var list=edges[current];int selected=0,best=-1;
                    for(int i=0;i<list.Count;i++){var d=list[i]-current;int cross=direction.x*d.y-direction.y*d.x;int dot=direction.x*d.x+direction.y*d.y;int priority=cross>0?3:dot>0?2:cross<0?1:0;if(priority>best){best=priority;selected=i;}}
                    var next=list[selected];list.RemoveAt(selected);if(list.Count==0)edges.Remove(current);direction=next-current;current=next;
                }while(current!=first);
                var compact=new List<Vector2>();for(int i=0;i<points.Count;i++){var a=points[i]-points[(i+points.Count-1)%points.Count];var b=points[(i+1)%points.Count]-points[i];if(a.x*b.y-a.y*b.x!=0)compact.Add(((Vector2)points[i]-sprite.pivot)/sprite.pixelsPerUnit);}
                if(compact.Count>=3)paths.Add(compact.ToArray());
            }
            return contourCache[sprite]=paths.ToArray();
        }
        public static void Apply(PolygonCollider2D target,Sprite sprite)
        {
            var paths=Contours(sprite);target.pathCount=paths.Length;
            for(int i=0;i<paths.Length;i++)target.SetPath(i,paths[i]);
        }
    }
}
