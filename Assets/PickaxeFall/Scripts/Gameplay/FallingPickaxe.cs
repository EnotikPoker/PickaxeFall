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
        float nextStrikeTime, lastReboundTime;
        Vector2 incomingVelocity;
        System.Random physicsRandom;
        public int TotalStrikes { get; private set; }
        public int HopCount { get; private set; }
        public bool IsVisible => visual && visual.enabled;
        public int Tier { get; private set; }
        public int OriginalTier { get; private set; }
        public int ToolIndex { get; private set; }
        public bool IsAlive => gameObject.activeSelf && Durability > 0 && visual.enabled;
        float spawnX;
        public int DamageHalfUnits => Stats.damage * 2 + game.RunUpgrades.DamageBonusHalfUnits(OriginalTier);
        public int Durability { get; private set; }
        public int MaxDurability => game.Balance.pickaxes[Tier].durability + game.RunUpgrades.DurabilityBonus(OriginalTier);
        public PickaxeStats Stats => game.Balance.pickaxes[Tier];
        public Rigidbody2D Body => body;

        public void Initialize(GameController owner)
        {
            game = owner; body = GetComponent<Rigidbody2D>(); visual = GetComponent<SpriteRenderer>();
            visual.sortingOrder = 10;
            transform.localScale = Vector3.one * PixelArt.PickaxeScale;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.angularDamping = 0.14f; body.linearDamping = 0.05f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            material = new PhysicsMaterial2D("Pickaxe bounce") { friction = game.Balance.surfaceFriction, bounciness = game.Balance.restitution };
            var head = gameObject.AddComponent<PolygonCollider2D>();
            head.points = PixelArt.PickaxeHeadOutline();
            head.sharedMaterial = material;
            // A child collider follows the diagonal wooden handle.
            var handleObject = new GameObject("Handle collider"); handleObject.transform.SetParent(transform, false);
            handleObject.transform.localPosition = new Vector3(-0.067f, -0.067f); handleObject.transform.localRotation = Quaternion.Euler(0, 0, -45);
            var handleCollider = handleObject.AddComponent<CapsuleCollider2D>();
            handleCollider.size = new Vector2(0.09f, 0.68f); handleCollider.sharedMaterial = material;
            body.simulated = false;
        }

        public void Equip(int tier, int toolIndex = 0, float x = 0)
        {
            Tier = OriginalTier = Mathf.Clamp(tier, 0, game.Balance.pickaxes.Length - 1);
            ToolIndex = toolIndex; spawnX = x;
            Durability = MaxDurability;
            visual.sprite = PixelArt.Pickaxe(Stats.color); visual.color = Color.white; visual.enabled = true;
            TotalStrikes = HopCount = 0; physicsRandom = new System.Random(unchecked(game.Seed ^ tier * 73856093 ^ (toolIndex + 1) * 19349663));
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
            Vector3 spawn = new(spawnX, game.Balance.spawnHeight, 0);
            transform.SetPositionAndRotation(spawn, Quaternion.Euler(0, 0, -12));
            body.position = spawn; body.rotation = -12;
            body.linearVelocity = Vector2.zero; body.angularVelocity = 0;
            nextStrikeTime = Time.time; lastReboundTime = Time.time; incomingVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void Launch()
        {
            ResetPose();
            body.simulated = true;
            body.AddForce(new Vector2((float)physicsRandom.NextDouble() * 0.7f - 0.35f, -0.3f), ForceMode2D.Impulse);
            body.angularVelocity = (physicsRandom.NextDouble() < 0.5 ? -1 : 1) * game.Balance.launchAngularSpeed;
        }

        void FixedUpdate()
        {
            if (game.Phase != RunPhase.Mining || !IsAlive) return;
            var velocity = body.linearVelocity;
            velocity.x = Mathf.Clamp(velocity.x, -9, 9);
            velocity.y = Mathf.Max(velocity.y, -game.Balance.maxFallSpeed);
            body.linearVelocity = velocity; incomingVelocity = velocity;
            body.angularVelocity = Mathf.Clamp(body.angularVelocity, -game.Balance.maximumAngularSpeed, game.Balance.maximumAngularSpeed);
        }

        void OnCollisionEnter2D(Collision2D collision) { Rebound(collision, true); Strike(collision); }
        void OnCollisionStay2D(Collision2D collision) { Rebound(collision, false); Strike(collision); }

        void Rebound(Collision2D collision, bool impact)
        {
            if (game.Phase != RunPhase.Mining || !IsAlive || collision.contactCount == 0 || !collision.collider.GetComponent<MineBlock>()) return;
            var contact = collision.GetContact(0); Vector2 normal = contact.normal;
            float approach = Mathf.Max(0, -Vector2.Dot(incomingVelocity, normal));
            bool resting = !impact && Time.time - lastReboundTime > 0.55f && body.linearVelocity.sqrMagnitude < 0.25f;
            if (!resting && (!impact || approach < 1.2f)) return;
            float desired = resting ? game.Balance.minimumReboundSpeed : Mathf.Clamp(approach * game.Balance.restitution,
                game.Balance.minimumReboundSpeed, game.Balance.maximumReboundSpeed);
            float outward = Vector2.Dot(body.linearVelocity, normal);
            if (outward > game.Balance.maximumReboundSpeed)
                body.linearVelocity -= normal * (outward - game.Balance.maximumReboundSpeed);
            else if (outward < desired)
                body.AddForce(normal * ((desired - outward) * body.mass), ForceMode2D.Impulse);
            lastReboundTime = Time.time; HopCount++;
        }

        void Strike(Collision2D collision)
        {
            if (game.Phase != RunPhase.Mining || Durability <= 0) return;
            var block = collision.collider.GetComponent<MineBlock>();
            if (!block || block.IsBroken || Time.time < nextStrikeTime) return;
            // One impact per tool, even when several block colliders touch it at once.
            nextStrikeTime = Time.time + game.Balance.strikeInterval;
            if (block.Kind == BlockKind.Workbench || block.Kind == BlockKind.Tnt)
            {
                block.DestroyBlock(this);
                return;
            }
            block.HitHalfUnits(DamageHalfUnits, this);
            if (!game.RollEffect(UpgradeKind.Careful)) Durability--;
            TotalStrikes++;
            game.Effects.Impact(collision.GetContact(0).point, block.BaseColor, 3);
            // Wake sleeping bodies so resting contact still mines a hard block.
            body.WakeUp(); body.AddTorque((float)physicsRandom.NextDouble() * 0.24f - 0.12f, ForceMode2D.Impulse);
            if (Durability <= 0) game.OnPickaxeExhausted(this);
        }

        public void RepairAndUpgrade(int steps = 1)
        {
            Tier = Mathf.Min(Tier + steps, game.Balance.pickaxes.Length - 1);
            int perStep = Mathf.Min(game.Balance.workbenchRepairCap, Mathf.CeilToInt(MaxDurability * game.Balance.workbenchRepairFraction));
            Durability = Mathf.Min(MaxDurability, Durability + perStep * steps);
            body.mass = Stats.mass; visual.sprite = PixelArt.Pickaxe(Stats.color);
        }

        public void Revive() { Durability = Mathf.Max(1, Mathf.CeilToInt(MaxDurability * 0.25f)); body.WakeUp(); }

        public void Stop() { body.linearVelocity = Vector2.zero; body.angularVelocity = 0; body.simulated = false; }
        public void BreakApart()
        {
            game.Effects.Shatter(visual.sprite, transform, body.linearVelocity);
            visual.enabled = false; Stop();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugExhaust() { Durability = 0; game.OnPickaxeExhausted(this); }
#endif
        void OnDestroy() { if (material) Destroy(material); }
    }
}
