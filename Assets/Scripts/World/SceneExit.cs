using GameJam.Player;
using UnityEngine;

namespace GameJam.World
{
    // [F] at a doorway that leads to another scene (hospital gate ↔ hospital main door).
    // Once usable and the player is within preloadDistance, the target scene starts loading in the background so
    // going through is near instant.
    public class SceneExit : MonoBehaviour, IInteractable
    {
        [SerializeField] private string targetScene = "Abandoned_Hospital";
        [SerializeField] private string targetSpawn = "MainDoor";
        [SerializeField] private string label = "들어가기";
        [Tooltip("Item needed to use it (empty = always open).")]
        [SerializeField] private string requiredItem = "";
        [SerializeField] private string lockedLabel = "아직 나갈 수 없다";
        [Tooltip("Item that closes it for good (e.g. the evidence: no reason to go back into the hospital).")]
        [SerializeField] private string closedByItem = "";
        [SerializeField] private string closedLabel = "더 갈 필요 없다";
        [Tooltip("Start loading the target scene when the player is this close and the exit is usable (m).")]
        [SerializeField] private float preloadDistance = 40f;

        private PlayerInventory _player;

        public Vector3 InteractionPoint => transform.position + Vector3.up * 1.1f;

        private void OnEnable() => Interactables.All.Add(this);
        private void OnDisable() => Interactables.All.Remove(this);

        private void Start()
        {
            _player = FindFirstObjectByType<PlayerInventory>();
        }

        private void Update()
        {
            if (_player == null || !IsUsable(_player)) return;
            if ((_player.transform.position - transform.position).sqrMagnitude > preloadDistance * preloadDistance) return;
            GameProgress.Preload(targetScene);
        }

        private bool IsLocked(PlayerInventory inventory) =>
            !string.IsNullOrEmpty(requiredItem) && (inventory == null || !inventory.Has(requiredItem));

        private bool IsClosed(PlayerInventory inventory) =>
            !string.IsNullOrEmpty(closedByItem) && inventory != null && inventory.Has(closedByItem);

        private bool IsUsable(PlayerInventory inventory) => !IsLocked(inventory) && !IsClosed(inventory);

        public string GetLabel(GameObject user)
        {
            var inventory = user != null ? user.GetComponent<PlayerInventory>() : null;
            if (IsClosed(inventory)) return closedLabel;
            return IsLocked(inventory) ? lockedLabel : label;
        }

        public void Interact(GameObject user)
        {
            if (!IsUsable(user != null ? user.GetComponent<PlayerInventory>() : null)) return;
            GameProgress.TravelTo(targetScene, targetSpawn);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1.1f, new Vector3(1.6f, 2.2f, 0.3f));
        }
    }
}
