using System.Collections.Generic;
using GameJam.Enemies;
using GameJam.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameJam.EditorTools
{
    // Builds the prototype play map in the open scene (Assets/Scenes/Prototype_Play_Map.unity): four indoor mazes
    // laid out 2×2 and joined through openings in their shared walls. Each section is a 9×9 grid of 4 m cells with its
    // own maze, rooms, floor and enemies, dressed with Abandoned_Asylum materials and furniture (no doors), with
    // claymores in the corridors. Every random choice comes from fixed seeds, so rebuilding gives the same map.
    public static class PrototypeMapBuilder
    {
        // Size of one section, in cells.
        private const int Size = 9;
        private const int SectionsX = 2;
        private const int SectionsZ = 2;
        private const int Width = Size * SectionsX;
        private const int Depth = Size * SectionsZ;
        private const float Cell = 4f;
        private const float WallHeight = 3f;
        private const float WallThickness = 0.2f;
        private const int Seed = 20261114;
        // Extra walls knocked out after each maze is carved, so there are loops (more than one way around a mine).
        private const int ExtraOpenings = 6;
        // Openings cut through each wall shared by two sections.
        private const int ConnectionsPerBorder = 2;
        private const int ClaymoresPerSection = 6;

        private const string AsylumPrefabs = "Assets/Abandoned_Asylum/Prefabs/";
        private const string EnemyFolder = "Assets/Prefabs/Enemies/";
        private const string ClaymorePrefab = "Assets/_Project/Prefabs/Traps/Claymore_LaserMine.prefab";
        private const string MaterialFolder = "Assets/Materials/Prototype";
        private const string NavMeshAssetPath = "Assets/Scenes/Prototype_Play_Map_NavMesh.asset";
        private const string TilesFolder = "Assets/Abandoned_Asylum/Materials/Tiled/Tiles/";

        private static readonly Vector2Int Start = new Vector2Int(0, 0);

        private enum RoomType { Hall, Ward, Office, Morgue }

        private struct Room
        {
            public RoomType type;
            public RectInt rect; // local to its section
            public Room(RoomType type, int x, int z, int w, int h)
            {
                this.type = type;
                rect = new RectInt(x, z, w, h);
            }
        }

        private class Section
        {
            public string name;
            public Vector2Int offset; // in cells
            public int seed;
            public Room[] rooms;
            public string floorSource;
            public Color floorTint;
            public System.Random rng;
            public Material floor;
        }

        // Section A is the original map; B, C, D were added around it.
        private static readonly Section[] Sections =
        {
            new Section
            {
                name = "A", offset = new Vector2Int(0, 0), seed = Seed,
                rooms = new[]
                {
                    new Room(RoomType.Hall, 3, 3, 3, 3), new Room(RoomType.Ward, 0, 6, 2, 3),
                    new Room(RoomType.Office, 6, 0, 3, 2), new Room(RoomType.Morgue, 7, 7, 2, 2),
                },
                floorSource = "Tiles_12", floorTint = new Color(0.8f, 0.78f, 0.72f),
            },
            new Section
            {
                name = "B", offset = new Vector2Int(Size, 0), seed = Seed + 101,
                rooms = new[]
                {
                    new Room(RoomType.Ward, 1, 1, 3, 2), new Room(RoomType.Hall, 5, 4, 3, 3),
                    new Room(RoomType.Morgue, 0, 6, 2, 2),
                },
                floorSource = "Tiles_6", floorTint = new Color(0.72f, 0.7f, 0.68f),
            },
            new Section
            {
                name = "C", offset = new Vector2Int(0, Size), seed = Seed + 202,
                rooms = new[]
                {
                    new Room(RoomType.Office, 0, 0, 3, 2), new Room(RoomType.Hall, 3, 4, 3, 3),
                    new Room(RoomType.Ward, 6, 0, 2, 3), new Room(RoomType.Morgue, 0, 7, 2, 2),
                },
                floorSource = "Tiles_5", floorTint = new Color(0.75f, 0.72f, 0.66f),
            },
            new Section
            {
                name = "D", offset = new Vector2Int(Size, Size), seed = Seed + 303,
                rooms = new[]
                {
                    new Room(RoomType.Hall, 2, 2, 4, 3), new Room(RoomType.Office, 6, 6, 3, 2),
                    new Room(RoomType.Morgue, 0, 6, 2, 3),
                },
                floorSource = "Tiles_13", floorTint = new Color(0.62f, 0.62f, 0.6f),
            },
        };

        // hWalls[x, z]: wall on the line z * Cell between cells (x, z-1) and (x, z). vWalls[x, z]: on the line x * Cell.
        private static bool[,] _hWalls;
        private static bool[,] _vWalls;
        // Which section and room (index into that section's rooms, -1 = corridor) each cell belongs to.
        private static int[,] _section;
        private static int[,] _room;

        private static Material _wallMat;
        private static Material _ceilingMat;

        [MenuItem("Tools/Game Jam/Build Prototype Play Map")]
        public static void Build()
        {
            foreach (var name in new[] { "PrototypeMap", "PrototypeMap_Ceiling", "Enemies", "Claymores" })
            {
                var old = GameObject.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            _wallMat = DerivedMaterial("M_Proto_Wall", "Assets/Abandoned_Asylum/Materials/Tiled/Walls/Wall_3.mat", 1f / 3f,
                new Color(0.78f, 0.78f, 0.74f));
            _ceilingMat = DerivedMaterial("M_Proto_Ceiling", "Assets/Abandoned_Asylum/Materials/Tiled/Walls/Wall_6.mat", 0.25f,
                new Color(0.55f, 0.55f, 0.55f));
            foreach (var s in Sections)
            {
                s.rng = new System.Random(s.seed);
                string matName = s.name == "A" ? "M_Proto_Floor" : $"M_Proto_Floor_{s.name}";
                s.floor = DerivedMaterial(matName, TilesFolder + s.floorSource + ".mat", 1f / 3f, s.floorTint);
            }
            SetupLighting();

            InitGrid();
            foreach (var s in Sections) GenerateMaze(s);

            var map = new GameObject("PrototypeMap");
            Undo.RegisterCreatedObjectUndo(map, "Build Prototype Map");
            var walls = Child(map, "Walls");
            var props = Child(map, "Props");
            var lights = Child(map, "Lights");
            var routes = Child(map, "PatrolRoutes");

            // Ceiling sits outside the NavMesh root so its top isn't baked as a walkable roof.
            var ceilingRoot = new GameObject("PrototypeMap_Ceiling");
            Undo.RegisterCreatedObjectUndo(ceilingRoot, "Build Prototype Map");

            float sectionSize = Size * Cell;
            foreach (var s in Sections)
            {
                Vector3 origin = new Vector3(s.offset.x * Cell, 0f, s.offset.y * Cell);
                Slab(map, $"Floor_{s.name}", origin + new Vector3(sectionSize * 0.5f, -0.1f, sectionSize * 0.5f),
                    new Vector3(sectionSize, 0.2f, sectionSize), s.floor);
            }
            float sizeX = Width * Cell;
            float sizeZ = Depth * Cell;
            Slab(ceilingRoot, "Ceiling", new Vector3(sizeX * 0.5f, WallHeight + 0.1f, sizeZ * 0.5f),
                new Vector3(sizeX + 1f, 0.2f, sizeZ + 1f), _ceilingMat);

            // Dead-end junk is decided on each section's own maze, before the sections are joined.
            foreach (var s in Sections) BuildProps(props, s);
            ConnectSections();
            BuildWalls(walls);
            BuildLights(lights);

            var claymores = new GameObject("Claymores");
            Undo.RegisterCreatedObjectUndo(claymores, "Build Prototype Map");
            var dist = Distances(Start);
            foreach (var s in Sections) PlaceClaymores(claymores, s, dist);

            var enemies = new GameObject("Enemies");
            Undo.RegisterCreatedObjectUndo(enemies, "Build Prototype Map");
            PlaceEnemiesA(enemies, routes);
            for (int i = 1; i < Sections.Length; i++) PlaceEnemies(enemies, routes, Sections[i]);

            var player = GameObject.Find("Player");
            if (player != null)
            {
                LevelBuildTools.MovePlayer(player, CellCenter(Start), 0f);
                LevelBuildTools.EnsurePlayerComponents(player);
            }

            var overlay = new GameObject("AI Debug Overlay");
            overlay.transform.SetParent(map.transform, false);
            overlay.AddComponent<EnemyDebugOverlay>();
            overlay.SetActive(false);

            NavMeshBakeTool.BakeSurface(map, CollectObjects.Children, NavMeshAssetPath, enemies, claymores, player);

            EditorSceneManager.MarkSceneDirty(map.scene);
            Debug.Log("Prototype play map built.");
        }

        // ---------------------------------------------------------------- maze

        private static void InitGrid()
        {
            _hWalls = new bool[Width, Depth + 1];
            _vWalls = new bool[Width + 1, Depth];
            _section = new int[Width, Depth];
            _room = new int[Width, Depth];
            for (int x = 0; x < Width; x++)
            for (int z = 0; z <= Depth; z++) _hWalls[x, z] = true;
            for (int x = 0; x <= Width; x++)
            for (int z = 0; z < Depth; z++) _vWalls[x, z] = true;

            for (int i = 0; i < Sections.Length; i++)
            {
                var s = Sections[i];
                for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                {
                    _section[s.offset.x + x, s.offset.y + z] = i;
                    _room[s.offset.x + x, s.offset.y + z] = -1;
                }
                for (int r = 0; r < s.rooms.Length; r++)
                    foreach (var c in s.rooms[r].rect.allPositionsWithin)
                        _room[s.offset.x + c.x, s.offset.y + c.y] = r;
            }
        }

        private static void GenerateMaze(Section s)
        {
            var rng = s.rng;

            // Recursive backtracker over the section's cells, from its bottom-left corner.
            var visited = new HashSet<Vector2Int>();
            var stack = new Stack<Vector2Int>();
            stack.Push(s.offset);
            visited.Add(s.offset);
            while (stack.Count > 0)
            {
                var cell = stack.Peek();
                var options = new List<Vector2Int>();
                foreach (var n in Neighbours(cell, s))
                    if (!visited.Contains(n)) options.Add(n);
                if (options.Count == 0)
                {
                    stack.Pop();
                    continue;
                }
                var next = options[rng.Next(options.Count)];
                SetWall(cell, next, false);
                visited.Add(next);
                stack.Push(next);
            }

            // Open up room interiors.
            foreach (var room in s.rooms)
                foreach (var local in room.rect.allPositionsWithin)
                {
                    var c = s.offset + local;
                    foreach (var n in Neighbours(c, s))
                        if (room.rect.Contains(n - s.offset)) SetWall(c, n, false);
                }

            // Knock out extra corridor walls for loops.
            int opened = 0, guard = 0;
            while (opened < ExtraOpenings && guard++ < 1000)
            {
                var a = s.offset + new Vector2Int(rng.Next(Size), rng.Next(Size));
                var dirs = Neighbours(a, s);
                var b = dirs[rng.Next(dirs.Count)];
                if (!HasWall(a, b)) continue;
                SetWall(a, b, false);
                opened++;
            }
        }

        // Cuts openings through every wall shared by two sections.
        private static void ConnectSections()
        {
            var rng = new System.Random(Seed + 999);
            for (int sx = 0; sx < SectionsX; sx++)
            for (int sz = 0; sz < SectionsZ; sz++)
            {
                if (sx + 1 < SectionsX)
                {
                    int x = (sx + 1) * Size;
                    foreach (int row in PickRows(rng))
                    {
                        int z = sz * Size + row;
                        SetWall(new Vector2Int(x - 1, z), new Vector2Int(x, z), false);
                    }
                }
                if (sz + 1 < SectionsZ)
                {
                    int z = (sz + 1) * Size;
                    foreach (int col in PickRows(rng))
                    {
                        int x = sx * Size + col;
                        SetWall(new Vector2Int(x, z - 1), new Vector2Int(x, z), false);
                    }
                }
            }
        }

        // Distinct positions along a border, at least 3 cells apart, kept off the corners.
        private static List<int> PickRows(System.Random rng)
        {
            var rows = new List<int>();
            int guard = 0;
            while (rows.Count < ConnectionsPerBorder && guard++ < 100)
            {
                int r = 1 + rng.Next(Size - 2);
                bool close = false;
                foreach (int o in rows)
                    if (Mathf.Abs(o - r) < 3) close = true;
                if (!close) rows.Add(r);
            }
            return rows;
        }

        // Neighbours inside the whole grid.
        private static List<Vector2Int> Neighbours(Vector2Int c)
        {
            var list = new List<Vector2Int>(4);
            if (c.x > 0) list.Add(new Vector2Int(c.x - 1, c.y));
            if (c.x < Width - 1) list.Add(new Vector2Int(c.x + 1, c.y));
            if (c.y > 0) list.Add(new Vector2Int(c.x, c.y - 1));
            if (c.y < Depth - 1) list.Add(new Vector2Int(c.x, c.y + 1));
            return list;
        }

        // Neighbours inside one section.
        private static List<Vector2Int> Neighbours(Vector2Int c, Section s)
        {
            var list = new List<Vector2Int>(4);
            foreach (var n in Neighbours(c))
                if (InSection(n, s)) list.Add(n);
            return list;
        }

        private static bool InSection(Vector2Int c, Section s) =>
            c.x >= s.offset.x && c.x < s.offset.x + Size && c.y >= s.offset.y && c.y < s.offset.y + Size;

        private static bool HasWall(Vector2Int a, Vector2Int b)
        {
            if (a.x == b.x) return _hWalls[a.x, Mathf.Max(a.y, b.y)];
            return _vWalls[Mathf.Max(a.x, b.x), a.y];
        }

        private static void SetWall(Vector2Int a, Vector2Int b, bool wall)
        {
            if (a.x == b.x) _hWalls[a.x, Mathf.Max(a.y, b.y)] = wall;
            else _vWalls[Mathf.Max(a.x, b.x), a.y] = wall;
        }

        private static List<Vector2Int> OpenNeighbours(Vector2Int c)
        {
            var list = new List<Vector2Int>(4);
            foreach (var n in Neighbours(c))
                if (!HasWall(c, n)) list.Add(n);
            return list;
        }

        private static int[,] Distances(Vector2Int from)
        {
            var dist = new int[Width, Depth];
            for (int x = 0; x < Width; x++)
            for (int z = 0; z < Depth; z++) dist[x, z] = -1;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            dist[from.x, from.y] = 0;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var n in OpenNeighbours(c))
                {
                    if (dist[n.x, n.y] >= 0) continue;
                    dist[n.x, n.y] = dist[c.x, c.y] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        private static Vector3 CellCenter(Vector2Int c) => new Vector3((c.x + 0.5f) * Cell, 0f, (c.y + 0.5f) * Cell);

        private static IEnumerable<Vector2Int> SectionCells(Section s)
        {
            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
                yield return s.offset + new Vector2Int(x, z);
        }

        private static IEnumerable<Vector2Int> AllCells()
        {
            for (int z = 0; z < Depth; z++)
            for (int x = 0; x < Width; x++)
                yield return new Vector2Int(x, z);
        }

        // ---------------------------------------------------------------- geometry

        private static void BuildWalls(GameObject walls)
        {
            // Merge runs of wall edges on each grid line into single wall pieces.
            for (int z = 0; z <= Depth; z++)
            {
                int runStart = -1;
                for (int x = 0; x <= Width; x++)
                {
                    bool wall = x < Width && _hWalls[x, z];
                    if (wall && runStart < 0) runStart = x;
                    if (!wall && runStart >= 0)
                    {
                        WallRun(walls, true, z * Cell, runStart * Cell, x * Cell);
                        runStart = -1;
                    }
                }
            }

            for (int x = 0; x <= Width; x++)
            {
                int runStart = -1;
                for (int z = 0; z <= Depth; z++)
                {
                    bool wall = z < Depth && _vWalls[x, z];
                    if (wall && runStart < 0) runStart = z;
                    if (!wall && runStart >= 0)
                    {
                        WallRun(walls, false, x * Cell, runStart * Cell, z * Cell);
                        runStart = -1;
                    }
                }
            }
        }

        // A wall along x (alongX) or z between a and b, at `line` on the other axis. Both ends are padded by half a
        // wall thickness so corners close.
        private static void WallRun(GameObject parent, bool alongX, float line, float a, float b)
        {
            float half = WallThickness * 0.5f;
            a -= half;
            b += half;
            float length = b - a;
            float mid = (a + b) * 0.5f;
            Vector3 center = alongX
                ? new Vector3(mid, WallHeight * 0.5f, line)
                : new Vector3(line, WallHeight * 0.5f, mid);
            Vector3 size = alongX
                ? new Vector3(length, WallHeight, WallThickness)
                : new Vector3(WallThickness, WallHeight, length);
            Slab(parent, $"Wall_{(alongX ? "X" : "Z")}_{line:0}_{a:0.0}", center, size, _wallMat);
        }

        // A box mesh whose UVs are in world metres, so the tiled Asylum textures keep one density everywhere.
        private static GameObject Slab(GameObject parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = center;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(center, size);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
            return go;
        }

        private static Mesh BoxMesh(Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void Face(Vector3 normal, Vector3 u, Vector3 v)
            {
                Vector3 c = Vector3.Scale(normal, h);
                int i = verts.Count;
                Vector3[] corners = { c - u - v, c + u - v, c + u + v, c - u + v };
                foreach (var p in corners)
                {
                    verts.Add(p);
                    normals.Add(normal);
                    Vector3 w = p + center;
                    Vector2 uv;
                    if (Mathf.Abs(normal.x) > 0.5f) uv = new Vector2(w.z * -Mathf.Sign(normal.x), w.y);
                    else if (Mathf.Abs(normal.y) > 0.5f) uv = new Vector2(w.x, w.z);
                    else uv = new Vector2(w.x * Mathf.Sign(normal.z), w.y);
                    uvs.Add(uv);
                }
                tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }

            Face(Vector3.right, new Vector3(0, 0, h.z), new Vector3(0, h.y, 0));
            Face(Vector3.left, new Vector3(0, 0, -h.z), new Vector3(0, h.y, 0));
            Face(Vector3.up, new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z));
            Face(Vector3.down, new Vector3(h.x, 0, 0), new Vector3(0, 0, -h.z));
            Face(Vector3.forward, new Vector3(-h.x, 0, 0), new Vector3(0, h.y, 0));
            Face(Vector3.back, new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0));

            var mesh = new Mesh { name = "ProtoBox" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            // Flip any face whose winding came out inward, then derive tangents for the normal maps.
            FixWinding(mesh);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void FixWinding(Mesh mesh)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 faceNormal = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(faceNormal, n[t[i]]) < 0f) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            mesh.triangles = t;
        }

        // ---------------------------------------------------------------- dressing

        private static void BuildProps(GameObject props, Section s)
        {
            var parent = Child(props, $"Section_{s.name}");
            foreach (var room in s.rooms)
            {
                Vector3 o = new Vector3((s.offset.x + room.rect.x) * Cell, 0f, (s.offset.y + room.rect.y) * Cell);
                float w = room.rect.width * Cell;
                float h = room.rect.height * Cell;
                switch (room.type)
                {
                    case RoomType.Ward:
                        int beds = room.rect.height >= 3 ? 3 : 2;
                        for (int i = 0; i < beds; i++) Prop(parent, "Bed", o + new Vector3(1.25f, 0f, 2f + i * 3.6f), 90f);
                        Prop(parent, "WheelChair", o + new Vector3(6.5f, 0f, h - 1.5f), 200f);
                        Prop(parent, "MedRack", o + new Vector3(w - 0.6f, 0f, 2.2f), 270f);
                        break;
                    case RoomType.Office:
                        Prop(parent, "TableOffice", o + new Vector3(3f, 0f, 3f), 0f);
                        Prop(parent, "TableOffice", o + new Vector3(7f, 0f, 3f), 0f);
                        Prop(parent, "ChairOffice", o + new Vector3(3f, 0f, 2.1f), 0f);
                        Prop(parent, "BigShelf", o + new Vector3(w - 0.7f, 0f, 5f), 90f);
                        Prop(parent, "Cupboard", o + new Vector3(6f, 0f, h - 0.45f), 180f);
                        break;
                    case RoomType.Hall:
                        Prop(parent, "BigShelf", o + new Vector3(4f, 0f, 4f), 0f);
                        Prop(parent, "BigShelf", o + new Vector3(8f, 0f, 8f), 90f);
                        Prop(parent, "Bench", o + new Vector3(2.5f, 0f, 9.5f), 0f);
                        Prop(parent, "MorgueWheelBed", o + new Vector3(9.5f, 0f, 3f), 30f);
                        for (int i = 0; i < 4; i++)
                            Prop(parent, i % 2 == 0 ? "Box_V1" : "Box_V2", o + new Vector3(5.4f + i * 0.5f, 0f, 2.2f), i * 25f);
                        break;
                    case RoomType.Morgue:
                        Prop(parent, "MorgueWheelBed", o + new Vector3(2f, 0f, 2f), 0f);
                        Prop(parent, "MorgueWheelBed", o + new Vector3(2f, 0f, 4.5f), 0f);
                        Prop(parent, "SurgeryTable", o + new Vector3(5.5f, 0f, 5.5f), 0f);
                        Prop(parent, "MedRack", o + new Vector3(w - 0.6f, 0f, 2f), 270f);
                        break;
                }
            }

            // A few things left in dead ends.
            string[] junk = { "WheelChair", "Bench", "Box_V2", "SurgeryTable" };
            foreach (var c in SectionCells(s))
            {
                if (_room[c.x, c.y] >= 0 || c == Start) continue;
                var open = OpenNeighbours(c);
                if (open.Count != 1 || s.rng.NextDouble() > 0.5) continue;
                Vector3 away = CellCenter(c) - (CellCenter(open[0]) - CellCenter(c)).normalized * 1.3f;
                Prop(parent, junk[s.rng.Next(junk.Length)], away, s.rng.Next(360));
            }
        }

        private static void BuildLights(GameObject lights)
        {
            foreach (var s in Sections)
            {
                for (int r = 0; r < s.rooms.Length; r++)
                {
                    var rect = s.rooms[r].rect;
                    Vector3 center = new Vector3((s.offset.x + rect.x + rect.width * 0.5f) * Cell, WallHeight - 0.3f,
                        (s.offset.y + rect.y + rect.height * 0.5f) * Cell);
                    // Only the halls get shadows, to keep the shadow-casting light count low on the bigger map.
                    PointLight(lights, $"{s.name}_Room_{r}", center, rect.width * Cell * 1.1f, 2.5f,
                        s.rooms[r].type == RoomType.Hall);
                }
            }

            foreach (var c in AllCells())
            {
                if (_room[c.x, c.y] >= 0 || (c.x + c.y * 2) % 3 != 0) continue;
                PointLight(lights, $"Corridor_{c.x}_{c.y}", CellCenter(c) + Vector3.up * (WallHeight - 0.3f), 8f, 1.8f, false);
            }
        }

        private static void PointLight(GameObject parent, string name, Vector3 position, float range, float intensity, bool shadows)
        {
            var go = new GameObject($"Light_{name}");
            go.transform.SetParent(parent.transform, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = new Color(1f, 0.9f, 0.78f);
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        private static void Prop(GameObject parent, string prefabName, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AsylumPrefabs + prefabName + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"Prototype map: prefab {prefabName} not found.");
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            PlaceOnFloor(go, position, yaw);
        }

        // Asylum prefabs keep their positions from the original building; move one so its rendered bounds sit
        // centred on `target` and resting on the floor.
        private static void PlaceOnFloor(GameObject go, Vector3 target, float yaw)
        {
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                go.transform.position = target;
                return;
            }
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            go.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
        }

        // ---------------------------------------------------------------- claymores and enemies

        // Claymores go in straight corridor cells away from the start. Each faces along its corridor, set back
        // toward one wall, so walking in from the front trips it — go around through a loop and disarm it from
        // behind, or shoot the sensor.
        private static void PlaceClaymores(GameObject parent, Section s, int[,] dist)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClaymorePrefab);
            var rng = s.rng;
            var candidates = new List<Vector2Int>();
            foreach (var c in SectionCells(s))
            {
                if (_room[c.x, c.y] >= 0 || dist[c.x, c.y] < 4) continue;
                var open = OpenNeighbours(c);
                if (open.Count != 2) continue;
                Vector2Int d0 = open[0] - c, d1 = open[1] - c;
                if (d0 + d1 != Vector2Int.zero) continue; // straight corridors only
                candidates.Add(c);
            }
            Shuffle(candidates, rng);

            var used = new List<Vector2Int>();
            foreach (var c in candidates)
            {
                if (used.Count >= ClaymoresPerSection) break;
                bool near = false;
                foreach (var u in used)
                    if (Mathf.Abs(u.x - c.x) + Mathf.Abs(u.y - c.y) < 3) near = true;
                if (near) continue;
                used.Add(c);

                var open = OpenNeighbours(c);
                Vector2Int facing = open[rng.Next(2)] - c;
                Vector3 forward = new Vector3(facing.x, 0f, facing.y);
                Vector3 side = Vector3.Cross(Vector3.up, forward) * (rng.Next(2) == 0 ? 1f : -1f);
                Vector3 position = CellCenter(c) - forward * 1.4f + side * 1.3f;

                var mine = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                mine.name = $"Claymore_{s.name}_{c.x}_{c.y}";
                mine.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            }
        }

        // Section A keeps its hand-placed guards and patrols.
        private static void PlaceEnemiesA(GameObject enemies, GameObject routes)
        {
            Enemy(enemies, "Shotgun", "A_Guard_Morgue", CellCenter(new Vector2Int(8, 8)), 225f, EnemyBehavior.Guard, null);
            Enemy(enemies, "Rifle", "A_Guard_Office", CellCenter(new Vector2Int(8, 0)) + new Vector3(0f, 0f, 1.5f), 270f,
                EnemyBehavior.Guard, null);
            Enemy(enemies, "Pistol", "A_Guard_Ward", CellCenter(new Vector2Int(1, 8)), 180f, EnemyBehavior.Guard, null);

            Vector2Int[][] routeCells =
            {
                new[] { new Vector2Int(4, 2), new Vector2Int(4, 6) },
                new[] { new Vector2Int(2, 4), new Vector2Int(6, 4), new Vector2Int(6, 7) },
                new[] { new Vector2Int(0, 4), new Vector2Int(2, 8), new Vector2Int(5, 8) },
                new[] { new Vector2Int(8, 3), new Vector2Int(8, 6), new Vector2Int(5, 1) },
            };
            string[] weapons = { "Rifle", "Pistol", "Rifle", "Shotgun" };
            for (int r = 0; r < routeCells.Length; r++)
                Patrol(enemies, routes, $"A_Patrol_{r}", weapons[r], routeCells[r]);
        }

        // Added sections: a guard in every room except the hall, and three patrols between far-apart corridor cells
        // (the NavMesh finds the way through the maze).
        private static void PlaceEnemies(GameObject enemies, GameObject routes, Section s)
        {
            var rng = new System.Random(s.seed + 7);
            string[] weapons = { "Rifle", "Pistol", "Shotgun" };
            int w = 0;

            foreach (var room in s.rooms)
            {
                if (room.type == RoomType.Hall) continue;
                // Far corner cell of the room, facing the room's centre.
                var rect = room.rect;
                var corner = s.offset + new Vector2Int(rect.xMax - 1, rect.yMax - 1);
                Vector3 pos = CellCenter(corner);
                Vector3 center = new Vector3((s.offset.x + rect.center.x) * Cell, 0f, (s.offset.y + rect.center.y) * Cell);
                Vector3 look = center - pos;
                float yaw = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
                Enemy(enemies, weapons[w++ % weapons.Length], $"{s.name}_Guard_{room.type}", pos, yaw, EnemyBehavior.Guard, null);
            }

            var corridors = new List<Vector2Int>();
            foreach (var c in SectionCells(s))
                if (_room[c.x, c.y] < 0) corridors.Add(c);
            Shuffle(corridors, rng);

            for (int p = 0; p < 3; p++)
            {
                var route = new List<Vector2Int>();
                foreach (var c in corridors)
                {
                    if (route.Count >= 3) break;
                    bool far = true;
                    foreach (var r in route)
                        if (Mathf.Abs(r.x - c.x) + Mathf.Abs(r.y - c.y) < 4) far = false;
                    if (far) route.Add(c);
                }
                foreach (var r in route) corridors.Remove(r);
                if (route.Count < 2) continue;
                Patrol(enemies, routes, $"{s.name}_Patrol_{p}", weapons[w++ % weapons.Length], route.ToArray());
            }
        }

        private static void Patrol(GameObject enemies, GameObject routes, string name, string weapon, Vector2Int[] cells)
        {
            var points = new Transform[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                points[i] = Point(routes, $"{name}_{i}", CellCenter(cells[i]));
            Enemy(enemies, weapon, name, points[0].position, 0f, EnemyBehavior.Patrol, points);
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

        private static Transform Point(GameObject parent, string name, Vector3 position)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent.transform, false);
            point.transform.position = position;
            return point.transform;
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        // A copy of an Asylum material with its tiling set for world-metre UVs (scale = repeats per metre) and a tint.
        // Rebuilt from the source every time, so changing the source or numbers here takes effect on the next build.
        private static Material DerivedMaterial(string name, string sourcePath, float repeatsPerMetre, Color tint)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
                AssetDatabase.CreateFolder("Assets/Materials", "Prototype");
            }

            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }
            material.SetTextureScale("_BaseMap", Vector2.one * repeatsPerMetre);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Indoors under a ceiling: a dim flat ambient so the lamps carry the lighting, and no sun.
        private static void SetupLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.09f, 0.1f);
            var sun = GameObject.Find("Directional Light");
            if (sun != null)
            {
                Undo.RecordObject(sun, "Build Prototype Map");
                sun.SetActive(false);
            }
        }
    }
}
