using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DroneSim.EditorTools
{
    /// <summary>
    /// Generated meshes with UVs in metres, so a material's tiling (1 / tile size) looks the same on every
    /// object regardless of its size. All meshes are stored as sub-assets of one file so the scene can reference them.
    /// </summary>
    static class MeshLibrary
    {
        const string AssetPath = "Assets/_Project/Art/GeneratedMeshes.asset";

        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();
        static Object container;

        public static void Begin()
        {
            Cache.Clear();
            AssetDatabase.DeleteAsset(AssetPath);
            container = new Mesh { name = "GeneratedMeshes" };
            AssetDatabase.CreateAsset(container, AssetPath);
        }

        public static void End() => AssetDatabase.SaveAssets();

        public static Mesh Box(Vector3 size)
        {
            string key = $"box_{size.x:0.###}_{size.y:0.###}_{size.z:0.###}";
            if (Cache.TryGetValue(key, out Mesh cached))
                return cached;

            var builder = new MeshBuilder();
            Vector3 half = size * 0.5f;
            builder.Face(Vector3.right * half.x, Vector3.right, Vector3.up, half.z, half.y);
            builder.Face(Vector3.left * half.x, Vector3.left, Vector3.up, half.z, half.y);
            builder.Face(Vector3.forward * half.z, Vector3.forward, Vector3.up, half.x, half.y);
            builder.Face(Vector3.back * half.z, Vector3.back, Vector3.up, half.x, half.y);
            builder.Face(Vector3.up * half.y, Vector3.up, Vector3.forward, half.x, half.z);
            builder.Face(Vector3.down * half.y, Vector3.down, Vector3.forward, half.x, half.z);
            return Store(key, builder.Build(key));
        }

        public static Mesh Cylinder(float radius, float height, int segments = 20)
        {
            string key = $"cyl_{radius:0.###}_{height:0.###}_{segments}";
            if (Cache.TryGetValue(key, out Mesh cached))
                return cached;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float halfHeight = height * 0.5f;

            // Side: a ring of segments + 1 vertices (duplicated seam), bottom then top.
            for (int ring = 0; ring < 2; ring++)
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(normal * radius + Vector3.up * (ring == 0 ? -halfHeight : halfHeight));
                normals.Add(normal);
                uvs.Add(new Vector2(angle * radius, ring == 0 ? 0f : height));
            }
            int stride = segments + 1;
            for (int i = 0; i < segments; i++)
            {
                int b0 = i, b1 = i + 1, t0 = i + stride, t1 = i + 1 + stride;
                triangles.AddRange(new[] { b0, t1, b1, b0, t0, t1 });
            }

            // Caps.
            for (int cap = 0; cap < 2; cap++)
            {
                bool top = cap == 1;
                float y = top ? halfHeight : -halfHeight;
                int centre = vertices.Count;
                vertices.Add(new Vector3(0f, y, 0f));
                normals.Add(top ? Vector3.up : Vector3.down);
                uvs.Add(Vector2.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    var point = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                    vertices.Add(point);
                    normals.Add(top ? Vector3.up : Vector3.down);
                    uvs.Add(new Vector2(point.x, point.z));
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = centre + 1 + i, b = centre + 2 + i;
                    if (top)
                        triangles.AddRange(new[] { centre, b, a });
                    else
                        triangles.AddRange(new[] { centre, a, b });
                }
            }

            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return Store(key, mesh);
        }

        /// <summary>Three crossed quads with upward normals (soft, even lighting), pivot at the bottom.</summary>
        public static Mesh GrassClump(float width = 0.55f, float height = 0.42f)
        {
            const string key = "grass_clump";
            if (Cache.TryGetValue(key, out Mesh cached))
                return cached;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                Quaternion rotation = Quaternion.Euler(0f, i * 60f, 0f);
                Vector3 side = rotation * Vector3.right * (width * 0.5f);
                int start = vertices.Count;
                vertices.Add(-side);
                vertices.Add(side);
                vertices.Add(side + Vector3.up * height);
                vertices.Add(-side + Vector3.up * height);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            var normals = new List<Vector3>();
            for (int i = 0; i < vertices.Count; i++)
                normals.Add(Vector3.up);

            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return Store(key, mesh);
        }

        static Mesh Store(string key, Mesh mesh)
        {
            AssetDatabase.AddObjectToAsset(mesh, container);
            Cache[key] = mesh;
            return mesh;
        }

        sealed class MeshBuilder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> triangles = new List<int>();

            /// <summary>
            /// Quad facing <paramref name="normal"/>. The horizontal axis is cross(normal, vAxis), which makes it
            /// appear to the right when seen from outside, so (0, 2, 1) / (0, 3, 2) is clockwise = front face in Unity.
            /// </summary>
            public void Face(Vector3 centre, Vector3 normal, Vector3 vAxis, float halfU, float halfV)
            {
                Vector3 uAxis = Vector3.Cross(normal, vAxis);
                Vector3 u = uAxis * halfU, v = vAxis * halfV;
                int start = vertices.Count;
                Vector3[] corners = { centre - u - v, centre + u - v, centre + u + v, centre - u + v };
                foreach (Vector3 corner in corners)
                {
                    vertices.Add(corner);
                    normals.Add(normal);
                    uvs.Add(new Vector2(Vector3.Dot(corner, uAxis), Vector3.Dot(corner, vAxis)));
                }
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }

    /// <summary>Creates static scene objects from generated meshes.</summary>
    static class Geo
    {
        public static GameObject Box(Transform parent, string name, Vector3 localCentre, Vector3 size, Material material,
            Quaternion? localRotation = null, bool collider = true)
        {
            var go = Create(parent, name, localCentre, localRotation, MeshLibrary.Box(size), material);
            if (collider)
                go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 localCentre, float radius, float height,
            Material material, Quaternion? localRotation = null, bool collider = true)
        {
            Mesh mesh = MeshLibrary.Cylinder(radius, height);
            var go = Create(parent, name, localCentre, localRotation, mesh, material);
            // Convex mesh collider: exact for flat discs (pads) where a capsule would bulge far out.
            if (collider)
                go.AddComponent<MeshCollider>().convex = true;
            return go;
        }

        public static Transform Group(Transform parent, string name, Vector3 localPosition = default,
            Quaternion? localRotation = null)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = localPosition;
            group.localRotation = localRotation ?? Quaternion.identity;
            return group;
        }

        static GameObject Create(Transform parent, string name, Vector3 localCentre, Quaternion? localRotation,
            Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }
    }
}
