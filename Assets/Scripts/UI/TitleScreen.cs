using GameJam.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameJam.UI
{
    // The Title scene: title + menu (작전 시작 / 설정 / 조작법 / 바탕화면으로 나가기). 작전 시작 opens the mission
    // briefing; Enter there starts the run in the first level.
    public class TitleScreen : MonoBehaviour
    {
        [SerializeField] private string gameTitle = "KILL OR DEAD : Black Ledger";
        [SerializeField] private string subtitle = "ST. MARROW HOSPITAL";

        [TextArea(6, 14)]
        [SerializeField] private string briefing =
            "도시 외곽, 폐쇄된 세인트 매로우 병원.\n" +
            "갱단이 이곳에서 불법 장기적출을 벌이고, 적출한 장기를 어딘가로 팔아넘기고 있다.\n\n" +
            "이 거래의 배후가 누구인지 밝혀낼 단서는 단 하나 — 그들의 장부다.\n\n" +
            "• 마을을 지나 병원 정문으로 잠입하라\n" +
            "• 2층 어딘가의 열쇠카드로 지하 시체안치소를 열고 장부를 회수하라\n" +
            "• 장부를 가지고 타고 온 차로 돌아오면 임무 완료\n\n" +
            "들키면 근처의 적이 모두 몰려온다. 조용히, 빠르게.";

        private GameObject _menu;
        private GameObject _settings;
        private GameObject _controls;
        private GameObject _briefing;
        private bool _starting;
        private Text _titleText;
        private float _titleWidth = -1f;

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var canvas = UIKit.CreateCanvas("TitleUI", 10);
            var root = canvas.transform;
            UIKit.Fill("Background", root, new Color(0.015f, 0.015f, 0.02f));
            // Dark red glow rising from the bottom edge, fading out upwards.
            var glow = UIKit.Fill("RedGlow", root, Color.white);
            glow.sprite = VerticalFade(new Color(0.4f, 0.03f, 0.03f, 0.45f));
            glow.rectTransform.anchorMin = new Vector2(0f, 0f);
            glow.rectTransform.anchorMax = new Vector2(1f, 0.6f);

            BuildMenu(root);
            _settings = SettingsPanel.Create(root, () => Show(_menu));
            _controls = BuildControls(root);
            _briefing = BuildBriefing(root);
            Show(_menu);
            Canvas.ForceUpdateCanvases();
            FitTitle();
            ScreenFade.Reveal();
        }

        private void Update()
        {
            FitTitle();
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (_starting) return;
            if (_briefing.activeSelf && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                // Once only: a second Enter during the fade would start loading the level again.
                _starting = true;
                GameProgress.StartRun();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame && !_menu.activeSelf)
                Show(_menu);
        }

        private void FitTitle()
        {
            if (_titleText == null) return;
            float width = _titleText.rectTransform.rect.width;
            if (width <= 0f || Mathf.Approximately(width, _titleWidth)) return;
            _titleWidth = width;
            var settings = _titleText.GetGenerationSettings(Vector2.zero);
            settings.fontSize = 90;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            float preferred = _titleText.cachedTextGeneratorForLayout.GetPreferredWidth(_titleText.text, settings)
                              / _titleText.pixelsPerUnit;
            _titleText.fontSize = Mathf.Clamp(Mathf.FloorToInt(90f * width / Mathf.Max(1f, preferred)), 24, 90);
        }

        // A 1×64 sprite going from the colour at the bottom to transparent at the top (eased, no visible edge).
        private static Sprite VerticalFade(Color color)
        {
            const int height = 64;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < height; y++)
            {
                float t = 1f - y / (height - 1f);
                texture.SetPixel(0, y, new Color(color.r, color.g, color.b, color.a * t * t));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f));
        }

        private void Show(GameObject panel)
        {
            _menu.SetActive(panel == _menu);
            _settings.SetActive(panel == _settings);
            _controls.SetActive(panel == _controls);
            _briefing.SetActive(panel == _briefing);
        }

        private void BuildMenu(Transform root)
        {
            var menu = UIKit.Rect("Menu", root);
            UIKit.Stretch(menu);
            _menu = menu.gameObject;

            var title = UIKit.Label("Title", menu, gameTitle, 90, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            _titleText = title;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -200f);
            title.rectTransform.sizeDelta = new Vector2(-280f, 160f);

            var line = UIKit.Fill("Line", menu, UIKit.Accent);
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0f, 1f);
            line.rectTransform.pivot = new Vector2(0f, 1f);
            line.rectTransform.anchoredPosition = new Vector2(144f, -370f);
            line.rectTransform.sizeDelta = new Vector2(420f, 4f);

            var sub = UIKit.Label("Subtitle", menu, subtitle, 30, UIKit.MutedText, TextAnchor.MiddleLeft);
            sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0f, 1f);
            sub.rectTransform.pivot = new Vector2(0f, 1f);
            sub.rectTransform.anchoredPosition = new Vector2(144f, -390f);
            sub.rectTransform.sizeDelta = new Vector2(900f, 50f);

            var buttons = UIKit.Rect("Buttons", menu);
            buttons.anchorMin = buttons.anchorMax = new Vector2(0f, 0f);
            buttons.pivot = new Vector2(0f, 0f);
            buttons.anchoredPosition = new Vector2(140f, 140f);
            buttons.sizeDelta = new Vector2(440f, 340f);
            UIKit.Column(buttons, 14f);
            UIKit.Button(buttons, "작전 시작", () => Show(_briefing));
            UIKit.Button(buttons, "설정", () => Show(_settings));
            UIKit.Button(buttons, "조작법", () => Show(_controls));
            UIKit.Button(buttons, "바탕화면으로 나가기", Quit);
        }

        private GameObject BuildBriefing(Transform root)
        {
            var window = UIKit.RoundedPanel("Briefing", root, UIKit.Panel);
            UIKit.Center(window.rectTransform, new Vector2(1100f, 760f));

            var head = UIKit.Label("Head", window.transform, "작전 브리핑", 26, UIKit.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            head.rectTransform.anchorMin = new Vector2(0f, 1f);
            head.rectTransform.anchorMax = new Vector2(1f, 1f);
            head.rectTransform.pivot = new Vector2(0.5f, 1f);
            head.rectTransform.offsetMin = new Vector2(60f, -110f);
            head.rectTransform.offsetMax = new Vector2(-60f, -50f);

            var body = UIKit.Label("Body", window.transform, briefing, 30, UIKit.Text, TextAnchor.UpperLeft);
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(60f, 130f);
            body.rectTransform.offsetMax = new Vector2(-60f, -120f);

            var hint = UIKit.Label("Hint", window.transform, "Enter  —  작전 개시          Esc  —  돌아가기", 26,
                UIKit.MutedText, TextAnchor.MiddleCenter);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.offsetMin = new Vector2(60f, 50f);
            hint.rectTransform.offsetMax = new Vector2(-60f, 100f);
            hint.gameObject.AddComponent<BlinkText>();
            return window.gameObject;
        }

        private GameObject BuildControls(Transform root)
        {
            var window = UIKit.RoundedPanel("Controls", root, UIKit.Panel);
            UIKit.Center(window.rectTransform, new Vector2(1100f, 920f));
            var column = window.rectTransform;
            UIKit.Column(column, 4f, 44);

            var title = UIKit.Label("Title", column, "조작법", 42, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;

            (string key, string action)[] rows =
            {
                ("WASD", "이동"), ("Shift", "달리기 / 조준 중 추가 줌"), ("Ctrl", "천천히 걷기 (소리 작음)"),
                ("마우스 우클릭", "조준"), ("마우스 좌클릭", "사격"), ("R", "장전"), ("C / 휠", "무기 전환"),
                ("F / 문 앞 휠", "상호작용 / 문 열기·내시경 선택"), ("내시경", "A·D 좌우 이동 / 마우스 시점 / F 접기"),
                ("Q / E", "왼쪽 / 오른쪽 기울이기 (토글)"),
                ("Tab", "조준경 전환 (45° 레드닷)"), ("Y / T", "라이트 / 레이저"), ("Esc", "일시정지 메뉴"),
            };
            foreach (var (key, action) in rows)
            {
                var row = UIKit.Rect("Row", column);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
                var k = UIKit.Label("Key", row, key, 26, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
                k.rectTransform.anchorMin = Vector2.zero;
                k.rectTransform.anchorMax = new Vector2(0.32f, 1f);
                k.rectTransform.offsetMin = k.rectTransform.offsetMax = Vector2.zero;
                var a = UIKit.Label("Action", row, action, 26, UIKit.Text, TextAnchor.MiddleLeft);
                a.rectTransform.anchorMin = new Vector2(0.34f, 0f);
                a.rectTransform.anchorMax = Vector2.one;
                a.rectTransform.offsetMin = a.rectTransform.offsetMax = Vector2.zero;
            }

            var spacer = UIKit.Rect("Spacer", column);
            spacer.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;
            UIKit.Button(column, "돌아가기", () => Show(_menu));
            return window.gameObject;
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    // Slow fade in and out, for "press Enter" style hints.
    public class BlinkText : MonoBehaviour
    {
        private Text _text;
        private void Awake() => _text = GetComponent<Text>();

        private void Update()
        {
            var c = _text.color;
            c.a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f));
            _text.color = c;
        }
    }
}
