using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Player
{
    // Mission items the player carries (keycard, evidence), by id.
    public class PlayerInventory : MonoBehaviour
    {
        private readonly HashSet<string> _items = new HashSet<string>();

        public event Action<string> ItemAdded;

        public bool Has(string id) => string.IsNullOrEmpty(id) || _items.Contains(id);

        public void Add(string id)
        {
            if (!_items.Add(id)) return;
            ItemAdded?.Invoke(id);
        }
    }
}
