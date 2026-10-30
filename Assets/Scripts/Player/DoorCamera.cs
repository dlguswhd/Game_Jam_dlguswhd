using System.Collections.Generic;
using GameJam.Combat;
using GameJam.UI;
using GameJam.World;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GameJam.Player
{
    // A separate low camera beyond the closed door. Never moves the player's body through the door.
    public class DoorCamera : MonoBehaviour
    {
        private Door _door;
        private Camera _camera;
        private RenderTexture _texture;
        private GameObject _overlay;
        private Quaternion _baseRotation;
        private Vector2 _look;
        private Vector3 _probeCenter;
        private float _slide;
        private Material _feedMaterial;
        private PlayerInput _input;
        private PlayerMovement _movement;
        private TacticalShooterPlayer _shooter;
        private Health _health;
        private bool _wasMoving;
        private bool _wasShooting;
        private readonly List<InputAction> _disabledActions = new List<InputAction>();

        public bool IsViewing => _camera != null;

        public void Open(Door door)
        {
            if (IsViewing || door == null || door.IsOpen || PauseMenu.IsPaused) return;
            _door = door;
            _input = GetComponent<PlayerInput>();
            _movement = GetComponent<PlayerMovement>();
            _shooter = GetComponent<TacticalShooterPlayer>();
            _health = GetComponent<Health>();
            _wasMoving = _movement != null && _movement.enabled;
            _wasShooting = _shooter != null && _shooter.enabled;
            foreach (var weapon in GetComponentsInChildren<TacticalShooterWeapon>(true)) weapon.StopFiring();
            // Leave only F active. This also blocks fire, weapon changes and movement callbacks.
            _disabledActions.Clear();
            if (_input != null)
                foreach (var action in _input.actions)
                    if (action.enabled && action.name != "Interact") _disabledActions.Add(action);
            foreach (var action in _disabledActions) action.Disable();
            if (_movement != null) _movement.enabled = false;
            if (_shooter != null) _shooter.enabled = false;

            var pose = door.CameraPose(transform.position);
            _baseRotation = pose.rotation;
            _probeCenter = pose.position;
            _slide = 0f;
            _look = Vector2.zero;
            _camera = new GameObject("EndoscopeCamera").AddComponent<Camera>();
            var main = Camera.main;
            if (main != null) _camera.CopyFrom(main);
            _camera.tag = "Untagged";
            _camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            _camera.nearClipPlane = 0.02f;
            _camera.farClipPlane = 60f;
            _camera.fieldOfView = 95f;
            _camera.depth = -10f;
            var data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.renderShadows = false;
            _texture = new RenderTexture(640, 360, 16) { name = "EndoscopeFeed", filterMode = FilterMode.Bilinear };
            _texture.Create();
            _camera.targetTexture = _texture;

            var canvas = UIKit.CreateCanvas("EndoscopeUI", 150);
            _overlay = canvas.gameObject;
            UIKit.Fill("Background", canvas.transform, new Color(0.005f, 0.008f, 0.007f));
            var frame = UIKit.Rect("Feed", canvas.transform);
            UIKit.Center(frame, new Vector2(1600f, 900f));
            var image = frame.gameObject.AddComponent<RawImage>();
            image.texture = _texture;
            var feedShader = Resources.Load<Shader>("EndoscopeFeed");
            if (feedShader != null) image.material = _feedMaterial = new Material(feedShader);
            image.color = new Color(0.82f, 1f, 0.9f);
            image.raycastTarget = false;
            var label = UIKit.Label("Status", canvas.transform, "●  내시경 카메라     |     A / D — 좌우 이동     마우스 — 둘러보기     [F] 접기", 26,
                new Color(0.6f, 0.9f, 0.72f));
            UIKit.Center(label.rectTransform, new Vector2(1500f, 60f), new Vector2(0f, -500f));
            var cross = UIKit.Label("Reticle", canvas.transform, "+", 32, new Color(0.7f, 1f, 0.8f, 0.5f));
            UIKit.Center(cross.rectTransform, new Vector2(40f, 40f));
        }

        private void Update()
        {
            if (!IsViewing) return;
            if (_door == null || !_door.isActiveAndEnabled || _door.IsOpen || (_health != null && _health.IsDead) ||
                PauseMenu.IsPaused || EndScreen.IsShowing || Vector3.Distance(transform.position, _door.Center) > 3f)
            {
                Close();
                return;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float input = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                float slide = Mathf.Clamp(_slide + input * 0.4f * Time.deltaTime, -0.32f, 0.32f);
                Vector3 target = _probeCenter + (_baseRotation * Vector3.right) * slide;
                Vector3 delta = target - _camera.transform.position;
                if (delta.sqrMagnitude < 0.000001f || !Physics.SphereCast(_camera.transform.position, 0.035f,
                        delta.normalized, out _, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    _slide = slide;
                    _camera.transform.position = target;
                }
            }
            if (Mouse.current == null) return;
            _look += Mouse.current.delta.ReadValue() * (0.08f * GameSettings.Sensitivity);
            _look.x = Mathf.Clamp(_look.x, -75f, 75f);
            _look.y = Mathf.Clamp(_look.y, -40f, 55f);
            _camera.transform.rotation = _baseRotation * Quaternion.Euler(-_look.y, _look.x, 0f);
        }

        public void Close()
        {
            bool wasViewing = _door != null || _camera != null;
            _door = null;
            if (_camera != null)
            {
                _camera.targetTexture = null;
                Destroy(_camera.gameObject);
                _camera = null;
            }
            if (_overlay != null) Destroy(_overlay);
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
            if (_feedMaterial != null) Destroy(_feedMaterial);
            if (!wasViewing) return;
            bool alive = _health == null || !_health.IsDead;
            if (_movement != null && alive) _movement.enabled = _wasMoving;
            if (_shooter != null && alive) _shooter.enabled = _wasShooting;
            if (alive && !PauseMenu.IsPaused && !EndScreen.IsShowing && _input != null && _input.isActiveAndEnabled)
                foreach (var action in _disabledActions) action.Enable();
            _disabledActions.Clear();
        }

        private void OnDisable() => Close();
    }
}
