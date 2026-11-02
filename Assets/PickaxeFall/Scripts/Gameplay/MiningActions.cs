using System;
using UnityEngine;

namespace PickaxeFall
{
    public enum DamageOrigin { Physical, Dash, Plunge, Shrapnel, Explosion }
    public enum AbilityKind { Dash, Plunge }

    public sealed class RunEnergy
    {
        public float Maximum { get; private set; } = 100;
        public float Value { get; private set; }
        public float Earned { get; private set; }
        public void Reset(RunUpgrades upgrades) { Maximum = upgrades.MaxEnergy; Value = Maximum; Earned = 0; }
        public bool CanSpend(float amount) => amount > 0 && Value + 0.0001f >= amount;
        public bool TrySpend(float amount) { if (!CanSpend(amount)) return false; Value = Mathf.Max(0, Value - amount); return true; }
        public void Add(float amount) { float before = Value; Value = Mathf.Min(Maximum, Value + Mathf.Max(0, amount)); Earned += Value - before; }
        public void Ensure(float minimum) => Add(Mathf.Max(0, minimum - Value));
    }

    public sealed class RunBenefits
    {
        public int BonusOre, SavedWear, AbilityBlocks, ShrapnelBlocks, StrongHits, BonusTnt, DoubleBenches, DoubleBlasts, Rescues, RestoredDurability, Abilities;
        public void Reset() { BonusOre = SavedWear = AbilityBlocks = ShrapnelBlocks = StrongHits = BonusTnt = DoubleBenches = DoubleBlasts = Rescues = RestoredDurability = Abilities = 0; }
        public string Summary => $"Пробито: {AbilityBlocks} · Доп. руда: {BonusOre} · Сбережено: {SavedWear}";
        public string Details(float energy) => $"Активных действий: {Abilities}\nБлоков пробито: {AbilityBlocks}\nДополнительная руда: {BonusOre}\nСбережено прочности: {SavedWear}\nСильных ударов: {StrongHits}\nБлоков добыто осколками: {ShrapnelBlocks}\nВзрывных находок: {BonusTnt}\nДвойных верстаков: {DoubleBenches}\nУсиленных взрывов: {DoubleBlasts}\nСпасений: {Rescues}\nВосстановлено прочности: {RestoredDurability}\nПолучено энергии: {Progression.Number(energy)}";
    }

    // One contact episode per cell, independent of elapsed time or collider count.
    public sealed class ImpactGate
    {
        sealed class Contact
        {
            public bool Armed = true;
            public int HitStep = -1, SeenStep;
            public Vector2 Normal, LocalPoint;
        }
        readonly System.Collections.Generic.Dictionary<Vector2Int, Contact> contacts = new();
        readonly System.Collections.Generic.List<Vector2Int> retired = new();
        public void Clear() => contacts.Clear();
        public static Vector2 Rotate(Vector2 point, float degrees)
        {
            float angle=degrees*Mathf.Deg2Rad, c=Mathf.Cos(angle), s=Mathf.Sin(angle);
            return new Vector2(c*point.x-s*point.y,s*point.x+c*point.y);
        }
        public void Prepare(int step, Vector2 linear, float angular, Vector2 center, Vector2 position, float rotation, float rearmSpeed)
        {
            retired.Clear();
            foreach (var pair in contacts)
            {
                var c = pair.Value;
                if (step - c.SeenStep > 50) { retired.Add(pair.Key); continue; }
                Vector2 offset = position + Rotate(c.LocalPoint,rotation) - center;
                Vector2 pointVelocity = linear + new Vector2(-offset.y, offset.x) * angular;
                if (step - c.SeenStep > 1 || Vector2.Dot(pointVelocity, c.Normal) > rearmSpeed) c.Armed = true;
            }
            foreach (var key in retired) contacts.Remove(key);
        }
        public bool TryHit(Vector2Int cell, int step, float approach, float minimum, Vector2 normal, Vector2 localPoint)
        {
            if (!contacts.TryGetValue(cell, out var c)) contacts.Add(cell, c = new Contact());
            c.SeenStep = step; c.Normal = normal; c.LocalPoint = localPoint;
            if (!c.Armed || c.HitStep == step || approach < minimum) return false;
            c.Armed = false; c.HitStep = step; return true;
        }
        public void Exit(Vector2Int cell) { if (contacts.TryGetValue(cell, out var c)) c.Armed = true; }
    }
}
