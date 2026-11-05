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
        SpriteRenderer selection;
        PolygonCollider2D hitbox;
        readonly ImpactGate impacts = new();
        readonly RaycastHit2D[] sweepHits = new RaycastHit2D[64];
        ContactFilter2D worldFilter;
        Vector2 incomingVelocity;
        float incomingAngular, lastReboundTime, abilityUntil, momentumUntil, spawnX, nextTrail;
        int step, remaining;
        float lavaWear;
        public bool InWater { get; private set; }
        public Bounds HitBounds=>hitbox.bounds;
        public Vector2 ClosestPoint(Vector2 point)=>hitbox.ClosestPoint(point);
        public int RawDamageHalfUnits=>Stats.damage*2+game.RunUpgrades.DamageBonusHalfUnits(Tier);
        int WetDamage(int value)=>value<=0?0:Mathf.Max(1,Mathf.RoundToInt(value*(InWater?game.Balance.waterDamageMultiplier:1)));
        AbilityKind ability;
        System.Random physicsRandom;
        public int TotalStrikes { get; private set; }
        public int NaturalStrikes { get; private set; }
        public int HopCount { get; private set; }
        public bool IsVisible => visual && visual.enabled;
        public int Tier { get; private set; }
        public int OriginalTier { get; private set; }
        public int ToolIndex { get; private set; }
        public bool IsAlive => gameObject.activeSelf && Durability > 0 && visual.enabled;
        public bool AbilityActive => remaining > 0 && game.Clock.Now < abilityUntil;
        public int DamageHalfUnits => WetDamage(RawDamageHalfUnits);
        public int Durability { get; private set; }
        public int MaxDurability => Stats.durability + game.RunUpgrades.DurabilityBonus(Tier);
        public PickaxeStats Stats => game.Balance.pickaxes[Tier];
        public Rigidbody2D Body => body;

        public void Initialize(GameController owner)
        {
            game = owner; body = GetComponent<Rigidbody2D>(); visual = GetComponent<SpriteRenderer>(); visual.sortingOrder = 10;
            transform.localScale = Vector3.one * PixelArt.PickaxeScale;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.angularDamping = 0.14f; body.linearDamping = 0.05f; body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            material = new PhysicsMaterial2D("Pickaxe bounce") { friction = game.Balance.surfaceFriction, bounciness = game.Balance.restitution };
            hitbox=gameObject.AddComponent<PolygonCollider2D>();hitbox.sharedMaterial=material;
            worldFilter = new ContactFilter2D { useTriggers = false };
            var marker=new GameObject("Selected tool silhouette");marker.transform.SetParent(transform,false);
            selection=marker.AddComponent<SpriteRenderer>();selection.sortingOrder=9;selection.enabled=false;
            body.simulated = false;
        }
        void OnDestroy()
        {
            if (material) DestroyImmediate(material);

        }
        public void SetSelected(bool selected) { if (selection) selection.enabled = selected && IsAlive && game.LivingToolCount>1; }
        public bool SelectionVisible => selection && selection.enabled;
        void SetArt()
        {
            visual.sprite=PixelArt.Pickaxe(Tier);selection.sprite=PixelArt.PickaxeOutline(Tier);
            int count=visual.sprite.GetPhysicsShapeCount();hitbox.pathCount=count;
            var path=new System.Collections.Generic.List<Vector2>();
            for(int i=0;i<count;i++){visual.sprite.GetPhysicsShape(i,path);hitbox.SetPath(i,path);}
        }
        public void Equip(int tier, int toolIndex = 0, float x = 0)
        {
            Tier = OriginalTier = Mathf.Clamp(tier, 0, game.Balance.pickaxes.Length - 1); ToolIndex = toolIndex; spawnX = x;
            Durability = MaxDurability;lavaWear=0;InWater=false; SetArt(); visual.color = Color.white; visual.enabled = true;
            TotalStrikes = NaturalStrikes = HopCount = 0; physicsRandom = new System.Random(unchecked(game.Seed ^ tier * 73856093 ^ (toolIndex + 1) * 19349663));
            body.mass = Stats.mass; ResetPose();
        }
        void ResetPose()
        {
            gameObject.SetActive(true); body.simulated = false; body.interpolation = RigidbodyInterpolation2D.None;
            Vector3 spawn = new(spawnX, game.Balance.spawnHeight, 0); transform.SetPositionAndRotation(spawn, Quaternion.Euler(0, 0, -12));
            body.position = spawn; body.rotation = -12; body.linearVelocity = Vector2.zero; body.angularVelocity = 0;
            remaining = 0; abilityUntil = momentumUntil = 0; step = 0; impacts.Clear();
            lastReboundTime = game.Clock.Now; incomingVelocity = Vector2.zero; incomingAngular = 0;
            Physics2D.SyncTransforms(); body.interpolation = game.IsSimulation ? RigidbodyInterpolation2D.None : RigidbodyInterpolation2D.Interpolate;
        }
        public void Launch()
        {
            ResetPose(); body.simulated = true;
            body.AddForce(new Vector2((float)physicsRandom.NextDouble() * 0.7f - 0.35f, -0.3f), ForceMode2D.Impulse);
            body.angularVelocity = (physicsRandom.NextDouble() < 0.5 ? -1 : 1) * game.Balance.launchAngularSpeed;
        }
        public bool StartAbility(AbilityKind kind, Vector2 direction)
        {
            if (!IsAlive || AbilityActive || !body.simulated) return false;
            ability = kind; remaining = kind == AbilityKind.Dash ? 1 : game.RunUpgrades.PlungeBlocks;
            abilityUntil = game.Clock.Now + (kind == AbilityKind.Dash ? 0.35f : 0.6f); momentumUntil = abilityUntil;
            Vector2 desired = kind == AbilityKind.Dash ? direction * game.RunUpgrades.DashSpeed : new Vector2(body.linearVelocity.x * 0.5f, -14);
            body.AddForce((desired - body.linearVelocity) * body.mass, ForceMode2D.Impulse); body.WakeUp(); nextTrail = 0; return true;
        }
        public void CancelAbility() { remaining = 0; abilityUntil = momentumUntil = 0; }
        public void PhysicsTick()
        {
            if (game.Phase != RunPhase.Mining || game.Paused || !IsAlive) return;
            InWater=game.World.Liquids.Touches(this,BlockKind.Water);
            if(game.World.Liquids.Touches(this,BlockKind.Lava))
            {
                lavaWear+=game.Balance.lavaWearPerSecond*game.Clock.Delta;
                int loss=Mathf.FloorToInt(lavaWear+0.00001f);lavaWear=Mathf.Max(0,lavaWear-loss);
                if(loss>0){int lost=Mathf.Min(loss,Durability);Durability-=lost;game.Benefits.LavaWear+=lost;if(Durability<=0){CancelAbility();game.OnPickaxeExhausted(this);return;}}
            }
            step++;
            var velocity = body.linearVelocity; float horizontalLimit = game.Clock.Now < momentumUntil ? 18 : 9;
            velocity.x = Mathf.Clamp(velocity.x, -horizontalLimit, horizontalLimit); velocity.y = Mathf.Max(velocity.y, -game.Balance.maxFallSpeed);
            body.linearVelocity = velocity;
            body.angularVelocity = Mathf.Clamp(body.angularVelocity, -game.Balance.maximumAngularSpeed, game.Balance.maximumAngularSpeed);
            incomingVelocity = velocity; incomingAngular = body.angularVelocity * Mathf.Deg2Rad;
            impacts.Prepare(step, velocity, incomingAngular, body.worldCenterOfMass, body.position, body.rotation, game.Balance.impactRearmSpeed);
            if (AbilityActive) SweepAbility();
        }
        void SweepAbility()
        {
            Vector2 velocity = body.linearVelocity; float distance = velocity.magnitude * game.Clock.Delta + 0.025f;
            if (velocity.sqrMagnitude < 0.01f) return;
            // Cast the whole rigidbody, including the handle, before the solver can bounce it.
            int count = body.Cast(velocity.normalized, worldFilter, sweepHits, distance);
            System.Array.Sort(sweepHits, 0, count, HitOrder.Instance);
            for (int i = 0; i < count && AbilityActive && IsAlive; i++)
            {
                if (!sweepHits[i].collider) continue;
                var block = sweepHits[i].collider.GetComponent<MineBlock>();
                if (!block) { if (!sweepHits[i].collider.attachedRigidbody) CancelAbility(); continue; }
                if (!block.IsBroken) Penetrate(block, sweepHits[i].point);
            }
            if (IsAlive && game.Clock.Now >= nextTrail)
            {
                game.Effects.Impact(body.position, new Color32(94, 225, 238, 255), 2, false, false); nextTrail = game.Clock.Now + 0.07f;
            }
        }
        sealed class HitOrder : System.Collections.Generic.IComparer<RaycastHit2D>
        {
            public static readonly HitOrder Instance = new();
            public int Compare(RaycastHit2D a, RaycastHit2D b) => a.distance.CompareTo(b.distance);
        }
        void Penetrate(MineBlock block, Vector2 point)
        {
            if (!AbilityActive || block.IsBroken) return;
            var kind = block.Kind;
            if (!ResourceInventory.IsMaterial(kind)) { CancelAbility(); block.DestroyBlock(this); return; }
            var origin = ability == AbilityKind.Dash ? DamageOrigin.Dash : DamageOrigin.Plunge;
            remaining--; block.DestroyBlock(this, origin);
            game.Effects.Impact(point, new Color32(94, 225, 238, 255), 6);
            SpendWear();
        }
        Vector2 PointVelocity(Vector2 point)
        {
            Vector2 offset = point - body.worldCenterOfMass;
            return incomingVelocity + new Vector2(-offset.y, offset.x) * incomingAngular;
        }
        void OnCollisionEnter2D(Collision2D collision) => Contact(collision, true);
        void OnCollisionStay2D(Collision2D collision) => Contact(collision, false);
        void OnCollisionExit2D(Collision2D collision)
        {
            var block = collision.collider.GetComponent<MineBlock>();
            if (block) impacts.Exit(new Vector2Int(block.Column, block.Row));
        }
        void Contact(Collision2D collision, bool enter)
        {
            if (game.Phase != RunPhase.Mining || game.Paused || !IsAlive || collision.contactCount == 0) return;
            var block = collision.collider.GetComponent<MineBlock>(); if (!block || block.IsBroken) return;
            if(block.Kind==BlockKind.Redstone){game.World.ActivateRedstone(block.Column,block.Row,this);if(block.IsBroken)return;}
            var contact = collision.GetContact(0); float approach = -Vector2.Dot(PointVelocity(contact.point), contact.normal);
            for (int i = 1; i < collision.contactCount; i++)
            {
                var candidate = collision.GetContact(i); float speed = -Vector2.Dot(PointVelocity(candidate.point), candidate.normal);
                if (speed > approach) { approach = speed; contact = candidate; }
            }
            if (AbilityActive)
            {
                // Normally the sweep destroys it first. This handles a rotating/overlapping tip.
                Vector2 before = incomingVelocity;
                Penetrate(block, contact.point);
                if (block.IsBroken && IsAlive && game.Clock.Now < momentumUntil) body.linearVelocity = before;
                return;
            }
            var key = new Vector2Int(block.Column, block.Row);
            if (impacts.TryHit(key, step, approach, game.Balance.minimumImpactSpeed, contact.normal, ImpactGate.Rotate(contact.point-body.position,-body.rotation)))
                PhysicalHit(block, approach, contact.point);
            if (IsAlive) Rebound(contact.normal, approach, enter);
        }
        void PhysicalHit(MineBlock block, float approach, Vector2 point)
        {
            if (!ResourceInventory.IsMaterial(block.Kind)) { block.DestroyBlock(this); return; }
            int x = block.Column, row = block.Row; Color color = block.BaseColor;
            bool strong = approach >= game.Balance.strongImpactSpeed;
            int damage = WetDamage(RawDamageHalfUnits + (strong ? game.RunUpgrades.StrongBonusHalfUnits : 0));
            block.HitHalfUnits(damage, this, DamageOrigin.Physical);
            NaturalStrikes++; game.OnPhysicalHit(this, strong);
            if (strong && game.RunUpgrades.ShrapnelHalfUnits > 0) game.World.DamageShrapnelNeighbour(x, row, WetDamage(game.RunUpgrades.ShrapnelHalfUnits), this, NaturalStrikes);
            game.Effects.Impact(point, color, 3); SpendWear();
        }
        void SpendWear()
        {
            if (!game.RollEffect(UpgradeKind.Careful)) Durability--; else game.Benefits.SavedWear++;
            TotalStrikes++;
            if (Durability <= 0) { CancelAbility(); game.OnPickaxeExhausted(this); }
        }
        void Rebound(Vector2 normal, float approach, bool impact)
        {
            bool resting = !impact && game.Clock.Now - lastReboundTime > 0.55f && body.linearVelocity.sqrMagnitude < 0.25f;
            if (!resting && (!impact || approach < game.Balance.minimumImpactSpeed)) return;
            float desired = resting ? game.Balance.minimumReboundSpeed : Mathf.Clamp(approach * game.Balance.restitution, game.Balance.minimumReboundSpeed, game.Balance.maximumReboundSpeed);
            float outward = Vector2.Dot(body.linearVelocity, normal);
            if (outward > game.Balance.maximumReboundSpeed) body.linearVelocity -= normal * (outward - game.Balance.maximumReboundSpeed);
            else if (outward < desired) body.AddForce(normal * ((desired - outward) * body.mass), ForceMode2D.Impulse);
            lastReboundTime = game.Clock.Now; HopCount++;
        }
        public void RepairAndUpgrade(int steps = 1)
        {
            Tier = Mathf.Min(Tier + steps, game.Balance.pickaxes.Length - 1); int before = Durability;
            Durability = MaxDurability; game.Benefits.RestoredDurability += Mathf.Max(0, Durability - before);
            body.mass = Stats.mass; SetArt();
        }
        public void Revive() { Durability = Mathf.Max(1, Mathf.CeilToInt(MaxDurability * 0.5f)); body.WakeUp(); }
        public void Stop() { CancelAbility(); body.linearVelocity = Vector2.zero; body.angularVelocity = 0; body.simulated = false; SetSelected(false); }
        public ToolSnapshot Capture()=>new ToolSnapshot
        {
            lavaWear=lavaWear,tier=Tier,originalTier=OriginalTier,index=ToolIndex,alive=IsAlive,durability=Durability,strikes=TotalStrikes,naturalStrikes=NaturalStrikes,hops=HopCount,step=step,
            position=body.position,velocity=body.linearVelocity,rotation=body.rotation,angularVelocity=body.angularVelocity,incomingVelocity=incomingVelocity,incomingAngular=incomingAngular,
            lastRebound=lastReboundTime,remaining=remaining,ability=(int)ability,abilityUntil=abilityUntil,momentumUntil=momentumUntil,nextTrail=nextTrail,contacts=impacts.Capture()
        };
        public void Restore(ToolSnapshot s)
        {
            Equip(s.tier,s.index);lavaWear=s.lavaWear;OriginalTier=s.originalTier;Durability=Mathf.Clamp(s.durability,0,MaxDurability);
            TotalStrikes=s.strikes;NaturalStrikes=s.naturalStrikes;HopCount=s.hops;step=s.step;
            transform.SetPositionAndRotation(s.position,Quaternion.Euler(0,0,s.rotation));body.position=s.position;body.rotation=s.rotation;
            body.linearVelocity=s.velocity;body.angularVelocity=s.angularVelocity;incomingVelocity=s.incomingVelocity;incomingAngular=s.incomingAngular;
            lastReboundTime=s.lastRebound;remaining=s.remaining;ability=(AbilityKind)s.ability;abilityUntil=s.abilityUntil;momentumUntil=s.momentumUntil;nextTrail=s.nextTrail;
            impacts.Restore(s.contacts);visual.enabled=s.alive;body.simulated=s.alive;gameObject.SetActive(s.alive);
        }
        public void BreakApart() { game.Effects.Shatter(visual.sprite, transform, body.linearVelocity); visual.enabled = false; Stop(); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugExhaust() { Durability = 0; game.OnPickaxeExhausted(this); }
        public void DebugPlace(Vector2 position, Vector2 velocity, float rotation = 0)
        {
            CancelAbility(); impacts.Clear(); body.interpolation = RigidbodyInterpolation2D.None;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0,0,rotation)); body.position = position; body.rotation = rotation;
            body.linearVelocity = velocity; body.angularVelocity = 0; incomingVelocity = velocity; incomingAngular = 0; Physics2D.SyncTransforms();
        }
#endif
    }
}
