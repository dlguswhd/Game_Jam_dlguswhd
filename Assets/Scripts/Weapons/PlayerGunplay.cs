using System;
using System.Collections.Generic;
using KINEMATION.TacticalShooterPack.Scripts.Animation;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameJam.Weapons
{
    [Serializable]
    public class GunRecoilSettings
    {
        [Tooltip("Upward camera kick per shot (degrees).")]
        public float verticalKick = 0.6f;

        [Tooltip("Extra vertical kick added per consecutive shot (degrees).")]
        public float verticalKickGrowth = 0.02f;

        [Tooltip("Max vertical kick per shot after growth (degrees).")]
        public float maxVerticalKick = 1.0f;

        [Tooltip("Random horizontal kick range per shot (degrees). x = left, y = right.")]
        public Vector2 horizontalKick = new Vector2(-0.35f, 0.35f);

        [Tooltip("Constant horizontal drift per shot (degrees). Negative = left, positive = right.")]
        public float horizontalBias = 0.05f;

        [Tooltip("Multiplier while aiming down sights.")]
        [Range(0f, 1f)] public float adsMultiplier = 0.75f;

        [Tooltip("How fast the kick is applied to the view (higher = snappier).")]
        public float kickSpeed = 25f;

        [Tooltip("Seconds without firing before the consecutive shot counter resets.")]
        public float resetTime = 0.25f;
    }

    [Serializable]
    public class GunplayEntry
    {
        public GameObject weaponPrefab;
        public float damage = 25f;
        public float range = 300f;
        public float impactForce = 5f;
        public GunRecoilSettings recoil = new GunRecoilSettings();

        [NonSerialized] public TacticalWeaponSettings resolvedSettings;
    }

    // Runs after TacticalShooterPlayer.Start so the weapons are already spawned.
    [DefaultExecutionOrder(100)]
    public class PlayerGunplay : MonoBehaviour
    {
        [SerializeField] private List<GunplayEntry> weapons = new List<GunplayEntry>();
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private bool drawDebugRays = true;

        private TacticalProceduralAnimation _proceduralAnimation;
        private readonly Dictionary<TacticalShooterWeapon, GunplayEntry> _entries =
            new Dictionary<TacticalShooterWeapon, GunplayEntry>();

        private Vector2 _pendingRecoil;
        private GunRecoilSettings _activeRecoil;
        private int _consecutiveShots;
        private float _lastShotTime;

        private void Start()
        {
            _proceduralAnimation = GetComponent<TacticalProceduralAnimation>();

            foreach (var entry in weapons)
            {
                if (entry.weaponPrefab == null) continue;
                var prefabWeapon = entry.weaponPrefab.GetComponentInChildren<TacticalShooterWeapon>(true);
                if (prefabWeapon != null) entry.resolvedSettings = prefabWeapon.tacWeaponSettings;
            }

            foreach (var weapon in GetComponentsInChildren<TacticalShooterWeapon>(true))
            {
                var entry = weapons.Find(e => e.resolvedSettings == weapon.tacWeaponSettings);
                if (entry == null) continue;

                _entries[weapon] = entry;
                weapon.Fired += OnWeaponFired;
            }
        }

        private void OnDestroy()
        {
            foreach (var weapon in _entries.Keys)
            {
                if (weapon != null) weapon.Fired -= OnWeaponFired;
            }
        }

        private void OnWeaponFired(TacticalShooterWeapon weapon)
        {
            if (!_entries.TryGetValue(weapon, out var entry)) return;

            FireRay(weapon, entry);
            AddRecoil(entry.recoil);
        }

        private void FireRay(TacticalShooterWeapon weapon, GunplayEntry entry)
        {
            Transform muzzle = weapon.GetMuzzlePoint();
            Vector3 origin = muzzle.position;
            Vector3 direction = muzzle.forward;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, entry.range, hitMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (drawDebugRays) Debug.DrawLine(origin, hit.point, Color.red, 1f);

                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(entry.damage, hit, gameObject);
                if (hit.rigidbody != null) hit.rigidbody.AddForceAtPosition(direction * entry.impactForce,
                    hit.point, ForceMode.Impulse);
                if (impactEffectPrefab != null)
                {
                    var fx = Instantiate(impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                    Destroy(fx, 5f);
                }
                return;
            }

            if (drawDebugRays) Debug.DrawRay(origin, direction * entry.range, Color.yellow, 1f);
        }

        private void AddRecoil(GunRecoilSettings recoil)
        {
            if (Time.time - _lastShotTime > recoil.resetTime) _consecutiveShots = 0;
            _lastShotTime = Time.time;

            float vertical = Mathf.Min(recoil.verticalKick + recoil.verticalKickGrowth * _consecutiveShots,
                recoil.maxVerticalKick);
            float horizontal = Random.Range(recoil.horizontalKick.x, recoil.horizontalKick.y) + recoil.horizontalBias;

            float scale = Mathf.Lerp(1f, recoil.adsMultiplier, _proceduralAnimation.aimingWeight);
            _pendingRecoil += new Vector2(horizontal, vertical) * scale;
            _activeRecoil = recoil;
            _consecutiveShots++;
        }

        private void Update()
        {
            if (_activeRecoil == null || _pendingRecoil.sqrMagnitude < 0.000001f) return;

            // Kick is pushed into the real look input, so the player has to pull the mouse down to control it.
            float alpha = 1f - Mathf.Exp(-_activeRecoil.kickSpeed * Time.deltaTime);
            Vector2 step = _pendingRecoil * alpha;
            _pendingRecoil -= step;

            _proceduralAnimation.pitchInput = Mathf.Clamp(_proceduralAnimation.pitchInput - step.y, -90f, 90f);
            _proceduralAnimation.yawInput = Mathf.Clamp(_proceduralAnimation.yawInput + step.x, -90f, 90f);
        }
    }
}
