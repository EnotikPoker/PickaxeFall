using UnityEngine;

namespace PickaxeFall
{
    public class MineCamera : MonoBehaviour
    {
        GameController game;
        Camera view;
        Vector3 velocity;
        SpriteRenderer backdrop;

        public void Initialize(GameController owner)
        {
            game = owner; view = GetComponent<Camera>();
            view.orthographic = true; view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color32(25, 38, 49, 255);
            var obj = new GameObject("Cave background");
            backdrop = obj.AddComponent<SpriteRenderer>(); backdrop.sprite = PixelArt.Square;
            backdrop.sortingOrder = -20; obj.transform.localScale = new Vector3(120, 180, 1);
            backdrop.color = new Color32(29, 36, 48, 255);
            ResetView();
        }

        public void ResetView() { transform.position = new Vector3(0, -3.2f, -10); velocity = Vector3.zero; }

        void LateUpdate()
        {
            view.orthographicSize = Mathf.Clamp(6.2f / Mathf.Max(0.35f, view.aspect), 8, 16);
            if (game.Phase == RunPhase.Mining || game.Phase == RunPhase.Finished)
            {
                Vector3 p = game.Pickaxe.transform.position;
                float halfWorld = game.Balance.worldWidth / 2f;
                float maxX = Mathf.Max(0, halfWorld - view.orthographicSize * view.aspect);
                Vector3 target = new(Mathf.Clamp(p.x, -maxX, maxX), Mathf.Min(-3.2f, p.y - 2.4f), -10);
                transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, 0.2f);
            }
            backdrop.transform.position = new Vector3(0, transform.position.y, 2);
        }

        void OnDestroy() { if (backdrop) Destroy(backdrop.gameObject); }
    }
}
