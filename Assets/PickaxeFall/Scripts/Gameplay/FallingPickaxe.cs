using UnityEngine;

namespace PickaxeFall
{
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public class FallingPickaxe : MonoBehaviour
    {
        GameController game;
        Rigidbody2D body;
        SpriteRenderer visual;
        PhysicsMaterial2D material;
        float nextNudge;
        public int Tier { get; private set; }
        public int Durability { get; private set; }
        public int MaxDurability => game.Balance.pickaxes[Tier].durability;
        public PickaxeStats Stats => game.Balance.pickaxes[Tier];
        public Rigidbody2D Body => body;

        public void Initialize(GameController owner)
        {
            game = owner; body = GetComponent<Rigidbody2D>(); visual = GetComponent<SpriteRenderer>();
            visual.sortingOrder = 10;
            transform.localScale = Vector3.one * 1.65f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.angularDamping = 0.15f; body.linearDamping = 0.05f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            material = new PhysicsMaterial2D("Pickaxe bounce") { friction = 0.25f, bounciness = 0.32f };
            var head = gameObject.AddComponent<BoxCollider2D>();
            head.offset = new Vector2(0.08f, 0.26f); head.size = new Vector2(0.73f, 0.19f); head.sharedMaterial = material;
            var tip = gameObject.AddComponent<BoxCollider2D>();
            tip.offset = new Vector2(0.31f, 0.12f); tip.size = new Vector2(0.17f, 0.30f); tip.sharedMaterial = material;
            // A child collider follows the diagonal wooden handle.
            var handleObject = new GameObject("Handle collider"); handleObject.transform.SetParent(transform, false);
            handleObject.transform.localPosition = new Vector3(-0.06f, -0.06f); handleObject.transform.localRotation = Quaternion.Euler(0, 0, -45);
            var handleCollider = handleObject.AddComponent<CapsuleCollider2D>();
            handleCollider.size = new Vector2(0.13f, 0.78f); handleCollider.sharedMaterial = material;
            body.simulated = false;
        }

        public void Equip(int tier)
        {
            Tier = Mathf.Clamp(tier, 0, game.Balance.pickaxes.Length - 1);
            Durability = MaxDurability;
            visual.sprite = PixelArt.Pickaxe(Stats.color); visual.color = Color.white;
            body.simulated = false; body.position = new Vector2(0, 2.6f); body.rotation = -20;
            body.linearVelocity = Vector2.zero; body.angularVelocity = 0; body.mass = Stats.mass;
            gameObject.SetActive(true);
        }

        public void Launch()
        {
            nextNudge = 0;
            body.simulated = true;
            body.AddForce(new Vector2(Random.Range(-0.65f, 0.65f), -1), ForceMode2D.Impulse);
            body.AddTorque(Random.value < 0.5f ? -1.8f : 1.8f, ForceMode2D.Impulse);
        }

        public void Nudge(int direction)
        {
            if (game.Phase != RunPhase.Mining || Time.time < nextNudge) return;
            nextNudge = Time.time + game.Balance.nudgeCooldown;
            body.WakeUp();
            body.AddForce(new Vector2(direction * game.Balance.nudgeImpulse, 1.4f) * body.mass, ForceMode2D.Impulse);
            body.AddTorque(-direction * 1.3f, ForceMode2D.Impulse);
        }

        void FixedUpdate()
        {
            if (game.Phase != RunPhase.Mining) return;
            var velocity = body.linearVelocity;
            velocity.x = Mathf.Clamp(velocity.x, -9, 9);
            velocity.y = Mathf.Max(velocity.y, -game.Balance.maxFallSpeed);
            body.linearVelocity = velocity;
        }

        void OnCollisionEnter2D(Collision2D collision) => Strike(collision);
        void OnCollisionStay2D(Collision2D collision) => Strike(collision);

        void Strike(Collision2D collision)
        {
            if (game.Phase != RunPhase.Mining || Durability <= 0) return;
            var block = collision.collider.GetComponent<MineBlock>();
            if (!block || block.IsBroken || Time.time < block.NextStrikeTime) return;
            block.NextStrikeTime = Time.time + game.Balance.strikeInterval;
            if (block.Kind == BlockKind.Workbench)
            {
                block.Hit(int.MaxValue);
                return;
            }
            block.Hit(Stats.damage);
            Durability--;
            game.Effects.Impact(collision.GetContact(0).point, PixelArt.BlockColor(block.Kind), 3);
            // Wake sleeping bodies so resting contact still mines a hard block.
            body.WakeUp(); body.AddTorque(Random.Range(-0.12f, 0.12f), ForceMode2D.Impulse);
            if (Durability <= 0) game.FinishRun();
        }

        public void RepairAndUpgrade()
        {
            Tier = Mathf.Min(Tier + 1, game.Balance.pickaxes.Length - 1);
            Durability = MaxDurability;
            body.mass = Stats.mass; visual.sprite = PixelArt.Pickaxe(Stats.color);
        }

        public void Stop() { body.linearVelocity = Vector2.zero; body.angularVelocity = 0; body.simulated = false; }
        void OnDestroy() { if (material) Destroy(material); }
    }
}
