using GameJam.Player;
using GameJam.UI;
using GameJam.World;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.Combat
{
    // On death: stop all control, show the "사망" screen, then restart the scene from its checkpoint.
    [RequireComponent(typeof(Health))]
    public class PlayerDeath : MonoBehaviour
    {
        private Health _health;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _health.Died += OnDied;
        }

        private void OnDestroy()
        {
            _health.Died -= OnDied;
        }

        private void OnDied(GameObject killer)
        {
            var shooter = GetComponent<TacticalShooterPlayer>();
            shooter.GetActiveWeapon().StopFiring();
            shooter.enabled = false;

            var input = GetComponent<PlayerInput>();
            if (input != null) input.enabled = false;

            var movement = GetComponent<PlayerMovement>();
            if (movement != null) movement.enabled = false;

            Debug.Log($"Player killed by {(killer != null ? killer.name : "unknown")}");
            // Back to how this scene was when the player entered it (items and kills since then are undone).
            EndScreen.ShowDeath(GameProgress.RestartFromCheckpoint);
        }
    }
}
