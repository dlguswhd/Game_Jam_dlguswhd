using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GameJam.UI
{
    // Shared building blocks for the code-built menus (title, briefing, pause, settings, death / success screens):
    // one font, one palette, rounded panels, buttons and sliders, so every screen looks the same.
    public static class UIKit
    {
        public static readonly Color Backdrop = new Color(0.02f, 0.02f, 0.025f, 0.92f);
        public static readonly Color Panel = new Color(0.07f, 0.07f, 0.08f, 0.94f);
        public static readonly Color Accent = new Color(0.72f, 0.08f, 0.08f);
        public static readonly Color Text = new Color(0.92f, 0.92f, 0.9f);
        public static readonly Color MutedText = new Color(0.62f, 0.62f, 0.6f);
        private static readonly Color ButtonNormal = new Color(0.12f, 0.12f, 0.13f, 0.95f);
        private static readonly Color ButtonHover = new Color(0.32f, 0.06f, 0.06f, 0.98f);

        private static Font _font;

        public static Font Font => _font != null
            ? _font
            : _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);

        // Screen-space canvas scaled from 1920×1080 by height.
        public static Canvas CreateCanvas(string name, int sortingOrder, bool keepAcrossScenes = false)
        {
            var root = new GameObject(name);
            if (keepAcrossScenes) UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            root.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        // Buttons need an EventSystem using the Input System's UI module (the project uses the new Input System).
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static void Center(RectTransform rect, Vector2 size, Vector2 offset = default)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        public static Image Fill(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent);
            Stretch(rect);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Image RoundedPanel(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = InteractionPrompt.RoundedSprite();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = color;
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.lineSpacing = 1.15f;
            label.raycastTarget = false;
            return label;
        }

        // Vertical stack that sizes its children (for button columns and setting rows).
        public static VerticalLayoutGroup Column(RectTransform rect, float spacing, int padding = 0)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static Button Button(Transform parent, string text, Action onClick, float height = 64f)
        {
            var background = RoundedPanel("Button_" + text, parent, ButtonNormal);
            background.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            var hover = button.gameObject.AddComponent<ButtonHoverTint>();
            hover.Setup(background, ButtonNormal, ButtonHover);

            var label = Label("Text", background.transform, text, 28, Text);
            Stretch(label.rectTransform);
            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
        }

        // "Label ──●── 1.00" row.
        public static Slider SliderRow(Transform parent, string title, float min, float max, float value,
            Func<float, string> format, Action<float> onChanged)
        {
            var row = Rect("Row_" + title, parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;

            var name = Label("Title", row, title, 26, Text, TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(0.3f, 1f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;

            var valueText = Label("Value", row, format(value), 26, MutedText, TextAnchor.MiddleRight);
            valueText.rectTransform.anchorMin = new Vector2(0.85f, 0f);
            valueText.rectTransform.anchorMax = new Vector2(1f, 1f);
            valueText.rectTransform.offsetMin = valueText.rectTransform.offsetMax = Vector2.zero;

            var area = Rect("Slider", row);
            area.anchorMin = new Vector2(0.32f, 0.5f);
            area.anchorMax = new Vector2(0.83f, 0.5f);
            area.sizeDelta = new Vector2(0f, 28f);

            var track = RoundedPanel("Track", area, new Color(0.18f, 0.18f, 0.19f));
            track.rectTransform.anchorMin = new Vector2(0f, 0.35f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.65f);
            track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;

            var fillArea = Rect("Fill Area", area);
            fillArea.anchorMin = new Vector2(0f, 0.35f);
            fillArea.anchorMax = new Vector2(1f, 0.65f);
            fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            var fill = RoundedPanel("Fill", fillArea, Accent);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect("Handle Area", area);
            Stretch(handleArea);
            var handle = RoundedPanel("Handle", handleArea, Text);
            handle.rectTransform.sizeDelta = new Vector2(24f, 0f);

            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v =>
            {
                valueText.text = format(v);
                onChanged?.Invoke(v);
            });
            return slider;
        }
    }

    // Swaps a button's background colour on hover (the colour block alone can't express "dark → dark red").
    public class ButtonHoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private Image _image;
        private Color _normal;
        private Color _hover;

        public void Setup(Image image, Color normal, Color hover)
        {
            _image = image;
            _normal = normal;
            _hover = hover;
        }

        public void OnPointerEnter(PointerEventData eventData) => _image.color = _hover;
        public void OnPointerExit(PointerEventData eventData) => _image.color = _normal;
        public void OnSelect(BaseEventData eventData) => _image.color = _hover;
        public void OnDeselect(BaseEventData eventData) => _image.color = _normal;
    }
}
