using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneSim.Environment
{
    /// <summary>
    /// Draws tens of thousands of grass clumps with GPU instancing (no GameObjects, no colliders).
    /// Clumps are scattered in an XZ area, skipping exclusion rectangles (concrete, roads, buildings).
    /// </summary>
    [ExecuteAlways]
    public sealed class GrassField : MonoBehaviour
    {
        [SerializeField] Mesh clumpMesh;
        [SerializeField] Material material;
        [SerializeField] int seed = 7;
        [SerializeField] int count = 30000;
        [SerializeField] Vector2 areaMin = new Vector2(-90f, -60f);
        [SerializeField] Vector2 areaMax = new Vector2(90f, 150f);
        [SerializeField] Vector2 scaleRange = new Vector2(0.7f, 1.35f);
        [Tooltip("XZ rectangles (x, z, width, depth) where no grass grows.")]
        [SerializeField] List<Rect> exclusions = new List<Rect>();

        const int BatchSize = 1023;

        Matrix4x4[] matrices;
        RenderParams renderParams;

        public void Configure(Mesh mesh, Material grassMaterial, Vector2 min, Vector2 max, int instanceCount,
            IEnumerable<Rect> excluded)
        {
            clumpMesh = mesh;
            material = grassMaterial;
            areaMin = min;
            areaMax = max;
            count = instanceCount;
            exclusions = new List<Rect>(excluded);
            Generate();
        }

        void OnEnable() => Generate();
        void OnValidate() => Generate();

        void Generate()
        {
            if (clumpMesh == null || material == null)
            {
                matrices = null;
                return;
            }

            var random = new System.Random(seed);
            var result = new List<Matrix4x4>(count);
            int attempts = count * 3;
            while (result.Count < count && attempts-- > 0)
            {
                float x = Mathf.Lerp(areaMin.x, areaMax.x, (float)random.NextDouble());
                float z = Mathf.Lerp(areaMin.y, areaMax.y, (float)random.NextDouble());
                if (IsExcluded(x, z))
                    continue;

                float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());
                var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                result.Add(Matrix4x4.TRS(new Vector3(x, 0f, z), rotation, new Vector3(scale, scale, scale)));
            }
            matrices = result.ToArray();

            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(areaMin.x, -1f, areaMin.y), new Vector3(areaMax.x, 3f, areaMax.y));
            renderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = bounds,
            };
        }

        bool IsExcluded(float x, float z)
        {
            var point = new Vector2(x, z);
            foreach (Rect rect in exclusions)
                if (rect.Contains(point))
                    return true;
            return false;
        }

        void Update() => Draw();

        /// <summary>Submits this frame's grass draw calls. Public so editor tools can render it on demand.</summary>
        public void Draw()
        {
            if (matrices == null || matrices.Length == 0)
                return;
            for (int start = 0; start < matrices.Length; start += BatchSize)
            {
                int batch = Mathf.Min(BatchSize, matrices.Length - start);
                Graphics.RenderMeshInstanced(renderParams, clumpMesh, 0, matrices, batch, start);
            }
        }
    }
}
