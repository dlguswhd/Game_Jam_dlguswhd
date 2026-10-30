using System;
using GameJam.Enemies;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;

namespace GameJam.Player
{
    // Turns what the player does into noise enemies can hear: every enemy inside the radius gains the suspicion.
    // Movement is continuous (suspicion per second while moving); everything else is a one-off event.
    public class PlayerNoise : MonoBehaviour
    {
        [Serializable]
        public struct NoiseProfile
        {
            public float radius;
            public float suspicion;

            public NoiseProfile(float radius, float suspicion)
            {
                this.radius = radius;
                this.suspicion = suspicion;
            }
        }

        [Header("Movement (suspicion per second)")]
        [SerializeField] private NoiseProfile slowWalk = new NoiseProfile(2f, 5f);
        [SerializeField] private NoiseProfile walk = new NoiseProfile(5f, 5f);
        [SerializeField] private NoiseProfile sprint = new NoiseProfile(10f, 10f);

        [Header("Events")]
        [SerializeField] private NoiseProfile reload = new NoiseProfile(2f, 5f);
        [SerializeField] private NoiseProfile suppressedShot = new NoiseProfile(10f, 30f);
        [Tooltip("Not in the design table: an unsuppressed shot puts everyone nearby straight into Combat.")]
        [SerializeField] private NoiseProfile shot = new NoiseProfile(50f, 100f);
        [SerializeField] private NoiseProfile door = new NoiseProfile(2f, 5f);

        private PlayerMovement _movement;
        private TacticalShooterPlayer _shooter;
        private TacticalShooterWeapon[] _weapons;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _shooter = GetComponent<TacticalShooterPlayer>();
        }

        private void OnDestroy()
        {
            if (_weapons == null) return;
            foreach (var weapon in _weapons)
            {
                if (weapon != null) weapon.Fired -= OnWeaponFired;
            }
        }

        private void Update()
        {
            // Weapons are spawned in TacticalShooterPlayer.Start, so hook them once they exist.
            if (_weapons == null) SubscribeToWeapons();

            if (_movement == null || !_movement.enabled) return;

            switch (_movement.Gait)
            {
                case MovementGait.SlowWalk: EmitContinuous(slowWalk); break;
                case MovementGait.Walk: EmitContinuous(walk); break;
                case MovementGait.Sprint: EmitContinuous(sprint); break;
            }
        }

        private void SubscribeToWeapons()
        {
            var weapons = GetComponentsInChildren<TacticalShooterWeapon>(true);
            if (weapons.Length == 0) return;

            _weapons = weapons;
            foreach (var weapon in _weapons) weapon.Fired += OnWeaponFired;
        }

        private void OnWeaponFired(TacticalShooterWeapon weapon)
        {
            Emit(weapon.IsSuppressed ? suppressedShot : shot, transform.position);
        }

        // Sent by PlayerInput (Send Messages) for the Reload action, alongside TacticalShooterPlayer.OnReload.
        public void OnReload()
        {
            if (_shooter == null || !_shooter.enabled) return;
            var weapon = _shooter.GetActiveWeapon();
            if (weapon == null || weapon.ActiveAmmo >= weapon.tacWeaponSettings.ammoCapacity) return;

            Emit(reload, transform.position);
        }

        public void EmitDoor(Vector3 doorPosition) => Emit(door, doorPosition);

        private void EmitContinuous(NoiseProfile profile)
        {
            Noise.Emit(transform.position, profile.radius, profile.suspicion * Time.deltaTime);
        }

        private static void Emit(NoiseProfile profile, Vector3 position)
        {
            Noise.Emit(position, profile.radius, profile.suspicion);
        }
    }
}
