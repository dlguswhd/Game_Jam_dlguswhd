using GameJam.Weapons;
using UnityEngine;

namespace GameJam.Combat
{
    // Extra hitbox (e.g. the head) that forwards scaled damage to the owner's Health.
    public class HitZone : MonoBehaviour, IDamageable
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
        {
            if (owner == null) return;
            float dealt = instantKill ? Mathf.Max(owner.Current, damage) : damage * damageMultiplier;
            owner.TakeDamage(dealt, hit, instigator);
        }
    }
}
