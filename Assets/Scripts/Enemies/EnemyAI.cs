using System.Collections;
using System.Collections.Generic;
using GameJam.Combat;
using GameJam.Weapons;
using GameJam.World;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;
using UnityEngine.AI;
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

    // Suspicion (S) drives the state: Patrol below 50, Suspicious from 50, Combat from 100 (Combat never ends).
    public enum AlertState
    {
        Patrol,
        Suspicious,
        Combat
    }

    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int WalkDirHash = Animator.StringToHash("WalkDir");
        private static readonly int FireHash = Animator.StringToHash("Fire");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");
        private static readonly int ReloadLoopHash = Animator.StringToHash("ReloadLoop");

        public static readonly List<EnemyAI> All = new List<EnemyAI>();

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
        [SerializeField] private float attackRange = 25f;

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

        [Header("Suspicion")]
        [SerializeField] private float suspiciousThreshold = 50f;
        [SerializeField] private float combatThreshold = 100f;
        [Tooltip("Seconds without a new sound (or sighting) before suspicion starts to drop.")]
        [SerializeField] private float decayDelay = 10f;
        [Tooltip("Suspicion lost per second once decaying. Combat never decays.")]
        [SerializeField] private float decayRate = 10f;

        [Header("Vision")]
        [Tooltip("Full width of the central cone (degrees). Seeing the player here raises suspicion fast.")]
        [SerializeField] private float centralVisionAngle = 60f;
        [Tooltip("Extra width on each side of the central cone (degrees). Only a moving player is noticed here.")]
        [SerializeField] private float peripheralVisionAngle = 20f;
        [SerializeField] private float centralSuspicionPerSecond = 50f;
        [SerializeField] private float peripheralSuspicionPerSecond = 15f;
        [Tooltip("Sight isn't limited by distance in the design; this just caps the raycast.")]
        [SerializeField] private float sightRange = 100f;
        [Tooltip("Player speed (m/s) that counts as moving for peripheral vision.")]
        [SerializeField] private float movingSpeedThreshold = 0.2f;

        [Header("Reaction")]
        [Tooltip("Delay from first spotting the player to the first shot, by the state the enemy was in.")]
        [SerializeField] private float patrolReactionDelay = 4f;
        [SerializeField] private float suspiciousReactionDelay = 3f;
        [SerializeField] private float combatReactionDelay = 2f;
        [Tooltip("Losing sight for shorter than this doesn't restart the reaction delay.")]
        [SerializeField] private float sightGraceTime = 0.5f;
        [Tooltip("Accuracy lost while still in Patrol (0.3 = spread ×1.3).")]
        [SerializeField, Range(0f, 1f)] private float patrolAccuracyPenalty = 0.3f;
        [Tooltip("Accuracy gained in Combat (0.3 = spread ×0.7).")]
        [SerializeField, Range(0f, 1f)] private float combatAccuracyBonus = 0.3f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1.4f;
        [Tooltip("Suspicious: slow walk toward the noise.")]
        [SerializeField] private float investigateSpeed = 0.9f;
        [Tooltip("Suspicious: time spent looking toward the noise before walking over.")]
        [SerializeField] private float investigateLookTime = 1.5f;
        [Tooltip("Combat: running speed while hunting the player.")]
        [SerializeField] private float runSpeed = 3.5f;
        [Tooltip("Combat: how far around the last known position to search once there.")]
        [SerializeField] private float searchRadius = 8f;
        [Tooltip("Ground speed of the Enemy_Walk clip (SKM_Operator@Walking), m/s.")]
        [SerializeField] private float walkClipSpeed = 1.67f;
        [Tooltip("Upper limit for the walk clip playback rate (running reuses the walk cycle).")]
        [SerializeField] private float maxWalkPlayback = 2.2f;
        [SerializeField] private float turnSpeed = 6f;
        [SerializeField] private float gravity = -20f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] walkStepSounds;
        [SerializeField] private AudioClip[] runStepSounds;
        [SerializeField, Range(0f, 1f)] private float walkStepVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float runStepVolume = 0.85f;
        [Tooltip("Footsteps fade out linearly to nothing at this distance (m).")]
        [SerializeField] private float stepAudibleDistance = 18f;
        [Tooltip("Other enemies within this (sound-travel) distance of a shot join the fight.")]
        [SerializeField] private float gunfireAlertRadius = 35f;

        [Header("Debug")]
        [Tooltip("Colored ball above the head: green Patrol, orange Suspicious, red Combat.")]
        [SerializeField] private bool showStateIndicator = true;

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
        private Vector3 _targetPreviousPosition;
        private float _targetSpeed;

        private float _suspicion;
        private bool _inCombat;
        private float _lastStimulusTime = float.NegativeInfinity;

        private bool _seesTarget;
        private float _sightStartTime;
        private float _sightLostTime = float.NegativeInfinity;
        private float _fireDelay;
        private Vector3 _lastKnownPosition;

        private bool _hasInvestigatePoint;
        private Vector3 _investigatePoint;
        private float _investigateStartTime;
        private bool _hasSearchPoint;
        private Vector3 _searchPoint;

        private readonly List<Vector3> _pathCorners = new List<Vector3>();
        private NavMeshPath _navPath;
        private Vector3 _pathDestination;
        private int _pathCorner;
        private float _nextRepathTime;
        private float _stuckTime;

        private AudioSource _stepSource;
        private float _stepTimer;
        private float _nextGunfireAlertTime;

        private int _ammo;
        private bool _isReloading;
        private float _nextShotTime;
        private int _shotsLeftInBurst;
        private float _verticalVelocity;
        private Vector3 _velocity;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private int _patrolIndex;
        private float _waitUntil;
        private float _scanBaseYaw;
        private bool _lastHitWasHeadshot;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public bool IsDead => _health.IsDead;
        public float Suspicion => _suspicion;
        public AlertState State => _inCombat ? AlertState.Combat
            : _suspicion >= suspiciousThreshold ? AlertState.Suspicious
            : AlertState.Patrol;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            _animator = GetComponentInChildren<Animator>();
            _audioSource = GetComponent<AudioSource>();
            _ik = GetComponentInChildren<EnemyAnimatorIK>();
            _navPath = new NavMeshPath();
            _stepSource = CreateStepSource();

            _health.Died += OnDied;
            _health.Damaged += OnDamaged;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start()
        {
            _target = FindFirstObjectByType<TacticalShooterPlayer>();
            if (_target != null)
            {
                _targetHealth = _target.GetComponent<Health>();
                var cam = _target.GetComponentInChildren<Camera>();
                _targetCamera = cam != null ? cam.transform : _target.transform;
                _targetPreviousPosition = _target.transform.position;
            }

            if (weapon != null) weapon.InitializeAsProp(Camera.main);
            _ammo = magazineSize;

            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _patrolIndex = ClosestPatrolPoint();

            if (showStateIndicator) gameObject.AddComponent<EnemyStateIndicator>();
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

            UpdatePerception();
            UpdateDecay();
            UpdateMovement();
            UpdateFiring();
            UpdateIK();
        }

        // ---------- Suspicion ----------

        private void AddSuspicion(float amount)
        {
            _suspicion = Mathf.Min(_suspicion + amount, combatThreshold);
            if (_suspicion >= combatThreshold) EnterCombat();
        }

        private void EnterCombat()
        {
            if (_inCombat) return;
            _inCombat = true;
            _suspicion = Mathf.Max(_suspicion, combatThreshold);
            _hasSearchPoint = false;
            if (!_seesTarget && _hasInvestigatePoint) _lastKnownPosition = _investigatePoint;
        }

        private void UpdateDecay()
        {
            if (_inCombat || _seesTarget) return;
            if (Time.time - _lastStimulusTime < decayDelay) return;

            _suspicion = Mathf.Max(0f, _suspicion - decayRate * Time.deltaTime);
            if (_suspicion < suspiciousThreshold) _hasInvestigatePoint = false;
        }

        public void HearNoise(Vector3 position, float suspicion)
        {
            if (IsDead) return;

            _lastStimulusTime = Time.time;

            if (_inCombat)
            {
                // Already fighting: a noise just tells them where to run when they can't see the player.
                if (!_seesTarget) SetLastKnownPosition(position);
                return;
            }

            // A noise somewhere new restarts the "look first, then walk over" investigation.
            if (!_hasInvestigatePoint || (position - _investigatePoint).sqrMagnitude > 4f)
                _investigateStartTime = Time.time;
            _hasInvestigatePoint = true;
            _investigatePoint = position;

            AddSuspicion(suspicion);
            if (_inCombat) SetLastKnownPosition(position);
        }

        // Another enemy is shooting at the player nearby: join the fight and run to where they're aiming.
        public void HearGunfire(Vector3 target)
        {
            if (IsDead) return;

            _lastStimulusTime = Time.time;
            EnterCombat();
            if (!_seesTarget) SetLastKnownPosition(target);
        }

        private void OnDamaged(float amount, RaycastHit hit, GameObject instigator)
        {
            // Remembered so the killing blow can pick the headshot death.
            _lastHitWasHeadshot = hit.collider != null && hit.collider.GetComponent<HitZone>() != null;

            // Getting shot means a fight, even from behind.
            _lastStimulusTime = Time.time;
            EnterCombat();
            if (!_seesTarget && instigator != null) SetLastKnownPosition(instigator.transform.position);
        }

        private void SetLastKnownPosition(Vector3 position)
        {
            _lastKnownPosition = position;
            _hasSearchPoint = false;
        }

        // ---------- Vision ----------

        private void UpdatePerception()
        {
            AlertState stateBefore = State;
            bool perceived = false;

            if (TargetAlive)
            {
                Vector3 position = _target.transform.position;
                _targetSpeed = Vector3.Distance(position, _targetPreviousPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
                _targetPreviousPosition = position;

                Vector3 toTarget = AimPoint - EyePosition;
                if (toTarget.magnitude <= sightRange && HasLineOfSight())
                {
                    toTarget.y = 0f;
                    float angle = Vector3.Angle(transform.forward, toTarget);
                    float centralHalf = centralVisionAngle * 0.5f;

                    if (angle <= centralHalf)
                    {
                        perceived = true;
                        AddSuspicion(centralSuspicionPerSecond * Time.deltaTime);
                    }
                    else if (angle <= centralHalf + peripheralVisionAngle &&
                             (_targetSpeed > movingSpeedThreshold || _inCombat))
                    {
                        perceived = true;
                        AddSuspicion(peripheralSuspicionPerSecond * Time.deltaTime);
                    }
                }
            }

            if (perceived)
            {
                // A new sighting starts the reaction delay, chosen by how alert the enemy was beforehand.
                if (!_seesTarget && Time.time - _sightLostTime > sightGraceTime)
                {
                    _sightStartTime = Time.time;
                    _fireDelay = stateBefore == AlertState.Combat ? combatReactionDelay
                        : stateBefore == AlertState.Suspicious ? suspiciousReactionDelay
                        : patrolReactionDelay;
                }

                _lastStimulusTime = Time.time;
                _lastKnownPosition = _target.transform.position;
                _hasSearchPoint = false;
                if (!_hasInvestigatePoint) _investigateStartTime = Time.time;
                _hasInvestigatePoint = true;
                _investigatePoint = _target.transform.position;
            }
            else if (_seesTarget)
            {
                _sightLostTime = Time.time;
            }

            _seesTarget = perceived;
        }

        private bool HasLineOfSight()
        {
            Vector3 eye = EyePosition;
            Vector3 toTarget = AimPoint - eye;
            if (!Raycast(eye, toTarget.normalized, toTarget.magnitude + 0.5f, out RaycastHit hit)) return false;
            return hit.collider.GetComponentInParent<TacticalShooterPlayer>() == _target;
        }

        // ---------- Movement ----------

        private void UpdateMovement()
        {
            Vector3 desired = Vector3.zero;
            Quaternion? face = null;

            switch (State)
            {
                case AlertState.Combat:
                    desired = UpdateCombat(out face);
                    break;
                case AlertState.Suspicious:
                    desired = UpdateInvestigate(out face);
                    break;
                default:
                    desired = behavior == EnemyBehavior.Patrol && patrolPoints != null && patrolPoints.Length > 0
                        ? UpdatePatrol(out face)
                        : UpdateGuard(out face);
                    break;
            }

            if (face.HasValue)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, face.Value,
                    1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            _velocity = Vector3.Lerp(_velocity, desired, 1f - Mathf.Exp(-8f * Time.deltaTime));

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;
            _controller.Move((_velocity + Vector3.up * _verticalVelocity) * Time.deltaTime);

            UpdateStuck(desired);
            UpdateWalkAnimation();
            UpdateFootsteps();
        }

        // Separate 3D source so step sounds don't inherit the gunshot pitch and can carry a shorter distance.
        private AudioSource CreateStepSource()
        {
            var source = new GameObject("Footsteps").AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = stepAudibleDistance;
            source.dopplerLevel = 0f;
            return source;
        }

        // One step per ~0.75 m of ground covered: slow and soft while walking, quick and heavy while running.
        private void UpdateFootsteps()
        {
            float speed = Flat(_controller.velocity).magnitude;
            if (speed < 0.3f || !_controller.isGrounded)
            {
                _stepTimer = Mathf.Min(_stepTimer, 0.15f);
                return;
            }

            _stepTimer -= Time.deltaTime;
            if (_stepTimer > 0f) return;
            _stepTimer = Mathf.Clamp(0.75f / speed, 0.28f, 0.9f);

            bool running = speed > 2.2f;
            var clips = running && runStepSounds != null && runStepSounds.Length > 0 ? runStepSounds : walkStepSounds;
            if (clips == null || clips.Length == 0) return;

            _stepSource.pitch = Random.Range(0.93f, 1.07f);
            _stepSource.PlayOneShot(clips[Random.Range(0, clips.Length)], running ? runStepVolume : walkStepVolume);
        }

        // Something the NavMesh doesn't know about (a body, a swinging door) can pin an enemy in place.
        // After a moment of pushing without moving, give up on the current goal and pick another.
        private void UpdateStuck(Vector3 desired)
        {
            Vector3 actual = Flat(_controller.velocity);
            bool pushing = desired.magnitude > 0.5f && actual.magnitude < 0.15f;
            _stuckTime = pushing ? _stuckTime + Time.deltaTime : 0f;
            if (_stuckTime < 1.5f) return;

            _stuckTime = 0f;
            _nextRepathTime = 0f;
            if (State == AlertState.Combat) _hasSearchPoint = false;
            else if (State == AlertState.Suspicious) _investigatePoint = transform.position;
        }

        // Sees the player: stand and shoot (closing in if out of range). Otherwise run to where they were
        // last seen or heard, then keep sweeping random spots around it.
        private Vector3 UpdateCombat(out Quaternion? face)
        {
            face = null;

            if (_seesTarget)
            {
                Vector3 toTarget = Flat(_target.transform.position - transform.position);
                if (toTarget.sqrMagnitude > 0.01f) face = Quaternion.LookRotation(toTarget);
                if (toTarget.magnitude <= attackRange) return Vector3.zero;
                return MoveTo(_target.transform.position, runSpeed, ref face);
            }

            if (!_hasSearchPoint)
            {
                if (Flat(_lastKnownPosition - transform.position).magnitude > 1f)
                    return MoveTo(_lastKnownPosition, runSpeed, ref face);

                _searchPoint = PickSearchPoint(_lastKnownPosition);
                _hasSearchPoint = true;
            }

            if (Flat(_searchPoint - transform.position).magnitude < 1f)
                _searchPoint = PickSearchPoint(_lastKnownPosition);

            return MoveTo(_searchPoint, runSpeed, ref face);
        }

        // Weapon up, look toward the noise for a moment, then walk over slowly and look around once there.
        private Vector3 UpdateInvestigate(out Quaternion? face)
        {
            face = null;
            if (!_hasInvestigatePoint) return Vector3.zero;

            Vector3 toPoint = Flat(_investigatePoint - transform.position);
            if (_seesTarget || Time.time - _investigateStartTime < investigateLookTime)
            {
                if (toPoint.sqrMagnitude > 0.01f) face = Quaternion.LookRotation(toPoint);
                _scanBaseYaw = transform.eulerAngles.y;
                return Vector3.zero;
            }

            if (toPoint.magnitude > 1.2f)
            {
                _scanBaseYaw = transform.eulerAngles.y;
                return MoveTo(_investigatePoint, investigateSpeed, ref face);
            }

            float sweep = Mathf.Sin(Time.time * 2f * Mathf.PI / guardScanPeriod) * 60f;
            face = Quaternion.Euler(0f, _scanBaseYaw + sweep, 0f);
            return Vector3.zero;
        }

        // Walks back to the guard spot if moved off it, then sweeps left and right around the home direction.
        private Vector3 UpdateGuard(out Quaternion? face)
        {
            face = null;
            if (Flat(_homePosition - transform.position).magnitude > 0.5f)
                return MoveTo(_homePosition, moveSpeed, ref face);

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

            if (Flat(point.position - transform.position).magnitude < 0.4f)
            {
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                _waitUntil = Time.time + patrolWaitTime;
                return Vector3.zero;
            }

            return MoveTo(point.position, moveSpeed, ref face);
        }

        // Follows a NavMesh path (straight line if there's no NavMesh) and opens closed doors on the way.
        private Vector3 MoveTo(Vector3 destination, float speed, ref Quaternion? face)
        {
            if (Time.time >= _nextRepathTime || (destination - _pathDestination).sqrMagnitude > 0.25f)
                RebuildPath(destination);

            while (_pathCorner < _pathCorners.Count - 1 &&
                   Flat(_pathCorners[_pathCorner] - transform.position).magnitude < 0.35f)
                _pathCorner++;

            Vector3 corner = _pathCorners.Count > 0 ? _pathCorners[_pathCorner] : destination;
            Vector3 toCorner = Flat(corner - transform.position);
            if (toCorner.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 direction = toCorner.normalized;
            if (!face.HasValue) face = Quaternion.LookRotation(direction);
            OpenDoorAhead(direction);

            // Turn on the spot first, so corners don't get cut through walls.
            float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, direction));
            return transform.forward * (speed * alignment * alignment);
        }

        private void RebuildPath(Vector3 destination)
        {
            _pathDestination = destination;
            _nextRepathTime = Time.time + 0.5f;
            _pathCorners.Clear();
            _pathCorner = 0;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit from, 1.5f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(destination, out NavMeshHit to, 2f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _navPath) &&
                _navPath.status != NavMeshPathStatus.PathInvalid)
            {
                _pathCorners.AddRange(_navPath.corners);
                if (_pathCorners.Count > 1) _pathCorner = 1;
                return;
            }

            _pathCorners.Add(destination);
        }

        private Vector3 PickSearchPoint(Vector3 around)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = Random.insideUnitCircle * searchRadius;
                Vector3 candidate = around + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return hit.position;
            }
            return around;
        }

        private void OpenDoorAhead(Vector3 direction)
        {
            foreach (var door in Door.All)
            {
                if (door.IsOpen) continue;
                Vector3 toDoor = door.Center - (transform.position + Vector3.up * 1.1f);
                if (Mathf.Abs(toDoor.y) > 1.5f) continue;
                toDoor.y = 0f;
                if (toDoor.magnitude > 1.3f || Vector3.Dot(toDoor.normalized, direction) < 0.3f) continue;
                door.Toggle(transform.position);
            }
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

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // Drives the SKM_Operator Walking cycle: full stride as soon as the enemy moves, played back at a
        // rate matching the ground speed, reversed when backing off, and legs turned toward a sidestep.
        private void UpdateWalkAnimation()
        {
            float speed = _velocity.magnitude;
            Vector3 local = transform.InverseTransformDirection(_velocity);
            bool backwards = local.z < -0.1f;

            _animator.SetFloat(SpeedHash, Mathf.Clamp01(speed / 0.4f), 0.15f, Time.deltaTime);

            float playback = Mathf.Clamp(speed / walkClipSpeed, 0.5f, maxWalkPlayback);
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

        // Patrol: weapon lowered. Suspicious / Combat: weapon up, aimed at the player or the point of interest.
        private void UpdateIK()
        {
            if (_ik == null) return;

            AlertState state = State;
            _ik.isAiming = state != AlertState.Patrol && TargetAlive;
            _ik.handIKWeight = _isReloading ? 0f : 1f;
            if (!_ik.isAiming) return;

            if (_seesTarget) _ik.aimTarget = AimPoint;
            else if (_velocity.magnitude > 0.3f) _ik.aimTarget = EyePosition + transform.forward * 5f;
            else if (state == AlertState.Combat) _ik.aimTarget = _lastKnownPosition + Vector3.up * 1.4f;
            else _ik.aimTarget = EyePosition + transform.forward * 5f;
        }

        // ---------- Firing ----------

        private void UpdateFiring()
        {
            if (!_seesTarget || !TargetAlive || _isReloading) return;
            if (Time.time < _sightStartTime + _fireDelay || Time.time < _nextShotTime) return;

            Vector3 toTarget = Flat(_target.transform.position - transform.position);
            if (toTarget.magnitude > attackRange) return;
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

        private float CurrentSpread()
        {
            switch (State)
            {
                case AlertState.Patrol: return spreadAngle * (1f + patrolAccuracyPenalty);
                case AlertState.Combat: return spreadAngle * (1f - combatAccuracyBonus);
                default: return spreadAngle;
            }
        }

        private void Fire()
        {
            _ammo--;

            if (weapon != null)
            {
                weapon.PlayMuzzleFlash();
                PlayFireSound();
            }

            // Gunfire draws in everyone nearby (once a second is enough during a burst).
            if (Time.time >= _nextGunfireAlertTime)
            {
                _nextGunfireAlertTime = Time.time + 1f;
                Noise.EmitGunfire(this, gunfireAlertRadius, _target.transform.position);
            }

            _animator.SetTrigger(FireHash);

            Vector3 origin = weapon != null ? weapon.GetMuzzlePoint().position : EyePosition;
            Vector3 aimDirection = (AimPoint - origin).normalized;
            float spread = CurrentSpread();

            for (int i = 0; i < pellets; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(Random.Range(0f, 360f), aimDirection)
                                    * Quaternion.AngleAxis(Random.Range(0f, spread), Vector3.Cross(aimDirection, Vector3.up).normalized)
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

        // ---------- Gizmos ----------

        private void OnDrawGizmosSelected()
        {
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            float centralHalf = centralVisionAngle * 0.5f;
            DrawCone(eye, centralHalf, Color.yellow);
            DrawCone(eye, centralHalf + peripheralVisionAngle, new Color(1f, 0.5f, 0f));

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

        private void DrawCone(Vector3 eye, float halfAngle, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, -halfAngle, 0f) * transform.forward * 8f);
            Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, halfAngle, 0f) * transform.forward * 8f);
        }

#if UNITY_EDITOR
        // Scene view label with the current state and suspicion, for tuning in Play mode.
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || IsDead) return;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.2f, $"{State}  S={_suspicion:F0}");
        }
#endif

        // ---------- Death ----------

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
