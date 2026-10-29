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
            var head = gameObject.AddComponent<PolygonCollider2D>();
            head.points = new[]
            {
                new Vector2(-0.32f, 0.35f), new Vector2(0.09f, 0.35f),
                new Vector2(0.36f, 0.10f), new Vector2(0.36f, -0.17f),
                new Vector2(0.22f, -0.17f), new Vector2(0.22f, 0.03f),
                new Vector2(0.03f, 0.20f), new Vector2(-0.32f, 0.20f)
            };
            head.sharedMaterial = material;
            // A child collider follows the diagonal wooden handle.
            var handleObject = new GameObject("Handle collider"); handleObject.transform.SetParent(transform, false);
            handleObject.transform.localPosition = new Vector3(-0.09f, -0.15f); handleObject.transform.localRotation = Quaternion.Euler(0, 0, -45);
            var handleCollider = handleObject.AddComponent<CapsuleCollider2D>();
            handleCollider.size = new Vector2(0.13f, 0.70f); handleCollider.sharedMaterial = material;
            body.simulated = false;
        }

        public void Equip(int tier)
        {
            Tier = Mathf.Clamp(tier, 0, game.Balance.pickaxes.Length - 1);
            Durability = MaxDurability;
            visual.sprite = PixelArt.Pickaxe(Stats.color); visual.color = Color.white;
            body.mass = Stats.mass;
            ResetPose();
        }

        void ResetPose()
        {
            gameObject.SetActive(true);
            body.simulated = false;
            // Unsimulated/interpolated Rigidbody2D.position alone can leave the visible
            // Transform at the previous run's underground position. Reset both poses.
            body.interpolation = RigidbodyInterpolation2D.None;
            Vector3 spawn = new(0, game.Balance.spawnHeight, 0);
            transform.SetPositionAndRotation(spawn, Quaternion.Euler(0, 0, -12));
            body.position = spawn; body.rotation = -12;
            body.linearVelocity = Vector2.zero; body.angularVelocity = 0;
            Physics2D.SyncTransforms();
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void Launch()
        {
            ResetPose();
            body.simulated = true;
            body.AddForce(new Vector2(Random.Range(-0.35f, 0.35f), -0.3f), ForceMode2D.Impulse);
            body.AddTorque(Random.value < 0.5f ? -1.8f : 1.8f, ForceMode2D.Impulse);
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
            if (block.Kind == BlockKind.Workbench || block.Kind == BlockKind.Tnt)
            {
                block.Hit(int.MaxValue);
                return;
            }
            block.Hit(Stats.damage);
            Durability--;
            game.Effects.Impact(collision.GetContact(0).point, block.BaseColor, 3);
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
