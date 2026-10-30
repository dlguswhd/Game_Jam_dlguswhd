using System.Collections.Generic;
using GameJam.Player;
using GameJam.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameJam.EditorTools
{
    // Turns the Asylum's room doors (Door_V*, DoorD_*) into interactive Door components and gives the player
    // a PlayerInteractor. Furniture doors (cupboards, toilet stalls, lockers) and the elevator are left alone.
    public static class AsylumDoorSetup
    {
        private const StaticEditorFlags MovingFlags = StaticEditorFlags.BatchingStatic |
                                                      StaticEditorFlags.OccluderStatic |
                                                      StaticEditorFlags.OccludeeStatic;

        [MenuItem("Tools/Game Jam/Setup Asylum Doors")]
        public static void Setup()
        {
            var asylum = GameObject.Find("Asylum");
            if (asylum == null)
            {
                Debug.LogError("Asylum not found. Open Assets/Abandoned_Asylum/Show.unity first.");
                return;
            }

            int count = 0;
            foreach (var root in asylum.GetComponentsInChildren<Transform>(true))
            {
                if (!root.name.StartsWith("Door_V") && !root.name.StartsWith("DoorD_")) continue;
                if (SetupDoor(root)) count++;
            }

            var player = GameObject.Find("Player");
            if (player != null && player.GetComponent<PlayerInteractor>() == null)
                Undo.AddComponent<PlayerInteractor>(player);

            EditorSceneManager.MarkSceneDirty(asylum.scene);
            Debug.Log($"Asylum doors set up: {count}.");
        }

        private static bool SetupDoor(Transform root)
        {
            var panels = new List<Transform>();
            var extras = new List<Transform>();
            foreach (Transform child in root)
            {
                if (child.name.Contains("Frame")) continue;
                if (child.name.Contains("Window")) extras.Add(child);
                else panels.Add(child);
            }
            if (panels.Count == 0) return false;

            var leaves = new Door.Leaf[panels.Count];
            var parts = new List<Transform>[panels.Count];
            for (int i = 0; i < panels.Count; i++) parts[i] = new List<Transform> { panels[i] };

            // Windows ride along with whichever panel they sit in.
            foreach (var extra in extras)
            {
                int nearest = 0;
                float best = float.MaxValue;
                for (int i = 0; i < panels.Count; i++)
                {
                    float d = (BoundsCenter(extra) - BoundsCenter(panels[i])).sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    nearest = i;
                }
                parts[nearest].Add(extra);
            }

            for (int i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                Undo.RecordObject(panel, "Setup Asylum Doors");

                // Single-door panels are modelled with their pivot on the hinge; a few start ajar, so close them.
                if (panels.Count == 1) panel.localRotation = Quaternion.identity;

                Vector3 center = root.InverseTransformPoint(BoundsCenter(panel));
                Vector3 hinge;
                if (panels.Count == 1)
                {
                    hinge = root.InverseTransformPoint(panel.position);
                }
                else
                {
                    // Double doors: hinge on the panel's outer edge (some panels pivot at the doorway center).
                    Bounds local = LocalBounds(root, panel);
                    hinge = new Vector3(center.x >= 0f ? local.max.x : local.min.x, 0f, local.center.z);
                }
                hinge.y = 0f;
                center.y = 0f;

                leaves[i] = new Door.Leaf { parts = parts[i].ToArray(), hinge = hinge, center = center };

                foreach (var part in parts[i])
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(part.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(part.gameObject, flags & ~MovingFlags);
                }
            }

            var door = root.GetComponent<Door>();
            if (door == null) door = Undo.AddComponent<Door>(root.gameObject);
            Undo.RecordObject(door, "Setup Asylum Doors");
            door.Configure(leaves);
            EditorUtility.SetDirty(door);
            return true;
        }

        private static Vector3 BoundsCenter(Transform t)
        {
            var r = t.GetComponent<Renderer>();
            return r != null ? r.bounds.center : t.position;
        }

        // Panel bounds in the door root's space, from the mesh corners so rotation doesn't inflate them.
        private static Bounds LocalBounds(Transform root, Transform panel)
        {
            var mesh = panel.GetComponent<MeshFilter>().sharedMesh;
            Bounds b = mesh.bounds;
            var result = new Bounds(root.InverseTransformPoint(panel.TransformPoint(b.center)), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                result.Encapsulate(root.InverseTransformPoint(panel.TransformPoint(corner)));
            }
            return result;
        }
    }
}
