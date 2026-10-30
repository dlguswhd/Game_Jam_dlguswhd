using System;
using GameJam.World;
using UnityEngine;

namespace GameJam.Player
{
    // Mission items the player carries (keycard, evidence), by id. Stored in GameProgress so they survive
    // moving between the village and the hospital.
    public class PlayerInventory : MonoBehaviour
    {
        public event Action<string> ItemAdded;

        public bool Has(string id) => string.IsNullOrEmpty(id) || GameProgress.Items.Contains(id);

        public void Add(string id)
        {
            if (!GameProgress.Items.Add(id)) return;
            ItemAdded?.Invoke(id);
        }
    }
}
