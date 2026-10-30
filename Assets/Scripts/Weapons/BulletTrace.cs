using System;
using GameJam.World;
using UnityEngine;

namespace GameJam.Weapons
{
    public static class BulletTrace
    {
        private const float Skin = 0.003f;
        private static readonly RaycastHit[] Hits = new RaycastHit[128];
        private static readonly HitComparer Comparer = new HitComparer();
        private class HitComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }

        public static void Fire(Vector3 origin, Vector3 direction, float range, float damage, float force,
            LayerMask mask, GameObject instigator, float penetrationDepth, int maxPenetrations,
            float retention, bool debug = false, GameObject impactEffect = null)
        {
            direction.Normalize();
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, range, mask, QueryTriggerInteraction.Ignore);
            // Never use a truncated set to decide that a nearer wall is absent.
            if (count == Hits.Length)
            {
                Physics.Raycast(origin, direction, out Hits[0], range, mask, QueryTriggerInteraction.Ignore);
                count = 1;
                maxPenetrations = 0;
            }
            Array.Sort(Hits, 0, count, Comparer);
            float budget = Mathf.Max(0f, penetrationDepth);
            float scale = 1f;
            int penetrated = 0;
            Vector3 last = origin;
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.collider == null) continue;
                if (instigator != null && hit.collider.transform.IsChildOf(instigator.transform)) continue;
                if (debug) Debug.DrawLine(last, hit.point, penetrated > 0 ? Color.cyan : Color.red, 1f);
                last = hit.point;
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target is IScaledDamageable scaled) scaled.TakeScaledDamage(damage, scale, hit, instigator);
                else target?.TakeDamage(damage * scale, hit, instigator);
                if (hit.rigidbody != null)
                    hit.rigidbody.AddForceAtPosition(direction * (force * scale), hit.point, ForceMode.Impulse);
                if (impactEffect != null)
                {
                    var fx = UnityEngine.Object.Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
                    UnityEngine.Object.Destroy(fx, 5f);
                }
                bool door = hit.collider.GetComponentInParent<Door>() != null;
                if (door) BulletImpactMarks.Add(hit);
                // Characters stop the shot. World solids need an actual, bounded exit in the same collider.
                if (target != null || penetrated >= maxPenetrations || budget <= Skin ||
                    !TryExit(hit, direction, Mathf.Min(budget, range - hit.distance), out var exit, out float thickness)) return;
                if (door) BulletImpactMarks.Add(exit);
                budget -= thickness;
                penetrated++;
                scale *= Mathf.Clamp01(retention) * Mathf.Lerp(1f, 0.5f, thickness / Mathf.Max(Skin, penetrationDepth));
                if (scale < 0.02f) return;
            }
            if (debug) Debug.DrawLine(last, origin + direction * range, Color.yellow, 1f);
        }

        public static bool TryExit(RaycastHit entry, Vector3 direction, float maxThickness,
            out RaycastHit exit, out float thickness)
        {
            exit = default;
            thickness = 0f;
            if (maxThickness <= Skin) return false;
            var reverse = new Ray(entry.point + direction * (maxThickness + Skin), -direction);
            if (!entry.collider.Raycast(reverse, out exit, maxThickness + Skin * 2f)) return false;
            thickness = Vector3.Dot(exit.point - entry.point, direction);
            // A plane with no back surface is not assumed to be an infinitely thin wall.
            return thickness > Skin && thickness <= maxThickness;
        }
    }
}
