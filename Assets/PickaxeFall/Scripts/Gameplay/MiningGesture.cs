using UnityEngine;

namespace PickaxeFall
{
    public sealed class MiningGesture
    {
        Vector2 start;
        float started, farthest;
        public bool Active { get; private set; }
        public void Begin(Vector2 position, float now) { start = position; started = now; farthest = 0; Active = true; }
        public void Move(Vector2 position) { if (Active) farthest = Mathf.Max(farthest, Vector2.Distance(start, position)); }
        public void Cancel() => Active = false;
        public bool End(Vector2 position, float now, out AbilityKind kind, out Vector2 direction)
        {
            kind = AbilityKind.Plunge; direction = Vector2.down;
            if (!Active) return false;
            Move(position); Active = false; float duration = now - started; Vector2 delta = position - start;
            if (duration >= 0 && duration <= 0.28f && farthest <= 18) return true;
            if (duration < 0 || duration > 0.75f || delta.magnitude < 60) return false;
            kind = AbilityKind.Dash;
            direction = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? new Vector2(Mathf.Sign(delta.x), 0) : new Vector2(0, -Mathf.Sign(delta.y));
            return true;
        }
    }
}
