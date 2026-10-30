using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;

namespace GameJam.Enemies
{
    // Lives on the Animator object (OnAnimatorIK is only sent there).
    // The weapon is held at the shoulder with its sight in front of the eye and pointed at the target;
    // both arms are then solved onto the grips measured from the player's KINEMATION weapon poses.
    // While reloading the IK fades out and the gun rides the animated right hand instead.
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimatorIK : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        [HideInInspector] public Vector3 aimTarget;
        [HideInInspector] public bool isAiming;
        [HideInInspector] public float handIKWeight = 1f;
        [HideInInspector] public float legYaw;

        [Header("Weapon")]
        [SerializeField] private Transform weaponRoot;
        [Tooltip("Right hand bone pose in weapon space (this character's bone axes).")]
        [SerializeField] private Vector3 rightGripPosition;
        [SerializeField] private Quaternion rightGripRotation = Quaternion.identity;
        [Tooltip("Left hand bone pose in weapon space (this character's bone axes).")]
        [SerializeField] private Vector3 leftGripPosition;
        [SerializeField] private Quaternion leftGripRotation = Quaternion.identity;

        [Header("Holding")]
        [Tooltip("Body-space offset from the head bone to the aiming eye.")]
        [SerializeField] private Vector3 eyeOffset = new Vector3(0.035f, 0.09f, 0.08f);
        [Tooltip("How far in front of the eye the sight sits when aiming (rifles ~0.1, pistols ~0.4).")]
        [SerializeField] private float sightDistance = 0.1f;
        [Tooltip("Body-space offset of the sight from the eye when not aiming (low ready).")]
        [SerializeField] private Vector3 lowReadyOffset = new Vector3(0f, -0.3f, 0.2f);
        [SerializeField] private float lowReadyAngle = 30f;
        [SerializeField] private float aimSpeed = 10f;
        [SerializeField, Range(0f, 1f)] private float headLookWeight = 0.8f;

        [Header("Walking")]
        [Tooltip("Foot bone height when the foot is flat on the ground.")]
        [SerializeField] private float ankleHeight = 0.095f;
        [Tooltip("How much of the walk's up/down hip motion is kept (0 = flat, 1 = full step bounce).")]
        [SerializeField, Range(0f, 1f)] private float bobAmount = 0.3f;

        private Animator _animator;
        private TacticalShooterWeapon _weapon;
        private Quaternion _aimRotation = Quaternion.identity;
        private float _aimBlend;
        private float _ikWeight;
        private float _averageDrop;
        private float _hipDrop;

