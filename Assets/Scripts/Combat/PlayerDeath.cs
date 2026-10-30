using GameJam.Player;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameJam.Combat
{
    [RequireComponent(typeof(Health))]
    public class PlayerDeath : MonoBehaviour
    {
        [SerializeField] private float restartDelay = 3f;

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
            Invoke(nameof(Restart), restartDelay);
        }

        private void Restart()
        {
            // The scene has to be listed in Build Profiles for this to work.
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
