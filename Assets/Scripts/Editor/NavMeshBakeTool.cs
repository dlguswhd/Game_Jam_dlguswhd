using System.Collections.Generic;
using GameJam.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace GameJam.EditorTools
{
    // Bakes the Asylum NavMesh that enemies use to investigate noises and hunt the player.
    // Door leaves are left out so paths go through doorways (enemies open closed doors on the way).
    public static class NavMeshBakeTool
    {
        private const string AssetPath = "Assets/Abandoned_Asylum/Show_NavMesh.asset";

        // Matches the enemy CharacterController; the default 0.5 m radius closes off the 1 m doorways.
        private const float AgentRadius = 0.3f;
        private const float AgentHeight = 1.8f;
        private const float AgentClimb = 0.4f;

        [MenuItem("Tools/Game Jam/Bake Asylum NavMesh")]
        public static void Bake()
        {
            var asylum = GameObject.Find("Asylum");
            if (asylum == null)
            {
                Debug.LogError("Asylum not found. Open Assets/Abandoned_Asylum/Show.unity first.");
                return;
            }

            BakeSurface(asylum, CollectObjects.Children, AssetPath);
        }

        // Builds a NavMeshSurface on `owner` and saves it as an asset. Door leaves (and anything in `hide`,
        // e.g. characters whose hitboxes would otherwise become obstacles) are left out of the bake.
        public static void BakeSurface(GameObject owner, CollectObjects collect, string assetPath,
            params GameObject[] hide)
        {
            SetAgentSettings();

            var surface = owner.GetComponent<NavMeshSurface>();
            if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(owner);
            surface.agentTypeID = 0;
            surface.collectObjects = collect;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var doorColliders = new List<Collider>();
            foreach (var door in Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                foreach (Transform child in door.transform)
                {
                    if (child.name.Contains("Frame")) continue;
                    var col = child.GetComponent<Collider>();
                    if (col != null && col.enabled) doorColliders.Add(col);
                }
            }

            var hidden = new List<GameObject>();
            foreach (var go in hide)
            {
                if (go == null || !go.activeSelf) continue;
                hidden.Add(go);
            }

            foreach (var col in doorColliders) col.enabled = false;
            foreach (var go in hidden) go.SetActive(false);
            try
            {
                surface.BuildNavMesh();
            }
            finally
            {
                foreach (var col in doorColliders) col.enabled = true;
                foreach (var go in hidden) go.SetActive(true);
            }

            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(owner.scene);
            Debug.Log($"NavMesh baked ({doorColliders.Count} door colliders skipped) → {assetPath}");
        }

        private static void SetAgentSettings()
        {
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings"));
            var agent = settings.FindProperty("m_Settings").GetArrayElementAtIndex(0);
            agent.FindPropertyRelative("agentRadius").floatValue = AgentRadius;
            agent.FindPropertyRelative("agentHeight").floatValue = AgentHeight;
            agent.FindPropertyRelative("agentClimb").floatValue = AgentClimb;
            settings.ApplyModifiedPropertiesWithoutUndo();

            NavMeshBuildSettings check = NavMesh.GetSettingsByID(0);
            if (!Mathf.Approximately(check.agentRadius, AgentRadius))
                Debug.LogWarning($"NavMesh agent radius is {check.agentRadius}, expected {AgentRadius}.");
        }
    }
}
