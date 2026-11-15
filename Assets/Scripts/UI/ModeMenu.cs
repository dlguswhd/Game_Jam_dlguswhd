using System;
using System.Collections.Generic;
using GameJam.Combat;
using GameJam.Player;
using GameJam.World;
using KINEMATION.TacticalShooterPack.Scripts.Attachments;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameJam.UI
{
    // Tab during play: the test mode menu. Clicking a mode toggles it; every change is announced bottom-right
    // for a few seconds. The game is paused while the menu is open (like the Esc menu).
    // Lives on the Player; runs after TacticalShooterPlayer/PlayerGunplay so the weapons already exist.
    [DefaultExecutionOrder(150)]
    public class ModeMenu : MonoBehaviour
    {
        private const float NoticeSeconds = 5f;
        private const float NoticeFade = 0.5f;

        private class Toggle
        {
            public string label;
            public Func<bool> get;
            public Action<bool> set;
            public Text text;
        }

        private class Notice
        {
            public Text text;
            public float expiresAt;
        }

        private readonly List<Toggle> _toggles = new List<Toggle>();
        private readonly List<Notice> _notices = new List<Notice>();

        private GameObject _root;
        private RectTransform _noticeArea;
        private PlayerInput _input;
        private Health _health;
        private bool _open;
        private int _closedFrame = -1;

        public static bool IsOpen { get; private set; }

        // True while the menu is open and on the frame it was closed with Esc, so the pause menu doesn't
        // open from that same key press.
        public static bool BlocksEscape { get; private set; }

        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            _health = GetComponent<Health>();
            Build();
        }

        private void Start()
        {
            // Attachment modes persist across scenes; put them back on the freshly spawned weapon.
            ApplyAttachments();

            // Level scenes get their Esc pause menu from SceneFlow; test scenes (Test_map, Prototype_Play_Map) don't
            // have one, so make it here.
            if (FindAnyObjectByType<PauseMenu>() == null) PauseMenu.Create();
        }

        private void OnDestroy()
        {
            // Leaving the scene with the menu open (e.g. 게임 리셋, quitting): unfreeze time only. The PlayerInput
            // is being torn down too, so it can't (and needn't) be reactivated.
            if (_open)
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
            IsOpen = BlocksEscape = false;
            if (_root != null) Destroy(_root.transform.root.gameObject);
        }

        // ------------------------------------------------------------------ UI

        private void Build()
        {
            var canvas = UIKit.CreateCanvas("ModeMenu", 350);
            var root = canvas.transform;

            _root = UIKit.Fill("Dim", root, new Color(0f, 0f, 0f, 0.55f)).gameObject;
            var panel = UIKit.RoundedPanel("Panel", _root.transform, UIKit.Panel);
            UIKit.Center(panel.rectTransform, new Vector2(620f, 980f));
            var column = panel.rectTransform;
            UIKit.Column(column, 6f, 32);

            Header(column, "모드 설정", 40);
            Section(column, "모드");
            AddToggle(column, "총알 무한", () => CheatModes.InfiniteAmmo, v =>
            {
                CheatModes.InfiniteAmmo = v;
                if (v) RefillAmmo();
            });
            AddToggle(column, "무적", () => CheatModes.GodMode, v => CheatModes.GodMode = v);
            AddToggle(column, "유령 모드", () => CheatModes.Ghost, v => CheatModes.Ghost = v);
            AddToggle(column, "반동 없음", () => CheatModes.NoRecoil, v => CheatModes.NoRecoil = v);
            AddToggle(column, "플레이어 발소리", () => CheatModes.FootstepSound, v => CheatModes.FootstepSound = v);
            AddToggle(column, "총 사운드", () => CheatModes.GunSound, v => CheatModes.GunSound = v);

            Section(column, "부착물");
            AddToggle(column, "소음기", () => CheatModes.Suppressor, v =>
            {
                CheatModes.Suppressor = v;
                ApplyAttachments();
            });
            AddToggle(column, "홀로그램 조준경", () => CheatModes.HoloSight, v =>
            {
                CheatModes.HoloSight = v;
                ApplyAttachments();
            });
            AddToggle(column, "수직 손잡이", () => CheatModes.VerticalGrip, v =>
            {
                CheatModes.VerticalGrip = v;
                ApplyAttachments();
            });

            Spacer(column, 10f);
            UIKit.Button(column, "적 의심도 초기화", ResetEnemyAlert, 56f);
            UIKit.Button(column, "게임 리셋", ResetGame, 56f);
            var hint = UIKit.Label("Hint", column, "Tab / Esc  —  닫기", 22, UIKit.MutedText, TextAnchor.MiddleCenter);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

            // Bottom-right notices, newest at the bottom. Stays visible after the menu closes.
            _noticeArea = UIKit.Rect("Notices", root);
            _noticeArea.anchorMin = _noticeArea.anchorMax = new Vector2(1f, 0f);
            _noticeArea.pivot = new Vector2(1f, 0f);
            _noticeArea.anchoredPosition = new Vector2(-40f, 40f);
            _noticeArea.sizeDelta = new Vector2(520f, 400f);
            var layout = _noticeArea.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerRight;
            layout.spacing = 6f;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = layout.childForceExpandWidth = false;

            _root.SetActive(false);
        }

        private static void Header(RectTransform column, string text, int size)
        {
            var label = UIKit.Label("Title", column, text, size, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
        }

        private static void Section(RectTransform column, string text)
        {
            var label = UIKit.Label("Section_" + text, column, text, 24, UIKit.Accent, TextAnchor.LowerLeft, FontStyle.Bold);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        }

        private static void Spacer(RectTransform column, float height)
        {
            UIKit.Rect("Spacer", column).gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        private void AddToggle(RectTransform column, string label, Func<bool> get, Action<bool> set)
        {
            var toggle = new Toggle { label = label, get = get, set = set };
            var button = UIKit.Button(column, label, () =>
            {
                bool value = !toggle.get();
                toggle.set(value);
                Refresh(toggle);
                Notify($"{toggle.label} {(value ? "활성화" : "비활성화")}", value);
            }, 52f);
            toggle.text = button.GetComponentInChildren<Text>();
            _toggles.Add(toggle);
            Refresh(toggle);
        }

        private static void Refresh(Toggle toggle)
        {
            bool on = toggle.get();
            toggle.text.text = $"{toggle.label}    {(on ? "ON" : "OFF")}";
            toggle.text.color = on ? new Color(1f, 0.45f, 0.4f) : UIKit.Text;
        }

        private void Notify(string message, bool on)
        {
            var text = UIKit.Label("Notice", _noticeArea, message, 30,
                on ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleRight, FontStyle.Bold);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            _notices.Add(new Notice { text = text, expiresAt = Time.unscaledTime + NoticeSeconds });
        }

        // ------------------------------------------------------------------ open / close

        private void Update()
        {
            UpdateNotices();
            BlocksEscape = _open || _closedFrame == Time.frameCount;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (_open)
            {
                if (keyboard.tabKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame) Close();
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame && CanOpen()) Open();
        }

        private bool CanOpen()
        {
            if (PauseMenu.IsPaused || EndScreen.IsShowing) return false;
            if (_health != null && _health.IsDead) return false;
            return true;
        }

        private void Open()
        {
            _open = IsOpen = BlocksEscape = true;
            foreach (var toggle in _toggles) Refresh(toggle);
            SetPaused(true);
            _root.SetActive(true);
        }

        private void Close()
        {
            _open = IsOpen = false;
            _closedFrame = Time.frameCount;
            _root.SetActive(false);
            SetPaused(false);
        }

        private void SetPaused(bool paused)
        {
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (_input != null && _input.isActiveAndEnabled)
            {
                if (paused) _input.DeactivateInput();
                else _input.ActivateInput();
            }
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }

        private void UpdateNotices()
        {
            float now = Time.unscaledTime;
            for (int i = _notices.Count - 1; i >= 0; i--)
            {
                var notice = _notices[i];
                float left = notice.expiresAt - now;
                if (left <= 0f || notice.text == null)
                {
                    if (notice.text != null) Destroy(notice.text.gameObject);
                    _notices.RemoveAt(i);
                    continue;
                }
                var c = notice.text.color;
                c.a = Mathf.Clamp01(left / NoticeFade);
                notice.text.color = c;
            }
        }

        // ------------------------------------------------------------------ actions

        // Every enemy back to calm patrol (green): suspicion 0, out of combat, forgets where the player was.
        private void ResetEnemyAlert()
        {
            foreach (var enemy in GameJam.Enemies.EnemyAI.All) enemy.ResetAlert();
            Notify("적 의심도 초기화", true);
        }

        private void ResetGame()
        {
            Close();
            GameProgress.ResetCurrentScene();
        }

        private void RefillAmmo()
        {
            foreach (var weapon in GetComponentsInChildren<TacticalShooterWeapon>(true)) weapon.ReloadWeapon();
        }

        // Index 0 of each AK105 attachment group is "nothing", index 1 the attachment (see W_AK105_Player).
        private void ApplyAttachments()
        {
            foreach (var weapon in GetComponentsInChildren<TacticalShooterWeapon>(true))
            {
                if (!weapon.name.Contains("AK105") || weapon.Attachments == null) continue;
                weapon.Attachments.SelectAttachment(AttachmentType.Muzzle, CheatModes.Suppressor ? 1 : 0);
                weapon.Attachments.SelectAttachment(AttachmentType.Scope, CheatModes.HoloSight ? 1 : 0);
                weapon.Attachments.SelectAttachment(AttachmentType.Grip, CheatModes.VerticalGrip ? 1 : 0);
            }
        }
    }
}
