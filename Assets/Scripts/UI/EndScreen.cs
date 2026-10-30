using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam.UI
{
    // Full-screen result screens: the screen fades to black, then a big logo fades in.
    //   Death   — red "사망" / "작전 실패", then the scene restarts from its checkpoint.
    //   Success — white "임무 완료" / "장부를 확보했다", then back to the title.
    public class EndScreen : MonoBehaviour
    {
        private Image _black;
        private Text _logo;
        private Text _sub;
        private Image _line;

        public static void ShowDeath(Action then) => Show("사망", "작전 실패", UIKit.Accent, 1.1f, 3.2f, then);

        public static void ShowMissionComplete(Action then) =>
            Show("임무 완료", "장부를 확보했다", UIKit.Text, 0.9f, 5f, then);

        public static bool IsShowing => _current != null;

        private static EndScreen _current;

        private static void Show(string logo, string sub, Color color, float fade, float hold, Action then)
        {
            var screen = Create();
            screen.StopAllCoroutines();
            screen.StartCoroutine(screen.Run(logo, sub, color, fade, hold, then));
        }

        private static EndScreen Create()
        {
            if (_current != null) return _current;

            var canvas = UIKit.CreateCanvas("EndScreen", 500);
            var screen = canvas.gameObject.AddComponent<EndScreen>();
            screen._black = UIKit.Fill("Black", canvas.transform, new Color(0f, 0f, 0f, 0f));

            screen._logo = UIKit.Label("Logo", canvas.transform, "", 150, Color.clear, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Center(screen._logo.rectTransform, new Vector2(1600f, 220f), new Vector2(0f, 60f));

            screen._line = UIKit.Fill("Line", canvas.transform, Color.clear);
            UIKit.Center(screen._line.rectTransform, new Vector2(520f, 4f), new Vector2(0f, -70f));

            screen._sub = UIKit.Label("Sub", canvas.transform, "", 40, Color.clear, TextAnchor.MiddleCenter);
            UIKit.Center(screen._sub.rectTransform, new Vector2(1200f, 70f), new Vector2(0f, -120f));

            _current = screen;
            return screen;
        }

        private IEnumerator Run(string logo, string sub, Color color, float fadeSeconds, float holdSeconds, Action then)
        {
            _logo.text = logo;
            _sub.text = sub;

            // Unscaled time: works even if the game is paused or slowed.
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _black.color = new Color(0f, 0f, 0f, t / fadeSeconds);
                yield return null;
            }
            _black.color = Color.black;

            for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
            {
                SetLogoAlpha(color, t / 0.8f);
                yield return null;
            }
            // A long frame can end the loop early: always finish fully visible.
            SetLogoAlpha(color, 1f);

            yield return new WaitForSecondsRealtime(holdSeconds);
            then?.Invoke();
        }

        private void SetLogoAlpha(Color color, float a)
        {
            _logo.color = new Color(color.r, color.g, color.b, a);
            _line.color = new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, a);
            _sub.color = new Color(UIKit.MutedText.r, UIKit.MutedText.g, UIKit.MutedText.b, a);
        }
    }
}
