using KINEMATION.TacticalShooterPack.Scripts.Player;
using UnityEngine;

namespace GameJam.Player
{
    // Player footstep sounds, replacing the pack's (which played every gait at full volume and couldn't tell a
    // slow walk from a walk). Volume and step spacing follow how the player moves: soft and sparse with Ctrl,
    // a bit quieter than before when walking, loud and quick when sprinting. Uses the pack's own clips.
    public class PlayerFootsteps : MonoBehaviour
    {
        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float slowWalkVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float walkVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float sprintVolume = 1f;

        [Header("Distance between steps (m)")]
        [SerializeField] private float slowWalkStride = 0.6f;
        [SerializeField] private float walkStride = 0.8f;
        [SerializeField] private float sprintStride = 1.3f;

        private PlayerMovement _movement;
        private TacticalShooterPlayer _shooter;
        private AudioSource _source;
        private float _distanceToStep;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _shooter = GetComponent<TacticalShooterPlayer>();
            _source = GetComponent<AudioSource>();
            if (_shooter != null) _shooter.builtInFootsteps = false;
        }

        private void OnDestroy()
        {
            if (_shooter != null) _shooter.builtInFootsteps = true;
        }

        private void Update()
        {
            if (_movement == null || !_movement.enabled || _source == null) return;

            var gait = _movement.Gait;
            if (gait == MovementGait.Still || !_movement.IsGrounded)
            {
                // The first step comes soon after starting to move, not a full stride later.
                _distanceToStep = Mathf.Min(_distanceToStep, 0.25f);
                return;
            }

            _distanceToStep -= _movement.PlanarSpeed * Time.deltaTime;
            if (_distanceToStep > 0f) return;

            bool sprinting = gait == MovementGait.Sprint;
            _distanceToStep = gait == MovementGait.SlowWalk ? slowWalkStride : sprinting ? sprintStride : walkStride;
            float volume = gait == MovementGait.SlowWalk ? slowWalkVolume : sprinting ? sprintVolume : walkVolume;

            var clips = sprinting ? _shooter.SprintSounds : _shooter.WalkSounds;
            if (clips == null || clips.Count == 0) return;
            _source.PlayOneShot(clips[Random.Range(0, clips.Count)], volume);
        }
    }
}
