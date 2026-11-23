using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    public class MineCamera : MonoBehaviour
    {
        GameController game;
        Camera view;
        Vector3 velocity;
        float zoomVelocity;
        bool snap;
        public void SnapToTools(){snap=true;LateUpdate();}
        SpriteRenderer landscape;
        Sprite caveArt;
        Transform caveRoot;
        readonly Dictionary<Vector2Int,SpriteRenderer> caveTiles=new();
        readonly List<Vector2Int> retiredTiles=new();
        RectInt caveRange;
        bool hasCaveRange;
        const float CaveWidth=90,CaveHeight=180;
        const float SurfaceCameraY = 3.2f;

        public void Initialize(GameController owner)
        {
            game = owner; view = GetComponent<Camera>();
            view.orthographic = true; view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color32(25, 38, 49, 255);
            caveRoot=new GameObject("Static cave backgrounds").transform;
            caveArt=PixelArt.CaveWall(game.CurrentMap);
            var sky = new GameObject("Sky, clouds and distant forest");
            landscape = sky.AddComponent<SpriteRenderer>(); landscape.sprite = PixelArt.WorldLandscape(game.CurrentMap);
            landscape.sortingOrder = -20; sky.transform.localScale = Vector3.one * 80;
            sky.transform.position = new Vector3(0, 20, 3);
            ResetView();
        }

        public void ResetView()
        {
            SetFraming(); zoomVelocity = 0; transform.position = new Vector3(0, SurfaceCameraY, -10); velocity = Vector3.zero;
            StreamCaveBackground();
        }

        public void SetMap()
        {
            if(!landscape)return;
            var nextLandscape=PixelArt.WorldLandscape(game.CurrentMap);
            if(landscape.sprite!=nextLandscape){PixelArt.ReleaseGenerated(landscape.sprite);landscape.sprite=nextLandscape;}
            var nextCave=PixelArt.CaveWall(game.CurrentMap);
            if(caveArt!=nextCave)
            {
                var previous=caveArt;caveArt=nextCave;
                foreach(var tile in caveTiles.Values)SetCaveArt(tile);
                PixelArt.ReleaseGenerated(previous);
            }
        }
        void SetFraming() => view.orthographicSize = Mathf.Clamp(6.2f / Mathf.Max(0.35f, view.aspect), 8, 16);

        public bool TryGetToolBounds(out Bounds bounds)
        {
            bounds = new Bounds(); bool found = false;
            foreach (var tool in game.Pickaxes)
            {
                if (!tool.IsAlive) continue;
                if (!found) { bounds = new Bounds(tool.transform.position, Vector3.one * 2); found = true; }
                else { bounds.Encapsulate(tool.transform.position + Vector3.one); bounds.Encapsulate(tool.transform.position - Vector3.one); }
            }
            return found;
        }
        void LateUpdate()
        {
            float minimumSize = Mathf.Clamp(6.2f / Mathf.Max(0.35f, view.aspect), 8, 16);
            if (game.Phase == RunPhase.Mining && TryGetToolBounds(out Bounds bounds))
            {
                Rect space = game.GameplayViewport;
                float width = Mathf.Max(0.2f, space.width), height = Mathf.Max(0.2f, space.height);
                float required = Mathf.Max(minimumSize, (bounds.extents.x + 1.5f) / (view.aspect * width), (bounds.extents.y + 1.5f) / height);
                bool instant=snap || game.Paused;
                view.orthographicSize = instant?required:Mathf.SmoothDamp(view.orthographicSize, required, ref zoomVelocity, required > view.orthographicSize ? 0.08f : 0.35f);
                Vector3 offset = new((space.center.x - 0.5f) * 2 * view.orthographicSize * view.aspect,
                    (space.center.y - 0.5f) * 2 * view.orthographicSize, 0);
                Vector3 target = bounds.center - offset; target.z = -10;
                target.y = Mathf.Min(SurfaceCameraY, target.y);
                float maxX = Mathf.Max(0, game.Balance.worldWidth / 2f - view.orthographicSize * view.aspect);
                target.x = Mathf.Clamp(target.x, -maxX, maxX);
                transform.position = instant?target:Vector3.SmoothDamp(transform.position, target, ref velocity, 0.16f);
                if(instant){velocity=Vector3.zero;zoomVelocity=0;}
            }
            else if (game.Phase == RunPhase.Ready || game.Phase == RunPhase.Equipped || game.Phase == RunPhase.Spinning) SetFraming();
            snap=false;StreamCaveBackground();
        }

        void SetCaveArt(SpriteRenderer tile)
        {
            tile.sprite=caveArt;
            tile.transform.localScale=new Vector3(CaveWidth/caveArt.bounds.size.x,CaveHeight/caveArt.bounds.size.y,1);
        }

        void StreamCaveBackground()
        {
            // Camera bounds select which fixed world tiles exist. They never change a
            // tile's placement, scale or texture coordinates, nor the surface panorama.
            float halfHeight=view.orthographicSize,halfWidth=halfHeight*view.aspect;
            int left=Mathf.FloorToInt((transform.position.x-halfWidth+CaveWidth*.5f)/CaveWidth)-1;
            int right=Mathf.FloorToInt((transform.position.x+halfWidth+CaveWidth*.5f)/CaveWidth)+1;
            int first=Mathf.Max(0,Mathf.FloorToInt(-(transform.position.y+halfHeight)/CaveHeight)-1);
            int last=Mathf.Max(first,Mathf.FloorToInt(-(transform.position.y-halfHeight)/CaveHeight)+1);
            var range=new RectInt(left,first,right-left+1,last-first+1);
            if(hasCaveRange&&range==caveRange)return;
            caveRange=range;hasCaveRange=true;retiredTiles.Clear();
            foreach(var pair in caveTiles)if(!range.Contains(pair.Key))retiredTiles.Add(pair.Key);
            foreach(var key in retiredTiles){Destroy(caveTiles[key].gameObject);caveTiles.Remove(key);}
            for(int row=first;row<=last;row++)for(int column=left;column<=right;column++)
            {
                var key=new Vector2Int(column,row);if(caveTiles.ContainsKey(key))continue;
                var obj=new GameObject($"Cave background {column},{row}");obj.transform.SetParent(caveRoot,false);
                obj.transform.localPosition=new Vector3(column*CaveWidth,-(row+.5f)*CaveHeight,4);
                var tile=obj.AddComponent<SpriteRenderer>();tile.sortingOrder=-30;SetCaveArt(tile);caveTiles.Add(key,tile);
            }
        }

        void OnDestroy()
        {
            if (caveRoot) Destroy(caveRoot.gameObject);
            PixelArt.ReleaseGenerated(caveArt);
            if (landscape) { PixelArt.ReleaseGenerated(landscape.sprite); Destroy(landscape.gameObject); }
        }
    }
}
