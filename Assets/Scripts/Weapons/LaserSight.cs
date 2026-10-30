using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.Weapons
{
    // Red aiming laser out of the small aperture under the LS3 flashlight lens, parallel to the light.
    // A faint beam plus a bright dot (with a tiny glow light) where it lands.
    public class LaserSight : MonoBehaviour
    {
        // Aperture position in the A_LS3_Flashlight root's space (measured off SM_LS3).
        public static readonly Vector3 LS3Aperture = new Vector3(0f, -0.028f, 0.016f);

        private const float MaxDistance = 100f;
        private const float BeamWidth = 0.0012f;
        private static readonly Color LaserColor = new Color(1f, 0.05f, 0.03f);

        private Vector3 _aperture = LS3Aperture;
        private Transform _ignoreRoot;
        private LineRenderer _beam;
        private Transform _dot;
        private Light _glow;
        private bool _isOn;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public bool IsOn => _isOn;

        public void Initialize(Vector3 aperture, Transform ignoreRoot)
        {
            _aperture = aperture;
            _ignoreRoot = ignoreRoot;
        }

        public void SetOn(bool on)
        {
            _isOn = on;
            if (_beam == null) Build();
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void OnDisable() => Refresh();

        private void Refresh()
        {
            bool visible = _isOn && isActiveAndEnabled;
            if (_beam != null) _beam.enabled = visible;
            if (_dot != null) _dot.gameObject.SetActive(visible);
        }

        private void Build()
        {
            var beamMaterial = new Material(Shader.Find("Sprites/Default"));
            _beam = new GameObject("LaserBeam").AddComponent<LineRenderer>();
            _beam.material = beamMaterial;
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
            _beam.widthMultiplier = BeamWidth;
            _beam.numCapVertices = 2;
            _beam.shadowCastingMode = ShadowCastingMode.Off;
            _beam.receiveShadows = false;
            // Bright at the aperture, fading out along the beam like dust-lit laser light.
            _beam.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(LaserColor, 0f), new GradientColorKey(LaserColor, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.25f, 0.15f), new GradientAlphaKey(0.08f, 1f) }
            };

            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "LaserDot";
            Destroy(dot.GetComponent<Collider>());
            // Over-bright so the dot still reads inside the flashlight's hotspot.
            var dotMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            dotMaterial.SetColor("_BaseColor", LaserColor * 4f);
            var dotRenderer = dot.GetComponent<MeshRenderer>();
            dotRenderer.sharedMaterial = dotMaterial;
            dotRenderer.shadowCastingMode = ShadowCastingMode.Off;
            dotRenderer.receiveShadows = false;
            _dot = dot.transform;

            _glow = dot.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.color = LaserColor;
            _glow.range = 0.25f;
            _glow.intensity = 1f;
            _glow.shadows = LightShadows.None;
        }

        // After animation and the weapon's procedural pose, so the beam leaves the gun where it is drawn.
        private void LateUpdate()
        {
            if (!_isOn || _beam == null) return;

            Vector3 origin = transform.TransformPoint(_aperture);
            Vector3 direction = transform.forward;
            Vector3 end = origin + direction * MaxDistance;
            bool hit = Raycast(origin, direction, out RaycastHit closest);
            if (hit) end = closest.point;

            _beam.SetPosition(0, origin);
            _beam.SetPosition(1, end);

            _dot.gameObject.SetActive(hit);
            if (!hit) return;

            // Slightly off the surface so it doesn't z-fight; grows a little with distance like a real dot.
            _dot.position = closest.point + closest.normal * 0.003f;
            _dot.localScale = Vector3.one * (0.016f + closest.distance * 0.002f);
        }

        private bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit closest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, MaxDistance, ~0,
                QueryTriggerInteraction.Ignore);
            closest = default;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_ignoreRoot != null && _hits[i].transform.IsChildOf(_ignoreRoot)) continue;
                if (_hits[i].distance >= best) continue;
                best = _hits[i].distance;
                closest = _hits[i];
            }
            return best < float.MaxValue;
        }

        private void OnDestroy()
        {
            if (_beam != null) Destroy(_beam.gameObject);
            if (_dot != null) Destroy(_dot.gameObject);
        }
    }
}
