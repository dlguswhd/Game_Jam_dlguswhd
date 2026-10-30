using GameJam.Weapons;
using UnityEngine;

namespace GameJam.Combat
{
    // Extra hitbox (e.g. the head) that forwards scaled damage to the owner's Health.
    public class HitZone : MonoBehaviour, IDamageable
    {
        [SerializeField] private Health owner;
        [SerializeField] private float damageMultiplier = 2f;

        private void Awake()
        {
            if (owner == null) owner = GetComponentInParent<Health>();
        }

        public void TakeDamage(float damage, RaycastHit hit, GameObject instigator)
        {
            if (owner != null) owner.TakeDamage(damage * damageMultiplier, hit, instigator);
        }
    }
}
