using UnityEngine;

namespace PickaxeFall
{
    public class MineCamera : MonoBehaviour
    {
        GameController game;
        Camera view;
        Vector3 velocity;
        float zoomVelocity;
        SpriteRenderer backdrop;
        SpriteRenderer landscape;
        const float SurfaceCameraY = 3.2f;

        public void Initialize(GameController owner)
        {
            game = owner; view = GetComponent<Camera>();
            view.orthographic = true; view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color32(25, 38, 49, 255);
            var obj = new GameObject("Cave background");
            backdrop = obj.AddComponent<SpriteRenderer>(); backdrop.sprite = PixelArt.CaveWall();
            backdrop.sortingOrder = -30; obj.transform.localScale = Vector3.one * 90;
            var sky = new GameObject("Sky, clouds and distant forest");
            landscape = sky.AddComponent<SpriteRenderer>(); landscape.sprite = PixelArt.Landscape();
            landscape.sortingOrder = -20; sky.transform.localScale = Vector3.one * 80;
            sky.transform.position = new Vector3(0, 20, 3);
            ResetView();
        }

        public void ResetView()
        {
            SetFraming(); zoomVelocity = 0; transform.position = new Vector3(0, SurfaceCameraY, -10); velocity = Vector3.zero;
            UpdateBackground();
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
                view.orthographicSize = Mathf.SmoothDamp(view.orthographicSize, required, ref zoomVelocity, required > view.orthographicSize ? 0.08f : 0.35f);
                Vector3 offset = new((space.center.x - 0.5f) * 2 * view.orthographicSize * view.aspect,
                    (space.center.y - 0.5f) * 2 * view.orthographicSize, 0);
                Vector3 target = bounds.center - offset; target.z = -10;
                target.y = Mathf.Min(SurfaceCameraY, target.y);
                float maxX = Mathf.Max(0, game.Balance.worldWidth / 2f - view.orthographicSize * view.aspect);
                target.x = Mathf.Clamp(target.x, -maxX, maxX);
                transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, 0.16f);
            }
            else if (game.Phase == RunPhase.Ready || game.Phase == RunPhase.Equipped || game.Phase == RunPhase.Spinning) SetFraming();
            UpdateBackground();
        }

        void UpdateBackground()
        {
            backdrop.transform.position = new Vector3(transform.position.x * 0.1f, transform.position.y - 20, 4);
            landscape.transform.position = new Vector3(transform.position.x * 0.06f, 20, 3);
        }

        void OnDestroy()
        {
            if (backdrop) { Destroy(backdrop.sprite.texture); Destroy(backdrop.sprite); Destroy(backdrop.gameObject); }
            if (landscape) { Destroy(landscape.sprite.texture); Destroy(landscape.sprite); Destroy(landscape.gameObject); }
        }
    }
}
