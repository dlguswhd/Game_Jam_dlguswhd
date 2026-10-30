using GameJam.Enemies;
using GameJam.Player;
using GameJam.Weapons;
using GameJam.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace GameJam.EditorTools
{
    // Places enemies in the Abandoned Asylum map (Show scene). Guards stand on the free floor spot closest to
    // their room's center and face the longest open sight line (usually the door); patrollers walk the halls.
    public static class AsylumEnemyPlacer
    {
        private const string EnemyFolder = "Assets/Prefabs/Enemies/";
        private const string RootName = "Enemies";

        private const float Floor1 = 0.03f;
        private const float Floor2 = 3.81f;
        private const float FloorBasement = -3.84f;

        // Inside the Gatehouse by the main door (z -13.6), facing its door to the 1st floor hall. Hidden from every
        // enemy while that door is closed. (The "porch" at z 14 is outside a window and can't reach the building.)
        private static readonly Vector3 PlayerSpawn = new Vector3(-4.7f, Floor1, -11.5f);
        private const float PlayerSpawnYaw = 0f;

        private const float BodyRadius = 0.5f;
        private const float EyeHeight = 1.6f;

        private struct GuardSpec
        {
            public string weapon, name, room;
            public float floor;
            public Vector3? anchor;
            // Facing; null = longest open sight line.
            public float? yaw;
            // Left/right sweep and its period; 0 = prefab default.
            public float scanAngle, scanPeriod;
        }

        private struct PatrolSpec
        {
            public string weapon, name;
            public float floor;
            public Vector3[] route;
            // Pause at each point; 0 = prefab default.
            public float wait;
        }

        // Level layout — see 레벨디자인.md. The critical path is Gatehouse → 1F hall → atrium stairs → 2F office
        // (keycard) → back down → east wing → basement → morgue (evidence) → main door.
        private static readonly GuardSpec[] Guards =
        {
            // 1st floor: rooms off the hall. Closed doors keep them out of play unless the player gets loud.
            new GuardSpec { weapon = "Rifle", name = "Guard_GuardRoom", room = "1stFloor/GuardRoom", floor = Floor1 },
            new GuardSpec { weapon = "Shotgun", name = "Guard_OfficeA", room = "1stFloor/OfficeA", floor = Floor1 },
            // 2nd floor: overwatch on the north gallery sweeping the atrium and both staircases.
            new GuardSpec
            {
                weapon = "Rifle", name = "Guard_Gallery", floor = Floor2, anchor = new Vector3(0.5f, 0f, 11f),
                yaw = 180f, scanAngle = 50f, scanPeriod = 9f
            },
            // Keycard room: stands by the desk facing the TV wall, back to the door. His sweep only reaches the
            // desk at one end, so the card has to be grabbed while he looks the other way.
            new GuardSpec
            {
                weapon = "Rifle", name = "Guard_Office", floor = Floor2, anchor = new Vector3(7.3f, 0f, -8.7f),
                yaw = 90f, scanAngle = 60f, scanPeriod = 8f
            },
            new GuardSpec { weapon = "Shotgun", name = "Guard_Surgery", room = "2ndFloor/Surgery", floor = Floor2 },
            // Reinforcements behind closed doors next to the critical path: quiet play never meets them,
            // but gunfire (theirs or the player's) pulls them in.
            new GuardSpec { weapon = "Rifle", name = "Guard_Stock", room = "1stFloor/Stock", floor = Floor1 },
            new GuardSpec
            {
                weapon = "Pistol", name = "Guard_Kitchen2F", floor = Floor2, anchor = new Vector3(13.5f, 0f, -10f)
            },
            new GuardSpec { weapon = "Pistol", name = "Guard_Classroom", room = "2ndFloor/Classroom", floor = Floor2 },
            new GuardSpec
            {
                weapon = "Shotgun", name = "Guard_SeclusionB", room = "Basement/SeclusionRoomB", floor = FloorBasement
            },
        };

        private static readonly PatrolSpec[] Patrols =
        {
            new PatrolSpec
            {
                weapon = "Rifle", name = "Patrol_Hall1F", floor = Floor1, wait = 1.5f,
                route = new[] { new Vector3(-22f, 0f, -4.5f), new Vector3(18f, 0f, -4.5f) }
            },
            new PatrolSpec
            {
                weapon = "Pistol", name = "Patrol_Hall2F", floor = Floor2, wait = 2f,
                route = new[] { new Vector3(-18f, 0f, -5.5f), new Vector3(16f, 0f, -5.5f) }
            },
            // Paces in front of the basement stairs on the east side of the 1st floor.
            new PatrolSpec
            {
                weapon = "Pistol", name = "Patrol_EastWing", floor = Floor1, wait = 3f,
                route = new[] { new Vector3(20.5f, 0f, -7.5f), new Vector3(20.5f, 0f, -13f) }
            },
            new PatrolSpec
            {
                weapon = "Shotgun", name = "Patrol_Basement", floor = FloorBasement, wait = 1.5f,
                route = new[] { new Vector3(22.5f, 0f, -7f), new Vector3(22.5f, 0f, -17f) }
            },
            // Wings: keep the west side of both floors alive, so the long way round isn't free either.
            new PatrolSpec
            {
                weapon = "Rifle", name = "Patrol_WestWing", floor = Floor1, wait = 2f,
                route = new[] { new Vector3(-26.5f, 0f, -4f), new Vector3(-26.5f, 0f, -12.4f) }
            },
            new PatrolSpec
            {
                weapon = "Shotgun", name = "Patrol_2FWest", floor = Floor2, wait = 2f,
                route = new[] { new Vector3(-22f, 0f, -5.5f), new Vector3(-21f, 0f, -12.5f) }
            },
            // Slow loop inside the morgue around the evidence table.
            new PatrolSpec
            {
                weapon = "Rifle", name = "Patrol_Morgue", floor = FloorBasement, wait = 3f,
                route = new[] { new Vector3(26.5f, 0f, -5.5f), new Vector3(30f, 0f, -4.5f), new Vector3(27.5f, 0f, -0.5f) }
            },
        };

        [MenuItem("Tools/Game Jam/Build Asylum Level (Enemies + Mission)")]
        public static void Place()
        {
            var asylum = GameObject.Find("Asylum");
            if (asylum == null)
            {
                Debug.LogError("Asylum not found. Open Assets/Abandoned_Asylum/Show.unity first.");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            Physics.SyncTransforms();

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Place Asylum Enemies");
            var routes = new GameObject("PatrolRoutes");
            routes.transform.SetParent(root.transform, false);

            foreach (var spec in Guards)
            {
                Vector3 anchor;
                if (spec.anchor.HasValue)
                {
                    anchor = spec.anchor.Value;
                }
                else
                {
                    var room = asylum.transform.Find(spec.room);
                    if (room == null)
                    {
                        Debug.LogWarning($"Room '{spec.room}' not found, skipping {spec.name}.");
                        continue;
                    }
                    anchor = GetBounds(room).center;
                }
                anchor.y = spec.floor;

                if (!FindFreeSpot(anchor, 6f, out Vector3 spot))
                {
                    Debug.LogWarning($"No free floor near {anchor} for {spec.name}.");
                    continue;
                }

                var enemy = Enemy(root, spec.weapon, spec.name, spot, spec.yaw ?? LongestSightYaw(spot),
                    EnemyBehavior.Guard, null);
                var so = new SerializedObject(enemy);
                if (spec.scanAngle > 0f) so.FindProperty("guardScanAngle").floatValue = spec.scanAngle;
                if (spec.scanPeriod > 0f) so.FindProperty("guardScanPeriod").floatValue = spec.scanPeriod;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var spec in Patrols)
            {
                var points = new Transform[spec.route.Length];
                for (int i = 0; i < spec.route.Length; i++)
                {
                    Vector3 p = spec.route[i];
                    p.y = spec.floor;
                    if (!FindFreeSpot(p, 2f, out p)) Debug.LogWarning($"{spec.name}: point {i} is not on free floor.");

                    var point = new GameObject($"{spec.name}_{i + 1}").transform;
                    point.SetParent(routes.transform, false);
                    point.position = p;
                    points[i] = point;
                }

                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 a = points[i].position, b = points[(i + 1) % points.Length].position;
                    if (!IsPathClear(a, b)) Debug.LogWarning($"{spec.name}: leg {i + 1} is blocked between {a} and {b}.");
                }

                Vector3 dir = points.Length > 1 ? points[1].position - points[0].position : Vector3.forward;
                var enemy = Enemy(root, spec.weapon, spec.name, points[0].position,
                    Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, EnemyBehavior.Patrol, points);
                if (spec.wait > 0f)
                {
                    var so = new SerializedObject(enemy);
                    so.FindProperty("patrolWaitTime").floatValue = spec.wait;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var player = GameObject.Find("Player");
            if (player != null)
            {
                Undo.RecordObject(player.transform, "Place Asylum Enemies");
                player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.Euler(0f, PlayerSpawnYaw, 0f));
                // Footsteps, shots, reloads and doors are what enemies hear.
                if (player.GetComponent<PlayerNoise>() == null) Undo.AddComponent<PlayerNoise>(player);
                if (player.GetComponent<PlayerLaser>() == null) Undo.AddComponent<PlayerLaser>(player);
                if (player.GetComponent<PlayerInventory>() == null) Undo.AddComponent<PlayerInventory>(player);
            }

            BuildMission(asylum);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"Asylum enemies placed: {root.transform.childCount - 1}.");
        }

        // Keycard on the 2F office desk, evidence on the morgue table (clangs when taken), morgue locked
        // behind the keycard, extraction at the main door.
        private static void BuildMission(GameObject asylum)
        {
            var old = GameObject.Find("Mission");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject("Mission");
            Undo.RegisterCreatedObjectUndo(root, "Build Asylum Level");

            Pickup(root, "Keycard", new Vector3(8.5f, 4.615f, -7.4f), "keycard", "열쇠카드",
                PickupItem.Shape.Keycard, new Color(0.85f, 0.9f, 1f), new Color(0.5f, 0.75f, 1f), 0f, 0f);
            Pickup(root, "Evidence", new Vector3(29.43f, -2.985f, -2.4f), "evidence", "증거 서류",
                PickupItem.Shape.Folder, new Color(0.55f, 0.4f, 0.22f), new Color(1f, 0.75f, 0.35f), 12f, 60f);

            var morgueDoor = asylum.transform.Find("Basement/Morgue/DoorD_V1 (1)")?.GetComponent<Door>();
            if (morgueDoor != null)
            {
                var so = new SerializedObject(morgueDoor);
                so.FindProperty("requiredItem").stringValue = "keycard";
                so.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("Morgue door not found (run Setup Asylum Doors first).");
            }

            var exit = new GameObject("Extraction").transform;
            exit.SetParent(root.transform, false);
            exit.position = new Vector3(0f, Floor1, -12.6f);

            var mission = root.AddComponent<Mission>();
            var missionSo = new SerializedObject(mission);
            var steps = missionSo.FindProperty("steps");
            steps.arraySize = 2;
            steps.GetArrayElementAtIndex(0).FindPropertyRelative("objective").stringValue = "2층 사무실에서 열쇠카드를 찾아라";
            steps.GetArrayElementAtIndex(0).FindPropertyRelative("itemId").stringValue = "keycard";
            steps.GetArrayElementAtIndex(1).FindPropertyRelative("objective").stringValue = "지하 시체안치소에서 증거 서류를 회수하라";
            steps.GetArrayElementAtIndex(1).FindPropertyRelative("itemId").stringValue = "evidence";
            missionSo.FindProperty("extractionPoint").objectReferenceValue = exit;
            missionSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Pickup(GameObject parent, string name, Vector3 position, string id, string displayName,
            PickupItem.Shape shape, Color color, Color glow, float noiseRadius, float noiseSuspicion)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = position;
            var item = go.AddComponent<PickupItem>();
            var so = new SerializedObject(item);
            so.FindProperty("itemId").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("shape").enumValueIndex = (int)shape;
            so.FindProperty("color").colorValue = color;
            so.FindProperty("glowColor").colorValue = glow;
            so.FindProperty("noiseRadius").floatValue = noiseRadius;
            so.FindProperty("noiseSuspicion").floatValue = noiseSuspicion;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Bounds GetBounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(t.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (i == 0) bounds = renderers[i].bounds;
                else bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        // Nearest point to the anchor (searched on a 0.25 m grid) that has floor under it and room for a body.
        private static bool FindFreeSpot(Vector3 anchor, float searchRadius, out Vector3 spot)
        {
            spot = anchor;
            float best = float.MaxValue;
            const float step = 0.25f;

            for (float x = -searchRadius; x <= searchRadius; x += step)
            {
                for (float z = -searchRadius; z <= searchRadius; z += step)
                {
                    float d = x * x + z * z;
                    if (d >= best || d > searchRadius * searchRadius) continue;

                    var candidate = new Vector3(anchor.x + x, anchor.y, anchor.z + z);
                    if (!TryGetFloor(candidate, out float floorY)) continue;
                    candidate.y = floorY;
                    if (!HasRoom(candidate)) continue;

                    best = d;
                    spot = candidate;
                }
            }
            return best < float.MaxValue;
        }

        private static bool TryGetFloor(Vector3 p, out float floorY)
        {
            floorY = p.y;
            if (!Physics.Raycast(p + Vector3.up * EyeHeight, Vector3.down, out RaycastHit hit, EyeHeight + 0.5f,
                    ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.point.y - p.y) > 0.3f) return false;
            floorY = hit.point.y;
            return true;
        }

        private static bool HasRoom(Vector3 feet)
        {
            return !Physics.CheckCapsule(feet + Vector3.up * (BodyRadius + 0.3f), feet + Vector3.up * 1.8f,
                BodyRadius, ~0, QueryTriggerInteraction.Ignore);
        }

        // Enemies follow the NavMesh, so a leg only has to be walkable; without a NavMesh it must be a straight line.
        private static bool IsPathClear(Vector3 a, Vector3 b)
        {
            if (NavMesh.SamplePosition(a, out NavMeshHit from, 1f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(b, out NavMeshHit to, 1f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                return NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) &&
                       path.status == NavMeshPathStatus.PathComplete;
            }

            Vector3 lift = Vector3.up * (BodyRadius + 0.3f);
            Vector3 delta = b - a;
            return !Physics.CapsuleCast(a + lift, a + Vector3.up * 1.8f, BodyRadius * 0.8f, delta.normalized,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        }

        // Faces the direction with the longest unobstructed view at eye height.
        private static float LongestSightYaw(Vector3 feet)
        {
            Vector3 eye = feet + Vector3.up * EyeHeight;
            float bestYaw = 0f, bestDistance = -1f;
            for (int i = 0; i < 36; i++)
            {
                float yaw = i * 10f;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float distance = Physics.Raycast(eye, dir, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)
                    ? hit.distance
                    : 40f;
                if (distance <= bestDistance) continue;
                bestDistance = distance;
                bestYaw = yaw;
            }
            return bestYaw;
        }

        private static EnemyAI Enemy(GameObject parent, string weapon, string name, Vector3 position, float yaw,
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
            return enemy.GetComponent<EnemyAI>();
        }
    }
}
