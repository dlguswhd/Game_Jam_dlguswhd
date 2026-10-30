using GameJam.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.UI
{
    // Esc during play: freezes the game and shows 게임 재개 / 설정 / 바탕화면으로 나가기.
    // Created by SceneFlow in every level scene.
    public class PauseMenu : MonoBehaviour
    {
        private GameObject _root;
        private GameObject _menu;
        private GameObject _settings;
        private PlayerInput _input;
        private Health _playerHealth;
        private bool _paused;

        public static bool IsPaused { get; private set; }

        public static PauseMenu Create()
        {
            var canvas = UIKit.CreateCanvas("PauseMenu", 400);
            var menu = canvas.gameObject.AddComponent<PauseMenu>();
            menu.Build(canvas.transform);
            return menu;
        }

        private void Build(Transform root)
        {
            _root = UIKit.Fill("Dim", root, UIKit.Backdrop).gameObject;

            var menu = UIKit.RoundedPanel("Menu", _root.transform, UIKit.Panel);
            UIKit.Center(menu.rectTransform, new Vector2(560f, 420f));
            UIKit.Column(menu.rectTransform, 16f, 44);
            var title = UIKit.Label("Title", menu.transform, "일시정지", 44, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 80f;
            UIKit.Button(menu.transform, "게임 재개", Resume);
            UIKit.Button(menu.transform, "설정", () => ShowSettings(true));
            UIKit.Button(menu.transform, "바탕화면으로 나가기", TitleScreen.Quit);
            _menu = menu.gameObject;

            _settings = SettingsPanel.Create(_root.transform, () => ShowSettings(false));
            ShowSettings(false);
            _root.SetActive(false);
        }

        private void Start()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            _input = player.GetComponent<PlayerInput>();
            _playerHealth = player.GetComponent<Health>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (!_paused)
            {
                // No pausing over the death / mission complete screens.
                if (EndScreen.IsShowing || (_playerHealth != null && _playerHealth.IsDead)) return;
                Pause();
            }
            else if (_settings.activeSelf) ShowSettings(false);
            else Resume();
        }

        private void ShowSettings(bool show)
        {
            _settings.SetActive(show);
            _menu.SetActive(!show);
        }

        private void Pause()
        {
            _paused = IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (_input != null) _input.DeactivateInput();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowSettings(false);
            _root.SetActive(true);
        }

        private void Resume()
        {
            _paused = IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (_input != null) _input.ActivateInput();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            // Leaving the scene while paused (e.g. quitting) must not leave the game frozen.
            if (!_paused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
