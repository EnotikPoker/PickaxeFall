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
        public float NextStrikeTime { get; set; }
        SpriteRenderer visual;
        BoxCollider2D hitbox;
        MiningWorld world;
        int maxHealth;

        public void Setup(MiningWorld owner, BlockKind kind, int x, int row, int variant)
        {
            world = owner; Kind = kind; Row = row;
            Health = maxHealth = owner.Balance.Block(kind).health; IsBroken = false; NextStrikeTime = 0;
            visual ??= GetComponent<SpriteRenderer>(); hitbox ??= GetComponent<BoxCollider2D>();
            transform.position = new Vector3(x, -row - 0.5f, 0);
            visual.sprite = PixelArt.Block(kind, variant); visual.color = Color.white;
            hitbox.size = new Vector2(0.99f, 0.99f); hitbox.enabled = true;
            gameObject.SetActive(true);
        }

        public bool Hit(int damage)
        {
            if (IsBroken) return false;
            Health = Mathf.Max(0, Health - damage);
            visual.color = Color.Lerp(new Color(0.45f, 0.47f, 0.53f), Color.white, (float)Health / maxHealth);
            if (Health > 0) return false;
            IsBroken = true; hitbox.enabled = false;
            world.Break(this);
            return true;
        }
    }
}
