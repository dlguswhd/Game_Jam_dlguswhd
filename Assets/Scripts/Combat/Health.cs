using System;
using GameJam.Weapons;
using UnityEngine;

namespace GameJam.Combat
{
    public class Health : MonoBehaviour, IScaledDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [Tooltip("If above 0, every hit takes an equal share of max health regardless of the weapon's damage, " +
                 "so this many hits kill (enemies: 3 body shots). 0 = use the damage numbers.")]
        [SerializeField] private int hitsToKill;

        public float MaxHealth => maxHealth;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        public event Action<float, RaycastHit, GameObject> Damaged;
        public event Action<GameObject> Died;

        private void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(float damage, RaycastHit hit, GameObject instigator)
            => TakeScaledDamage(damage, 1f, hit, instigator);

        public void TakeScaledDamage(float damage, float scale, RaycastHit hit, GameObject instigator)
        {
            if (IsDead || damage <= 0f || scale <= 0f) return;

            float dealt = hitsToKill > 0 ? maxHealth / hitsToKill : damage;
            Apply(dealt * Mathf.Clamp01(scale), hit, instigator);
        }

        public void TakeHeadshot(float scale, RaycastHit hit, GameObject instigator)
        {
            if (!IsDead && scale > 0f) Apply(maxHealth * Mathf.Clamp01(scale), hit, instigator);
        }

        // Dies outright, ignoring hitsToKill (headshots).
        public void Kill(RaycastHit hit, GameObject instigator)
        {
            if (IsDead) return;
            Apply(Current, hit, instigator);
        }

        private void Apply(float amount, RaycastHit hit, GameObject instigator)
        {
            // Small epsilon so N equal shares of maxHealth always add up to a kill despite float rounding.
            Current = Current - amount <= 0.001f ? 0f : Current - amount;
            Damaged?.Invoke(amount, hit, instigator);

            if (IsDead) Died?.Invoke(instigator);
        }
    }
}
