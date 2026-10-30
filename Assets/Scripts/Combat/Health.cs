using System;
using GameJam.Weapons;
using UnityEngine;

namespace GameJam.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;

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
        {
            if (IsDead || damage <= 0f) return;

            Current = Mathf.Max(0f, Current - damage);
            Damaged?.Invoke(damage, hit, instigator);

            if (IsDead) Died?.Invoke(instigator);
        }
    }
}
