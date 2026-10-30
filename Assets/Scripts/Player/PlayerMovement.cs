using KINEMATION.TacticalShooterPack.Scripts;
using KINEMATION.TacticalShooterPack.Scripts.Animation;
using UnityEngine;

namespace GameJam.Player
{
    // Runs before TacticalShooterPlayer so the yaw it reads has already been turned into body rotation.
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 1.8f;
        [SerializeField] private float sprintSpeed = 3.6f;
        [SerializeField] private float acceleration = 7f;
        [SerializeField] private float gravity = -20f;

        [Header("Leg Animation")]
        [SerializeField] private string legLayerName = "LowerBody";
        [Tooltip("Ground speed the leg clip was authored at (m/s). Walking ≈ 1.67.")]
        [SerializeField] private float legClipSpeed = 1.67f;
        [Tooltip("Upper limit for the leg playback rate, so sprinting doesn't turn into a sped-up walk.")]
        [SerializeField] private float maxLegPlaybackSpeed = 1.5f;
        [SerializeField] private float legBlendSpeed = 6f;
        [Tooltip("How far the legs turn toward the movement direction when strafing (degrees).")]
        [SerializeField] private float maxLegYaw = 60f;
        [SerializeField] private float legYawSpeed = 8f;
        [Tooltip("Foot bone height when the foot is flat on the ground.")]
        [SerializeField] private float ankleHeight = 0.09f;

        private static readonly int LegSpeedHash = Animator.StringToHash("LegSpeed");

        private CharacterController _controller;
        private TacticalProceduralAnimation _proceduralAnimation;
        private Animator _animator;
        private Transform _thighL;
        private Transform _thighR;
        private Transform _pelvis;
        private Transform _spine;
        private Transform _footL;
        private Transform _footR;

        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _legWeight;
        private float _legYaw;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _proceduralAnimation = GetComponent<TacticalProceduralAnimation>();
            _animator = GetComponentInChildren<Animator>();
            _pelvis = _animator.transform.Find("root/pelvis");
            _spine = _pelvis.Find("spine_01");
            _thighL = _pelvis.Find("thigh_l");
            _thighR = _pelvis.Find("thigh_r");
            _footL = _thighL.Find("calf_l/foot_l");
            _footR = _thighR.Find("calf_r/foot_r");
        }

        private void Update()
        {
            UpdateBodyYaw();
            UpdateMovement();
            UpdateLegAnimation();
        }

        private void LateUpdate()
        {
            ApplyLegYaw();
            GroundPelvis();
        }

        // The walk clip keeps the pelvis at a fixed height, so the lower foot floats mid-stride (looks like
        // hopping) or sinks into the floor. Move the pelvis until the lower foot sits on the ground, then
        // move the spine back by the same amount so the upper body (and the head-mounted camera) stays put.
        private void GroundPelvis()
        {
            if (_legWeight < 0.01f || _spine == null) return;

            float lowest = Mathf.Min(_footL.position.y, _footR.position.y) - transform.position.y;
            float drop = (lowest - ankleHeight) * _legWeight;
            if (Mathf.Abs(drop) < 0.0001f) return;

            Vector3 offset = transform.up * drop;
            _pelvis.position -= offset;
            _spine.position += offset;
        }

        // Plays the walk clip on a legs-only layer, faded in by how fast the character is moving.
        private void UpdateLegAnimation()
        {
            // The controller is swapped on every weapon draw, so the layer index is looked up each frame.
            int layer = _animator.GetLayerIndex(legLayerName);
            if (layer < 0) return;

            float planarSpeed = _horizontalVelocity.magnitude;
            float target = Mathf.Clamp01(planarSpeed / Mathf.Max(walkSpeed * 0.5f, 0.01f));
            _legWeight = Mathf.Lerp(_legWeight, target, 1f - Mathf.Exp(-legBlendSpeed * Time.deltaTime));
            _animator.SetLayerWeight(layer, _legWeight);

            // Walking backwards plays the clip in reverse.
            float forward = Vector3.Dot(_horizontalVelocity, transform.forward);
            float direction = forward < -0.1f ? -1f : 1f;
            float playback = Mathf.Min(planarSpeed / legClipSpeed, maxLegPlaybackSpeed);
            _animator.SetFloat(LegSpeedHash, direction * playback);

            // Turn the legs toward the strafe direction (mirrored when backpedalling).
            float targetYaw = 0f;
            if (planarSpeed > 0.2f)
            {
                Vector3 local = transform.InverseTransformDirection(_horizontalVelocity);
                if (local.z < -0.1f) local = -local;
                targetYaw = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(local.z, 0f)) * Mathf.Rad2Deg,
                    -maxLegYaw, maxLegYaw);
            }
            _legYaw = Mathf.Lerp(_legYaw, targetYaw, 1f - Mathf.Exp(-legYawSpeed * Time.deltaTime));
        }

        private void ApplyLegYaw()
        {
            float yaw = _legYaw * _legWeight;
            if (Mathf.Abs(yaw) < 0.01f || _thighL == null || _thighR == null) return;

            Quaternion turn = Quaternion.AngleAxis(yaw, transform.up);
            _thighL.rotation = turn * _thighL.rotation;
            _thighR.rotation = turn * _thighR.rotation;
        }

        // The pack only offsets the upper body by yawInput (clamped to ±90°).
        // Moving it into the root rotation lets the mouse turn the whole character freely.
        private void UpdateBodyYaw()
        {
            float yaw = _proceduralAnimation.yawInput;
            if (Mathf.Approximately(yaw, 0f)) return;

            transform.Rotate(0f, yaw, 0f, Space.World);
            _proceduralAnimation.yawInput = 0f;
        }

        private void UpdateMovement()
        {
            Vector2 input = Vector2.ClampMagnitude(_proceduralAnimation.moveInput, 1f);

            // Gait is driven by TacticalShooterPlayer: 1 = walk, 2 = sprint.
            float gait = _animator.GetFloat(TacShooterUtility.Animator_Gait.hash);
            float speed = gait > 1f ? Mathf.Lerp(walkSpeed, sprintSpeed, gait - 1f) : walkSpeed;

            Vector3 targetVelocity = (transform.right * input.x + transform.forward * input.y) * speed;
            float alpha = 1f - Mathf.Exp(-acceleration * Time.deltaTime);
            _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, targetVelocity, alpha);

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }
    }
}
