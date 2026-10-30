using UnityEngine;

namespace GameJam.Weapons
{
    public interface IDamageable
    {
        void TakeDamage(float damage, RaycastHit hit, GameObject instigator);
    }

    // Keeps attenuation meaningful for enemies whose normal body damage is a fixed fraction of health.
    public interface IScaledDamageable : IDamageable
    {
        void TakeScaledDamage(float damage, float scale, RaycastHit hit, GameObject instigator);
    }
}
