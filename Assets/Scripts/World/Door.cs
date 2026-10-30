using System;
using System.Collections.Generic;
using GameJam.Player;
using UnityEngine;
using UnityEngine.AI;

namespace GameJam.World
{
    // A hinged door (single or double). Each leaf swings 90° around its hinge, always away from whoever opens it.
    // Leaves are set up by Tools → Game Jam → Setup Asylum Doors; everything is in the door root's local space.
    public class Door : MonoBehaviour, IInteractable
    {
        [Serializable]
        public class Leaf
        {
            [Tooltip("Door panel plus anything that swings with it (e.g. its window).")]
            public Transform[] parts;
            public Vector3 hinge;
            [Tooltip("Middle of the closed leaf, used to pick the swing direction.")]
            public Vector3 center;
            [HideInInspector] public Vector3[] closedPositions;
            [HideInInspector] public Quaternion[] closedRotations;
        }

        public static readonly List<Door> All = new List<Door>();

        [SerializeField] private Leaf[] leaves;
        [Tooltip("Item id the player needs to open it (empty = unlocked). Enemies can always open it.")]
        [SerializeField] private string requiredItem = "";
        [SerializeField] private string lockedLabel = "잠김 · 열쇠카드 필요";
        [SerializeField] private float openAngle = 90f;
        [Tooltip("How quickly the leaves ease toward the target angle.")]
        [SerializeField] private float swingSharpness = 10f;

        private float[] _angles;
        private float[] _targets;
        // The NavMesh is baked without door leaves (so paths run through doorways); an open leaf sticks out
        // into the room, so it carves itself out of the NavMesh while it rests open.
        private NavMeshObstacle[] _obstacles;

        public bool IsOpen { get; private set; }

        // World-space middle of the doorway, for proximity checks.
        public Vector3 Center => transform.position + Vector3.up * 1.1f;

        public void Configure(Leaf[] newLeaves)
        {
            leaves = newLeaves;
            foreach (var leaf in leaves)
            {
                leaf.closedPositions = new Vector3[leaf.parts.Length];
                leaf.closedRotations = new Quaternion[leaf.parts.Length];
                for (int i = 0; i < leaf.parts.Length; i++)
                {
                    leaf.closedPositions[i] = leaf.parts[i].localPosition;
                    leaf.closedRotations[i] = leaf.parts[i].localRotation;
                }
            }
        }

        private void Awake()
        {
            _angles = new float[leaves.Length];
            _targets = new float[leaves.Length];

            _obstacles = new NavMeshObstacle[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
            {
                var panel = leaves[i].parts[0];
                var mesh = panel.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) continue;

                var obstacle = panel.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = mesh.sharedMesh.bounds.center;
                obstacle.size = mesh.sharedMesh.bounds.size;
                obstacle.carving = true;
                obstacle.enabled = false;
                _obstacles[i] = obstacle;
            }
        }

        private void OnEnable()
        {
            All.Add(this);
            Interactables.All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
            Interactables.All.Remove(this);
        }

        public Vector3 InteractionPoint => Center;

        public bool IsLockedFor(GameObject user)
        {
            if (string.IsNullOrEmpty(requiredItem)) return false;
            var inventory = user != null ? user.GetComponent<PlayerInventory>() : null;
            return inventory == null || !inventory.Has(requiredItem);
        }

        public string GetLabel(GameObject user)
        {
            if (IsLockedFor(user)) return lockedLabel;
            return IsOpen ? "문 닫기" : "문 밀기";
        }

        public void Interact(GameObject user)
        {
            if (IsLockedFor(user)) return;
            Toggle(user.transform.position);
        }

        public void Toggle(Vector3 userPosition)
        {
            IsOpen = !IsOpen;

            Vector3 user = transform.InverseTransformPoint(userPosition);
            for (int i = 0; i < leaves.Length; i++)
            {
                if (!IsOpen)
                {
                    _targets[i] = 0f;
                    continue;
                }

                // Pick the swing whose free edge ends up on the far side of the doorway from the user.
                Leaf leaf = leaves[i];
                Vector3 swung = Quaternion.Euler(0f, openAngle, 0f) * (leaf.center - leaf.hinge);
                bool positiveGoesAway = Mathf.Sign(swung.z) != Mathf.Sign(user.z - leaf.hinge.z);
                _targets[i] = positiveGoesAway ? openAngle : -openAngle;
            }
        }

        private void Update()
        {
            for (int i = 0; i < leaves.Length; i++)
            {
                bool settled = Mathf.Approximately(_angles[i], _targets[i]);
                if (_obstacles[i] != null) _obstacles[i].enabled = settled && IsOpen;
                if (settled) continue;

                float t = 1f - Mathf.Exp(-swingSharpness * Time.deltaTime);
                _angles[i] = Mathf.Lerp(_angles[i], _targets[i], t);
                if (Mathf.Abs(_angles[i] - _targets[i]) < 0.1f) _angles[i] = _targets[i];

                ApplyAngle(leaves[i], _angles[i]);
            }
        }

        private static void ApplyAngle(Leaf leaf, float angle)
        {
            Quaternion swing = Quaternion.Euler(0f, angle, 0f);
            for (int i = 0; i < leaf.parts.Length; i++)
            {
                leaf.parts[i].localPosition = leaf.hinge + swing * (leaf.closedPositions[i] - leaf.hinge);
                leaf.parts[i].localRotation = swing * leaf.closedRotations[i];
            }
        }
    }
}
