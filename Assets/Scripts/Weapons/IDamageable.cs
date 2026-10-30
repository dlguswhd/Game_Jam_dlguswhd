using UnityEngine;

namespace GameJam.Weapons
{
    public interface IDamageable
    {
        void TakeDamage(float damage, RaycastHit hit, GameObject instigator);
    }
}
