using UnityEngine;
using UnityEngine.UI;

namespace GameJam.UI
{
    // Full-screen black overlay that survives scene loads: snaps to black when a scene change starts (so the
    // last frame doesn't sit frozen while the next scene activates) and fades back in once the new scene runs.
    public class ScreenFade : MonoBehaviour
    {
        private const float RevealSeconds = 0.5f;

        private static ScreenFade _instance;

        private Image _image;
        private float _alpha;
        private bool _revealing;

        public static void Cover() => Get().SetAlpha(1f, false);

        public static void Reveal()
        {
            if (_instance != null) _instance._revealing = true;
        }

        private static ScreenFade Get()
        {
            if (_instance != null) return _instance;

            var root = new GameObject("ScreenFade");
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var imageObject = new GameObject("Black", typeof(RectTransform));
            imageObject.transform.SetParent(root.transform, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            _instance = root.AddComponent<ScreenFade>();
            _instance._image = imageObject.AddComponent<Image>();
            _instance._image.color = Color.black;
            _instance._image.raycastTarget = false;
            _instance.SetAlpha(0f, false);
            return _instance;
        }

        private void SetAlpha(float alpha, bool revealing)
        {
            _alpha = alpha;
            _revealing = revealing;
            _image.color = new Color(0f, 0f, 0f, alpha);
            _image.enabled = alpha > 0f;
        }

        private void Update()
        {
            if (!_revealing) return;
            float alpha = Mathf.MoveTowards(_alpha, 0f, Time.unscaledDeltaTime / RevealSeconds);
            SetAlpha(alpha, alpha > 0f);
        }
    }
}
