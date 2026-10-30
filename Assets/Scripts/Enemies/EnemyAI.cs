using System.Collections;
using GameJam.Combat;
using GameJam.Weapons;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameJam.Enemies
{
    public enum EnemyWeaponClass
    {
        Rifle,
        Shotgun,
        Pistol
    }

    public enum EnemyBehavior
    {
        // Holds a spot (room, stairs) and scans left and right.
        Guard,
        // Walks a loop of patrol points.
        Patrol
    }

    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int WalkDirHash = Animator.StringToHash("WalkDir");
        private static readonly int FireHash = Animator.StringToHash("Fire");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");
        private static readonly int ReloadLoopHash = Animator.StringToHash("ReloadLoop");

        [Header("Weapon")]
        [SerializeField] private TacticalShooterWeapon weapon;
        [SerializeField] private EnemyWeaponClass weaponClass;
        [SerializeField] private float damage = 8f;
        [SerializeField] private int pellets = 1;
        [SerializeField] private float spreadAngle = 2.5f;
        [SerializeField] private float fireInterval = 0.12f;
        [SerializeField] private Vector2Int burstSize = new Vector2Int(3, 6);
        [SerializeField] private Vector2 burstPause = new Vector2(0.6f, 1.2f);
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private float range = 60f;

        [Header("Reload")]
        [Tooltip("Rifle / pistol: length of the reload animation.")]
        [SerializeField] private float reloadTime = 3.3f;
        [Tooltip("Shotgun: start, per shell and end timings of the reload animations.")]
        [SerializeField] private Vector3 shotgunReloadTimes = new Vector3(0.67f, 0.92f, 0.85f);

        [Header("Behavior")]
        [SerializeField] private EnemyBehavior behavior = EnemyBehavior.Guard;
        [Tooltip("Guard: how far the enemy looks left/right of its starting direction (degrees).")]
        [SerializeField] private float guardScanAngle = 45f;
        [SerializeField] private float guardScanPeriod = 6f;
        [Tooltip("Patrol: points walked in order, looping back to the first.")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float patrolWaitTime = 1.5f;

        [Header("Senses & Movement")]
        [SerializeField] private float detectionRange = 35f;
        [Tooltip("Field of view used to spot the player before the enemy is alerted (degrees).")]
        [SerializeField] private float fieldOfView = 120f;
        [SerializeField] private float attackRange = 25f;
        [Tooltip("Seconds without seeing the player before going back to guarding / patrolling.")]
        [SerializeField] private float loseTargetTime = 6f;
        [SerializeField] private float moveSpeed = 1.4f;
        [Tooltip("Ground speed of the Enemy_Walk clip (SKM_Operator@Walking), m/s.")]
        [SerializeField] private float walkClipSpeed = 1.67f;
        [SerializeField] private float turnSpeed = 6f;
        [SerializeField] private float reactionTime = 0.6f;
        [SerializeField] private float gravity = -20f;

        [Header("Death")]
        [SerializeField] private float corpseLifetime = 20f;
        [Tooltip("Seconds into the death animation before the hands let go of the gun.")]
        [SerializeField] private float weaponDropDelay = 0.3f;

        public const string DeathState1 = "Death_1";
        public const string DeathState2 = "Death_2";
        public const string HeadshotDeathState = "Death_Headshot";

        private CharacterController _controller;
        private Health _health;
        private Animator _animator;
        private AudioSource _audioSource;
        private EnemyAnimatorIK _ik;

        private TacticalShooterPlayer _target;
        private Health _targetHealth;
        private Transform _targetCamera;

        private int _ammo;
        private bool _isReloading;
        private bool _isAware;
        private float _lastSeenTime;
        private Vector3 _lastKnownPosition;
        private float _nextShotTime;
        private int _shotsLeftInBurst;
        private float _verticalVelocity;
        private Vector3 _velocity;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private int _patrolIndex;
        private float _waitUntil;
        private bool _lastHitWasHeadshot;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public bool IsDead => _health.IsDead;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            _animator = GetComponentInChildren<Animator>();
            _audioSource = GetComponent<AudioSource>();
            _ik = GetComponentInChildren<EnemyAnimatorIK>();

            _health.Died += OnDied;
            _health.Damaged += OnDamaged;
        }

        private void Start()
        {
            _target = FindFirstObjectByType<TacticalShooterPlayer>();
            if (_target != null)
            {
                _targetHealth = _target.GetComponent<Health>();
                var cam = _target.GetComponentInChildren<Camera>();
                _targetCamera = cam != null ? cam.transform : _target.transform;
            }

            if (weapon != null) weapon.InitializeAsProp(Camera.main);
            _ammo = magazineSize;

            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _patrolIndex = ClosestPatrolPoint();
        }

        private void OnDestroy()
        {
            _health.Died -= OnDied;
            _health.Damaged -= OnDamaged;
        }

        private Vector3 AimPoint => _targetCamera.position + Vector3.down * 0.35f;

        private Vector3 EyePosition
        {
            get
            {
                Transform head = _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
                return head != null ? head.position : transform.position + Vector3.up * 1.6f;
            }
        }

        private bool TargetAlive => _target != null && (_targetHealth == null || !_targetHealth.IsDead);

        private void Update()
        {
            if (IsDead) return;

            bool canSee = false;
            float distance = float.MaxValue;

            if (TargetAlive)
            {
                distance = Vector3.Distance(transform.position, _target.transform.position);
                canSee = distance <= detectionRange && (_isAware || InFieldOfView()) && HasLineOfSight();
                if (canSee)
                {
                    if (!_isAware) BecomeAware();
                    _lastSeenTime = Time.time;
                    _lastKnownPosition = AimPoint;
                }
                else if (_isAware && Time.time - _lastSeenTime > loseTargetTime)
                {
                    _isAware = false;
                }
            }
            else
            {
                _isAware = false;
            }

            UpdateMovement();
            UpdateFiring(distance, canSee);

            if (_ik != null)
            {
                _ik.isAiming = _isAware && TargetAlive;
                if (_ik.isAiming) _ik.aimTarget = canSee ? AimPoint : _lastKnownPosition;
                _ik.handIKWeight = _isReloading ? 0f : 1f;
            }
        }

        private void BecomeAware()
        {
            _isAware = true;
            _lastSeenTime = Time.time;
            _lastKnownPosition = AimPoint;
            _nextShotTime = Time.time + reactionTime;
        }

        private void OnDamaged(float amount, RaycastHit hit, GameObject instigator)
        {
            // Remembered so the killing blow can pick the headshot death.
            _lastHitWasHeadshot = hit.collider != null && hit.collider.GetComponent<HitZone>() != null;

            // Getting shot reveals the shooter even from behind.
            if (!_isAware && TargetAlive) BecomeAware();
        }

        private bool InFieldOfView()
        {
            Vector3 toTarget = AimPoint - EyePosition;
            toTarget.y = 0f;
            return Vector3.Angle(transform.forward, toTarget) <= fieldOfView * 0.5f;
        }

        // Alerted enemies hold their ground and turn to shoot; otherwise they guard or patrol.
        private void UpdateMovement()
        {
            Vector3 desired = Vector3.zero;
            Quaternion? face = null;

            if (_isAware)
            {
                Vector3 toTarget = _lastKnownPosition - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f) face = Quaternion.LookRotation(toTarget);
            }
            else if (behavior == EnemyBehavior.Patrol && patrolPoints != null && patrolPoints.Length > 0)
            {
                desired = UpdatePatrol(out face);
            }
            else
            {
                desired = UpdateGuard(out face);
            }

            if (face.HasValue)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, face.Value,
                    1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            Vector3 targetVelocity = desired * moveSpeed;
            _velocity = Vector3.Lerp(_velocity, targetVelocity, 1f - Mathf.Exp(-8f * Time.deltaTime));

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;
            _controller.Move((_velocity + Vector3.up * _verticalVelocity) * Time.deltaTime);

            UpdateWalkAnimation();
        }

        // Walks back to the guard spot if pushed off it, then sweeps left and right around the home direction.
        private Vector3 UpdateGuard(out Quaternion? face)
        {
            Vector3 toHome = _homePosition - transform.position;
            toHome.y = 0f;
            if (toHome.magnitude > 0.5f)
            {
                face = Quaternion.LookRotation(toHome);
                return toHome.normalized;
            }

            float sweep = Mathf.Sin(Time.time * 2f * Mathf.PI / guardScanPeriod) * guardScanAngle;
            face = _homeRotation * Quaternion.Euler(0f, sweep, 0f);
            return Vector3.zero;
        }

        private Vector3 UpdatePatrol(out Quaternion? face)
        {
            face = null;
            if (Time.time < _waitUntil) return Vector3.zero;

            Transform point = patrolPoints[_patrolIndex];
            if (point == null)
            {
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                return Vector3.zero;
            }

            Vector3 toPoint = point.position - transform.position;
            toPoint.y = 0f;
            if (toPoint.magnitude < 0.4f)
            {
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                _waitUntil = Time.time + patrolWaitTime;
                return Vector3.zero;
            }

            face = Quaternion.LookRotation(toPoint);
            // Only walk once roughly facing the next point, so corners are turned on the spot.
            float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, toPoint.normalized));
            return transform.forward * alignment * alignment;
        }

        private int ClosestPatrolPoint()
        {
            if (patrolPoints == null) return 0;

            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;
                float d = (patrolPoints[i].position - transform.position).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = i;
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 left = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f) * transform.forward;
            Gizmos.DrawLine(eye, eye + left * Mathf.Min(detectionRange, 10f));
            Gizmos.DrawLine(eye, eye + right * Mathf.Min(detectionRange, 10f));

            if (behavior != EnemyBehavior.Patrol || patrolPoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Transform a = patrolPoints[i];
                Transform b = patrolPoints[(i + 1) % patrolPoints.Length];
                if (a == null || b == null) continue;
                Gizmos.DrawSphere(a.position, 0.15f);
                Gizmos.DrawLine(a.position, b.position);
            }
        }

        // Drives the SKM_Operator Walking cycle: full stride as soon as the enemy moves, played back at a
        // rate matching the ground speed, reversed when backing off, and legs turned toward a sidestep.
        private void UpdateWalkAnimation()
        {
            float speed = _velocity.magnitude;
            Vector3 local = transform.InverseTransformDirection(_velocity);
            bool backwards = local.z < -0.1f;

            _animator.SetFloat(SpeedHash, Mathf.Clamp01(speed / 0.4f), 0.15f, Time.deltaTime);

            float playback = Mathf.Clamp(speed / walkClipSpeed, 0.5f, 1.5f);
            _animator.SetFloat(WalkDirHash, backwards ? -playback : playback);

            if (_ik == null) return;
            float legYaw = 0f;
            if (speed > 0.2f)
            {
                if (backwards) local = -local;
                legYaw = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(local.z, 0f)) * Mathf.Rad2Deg, -60f, 60f);
            }
            _ik.legYaw = Mathf.Lerp(_ik.legYaw, legYaw, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        private void UpdateFiring(float distance, bool canSee)
        {
            if (!_isAware || !canSee || _isReloading || distance > attackRange) return;
            if (Time.time < _nextShotTime) return;

            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0f;
            if (Vector3.Angle(transform.forward, toTarget) > 20f) return;
            // Standing still while shooting: wait until the walk has come to a stop.
            if (_velocity.magnitude > 0.3f) return;

            if (_shotsLeftInBurst <= 0) _shotsLeftInBurst = Random.Range(burstSize.x, burstSize.y + 1);

            Fire();
            _shotsLeftInBurst--;

            _nextShotTime = Time.time + (_shotsLeftInBurst > 0
                ? fireInterval
                : Random.Range(burstPause.x, burstPause.y));

            if (_ammo <= 0) StartCoroutine(Reload());
        }

        private void Fire()
        {
            _ammo--;

            if (weapon != null)
            {
                weapon.PlayMuzzleFlash();
                PlayFireSound();
            }

            _animator.SetTrigger(FireHash);

            Vector3 origin = weapon != null ? weapon.GetMuzzlePoint().position : EyePosition;
            Vector3 aimDirection = (AimPoint - origin).normalized;

            for (int i = 0; i < pellets; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(Random.Range(0f, 360f), aimDirection)
                                    * Quaternion.AngleAxis(Random.Range(0f, spreadAngle), Vector3.Cross(aimDirection, Vector3.up).normalized)
                                    * aimDirection;

                if (Raycast(origin, direction, range, out RaycastHit hit))
                {
                    Debug.DrawLine(origin, hit.point, Color.magenta, 0.5f);
                    hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(damage, hit, gameObject);
                }
                else
                {
                    Debug.DrawRay(origin, direction * range, Color.gray, 0.5f);
                }
            }
        }

        private void PlayFireSound()
        {
            if (_audioSource == null) return;

            var settings = weapon.tacWeaponSettings;
            var sounds = weapon.IsSuppressed ? settings.suppressedFireSounds : settings.fireSounds;
            if (sounds == null || sounds.Count == 0) return;

            _audioSource.pitch = Random.Range(settings.firePitchRange.x, settings.firePitchRange.y);
            _audioSource.PlayOneShot(sounds[Random.Range(0, sounds.Count)],
                Random.Range(settings.fireVolumeRange.x, settings.fireVolumeRange.y));
        }

        private IEnumerator Reload()
        {
            _isReloading = true;
            _shotsLeftInBurst = 0;
            _animator.SetTrigger(ReloadHash);

            if (_audioSource != null && weapon != null && weapon.tacWeaponSettings.reloadTacSound != null)
            {
                _audioSource.pitch = 1f;
                _audioSource.PlayOneShot(weapon.tacWeaponSettings.reloadTacSound);
            }

            if (weaponClass == EnemyWeaponClass.Shotgun)
            {
                _animator.SetBool(ReloadLoopHash, true);
                yield return new WaitForSeconds(shotgunReloadTimes.x);
                while (_ammo < magazineSize)
                {
                    yield return new WaitForSeconds(shotgunReloadTimes.y);
                    _ammo++;
                }
                _animator.SetBool(ReloadLoopHash, false);
                yield return new WaitForSeconds(shotgunReloadTimes.z);
            }
            else
            {
                yield return new WaitForSeconds(reloadTime);
                _ammo = magazineSize;
            }

            _isReloading = false;
        }

        private bool HasLineOfSight()
        {
            Vector3 eye = EyePosition;
            Vector3 toTarget = AimPoint - eye;
            if (!Raycast(eye, toTarget.normalized, toTarget.magnitude + 0.5f, out RaycastHit hit)) return false;
            return hit.collider.GetComponentInParent<TacticalShooterPlayer>() == _target;
        }

        // Nearest hit that isn't an enemy (no friendly fire, and teammates don't block line of sight).
        private bool Raycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, ~0,
                QueryTriggerInteraction.Ignore);

            closest = default;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.GetComponentInParent<EnemyAI>() != null) continue;
                if (_hits[i].distance >= best) continue;
                best = _hits[i].distance;
                closest = _hits[i];
            }

            return best < float.MaxValue;
        }

        private void OnDied(GameObject killer)
        {
            StopAllCoroutines();
            _controller.enabled = false;
            _velocity = Vector3.zero;
            foreach (var zone in GetComponentsInChildren<HitZone>()) zone.GetComponent<Collider>().enabled = false;

            if (_ik != null)
            {
                // Hands let go of the grips; the gun rides the right hand until it's dropped.
                _ik.isAiming = false;
                _ik.handIKWeight = 0f;
                _ik.legYaw = 0f;
            }

            string state = _lastHitWasHeadshot
                ? HeadshotDeathState
                : (Random.value < 0.5f ? DeathState1 : DeathState2);

            _animator.SetFloat(SpeedHash, 0f);
            _animator.SetFloat(WalkDirHash, 1f);
            _animator.CrossFadeInFixedTime(state, 0.15f, 0);
            StartCoroutine(FadeOutUpperBody(0.2f));
            StartCoroutine(DropWeaponAfter(_lastHitWasHeadshot ? weaponDropDelay * 0.5f : weaponDropDelay));

            Destroy(gameObject, corpseLifetime);
        }

        // The upper-body layer (weapon hold / fire / reload) would otherwise keep the arms up while dying.
        private IEnumerator FadeOutUpperBody(float duration)
        {
            int layer = _animator.GetLayerIndex("UpperBody");
            if (layer < 0) yield break;

            float start = _animator.GetLayerWeight(layer);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                _animator.SetLayerWeight(layer, Mathf.Lerp(start, 0f, t / duration));
                yield return null;
            }
            _animator.SetLayerWeight(layer, 0f);
        }

        // Keeps the gun in the hand for a moment as the body starts to fall, then lets it go with the
        // hand's momentum plus a little spin, so it tumbles to the floor instead of popping out.
        private IEnumerator DropWeaponAfter(float delay)
        {
            if (weapon == null) yield break;
            Transform root = weapon.GetWeaponRoot();

            yield return new WaitForSeconds(delay);

            Vector3 previous = root.position;
            yield return null;
            Vector3 handVelocity = (root.position - previous) / Mathf.Max(Time.deltaTime, 0.001f);

            DropWeapon(Vector3.ClampMagnitude(handVelocity, 4f));
        }

        private void DropWeapon(Vector3 velocity)
        {
            if (weapon == null) return;

            Transform root = weapon.GetWeaponRoot();
            root.SetParent(null, true);

            // Size the collider from the weapon meshes only: particle renderers (muzzle flash, casings)
            // report huge bounds and turned the dropped gun into a 40 m box that launched everyone nearby.
            bool hasBounds = false;
            Bounds bounds = default;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!hasBounds) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                hasBounds = true;
            }

            var box = root.gameObject.AddComponent<BoxCollider>();
            if (hasBounds)
            {
                box.center = root.InverseTransformPoint(bounds.center);
                Vector3 size = root.InverseTransformVector(bounds.size);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
            else
            {
                box.size = new Vector3(0.1f, 0.2f, 0.8f);
            }

            var body = root.gameObject.AddComponent<Rigidbody>();
            body.mass = 3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity + transform.forward * 0.6f;
            body.angularVelocity = new Vector3(Random.Range(-3f, 3f), Random.Range(-2f, 2f), Random.Range(-4f, 4f));
            Destroy(root.gameObject, corpseLifetime);
        }
    }
}
