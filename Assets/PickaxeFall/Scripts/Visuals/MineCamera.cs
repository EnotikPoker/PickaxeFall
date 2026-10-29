using UnityEngine;

namespace PickaxeFall
{
    public class MineCamera : MonoBehaviour
    {
        GameController game;
        Camera view;
        Vector3 velocity;
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
            SetFraming(); transform.position = new Vector3(0, SurfaceCameraY, -10); velocity = Vector3.zero;
            UpdateBackground();
        }

        void SetFraming() => view.orthographicSize = Mathf.Clamp(6.2f / Mathf.Max(0.35f, view.aspect), 8, 16);

        void LateUpdate()
        {
            SetFraming();
            if (game.Phase == RunPhase.Mining || game.Phase == RunPhase.Breaking || game.Phase == RunPhase.Finished)
            {
                Vector3 p = game.Pickaxe.transform.position;
                float halfWorld = game.Balance.worldWidth / 2f;
                float maxX = Mathf.Max(0, halfWorld - view.orthographicSize * view.aspect);
                Vector3 target = new(Mathf.Clamp(p.x, -maxX, maxX), Mathf.Min(SurfaceCameraY, p.y - 2.4f), -10);
                transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, 0.2f);
            }
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