        public Transform WeaponRoot => weaponRoot;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _aimRotation = transform.rotation;
            if (weaponRoot != null) _weapon = weaponRoot.GetComponentInChildren<TacticalShooterWeapon>(true);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            _animator.SetLookAtWeight(isAiming ? 1f : 0f, 0f, headLookWeight, 0f, 0.5f);
            _animator.SetLookAtPosition(aimTarget);
        }

        private void LateUpdate()
        {
            if (!_animator.enabled) return;

            ApplyLegYaw();
            GroundHips();

            if (weaponRoot == null || weaponRoot.parent != transform) return;
            HoldWeapon();
        }

        private void HoldWeapon()
        {
            float dt = Time.deltaTime;
            // The model sits at identity under the enemy root (transform.root may be a scene folder).
            Transform body = transform;
            Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);
            Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);

            _aimBlend = Mathf.MoveTowards(_aimBlend, isAiming ? 1f : 0f, 3f * dt);
            _ikWeight = Mathf.MoveTowards(_ikWeight, handIKWeight, 4f * dt);

            // Aim direction: at the target when alerted, otherwise pointed down in front of the body.
            Quaternion lowReady = Quaternion.AngleAxis(lowReadyAngle, body.right) * body.rotation;
            Quaternion desired = lowReady;
            if (isAiming)
            {
                Vector3 dir = aimTarget - head.position;
                if (dir.sqrMagnitude > 0.01f) desired = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            _aimRotation = Quaternion.Slerp(_aimRotation, desired, 1f - Mathf.Exp(-aimSpeed * dt));

            // Pose 1: shouldered, sight in front of the eye.
            Vector3 eye = head.position + body.rotation * eyeOffset;
            Vector3 sight = Vector3.Lerp(eye + body.rotation * lowReadyOffset,
                eye + _aimRotation * Vector3.forward * sightDistance, _aimBlend);
            Vector3 aimLocal = _weapon != null && _weapon.GetAimPoint() != null
                ? weaponRoot.InverseTransformPoint(_weapon.GetAimPoint().position)
                : Vector3.zero;
            Vector3 heldPosition = sight - _aimRotation * aimLocal;

            // Pose 2: riding the animated right hand (used while reloading).
            Quaternion handRotation = rightHand.rotation * Quaternion.Inverse(rightGripRotation);
            Vector3 handPosition = rightHand.position - handRotation * rightGripPosition;

            Quaternion rotation = Quaternion.Slerp(handRotation, _aimRotation, _ikWeight);
            Vector3 position = Vector3.Lerp(handPosition, heldPosition, _ikWeight);
            weaponRoot.SetPositionAndRotation(position, rotation);

            if (_ikWeight <= 0.001f) return;

            SolveArm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                weaponRoot.TransformPoint(rightGripPosition), weaponRoot.rotation * rightGripRotation,
                -body.up + body.right * 0.8f, _ikWeight);
            SolveArm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                weaponRoot.TransformPoint(leftGripPosition), weaponRoot.rotation * leftGripRotation,
                -body.up - body.right * 0.3f, _ikWeight);
        }

        // Analytic two-bone IK. The elbow bends toward `pole`; the hand takes `targetRotation`.
        private void SolveArm(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone,
            Vector3 targetPosition, Quaternion targetRotation, Vector3 pole, float weight)
        {
            Transform upper = _animator.GetBoneTransform(upperBone);
            Transform lower = _animator.GetBoneTransform(lowerBone);
            Transform hand = _animator.GetBoneTransform(handBone);

            Vector3 a = upper.position;
            Vector3 target = Vector3.Lerp(hand.position, targetPosition, weight);
            float upperLength = Vector3.Distance(a, lower.position);
            float lowerLength = Vector3.Distance(lower.position, hand.position);

            Vector3 toTarget = target - a;
            float reach = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
                upperLength + lowerLength - 0.001f);
            Vector3 dir = toTarget.normalized;

            Vector3 bend = pole - dir * Vector3.Dot(pole, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.Cross(dir, transform.right);
            bend.Normalize();

            float cos = Mathf.Clamp((upperLength * upperLength + reach * reach - lowerLength * lowerLength)
                                    / (2f * upperLength * reach), -1f, 1f);
            Vector3 elbow = a + upperLength * (dir * cos + bend * Mathf.Sqrt(1f - cos * cos));

            upper.rotation = Quaternion.FromToRotation(lower.position - a, elbow - a) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, a + dir * reach - lower.position)
                             * lower.rotation;
            hand.rotation = Quaternion.Slerp(hand.rotation, targetRotation, weight);
        }

        // The walk cycle keeps the hips at standing height, so mid-stride both feet float. Lowering the
        // hips by the exact amount every frame plants the feet but makes a 10 cm saw-tooth bounce, so only
        // the average drop is applied in full and a fraction of the rest, smoothed into a gentle bob.
        private void GroundHips()
        {
            float weight = Mathf.Clamp01(_animator.GetFloat(SpeedHash));
            float lowest = Mathf.Min(_animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,
                _animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y) - transform.position.y;
            float raw = Mathf.Max(0f, lowest - ankleHeight) * weight;

            float dt = Time.deltaTime;
            _averageDrop = Mathf.Lerp(_averageDrop, raw, 1f - Mathf.Exp(-dt / 0.8f));
            float target = _averageDrop + (raw - _averageDrop) * bobAmount;
            _hipDrop = Mathf.Lerp(_hipDrop, target, 1f - Mathf.Exp(-12f * dt));

            if (_hipDrop > 0.0001f)
            {
                _animator.GetBoneTransform(HumanBodyBones.Hips).position -= transform.up * _hipDrop;
            }
        }

        // Turns both thighs toward the movement direction so sidesteps don't look like forward walking.
        private void ApplyLegYaw()
        {
            if (Mathf.Abs(legYaw) < 0.01f) return;

            Quaternion turn = Quaternion.AngleAxis(legYaw, transform.up);
            Transform left = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform right = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            left.rotation = turn * left.rotation;
            right.rotation = turn * right.rotation;
        }
    }
}
