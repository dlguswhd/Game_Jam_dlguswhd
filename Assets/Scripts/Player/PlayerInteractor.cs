using GameJam.Combat;
using GameJam.UI;
using GameJam.World;
using UnityEngine;
using UnityEngine.InputSystem;

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
        private DoorCamera _doorCamera;
        private int _selectedAction;

        public int SelectedDoorAction => _selectedAction;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _noise = GetComponent<PlayerNoise>();
            _prompt = InteractionPrompt.Create();
            _doorCamera = gameObject.AddComponent<DoorCamera>();
        }

        private void OnDestroy()
        {
            if (_prompt != null) Destroy(_prompt.gameObject);
        }

        private void Update()
        {
            if (PauseMenu.IsPaused || EndScreen.IsShowing || _doorCamera.IsViewing)
            {
                _prompt.Hide();
                return;
            }
            RefreshFocus();

            if (_focused == null)
            {
                _prompt.Hide();
                return;
            }

            if (_focused is Door door)
            {
                if (door.IsOpen) _selectedAction = 0;
                _prompt.ShowDoor(door.GetLabel(gameObject), _selectedAction, !door.IsOpen);
            }
            else _prompt.Show("F", _focused.GetLabel(gameObject));
        }

        private void RefreshFocus()
        {
            var next = _health != null && _health.IsDead ? null : FindClosest();
            if (!ReferenceEquals(next, _focused)) _selectedAction = 0;
            _focused = next;
        }

        // Called before the weapon pack consumes wheel input, including on the first frame in reach.
        public bool TrySelectDoorAction(float scroll)
        {
            if (_doorCamera.IsViewing || PauseMenu.IsPaused || EndScreen.IsShowing) return true;
            RefreshFocus();
            if (!(_focused is Door door)) return false;
            if (!door.IsOpen && Mathf.Abs(scroll) > 0.01f) _selectedAction = 1 - _selectedAction;
            return true;
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
        public void OnInteract(InputValue value)
        {
            if (value.isPressed) UseFocused();
        }

        public void UseFocused()
        {
            if (PauseMenu.IsPaused || EndScreen.IsShowing || (_health != null && _health.IsDead)) return;
            if (_doorCamera.IsViewing)
            {
                _doorCamera.Close();
                return;
            }
            RefreshFocus();
            if (_focused == null) return;

            if (_focused is Door door)
            {
                if (_selectedAction == 1 && !door.IsOpen)
                {
                    _doorCamera.Open(door);
                    return;
                }
                if (door.IsLockedFor(gameObject)) return;
                if (_noise != null) _noise.EmitDoor(door.Center);
            }
            _focused.Interact(gameObject);
        }
    }
}
