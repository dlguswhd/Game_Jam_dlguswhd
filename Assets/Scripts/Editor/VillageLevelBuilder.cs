using GameJam.Enemies;
using GameJam.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameJam.EditorTools
{
    // Gameplay for the outdoor map (Bridge_Village): start beside the getaway car, cross the bridge and the
    // village to the hospital gate, go in (Abandoned_Hospital), come back out with the evidence and reach the car.
    // Positions are local to the map root "Bridge Village" (world origin 2000, 0, 2000).
    public static class VillageLevelBuilder
    {
        private const string MapRoot = "Bridge Village";
        private const string CarName = "black+srt";
        private const string RootName = "Village Gameplay";
        private const string HospitalScene = "Abandoned_Hospital";
        private const string NavMeshAssetPath = "Assets/Scenes/Bridge_Village_NavMesh.asset";
        private const float Ground = 0.05f;

        // Fog blocks the player's view past ~62 m, so enemies don't get to see further than the player can.
        private const float VillageSightRange = 45f;
        private static readonly Color FogColor = new Color(0.12f, 0.165f, 0.19f);

        private struct GuardSpec
        {
            public string weapon, name;
            public Vector2 anchor;
            public float yaw, scanAngle, scanPeriod;
        }

        private struct PatrolSpec
        {
            public string weapon, name;
            public Vector2[] route;
            public float wait;
        }

        // Four enemies: the road, the wrecked car in the middle, the plaza and the hospital gate.
        private static readonly GuardSpec[] Guards =
        {
            // Crouched behind the wrecked car in the middle of the village.
            new GuardSpec { weapon = "Shotgun", name = "Guard_Car", anchor = new Vector2(-9f, 93f), yaw = 120f, scanAngle = 35f, scanPeriod = 7f },
            // Gatekeeper under the hospital canopy, looking down the forecourt.
            new GuardSpec { weapon = "Shotgun", name = "Guard_Gate", anchor = new Vector2(3.5f, 171f), yaw = 180f, scanAngle = 50f, scanPeriod = 9f },
        };

        private static readonly PatrolSpec[] Patrols =
        {
            new PatrolSpec { weapon = "Pistol", name = "Patrol_Road1", wait = 2.5f, route = new[] { new Vector2(1.5f, 52f), new Vector2(1.5f, 92f) } },
            new PatrolSpec
            {
                weapon = "Rifle", name = "Patrol_Plaza", wait = 1.5f,
                route = new[] { new Vector2(-12f, 150f), new Vector2(12f, 150f), new Vector2(12f, 163f), new Vector2(-12f, 163f) }
            },
        };

        [MenuItem("Tools/Game Jam/Build Bridge Village Level")]
        public static void Build()
        {
            var map = GameObject.Find(MapRoot);
            var car = GameObject.Find(CarName);
            var player = GameObject.Find("Player");
            if (map == null || car == null || player == null)
            {
                Debug.LogError($"Open Bridge_Village with '{MapRoot}', '{CarName}' and 'Player' in it first.");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Bridge Village Level");

            Vector3 carHalfSize = SetupCar(car);

            // Start beside the driver's door, facing the bridge like the car.
            float carYaw = car.transform.eulerAngles.y;
            Vector3 start = car.transform.TransformPoint(new Vector3(-(carHalfSize.x + 1.1f), 0f, 0.4f));
            start.y = Ground;
            LevelBuildTools.Spawn(root, "Start", start, carYaw, true);
            LevelBuildTools.Spawn(root, "HospitalGate", World(0f, 172f), 180f, false);
            // Preloads the hospital from the forecourt (40 m); closed once the evidence is out.
            LevelBuildTools.Exit(root, "Exit_HospitalGate", World(0f, 176.5f), HospitalScene, "MainDoor",
                "폐병원으로 들어가기", closedByItem: "evidence", closedLabel: "이미 장부를 확보했다 — 차로 돌아가라",
                preloadDistance: 40f);

            var goal = new GameObject("Goal_GetawayCar");
            goal.transform.SetParent(root.transform, false);
            goal.transform.position = car.transform.TransformPoint(new Vector3(-(carHalfSize.x + 0.3f), 1f, 0.4f));
            goal.AddComponent<MissionGoal>();

            // The start approach slab ends ~0.43 m above the bridge deck (z -30.5..-29.25): too high for the player to
            // step back up when returning to the car, and for the NavMesh to connect. An invisible slab evens it out.
            var seam = new GameObject("Bridge seam filler");
            seam.transform.SetParent(root.transform, false);
            seam.transform.position = World(0f, -30.1f) + Vector3.up * (-0.04f - Ground);
            var seamBox = seam.AddComponent<BoxCollider>();
            seamBox.size = new Vector3(12f, 0.2f, 2.4f);

            root.AddComponent<SceneFlow>();
            var mission = root.AddComponent<Mission>();
            var missionSo = new SerializedObject(mission);
            var steps = missionSo.FindProperty("steps");
            steps.arraySize = 1;
            steps.GetArrayElementAtIndex(0).FindPropertyRelative("objective").stringValue = "폐병원 정문으로 잠입하라";
            steps.GetArrayElementAtIndex(0).FindPropertyRelative("itemId").stringValue = "evidence";
            missionSo.FindProperty("extractObjective").stringValue = "차로 돌아가 탈출하라";
            missionSo.ApplyModifiedPropertiesWithoutUndo();

            SetupPlayer(player, start, carYaw);

            // Bake before placing enemies (so patrol routes are checked against it and their hitboxes stay out of
            // it). All objects so the seam filler counts; the player is hidden during the bake.
            NavMeshBakeTool.BakeSurface(map, CollectObjects.All, NavMeshAssetPath, player);
            var enemies = PlaceEnemies(root);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"Bridge Village level built: {enemies} enemies.");
        }

        private static Vector3 World(float x, float z) => new Vector3(2000f + x, Ground, 2000f + z);

        // The car had no collider (the player walked through it). Box it from its meshes, in its own space.
        private static Vector3 SetupCar(GameObject car)
        {
            bool has = false;
            var bounds = new Bounds();
            foreach (var filter in car.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 local = car.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!has) { bounds = new Bounds(local, Vector3.zero); has = true; }
                    else bounds.Encapsulate(local);
                }
            }

            var box = car.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(car);
            Undo.RecordObject(box, "Car Collider");
            box.center = bounds.center;
            box.size = bounds.size;
            return bounds.extents;
        }

        private static void SetupPlayer(GameObject player, Vector3 start, float yaw)
        {
            LevelBuildTools.MovePlayer(player, start, yaw);
            LevelBuildTools.EnsurePlayerComponents(player);

            // No skybox outdoors: clear to the fog colour so the far distance blends into the fog.
            var camera = player.GetComponentInChildren<Camera>(true);
            if (camera == null) return;
            Undo.RecordObject(camera, "Village Camera");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = FogColor;
        }

        private static int PlaceEnemies(GameObject root)
        {
            var enemies = new GameObject("Enemies");
            enemies.transform.SetParent(root.transform, false);
            var routes = new GameObject("PatrolRoutes");
            routes.transform.SetParent(enemies.transform, false);
            int count = 0;

            foreach (var spec in Guards)
            {
                if (!AsylumEnemyPlacer.FindFreeSpot(World(spec.anchor.x, spec.anchor.y), 6f, out Vector3 spot))
                {
                    Debug.LogWarning($"No free ground near {spec.anchor} for {spec.name}.");
                    continue;
                }
                var enemy = AsylumEnemyPlacer.Enemy(enemies, spec.weapon, spec.name, spot, spec.yaw,
                    EnemyBehavior.Guard, null);
                var so = new SerializedObject(enemy);
                so.FindProperty("guardScanAngle").floatValue = spec.scanAngle;
                so.FindProperty("guardScanPeriod").floatValue = spec.scanPeriod;
                so.FindProperty("sightRange").floatValue = VillageSightRange;
                so.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }

            foreach (var spec in Patrols)
            {
                var points = new Transform[spec.route.Length];
                for (int i = 0; i < spec.route.Length; i++)
                {
                    Vector3 p = World(spec.route[i].x, spec.route[i].y);
                    if (!AsylumEnemyPlacer.FindFreeSpot(p, 2f, out p))
                        Debug.LogWarning($"{spec.name}: point {i} is not on free ground.");
                    var point = new GameObject($"{spec.name}_{i + 1}").transform;
                    point.SetParent(routes.transform, false);
                    point.position = p;
                    points[i] = point;
                }

                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 a = points[i].position, b = points[(i + 1) % points.Length].position;
                    if (!AsylumEnemyPlacer.IsPathClear(a, b))
                        Debug.LogWarning($"{spec.name}: leg {i + 1} is blocked between {a} and {b}.");
                }

                Vector3 dir = points[1].position - points[0].position;
                var enemy = AsylumEnemyPlacer.Enemy(enemies, spec.weapon, spec.name, points[0].position,
                    Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, EnemyBehavior.Patrol, points);
                var so = new SerializedObject(enemy);
                so.FindProperty("patrolWaitTime").floatValue = spec.wait;
                so.FindProperty("sightRange").floatValue = VillageSightRange;
                so.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }

            return count;
        }
    }
}
