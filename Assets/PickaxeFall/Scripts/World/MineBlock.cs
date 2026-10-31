using UnityEngine;

namespace PickaxeFall
{
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class MineBlock : MonoBehaviour
    {
        public BlockKind Kind { get; private set; }
        public int Row { get; private set; }
        public int Column { get; private set; }
        public int HealthHalfUnits { get; private set; }
        public float Health => HealthHalfUnits * 0.5f;
        public bool IsBroken { get; private set; }
        public bool IsDeep { get; private set; }
        public Color BaseColor => PixelArt.BlockColor(Kind, IsDeep);
        public int DamageStage { get; private set; }
        SpriteRenderer visual, cracks;
        BoxCollider2D hitbox;
        MiningWorld world;
        int maxHealthHalfUnits;

        public void Setup(MiningWorld owner, BlockKind kind, int x, int row, int variant, bool deep = false, bool surface = false)
        {
            world = owner; Kind = kind; Row = row; Column = x; IsDeep = deep;
            HealthHalfUnits = maxHealthHalfUnits = owner.Balance.BlockHealthHalfUnits(kind, row); IsBroken = false; DamageStage = 0;
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
        public void RestoreHealth(int halfUnits)
        {
            HealthHalfUnits = Mathf.Clamp(halfUnits, 1, maxHealthHalfUnits); UpdateCracks();
        }
        void UpdateCracks()
        {
            if (HealthHalfUnits >= maxHealthHalfUnits) { DamageStage = 0; cracks.enabled = false; return; }
            DamageStage = Mathf.Clamp(Mathf.CeilToInt((1f - (float)HealthHalfUnits / maxHealthHalfUnits) * 8), 1, 8);
            cracks.sprite = PixelArt.Cracks(DamageStage); cracks.enabled = true;
        }
        public bool HitHalfUnits(int damage, FallingPickaxe source)
        {
            if (IsBroken || damage <= 0 || !world.CanMine) return false;
            HealthHalfUnits = damage >= HealthHalfUnits ? 0 : HealthHalfUnits - damage;
            if (HealthHalfUnits == 0) return DestroyBlock(source);
            UpdateCracks(); world.Remember(this); return false;
        }
        public bool DestroyBlock(FallingPickaxe source, bool explosion = false)
        {
            if (IsBroken || !world.CanMine || explosion && Kind == BlockKind.Workbench) return false;
            HealthHalfUnits = 0; IsBroken = true; hitbox.enabled = false; world.Break(this, source, explosion); return true;
        }
        // Legacy full-damage entrypoint for editor inspection; production impacts carry a source.
        public bool Hit(int damage) => damage == int.MaxValue ? DestroyBlock(null) : HitHalfUnits(damage > int.MaxValue / 2 ? int.MaxValue : damage * 2, null);
    }
}
