using KillOrDead.Combat;
using UnityEngine;

namespace KillOrDead.Traps
{
    /// <summary>
    /// 크레모어의 "보이고 들리는 부분"만 담당한다. 판정은 하지 않는다.
    ///
    /// - 레이저 빔은 그리지 않는다. 감지는 <see cref="ClaymoreMine"/>이 보이지 않는 레이로 그대로 한다.
    /// - LED: 센서 옆 빨간 LED가 천천히 깜빡인다.
    /// - 부저음: 플레이어가 일정 범위 안에 들어오면 반복 재생하고, 나가면 멈춘다 (클립이 비어 있으면 무음).
    /// - 센서가 부서지거나 해체되면(<see cref="ClaymoreMine.IsArmed"/> = false) LED·부저음을 모두 끈다.
    /// - 센서가 총에 맞아 부서지는 순간 전기 스파크 이펙트와 소리를 한 번 낸다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Traps/Claymore Laser View")]
    [RequireComponent(typeof(ClaymoreMine))]
    public class ClaymoreLaserView : MonoBehaviour
    {
        [Header("LED")]
        [Tooltip("깜빡일 LED 렌더러. 센서 렌즈 옆에 붙은 작은 구.")]
        [SerializeField] private Renderer ledRenderer;

        [SerializeField] private Color ledOnColor = new Color(1f, 0.05f, 0.03f, 1f);
        [SerializeField] private Color ledOffColor = new Color(0.12f, 0.01f, 0.01f, 1f);

        [Tooltip("한 번 켜졌다 꺼지는 데 걸리는 시간(초). 기획: 천천히 깜빡임.")]
        [SerializeField, Min(0.05f)] private float ledBlinkPeriod = 1.5f;

        [Header("Buzzer")]
        [Tooltip("플레이어가 범위 안에 있는 동안 반복 재생되는 부저음. 비워두면 무음.")]
        [SerializeField] private AudioClip buzzerSound;

        [SerializeField, Range(0f, 1f)] private float buzzerVolume = 0.5f;

        [Tooltip("플레이어가 크레모어로부터 이 거리(m) 안에 들어오면 부저음이 울린다. 멀어질수록 작게 들린다.")]
        [SerializeField, Min(0.1f)] private float buzzerRange = 6f;

        [Header("Sensor Break (센서 피격)")]
        [Tooltip("센서가 총에 맞아 부서지는 순간 센서 렌즈 위치에 생성할 전기 스파크 이펙트. 비워두면 안 나온다.")]
        [SerializeField] private GameObject sensorBreakEffect;

        [Tooltip("센서가 부서지는 순간 재생할 소리. 비워두면 무음.")]
        [SerializeField] private AudioClip sensorBreakSound;

        [SerializeField, Range(0f, 1f)] private float sensorBreakVolume = 1f;

        [Header("Disarm (해체 성공)")]
        [Tooltip("F 홀드로 해체에 성공한 순간 한 번 재생할 소리. 센서 파괴(총격)로 꺼질 때는 재생하지 않는다. 비워두면 무음.")]
        [SerializeField] private AudioClip disarmSuccessSound;

        [SerializeField, Range(0f, 1f)] private float disarmSuccessVolume = 1f;

        private ClaymoreMine _mine;
        private AudioSource _buzzer;
        private Material _ledOnMaterial;
        private Material _ledOffMaterial;
        private bool _sensorWasActive;
        private bool _wasDisarmed;

        private void Awake()
        {
            _mine = GetComponent<ClaymoreMine>();
        }

        private void Start()
        {
            SetupLed();
            SetupBuzzer();
            _sensorWasActive = _mine.IsSensorActive;
            _wasDisarmed = _mine.IsDisarmed;
        }

