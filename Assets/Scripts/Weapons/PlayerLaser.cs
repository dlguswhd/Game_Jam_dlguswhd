using KINEMATION.TacticalShooterPack.Scripts.Attachments;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using UnityEngine;

namespace GameJam.Weapons
{
    // T toggles the laser of the active weapon's LS3 flashlight (the light itself is Y = UseUnderbarrel).
    public class PlayerLaser : MonoBehaviour
    {
        private TacticalShooterPlayer _shooter;

        private void Awake()
        {
            _shooter = GetComponent<TacticalShooterPlayer>();
        }

        // Sent by PlayerInput (Send Messages) for the ToggleLaser action.
        public void OnToggleLaser()
        {
            if (_shooter == null || !_shooter.enabled) return;

            var weapon = _shooter.GetPrimaryWeapon();
            var flashlight = weapon != null && weapon.Attachments != null
                ? weapon.Attachments.ActiveUnderbarrel as FlashlightAttachment
                : null;
            if (flashlight == null) return;

            var laser = flashlight.GetComponent<LaserSight>();
            if (laser == null)
            {
                laser = flashlight.gameObject.AddComponent<LaserSight>();
                laser.Initialize(LaserSight.LS3Aperture, transform);
            }
            laser.SetOn(!laser.IsOn);
        }
    }
}
