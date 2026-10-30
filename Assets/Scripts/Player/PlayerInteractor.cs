using GameJam.Combat;
using GameJam.UI;
using GameJam.World;
using UnityEngine;

namespace GameJam.Player
{
    // Finds the closest interactable (door, pickup) within reach and uses it on Interact (F).
    // Shows the key prompt meanwhile.
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private float reach = 2f;
        [Tooltip("Max height difference so things on the floor above/below don't count.")]
        [SerializeField] private float maxHeightDifference = 1.5f;

        private Health _health;
        private PlayerNoise _noise;
        private InteractionPrompt _prompt;
        private IInteractable _focused;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _noise = GetComponent<PlayerNoise>();
            _prompt = InteractionPrompt.Create();
        }

        private void OnDestroy()
        {
            if (_prompt != null) Destroy(_prompt.gameObject);
        }

        private void Update()
        {
            _focused = _health != null && _health.IsDead ? null : FindClosest();

            if (_focused == null)
            {
                _prompt.Hide();
                return;
            }

            _prompt.Show("F", _focused.GetLabel(gameObject));
        }

        private IInteractable FindClosest()
        {
            Vector3 body = transform.position + Vector3.up;
            IInteractable closest = null;
            float best = reach * reach;

            foreach (var interactable in Interactables.All)
            {
                Vector3 offset = interactable.InteractionPoint - body;
                if (Mathf.Abs(offset.y) > maxHeightDifference) continue;

                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance >= best) continue;

                best = distance;
                closest = interactable;
            }

            return closest;
        }

        // Sent by PlayerInput (Send Messages) for the Interact action.
        public void OnInteract()
        {
            if (_focused == null) return;

            if (_focused is Door door)
            {
                if (door.IsLockedFor(gameObject)) return;
                if (_noise != null) _noise.EmitDoor(door.Center);
            }
            _focused.Interact(gameObject);
        }
    }
}
