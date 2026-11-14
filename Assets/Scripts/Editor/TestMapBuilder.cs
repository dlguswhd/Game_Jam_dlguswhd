using GameJam.Enemies;
using GameJam.UI;
using GameJam.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameJam.EditorTools
{
    // Blocks out a simple walled test map in the open scene: start corridor, main hall with cover,
    // a side room, a back corridor and a staircase up to a balcony. Guards and patrollers included.
    public static class TestMapBuilder
    {
        private const float WallHeight = 3.5f;
        private const float WallThickness = 0.3f;
        private const string MaterialFolder = "Assets/Materials";
        private const string EnemyFolder = "Assets/Prefabs/Enemies/";
        private const string NavMeshAssetPath = "Assets/Scenes/Test_map_NavMesh.asset";

        private static Material _wall;
        private static Material _cover;
        private static Material _stairs;

        [MenuItem("Tools/Game Jam/Build Test Map")]
        public static void Build()
        {
            var old = GameObject.Find("TestMap");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var oldEnemies = GameObject.Find("Enemies");
            if (oldEnemies != null) Undo.DestroyObjectImmediate(oldEnemies);

            _wall = GetMaterial("M_TestWall", new Color(0.62f, 0.62f, 0.6f));
            _cover = GetMaterial("M_TestCover", new Color(0.45f, 0.38f, 0.3f));
            _stairs = GetMaterial("M_TestStairs", new Color(0.35f, 0.42f, 0.5f));

            var map = new GameObject("TestMap");
            Undo.RegisterCreatedObjectUndo(map, "Build Test Map");
            var walls = Child(map, "Walls");
            var cover = Child(map, "Cover");
            var stairs = Child(map, "Stairs");
            var routes = Child(map, "PatrolRoutes");

            // Outer boundary: x -20..20, z -5..45.
            WallX(walls, -20f, 20f, -5f);
            WallX(walls, -20f, 20f, 45f);
            WallZ(walls, -5f, 45f, -20f);
            WallZ(walls, -5f, 45f, 20f);

            // Start corridor walls (x -4..4, z -5..3) opening into the hall.
            WallZ(walls, -5f, 3f, -4f);
            WallZ(walls, -5f, 3f, 4f);
            WallX(walls, -20f, -4f, 3f);
            WallX(walls, 4f, 20f, 3f);

            // Back wall of the hall (z = 25) with doors to the room, corridor and stair area.
            WallX(walls, -20f, -10.2f, 25f);
            WallX(walls, -9f, -4f, 25f);   // swinging door x -10.2..-9 into the side room
            BuildDoor(map, new Vector3(-9.6f, 0f, 25f), 1.2f);
            WallX(walls, 4f, 9f, 25f);     // corridor x -4..4 left open
            WallX(walls, 12f, 20f, 25f);   // door x 9..12 into the stair area
            WallZ(walls, 25f, 45f, -4f);
            WallZ(walls, 25f, 45f, 4f);

            // Cover in the hall.
            Box(cover, "Crate_A", new Vector3(-7f, 0.6f, 10f), new Vector3(1.2f, 1.2f, 1.2f), _cover);
            Box(cover, "Crate_B", new Vector3(6f, 0.6f, 13f), new Vector3(1.2f, 1.2f, 1.2f), _cover);
            Box(cover, "Crate_C", new Vector3(-2f, 0.6f, 19f), new Vector3(1.2f, 1.2f, 1.2f), _cover);
            Box(cover, "Crate_D", new Vector3(13f, 0.6f, 7f), new Vector3(1.2f, 1.2f, 1.2f), _cover);
            Box(cover, "LowWall", new Vector3(0f, 0.55f, 8f), new Vector3(5f, 1.1f, 0.4f), _cover);
            Box(cover, "Pillar_L", new Vector3(-10f, WallHeight * 0.5f, 16f), new Vector3(1f, WallHeight, 1f), _wall);
            Box(cover, "Pillar_R", new Vector3(10f, WallHeight * 0.5f, 16f), new Vector3(1f, WallHeight, 1f), _wall);
            Box(cover, "RoomTable", new Vector3(-14f, 0.45f, 34f), new Vector3(2.5f, 0.9f, 1.2f), _cover);

            // Staircase (x 15..18) climbing toward +z, 10 steps of 0.25 m, onto a 2.5 m high balcony.
            const int steps = 10;
            const float rise = 0.25f;
            const float run = 0.45f;
            const float stairStartZ = 28f;
            for (int i = 0; i < steps; i++)
            {
                float height = (i + 1) * rise;
                Box(stairs, $"Step_{i + 1:00}",
                    new Vector3(16.5f, height * 0.5f, stairStartZ + run * (i + 0.5f)),
                    new Vector3(3f, height, run), _stairs);
            }
            float balconyStart = stairStartZ + run * steps;
            float balconyHeight = steps * rise;
            Box(stairs, "Balcony", new Vector3(16f, balconyHeight * 0.5f, (balconyStart + 45f) * 0.5f),
                new Vector3(8f, balconyHeight, 45f - balconyStart), _stairs);
            Box(stairs, "BalconyRail", new Vector3(12.15f, balconyHeight + 0.5f, (balconyStart + 45f) * 0.5f),
                new Vector3(0.1f, 1f, 45f - balconyStart), _wall);

            // Patrol routes.
            var hallRoute = new[]
            {
                Point(routes, "Hall_1", new Vector3(-14f, 0f, 12f)),
                Point(routes, "Hall_2", new Vector3(-14f, 0f, 21f)),
                Point(routes, "Hall_3", new Vector3(14f, 0f, 21f)),
                Point(routes, "Hall_4", new Vector3(14f, 0f, 12f)),
            };
            var corridorRoute = new[]
            {
                Point(routes, "Corridor_1", new Vector3(0f, 0f, 27f)),
                Point(routes, "Corridor_2", new Vector3(0f, 0f, 43f)),
            };

            var enemies = new GameObject("Enemies");
            Undo.RegisterCreatedObjectUndo(enemies, "Build Test Map");
            Enemy(enemies, "Shotgun", "Guard_Room", new Vector3(-14f, 0f, 41f), 180f, EnemyBehavior.Guard, null);
            Enemy(enemies, "Rifle", "Guard_Balcony", new Vector3(16f, balconyHeight, 42f), 180f, EnemyBehavior.Guard, null);
            Enemy(enemies, "Pistol", "Patrol_Hall", hallRoute[0].position, 0f, EnemyBehavior.Patrol, hallRoute);
            Enemy(enemies, "Rifle", "Patrol_Corridor", corridorRoute[0].position, 0f, EnemyBehavior.Patrol, corridorRoute);
            // Sealed room beside the start corridor: footsteps through the wall must NOT reach him,
            // an unsuppressed shot should (sound through walls counts triple distance).
            Enemy(enemies, "Pistol", "Guard_BehindWall", new Vector3(-7f, 0f, -1f), 90f, EnemyBehavior.Guard, null);

            var player = GameObject.Find("Player");
            if (player != null)
            {
                LevelBuildTools.MovePlayer(player, new Vector3(0f, 0f, -2f), 0f);
                LevelBuildTools.EnsurePlayerComponents(player);
            }

            var overlay = new GameObject("AI Debug Overlay");
            overlay.transform.SetParent(map.transform, false);
            overlay.AddComponent<EnemyDebugOverlay>();
            // Off by default; enable the object in the hierarchy when debugging AI.
            overlay.SetActive(false);

            // Characters are hidden during the bake so their hitboxes don't punch holes in the NavMesh.
            NavMeshBakeTool.BakeSurface(map, CollectObjects.All, NavMeshAssetPath, enemies, player);

            EditorSceneManager.MarkSceneDirty(map.scene);
            Debug.Log("Test map built.");
        }

        // A plain slab door that swings like the Asylum doors (hinge on its -x edge).
        private static void BuildDoor(GameObject parent, Vector3 position, float width)
        {
            var root = new GameObject("Door_SideRoom");
            root.transform.SetParent(parent.transform, false);
            root.transform.localPosition = position;

            var leaf = Box(root, "Leaf", new Vector3(0f, 1.1f, 0f), new Vector3(width, 2.2f, 0.06f), _cover);
            leaf.isStatic = false;

            var door = root.AddComponent<Door>();
            door.Configure(new[]
            {
                new Door.Leaf
                {
                    parts = new[] { leaf.transform },
                    hinge = new Vector3(-width * 0.5f, 0f, 0f),
                    center = Vector3.zero
                }
            });
        }

        private static void Enemy(GameObject parent, string weapon, string name, Vector3 position, float yaw,
            EnemyBehavior behavior, Transform[] route)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyFolder}Enemy_{weapon}.prefab");
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            enemy.name = $"{name}_{weapon}";
            enemy.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var so = new SerializedObject(enemy.GetComponent<EnemyAI>());
            so.FindProperty("behavior").enumValueIndex = (int)behavior;
            var points = so.FindProperty("patrolPoints");
            points.arraySize = route?.Length ?? 0;
            for (int i = 0; i < points.arraySize; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = route[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WallX(GameObject parent, float x0, float x1, float z)
        {
            Box(parent, $"Wall_X_{z}_{x0}", new Vector3((x0 + x1) * 0.5f, WallHeight * 0.5f, z),
                new Vector3(Mathf.Abs(x1 - x0) + WallThickness, WallHeight, WallThickness), _wall);
        }

        private static void WallZ(GameObject parent, float z0, float z1, float x)
        {
            Box(parent, $"Wall_Z_{x}_{z0}", new Vector3(x, WallHeight * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(WallThickness, WallHeight, Mathf.Abs(z1 - z0) + WallThickness), _wall);
        }

        private static GameObject Box(GameObject parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            box.isStatic = true;
            return box;
        }

        private static Transform Point(GameObject parent, string name, Vector3 position)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent.transform, false);
            point.transform.position = position;
            return point.transform;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static Material GetMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets", "Materials");

            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
