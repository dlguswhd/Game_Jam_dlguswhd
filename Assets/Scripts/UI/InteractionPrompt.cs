using UnityEngine;
using UnityEngine.UI;

namespace GameJam.UI
{
    // "[F] 문 밀기" style prompt under the crosshair: a light key cap and a label on a dark rounded pill.
    // Built entirely in code so it needs no prefab, sprites or font assets.
    public class InteractionPrompt : MonoBehaviour
    {
        private const float Height = 54f;
        private const float FadeSpeed = 12f;

        private static Sprite _rounded;

        private CanvasGroup _group;
        [System.Serializable]
        private class Row
        {
            public GameObject root;
            public Text key;
            public Text label;
            public CanvasGroup group;
        }
        private Row _primary;
        private Row _secondary;
        private float _alpha;
        private bool _visible;

        public static InteractionPrompt Create()
        {
            var root = new GameObject("InteractionPrompt");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var prompt = root.AddComponent<InteractionPrompt>();
            prompt.Build();
            return prompt;
        }

        public void Show(string key, string label)
        {
            _primary.key.text = key;
            _primary.label.text = label;
            _secondary.root.SetActive(false);
            SetSelected(_primary, true);
            _visible = true;
        }

        public void ShowDoor(string label, int selected, bool cameraAvailable)
        {
            _primary.key.text = "F";
            _primary.label.text = label;
            _secondary.key.text = "F";
            _secondary.label.text = "내시경 카메라 사용";
            _secondary.root.SetActive(cameraAvailable);
            SetSelected(_primary, selected == 0 || !cameraAvailable);
            SetSelected(_secondary, selected == 1);
            _visible = true;
        }

        private static void SetSelected(Row row, bool selected)
        {
            row.key.fontStyle = row.label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            row.group.alpha = selected ? 1f : 0.38f;
        }

        public void Hide() => _visible = false;

        private void Update()
        {
            _alpha = Mathf.MoveTowards(_alpha, _visible ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
            _group.alpha = _alpha;
        }

        private void Build()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);

            var stack = UIKit.Rect("Options", transform);
            stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 0.5f);
            stack.pivot = new Vector2(0.5f, 1f);
            stack.anchoredPosition = new Vector2(0f, -95f);
            UIKit.Column(stack, 8f);
            var stackFit = stack.gameObject.AddComponent<ContentSizeFitter>();
            stackFit.horizontalFit = stackFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _primary = BuildRow(stack, font);
            _secondary = BuildRow(stack, font);
            _secondary.root.SetActive(false);

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private static Row BuildRow(Transform parent, Font font)
        {
            var pill = Panel("Pill", parent, new Color(0.06f, 0.06f, 0.07f, 0.8f));
            var row = new Row { root = pill.gameObject, group = pill.gameObject.AddComponent<CanvasGroup>() };

            var layout = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 22, 8, 8);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var cap = Panel("KeyCap", pill.transform, new Color(0.93f, 0.93f, 0.9f, 1f));
            var capSize = cap.gameObject.AddComponent<LayoutElement>();
            capSize.preferredWidth = capSize.preferredHeight = Height - 16f;
            row.key = Label("Key", cap.transform, font, 24, FontStyle.Bold, new Color(0.08f, 0.08f, 0.09f));
            Stretch(row.key.rectTransform);

            row.label = Label("Label", pill.transform, font, 25, FontStyle.Normal, new Color(1f, 1f, 1f, 0.94f));
            row.label.gameObject.AddComponent<LayoutElement>().preferredHeight = Height - 16f;
            return row;
        }

        private static Image Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(string name, Transform parent, Font font, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // White anti-aliased rounded square, 9-sliced so any panel size keeps the same corner radius.
        public static Sprite RoundedSprite()
        {
            if (_rounded != null) return _rounded;

            const int size = 64;
            const float radius = 16f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius), 0f);
                    float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius), 0f);
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            _rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _rounded;
        }
    }
}
