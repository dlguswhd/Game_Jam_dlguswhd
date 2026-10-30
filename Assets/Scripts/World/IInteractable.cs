using System.Collections.Generic;
using UnityEngine;

namespace GameJam.World
{
    // Anything the player can use with Interact (F): doors, pickups.
    public interface IInteractable
    {
        // Where the prompt distance is measured from.
        Vector3 InteractionPoint { get; }
        // Text next to the [F] key, e.g. "문 밀기". The user is the player GameObject.
        string GetLabel(GameObject user);
        void Interact(GameObject user);
    }

    public static class Interactables
    {
        public static readonly List<IInteractable> All = new List<IInteractable>();
    }
}
