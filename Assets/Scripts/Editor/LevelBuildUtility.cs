using GameJam.Player;
using GameJam.Weapons;
using GameJam.World;
using UnityEditor;
using UnityEngine;

namespace GameJam.EditorTools
{
    // Shared bits for the level builders: player components, spawn points, scene exits and the scene's flow.
    public static class LevelBuildTools
    {
        // Every gameplay component the player prefab instance needs on top of the KINEMATION character.
        public static void EnsurePlayerComponents(GameObject player)
        {
            AddIfMissing<PlayerNoise>(player);
            AddIfMissing<PlayerLaser>(player);
            AddIfMissing<PlayerInventory>(player);
            AddIfMissing<PlayerInteractor>(player);
            AddIfMissing<MouseLook>(player);
            AddIfMissing<PlayerFootsteps>(player);
        }

        public static void AddIfMissing<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null) Undo.AddComponent<T>(go);
        }

        public static SpawnPoint Spawn(GameObject parent, string id, Vector3 position, float yaw, bool isDefault)
        {
            var go = new GameObject($"Spawn_{id}");
            go.transform.SetParent(parent.transform, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var spawn = go.AddComponent<SpawnPoint>();
            var so = new SerializedObject(spawn);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("isDefault").boolValue = isDefault;
            so.ApplyModifiedPropertiesWithoutUndo();
            return spawn;
        }

        public static SceneExit Exit(GameObject parent, string name, Vector3 position, string targetScene,
            string targetSpawn, string label, string requiredItem = "", string lockedLabel = "",
            string closedByItem = "", string closedLabel = "", float preloadDistance = 40f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = position;
            var exit = go.AddComponent<SceneExit>();
            var so = new SerializedObject(exit);
            so.FindProperty("targetScene").stringValue = targetScene;
            so.FindProperty("targetSpawn").stringValue = targetSpawn;
            so.FindProperty("label").stringValue = label;
            so.FindProperty("requiredItem").stringValue = requiredItem;
            if (!string.IsNullOrEmpty(lockedLabel)) so.FindProperty("lockedLabel").stringValue = lockedLabel;
            so.FindProperty("closedByItem").stringValue = closedByItem;
            if (!string.IsNullOrEmpty(closedLabel)) so.FindProperty("closedLabel").stringValue = closedLabel;
            so.FindProperty("preloadDistance").floatValue = preloadDistance;
            so.ApplyModifiedPropertiesWithoutUndo();
            return exit;
        }

        public static void MovePlayer(GameObject player, Vector3 position, float yaw)
        {
            Undo.RecordObject(player.transform, "Place Player");
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
