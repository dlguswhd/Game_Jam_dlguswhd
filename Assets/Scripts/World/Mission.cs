using System;
using GameJam.Player;
using GameJam.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.World
{
    // The level's objective chain: collect each item in order, then reach the exit.
    // Shows the current objective top-left; on success shows "임무 완료" and restarts the scene.
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
        [SerializeField] private string extractObjective = "정문으로 탈출하라";
        [SerializeField] private Transform extractionPoint;
        [SerializeField] private float extractionRadius = 1.5f;
        [SerializeField] private float restartDelay = 6f;

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

        private bool AllItemsCollected => CurrentStep == null;

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

        private void Update()
        {
            if (_complete || _inventory == null || extractionPoint == null || !AllItemsCollected) return;

            Vector3 offset = _inventory.transform.position - extractionPoint.position;
            if (Mathf.Abs(offset.y) > 1.5f) return;
            offset.y = 0f;
            if (offset.magnitude > extractionRadius) return;

            _complete = true;
            _hud.SetObjective("탈출 성공");
            _hud.ShowBanner("임무 완료");
            Invoke(nameof(Restart), restartDelay);
        }

        private void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        private void OnDrawGizmos()
        {
            if (extractionPoint == null) return;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(extractionPoint.position, extractionRadius);
        }
    }
}
