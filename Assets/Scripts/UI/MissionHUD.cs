using UnityEngine;
using UnityEngine.UI;

namespace GameJam.UI
{
    // Objective panel in the top-left corner, plus a centered banner for "임무 완료".
    // Built in code like InteractionPrompt (same rounded dark style).
    public class MissionHUD : MonoBehaviour
    {
        private Text _objective;
        private Image _banner;
        private Text _bannerText;

        public static MissionHUD Create()
        {
            var root = new GameObject("MissionHUD");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var hud = root.AddComponent<MissionHUD>();
            hud.Build();
            return hud;
        }

        public void SetObjective(string text) => _objective.text = text;

        public void ShowBanner(string text)
        {
            _bannerText.text = text;
            _banner.gameObject.SetActive(true);
        }

        private void Build()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);

            var panel = Panel("Objective", transform, new Color(0.06f, 0.06f, 0.07f, 0.72f));
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(32f, -32f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 22, 12, 14);
            layout.spacing = 4f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Label("Title", panel.transform, font, 17, FontStyle.Bold, new Color(1f, 0.78f, 0.35f), "목표");
            _objective = Label("Text", panel.transform, font, 23, FontStyle.Normal, new Color(1f, 1f, 1f, 0.95f), "");

            _banner = Panel("Banner", transform, new Color(0.06f, 0.06f, 0.07f, 0.85f));
            var bannerRect = _banner.rectTransform;
            bannerRect.anchorMin = bannerRect.anchorMax = bannerRect.pivot = new Vector2(0.5f, 0.5f);
            bannerRect.sizeDelta = new Vector2(520f, 110f);
            bannerRect.anchoredPosition = new Vector2(0f, 120f);
            _bannerText = Label("Text", _banner.transform, font, 44, FontStyle.Bold, Color.white, "");
            _bannerText.alignment = TextAnchor.MiddleCenter;
            var textRect = _bannerText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            _banner.gameObject.SetActive(false);
        }

        private static Image Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = InteractionPrompt.RoundedSprite();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(string name, Transform parent, Font font, int size, FontStyle style, Color color,
            string text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.text = text;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }
    }
}
