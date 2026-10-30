using GameJam.Weapons;
using UnityEngine;

namespace GameJam.Combat
{
    // Extra hitbox (e.g. the head) that forwards scaled damage to the owner's Health.
    public class HitZone : MonoBehaviour, IScaledDamageable
    {
        [SerializeField] private Health owner;
        [SerializeField] private float damageMultiplier = 2f;
        [Tooltip("Any hit here kills outright (headshots).")]
        [SerializeField] private bool instantKill = true;

        private void Awake()
        {
            if (owner == null) owner = GetComponentInParent<Health>();
        }

        public void TakeDamage(float damage, RaycastHit hit, GameObject instigator)
            => TakeScaledDamage(damage, 1f, hit, instigator);

        public void TakeScaledDamage(float damage, float scale, RaycastHit hit, GameObject instigator)
        {
            if (owner == null || damage <= 0f) return;
            if (instantKill && scale >= 0.9999f) owner.Kill(hit, instigator);
            else if (instantKill) owner.TakeHeadshot(scale, hit, instigator);
            else owner.TakeScaledDamage(damage * damageMultiplier, scale, hit, instigator);
        }
    }
}
