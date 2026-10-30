using GameJam.Enemies;
using GameJam.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.World
{
    // A mission item lying on furniture. F picks it up into the PlayerInventory.
    // Builds its own simple look in code, with a faint glow so it can be found in the dark.
    public class PickupItem : MonoBehaviour, IInteractable
    {
        public enum Shape
        {
            Keycard,
            Folder
        }

        [SerializeField] private string itemId = "keycard";
        [SerializeField] private string displayName = "열쇠카드";
        [SerializeField] private Shape shape = Shape.Keycard;
        [SerializeField] private Color color = new Color(0.85f, 0.9f, 1f);
        [SerializeField] private Color glowColor = new Color(0.6f, 0.8f, 1f);

        [Header("Pickup noise (0 radius = silent)")]
        [SerializeField] private float noiseRadius;
        [SerializeField] private float noiseSuspicion;

        public string ItemId => itemId;
        public Vector3 InteractionPoint => transform.position;

        private void Awake()
        {
            // Already taken on an earlier visit to this scene.
            if (GameProgress.Items.Contains(itemId))
            {
                gameObject.SetActive(false);
                return;
            }
            BuildVisual();
        }

        private void OnEnable() => Interactables.All.Add(this);
        private void OnDisable() => Interactables.All.Remove(this);

        // Built once: the prompt asks for the label every frame while the player stands next to the item.
        private string _label;
        public string GetLabel(GameObject user) => _label ??= $"줍기 · {displayName}";

        public void Interact(GameObject user)
        {
            var inventory = user.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            inventory.Add(itemId);
            if (noiseRadius > 0f) Noise.Emit(transform.position, noiseRadius, noiseSuspicion);
            gameObject.SetActive(false);
        }

        private void BuildVisual()
        {
            Vector3 size = shape == Shape.Keycard
                ? new Vector3(0.054f, 0.004f, 0.086f)
                : new Vector3(0.24f, 0.025f, 0.31f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Visual";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            body.transform.localPosition = Vector3.up * (size.y * 0.5f);
            body.transform.localScale = size;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", glowColor * 0.6f);
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.up * 0.15f;
            glow.type = LightType.Point;
            glow.color = glowColor;
            glow.range = 0.9f;
            glow.intensity = 0.8f;
            glow.shadows = LightShadows.None;
        }
    }
}
