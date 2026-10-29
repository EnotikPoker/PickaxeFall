using UnityEngine;

namespace PickaxeFall
{
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class MineBlock : MonoBehaviour
    {
        public BlockKind Kind { get; private set; }
        public int Row { get; private set; }
        public int Health { get; private set; }
        public bool IsBroken { get; private set; }
        public bool IsDeep { get; private set; }
        public Color BaseColor => PixelArt.BlockColor(Kind, IsDeep);
        public int DamageStage { get; private set; }
        SpriteRenderer visual;
        SpriteRenderer cracks;
        BoxCollider2D hitbox;
        MiningWorld world;
        int maxHealth;

        public void Setup(MiningWorld owner, BlockKind kind, int x, int row, int variant, bool deep = false, bool surface = false)
        {
            world = owner; Kind = kind; Row = row; IsDeep = deep;
            Health = maxHealth = owner.Balance.Block(kind).health; IsBroken = false; DamageStage = 0;
            visual ??= GetComponent<SpriteRenderer>(); hitbox ??= GetComponent<BoxCollider2D>();
            if (!cracks)
            {
                var overlay = new GameObject("Damage cracks"); overlay.transform.SetParent(transform, false);
                overlay.transform.localPosition = new Vector3(0, 0, -0.01f);
                cracks = overlay.AddComponent<SpriteRenderer>(); cracks.sortingOrder = visual.sortingOrder + 1;
            }
            cracks.enabled = false; cracks.sprite = null;
            transform.position = new Vector3(x, -row - 0.5f, 0);
            visual.sprite = PixelArt.Block(kind, variant, deep, surface); visual.color = Color.white;
            hitbox.size = new Vector2(0.99f, 0.99f); hitbox.enabled = true;
            gameObject.SetActive(true);
        }

        public bool Hit(int damage)
        {
            if (IsBroken) return false;
            Health = Mathf.Max(0, Health - damage);
            DamageStage = Mathf.Clamp(Mathf.CeilToInt((1f - (float)Health / maxHealth) * 8), 1, 8);
            cracks.sprite = PixelArt.Cracks(DamageStage); cracks.enabled = true;
            if (Health > 0) return false;
            IsBroken = true; hitbox.enabled = false;
            world.Break(this);
            return true;
        }
    }
}