        private void SetupLed()
        {
            if (ledRenderer == null) return;

            _ledOnMaterial = RuntimeUnlitMaterial.CreateOpaque(ledOnColor);
            _ledOffMaterial = RuntimeUnlitMaterial.CreateOpaque(ledOffColor);
            ledRenderer.sharedMaterial = _ledOffMaterial;
            ledRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void SetupBuzzer()
        {
            if (buzzerSound == null) return;

            _buzzer = gameObject.AddComponent<AudioSource>();
            _buzzer.clip = buzzerSound;
            _buzzer.loop = true;
            _buzzer.playOnAwake = false;
            _buzzer.volume = buzzerVolume;
            _buzzer.spatialBlend = 1f;                                // 완전 3D 소리 — 소리 나는 방향을 알 수 있다
            _buzzer.rolloffMode = AudioRolloffMode.Linear;
            _buzzer.minDistance = 0.5f;
            _buzzer.maxDistance = buzzerRange;                        // 범위 끝에서 0, 가까울수록 크게
        }

        private void LateUpdate()
        {
            // 센서가 부서지거나 해체되면 LED·부저음이 꺼진다 (기획: 센서 피격 → 무력화).
            bool active = _mine.IsArmed;

            UpdateBuzzer(active);
            UpdateLed(active);
            CheckSensorBreak();
            CheckDisarm();
        }

        /// <summary>
        /// 해체되지 않았다가 이번에 해체됐으면(F 홀드 성공) 성공음을 한 번 낸다.
        /// 센서 파괴는 IsDisarmed를 바꾸지 않으므로 여기에 걸리지 않는다 — 총으로 센서를 부숴도 성공음은 안 난다.
        /// </summary>
        private void CheckDisarm()
        {
            bool disarmed = _mine.IsDisarmed;
            // 이미 총에 부서진 크레모어를 해체하는 경우도 성공음을 내지 않는다.
            if (!_wasDisarmed && disarmed && _mine.IsSensorActive && disarmSuccessSound != null)
            {
                AudioSource.PlayClipAtPoint(disarmSuccessSound, transform.position, disarmSuccessVolume);
            }
            _wasDisarmed = disarmed;
        }

        /// <summary>
        /// 센서가 살아 있다가 이번에 부서졌으면 스파크와 소리를 한 번 낸다.
        /// 센서 파괴는 총(HitscanTacticalWeapon)이 ClaymoreParts.DestroySensor()를 불러서 일어나는데,
        /// 여기서는 그 결과(살아 있음 → 부서짐)만 보고 반응한다. 이미 부서진 센서를 또 맞혀도 다시 나오지 않는다.
        /// </summary>
        private void CheckSensorBreak()
        {
            bool sensorActive = _mine.IsSensorActive;
            if (_sensorWasActive && !sensorActive) PlaySensorBreak();
            _sensorWasActive = sensorActive;
        }

        private void PlaySensorBreak()
        {
            Vector3 position = _mine.LaserOrigin.position;   // 센서 렌즈 중심

            if (sensorBreakEffect != null)
            {
                // 이펙트는 다 튀고 나면 스스로 사라진다(파티클 Stop Action = Destroy).
                Instantiate(sensorBreakEffect, position, Quaternion.LookRotation(_mine.transform.forward));
            }

            if (sensorBreakSound != null)
            {
                AudioSource.PlayClipAtPoint(sensorBreakSound, position, sensorBreakVolume);
            }
        }

        private void UpdateBuzzer(bool active)
        {
            if (_buzzer == null) return;

            bool play = active && IsPlayerInBuzzerRange();
            if (_buzzer.isPlaying == play) return;

            if (play) _buzzer.Play();
            else _buzzer.Stop();
        }

        private bool IsPlayerInBuzzerRange()
        {
            Transform player = _mine.Player;
            if (player == null) return false;
            return (player.position - transform.position).sqrMagnitude <= buzzerRange * buzzerRange;
        }

        private void UpdateLed(bool active)
        {
            if (ledRenderer == null) return;

            // 꺼진 크레모어는 LED도 꺼진 색으로 둔다. 해체는 센서 모델이 남아 있으니 따로 꺼야 한다.
            bool on = active && Mathf.Repeat(Time.time, ledBlinkPeriod) < ledBlinkPeriod * 0.5f;
            var target = on ? _ledOnMaterial : _ledOffMaterial;
            if (ledRenderer.sharedMaterial != target) ledRenderer.sharedMaterial = target;
        }

        private void OnDestroy()
        {
            // 코드로 만든 머티리얼은 직접 치워야 메모리에 남지 않는다.
            if (_ledOnMaterial != null) Destroy(_ledOnMaterial);
            if (_ledOffMaterial != null) Destroy(_ledOffMaterial);
        }
    }
}
