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
        public Color BaseColor => Kind>=BlockKind.Sand?PixelArt.WorldColor(Kind):PixelArt.BlockColor(Kind, IsDeep);
        public int DamageStage { get; private set; }
        public float LitUntil { get; private set; }
        public bool IsLit=>Kind==BlockKind.Redstone && LitUntil>world.Now;
        SpriteRenderer visual, cracks;
        BoxCollider2D hitbox;
        MiningWorld world;
        int maxHealthHalfUnits;

        public void Setup(MiningWorld owner, BlockKind kind, int x, int row, int variant, bool deep = false, bool surface = false)
        {
            world = owner; Kind = kind; Row = row; Column = x; IsDeep = deep;
            HealthHalfUnits = maxHealthHalfUnits = owner.Balance.BlockHealthHalfUnits(kind, row); IsBroken = false; DamageStage = 0;LitUntil=0;
            visual ??= GetComponent<SpriteRenderer>(); hitbox ??= GetComponent<BoxCollider2D>();
            if (!cracks)
            {
                var overlay = new GameObject("Damage cracks"); overlay.transform.SetParent(transform, false);
                overlay.transform.localPosition = new Vector3(0, 0, -0.01f);
                cracks = overlay.AddComponent<SpriteRenderer>(); cracks.sortingOrder = visual.sortingOrder + 1;
            }
            cracks.enabled = false; cracks.sprite = null;
            transform.position = new Vector3(x, -row - 0.5f, 0);
            transform.rotation=Quaternion.identity;
            visual.sprite = owner.Balance.rulesVersion>=3?PixelArt.WorldBlock(kind,variant,owner.Balance.TerrainAt(row),surface):PixelArt.Block(kind, variant, deep, surface); visual.color = Color.white;
            hitbox.size = new Vector2(0.99f, 0.99f); hitbox.enabled = kind!=BlockKind.Rail;
            gameObject.SetActive(true);
        }
        public void RestoreHealth(int halfUnits)
        {
            HealthHalfUnits = Mathf.Clamp(halfUnits, 1, maxHealthHalfUnits); UpdateCracks();
        }
        public void SetLit(float until){LitUntil=until;world.Remember(this);}
        void UpdateCracks()
        {
            if (HealthHalfUnits >= maxHealthHalfUnits) { DamageStage = 0; cracks.enabled = false; return; }
            DamageStage = Mathf.Clamp(Mathf.CeilToInt((1f - (float)HealthHalfUnits / maxHealthHalfUnits) * 8), 1, 8);
            cracks.sprite = PixelArt.Cracks(DamageStage); cracks.enabled = true;
        }
        public bool HitHalfUnits(int damage, FallingPickaxe source, DamageOrigin origin = DamageOrigin.Physical)
        {
            if(Kind==BlockKind.Spawner && world.Balance.rulesVersion>=3){world.TrialHit(this,source,origin);return false;}
            if (IsBroken || damage <= 0 || !world.CanMine || origin == DamageOrigin.Shrapnel && !ResourceInventory.IsMaterial(Kind)) return false;
            if(Kind==BlockKind.Redstone && origin!=DamageOrigin.Shrapnel)world.ActivateRedstone(Column,Row,source);
            HealthHalfUnits = damage >= HealthHalfUnits ? 0 : HealthHalfUnits - damage;
            if (HealthHalfUnits == 0) return DestroyBlock(source, origin);
            UpdateCracks(); world.Remember(this); return false;
        }
        public bool DestroyBlock(FallingPickaxe source, bool explosion) => DestroyBlock(source, explosion ? DamageOrigin.Explosion : DamageOrigin.Physical);
        public bool DestroyBlock(FallingPickaxe source, DamageOrigin origin = DamageOrigin.Physical)
        {
            if(Kind==BlockKind.Spawner && world.Balance.rulesVersion>=3){world.TrialHit(this,source,origin);return false;}
            if (IsBroken || !world.CanMine || origin == DamageOrigin.Explosion && GameBalance.IsWorkbench(Kind) || origin == DamageOrigin.Shrapnel && !ResourceInventory.IsMaterial(Kind)) return false;
            if(Kind==BlockKind.Redstone && (origin==DamageOrigin.Physical || origin==DamageOrigin.Dash || origin==DamageOrigin.Plunge))
            {world.ActivateRedstone(Column,Row,source);if(IsBroken)return false;}
            HealthHalfUnits = 0; IsBroken = true; hitbox.enabled = false; world.Break(this, source, origin); return true;
        }
        public void RetireTrial(){if(IsBroken)return;HealthHalfUnits=0;IsBroken=true;hitbox.enabled=false;world.RetireTrial(this);gameObject.SetActive(false);}
        // Legacy full-damage entrypoint for editor inspection; production impacts carry a source.
        public bool Hit(int damage) => damage == int.MaxValue ? DestroyBlock(null) : HitHalfUnits(damage > int.MaxValue / 2 ? int.MaxValue : damage * 2, null);
    }
}
