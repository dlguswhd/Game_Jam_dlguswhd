using KINEMATION.TacticalShooterPack.Scripts.Animation;
using KINEMATION.TacticalShooterPack.Scripts.Attachments;
using GameJam.World;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.Player
{
    // Reads the mouse once per frame and feeds it to TacticalShooterPlayer.ApplyLook.
    // Runs before PlayerMovement (-50) so the body turns on the same frame the mouse moved.
    [DefaultExecutionOrder(-60)]
    public class MouseLook : MonoBehaviour
    {
        [Tooltip("Seconds to ease each frame's mouse movement in. 0 = raw. Small values (≈0.02) even out " +
                 "uneven frames with no noticeable lag.")]
        [SerializeField, Range(0f, 0.1f)] private float smoothing = 0.02f;
        [Tooltip("Sensitivity multiplier while aiming down sights (blended in with the aim animation).")]
        [SerializeField, Range(0.1f, 1f)] private float aimSensitivity = 0.5f;
        [Tooltip("Through a magnified scope the lens view moves faster, so aiming sensitivity is further divided " +
                 "by magnification^this (0.5 → a 4x scope halves it again, 0 = ignore magnification).")]
        [SerializeField, Range(0f, 1f)] private float magnificationCompensation = 0.5f;

        private TacticalShooterPlayer _shooter;
        private TacticalProceduralAnimation _animation;
        private InputAction _look;
        private Vector2 _pending;

        private void Awake()
        {
            _shooter = GetComponent<TacticalShooterPlayer>();
            _animation = GetComponent<TacticalProceduralAnimation>();
            var input = GetComponent<PlayerInput>();
            if (input != null) _look = input.actions.FindAction("Look");
        }

        private void Update()
        {
            if (_look == null || _shooter == null || !_shooter.enabled) return;

            // ReadValue on a mouse delta gives exactly this frame's accumulated movement.
            _pending += _look.ReadValue<Vector2>();

            // Hand over a share of the pending movement each frame; nothing is lost, only spread out.
            float share = smoothing <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothing);
            Vector2 step = _pending * share;
            _pending -= step;

            float aim = _animation != null ? _animation.aimingWeight : 0f;
            float aimed = aimSensitivity / Mathf.Pow(ActiveMagnification(), magnificationCompensation);
            _shooter.ApplyLook(step * (GameSettings.Sensitivity * Mathf.Lerp(1f, aimed, aim)));
        }

        // Magnification of the sight currently looked through (1 for red dots, iron sights, the canted dot).
        private float ActiveMagnification()
        {
            var weapon = _shooter.GetPrimaryWeapon();
            var scope = weapon != null && weapon.Attachments != null ? weapon.Attachments.ActiveSight as ScopeAttachment : null;
            if (scope == null || scope.ActiveScopeIndex != 0) return 1f;
            return Mathf.Max(1f, scope.magnification);
        }
    }
}
