using GameJam.Player;
using UnityEngine;

namespace GameJam.World
{
    // The end of the run: [F] at the getaway car once the evidence is in hand completes the mission.
    public class MissionGoal : MonoBehaviour, IInteractable
    {
        [SerializeField] private string requiredItem = "evidence";
        [SerializeField] private string label = "차에 타고 탈출";
        [SerializeField] private string lockedLabel = "장부를 가져와야 한다";

        public Vector3 InteractionPoint => transform.position;

        private void OnEnable() => Interactables.All.Add(this);
        private void OnDisable() => Interactables.All.Remove(this);

        private bool IsLocked(GameObject user)
        {
            var inventory = user != null ? user.GetComponent<PlayerInventory>() : null;
            return inventory == null || !inventory.Has(requiredItem);
        }

        public string GetLabel(GameObject user) => IsLocked(user) ? lockedLabel : label;

        public void Interact(GameObject user)
        {
            if (IsLocked(user)) return;
            var mission = FindFirstObjectByType<Mission>();
            if (mission != null) mission.Complete();
        }
    }
}
