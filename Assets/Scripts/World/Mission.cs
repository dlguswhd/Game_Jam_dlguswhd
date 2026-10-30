using System;
using GameJam.Player;
using GameJam.UI;
using UnityEngine;

namespace GameJam.World
{
    // One per level scene: shows the current objective top-left. The objective is the first step whose item the
    // player doesn't carry yet, then the final objective. Complete() (called by the getaway car) shows the
    // "임무 완료" screen and returns to the title.
    public class Mission : MonoBehaviour
    {
        [Serializable]
        public class Step
        {
            [TextArea] public string objective;
            [Tooltip("Item id that completes this step.")]
            public string itemId;
        }

        [SerializeField] private Step[] steps;
        [Tooltip("Shown once every step's item is carried.")]
        [SerializeField] private string extractObjective = "정문으로 탈출하라";
        private PlayerInventory _inventory;
        private MissionHUD _hud;
        private bool _complete;

        private void Start()
        {
            _inventory = FindFirstObjectByType<PlayerInventory>();
            _hud = MissionHUD.Create();
            if (_inventory != null) _inventory.ItemAdded += _ => Refresh();
            Refresh();
        }

        private void OnDestroy()
        {
            if (_hud != null) Destroy(_hud.gameObject);
        }

        private Step CurrentStep
        {
            get
            {
                foreach (var step in steps)
                {
                    if (_inventory == null || !_inventory.Has(step.itemId)) return step;
                }
                return null;
            }
        }

        private void Refresh()
        {
            if (_complete) return;
            var step = CurrentStep;
            _hud.SetObjective(step != null ? step.objective : extractObjective);
        }

        // Mission accomplished: freeze the player, show the "임무 완료" screen, then back to the title.
        public void Complete()
        {
            if (_complete) return;
            _complete = true;
            _hud.SetObjective("탈출 성공");

            var player = _inventory != null ? _inventory.gameObject : null;
            var input = player != null ? player.GetComponent<UnityEngine.InputSystem.PlayerInput>() : null;
            if (input != null) input.DeactivateInput();

            EndScreen.ShowMissionComplete(GameProgress.ReturnToTitle);
        }
    }
}
