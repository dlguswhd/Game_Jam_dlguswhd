using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.Weapons
{
    // Shared mesh/material, bounded object count. Marks follow the exact struck leaf when the door swings.
    public static class BulletImpactMarks
    {
        private static readonly Queue<GameObject> Marks = new Queue<GameObject>();
        private static Mesh _mesh;
        private static Material _material;

        public static void Add(RaycastHit hit)
        {
            if (hit.collider == null) return;
            if (_material == null)
            {
                var shader = Resources.Load<Shader>("BulletMark");
                if (shader == null) return;
                _material = new Material(shader) { name = "BulletMarkShared" };
            }
            if (_mesh == null) _mesh = BuildMesh();
            while (Marks.Count >= 256)
            {
                var oldest = Marks.Dequeue();
                if (oldest != null) Object.Destroy(oldest);
            }
            var mark = new GameObject("BulletImpactMark", typeof(MeshFilter), typeof(MeshRenderer));
            mark.transform.SetPositionAndRotation(hit.point + hit.normal * 0.0015f,
                Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            mark.transform.localScale = Vector3.one * Random.Range(0.065f, 0.085f);
            mark.transform.SetParent(hit.collider.transform, true);
            mark.GetComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = mark.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Marks.Enqueue(mark);
        }

        private static Mesh BuildMesh()
        {
            const int segments = 24;
            var vertices = new Vector3[1 + segments * 3];
            var colors = new Color[vertices.Length];
            var triangles = new List<int>(segments * 15);
            colors[0] = new Color(0.008f, 0.006f, 0.004f, 1f);
            for (int ring = 0; ring < 3; ring++)
            {
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    float radius = ring == 0 ? 0.13f : ring == 1 ? 0.24f : 0.5f;
                    radius *= 0.85f + 0.15f * Mathf.Sin(i * 17.3f);
                    int index = 1 + ring * segments + i;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                    colors[index] = ring == 0 ? colors[0] : ring == 1
                        ? new Color(0.28f, 0.20f, 0.10f, 0.95f) : new Color(0.06f, 0.04f, 0.02f, 0f);
                    int next = 1 + ring * segments + (i + 1) % segments;
                    if (ring == 0) { triangles.Add(0); triangles.Add(index); triangles.Add(next); }
                    else
                    {
                        int inner = index - segments;
                        int innerNext = next - segments;
                        triangles.Add(inner); triangles.Add(index); triangles.Add(next);
                        triangles.Add(inner); triangles.Add(next); triangles.Add(innerNext);
                    }
                }
            }
            var mesh = new Mesh { name = "BulletMarkShared", vertices = vertices, colors = colors };
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
