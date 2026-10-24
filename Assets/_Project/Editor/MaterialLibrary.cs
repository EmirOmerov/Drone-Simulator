using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneSim.EditorTools
{
    /// <summary>
    /// URP Lit materials built from the CC0 texture sets in Art/Textures/CC0/&lt;Set&gt;/
    /// (Color, Normal, Roughness, optional AO and Metalness). Meshes from <see cref="MeshLibrary"/> have UVs in
    /// metres, so each material's tiling is 1 / tile size.
    /// </summary>
    static class MaterialLibrary
    {
        public const string MaterialsDir = "Assets/_Project/Art/Materials";
        const string TextureRoot = "Assets/_Project/Art/Textures/CC0";
        const string GeneratedTextureDir = "Assets/_Project/Art/Textures/Generated";

        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static void Begin()
        {
            Cache.Clear();
            // The builder owns this folder: start clean so no stale materials linger.
            AssetDatabase.DeleteAsset(MaterialsDir);
            SceneBuilder.EnsureFolder(MaterialsDir);
            SceneBuilder.EnsureFolder(GeneratedTextureDir);
        }

        // ------------------------------------------------------------ CC0 surfaces

        public static Material Grass => Pbr("Grass004", 3f, new Color(0.8f, 0.88f, 0.7f), 0.5f);
        public static Material Concrete => Pbr("Concrete034", 4f, Color.white, 0.8f);
        public static Material ConcreteDark => Pbr("Concrete034", 4f, new Color(0.62f, 0.62f, 0.62f), 0.8f, "Dark");
        public static Material Asphalt => Pbr("Asphalt010", 4f, Color.white, 0.8f);
        public static Material Bricks => Pbr("Bricks085", 3f, Color.white, 0.6f);
        public static Material CorrugatedSteel => Pbr("CorrugatedSteel005", 2.5f, Color.white, 1f);
        public static Material RedPaintedMetal => Pbr("PaintedMetal004", 2f, Color.white, 1f);
        public static Material MetalPlates => Pbr("MetalPlates006", 2f, Color.white, 1f);
        public static Material Wood => Pbr("Wood049", 1.5f, Color.white, 0.7f);

        /// <summary>Painted (non-metallic) corrugated steel, so the tint reads clearly in sunlight.</summary>
        public static Material Container(Color tint, string variant) =>
            Pbr("CorrugatedSteel005", 2.5f, tint, 0.7f, variant, metallic: false);

        public static Material Sky()
        {
            const string name = "Sky";
            if (Cache.TryGetValue(name, out Material cached))
                return cached;
            var material = new Material(Shader.Find("Skybox/Procedural")) { name = name };
            material.SetFloat("_SunSize", 0.035f);
            material.SetFloat("_AtmosphereThickness", 0.9f);
            material.SetColor("_SkyTint", new Color(0.5f, 0.52f, 0.55f));
            // Below-horizon colour matches the fogged ground, so there is no grey band at the world's edge.
            material.SetColor("_GroundColor", new Color(0.55f, 0.62f, 0.6f));
            material.SetFloat("_Exposure", 1.25f);
            return Save(name, material);
        }

        // ------------------------------------------------------------ plain materials

        public static Material Plain(string name, Color color, float smoothness, float metallic = 0f)
        {
            if (Cache.TryGetValue(name, out Material cached))
                return cached;
            var material = NewLit(name);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            return Save(name, material);
        }

        public static Material Emissive(string name, Color color, float intensity)
        {
            if (Cache.TryGetValue(name, out Material cached))
                return cached;
            var material = NewLit(name);
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", color * intensity);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return Save(name, material);
        }

        public static Material Unlit(string name, Color color)
        {
            if (Cache.TryGetValue(name, out Material cached))
                return cached;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            material.SetColor("_BaseColor", color);
            return Save(name, material);
        }

        /// <summary>Alpha-clipped, double-sided, instanced material for <see cref="DroneSim.Environment.GrassField"/>.</summary>
        public static Material GrassBlades()
        {
            const string name = "GrassBlades";
            if (Cache.TryGetValue(name, out Material cached))
                return cached;

            var material = NewLit(name);
            material.SetTexture("_BaseMap", GrassBladeTexture());
            material.SetColor("_BaseColor", new Color(0.9f, 0.95f, 0.85f));
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.doubleSidedGI = true;
            return Save(name, material);
        }

        // ------------------------------------------------------------ implementation

        static Material Pbr(string set, float tileMetres, Color tint, float smoothnessScale, string variant = null,
            bool metallic = true)
        {
            string name = variant == null ? set : $"{set}_{variant}";
            if (Cache.TryGetValue(name, out Material cached))
                return cached;

            string folder = $"{TextureRoot}/{set}";
            var material = NewLit(name);
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BaseMap", ImportTexture($"{folder}/Color.jpg", TextureKind.Color));

            var normal = ImportTexture($"{folder}/Normal.jpg", TextureKind.Normal);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            var ao = ImportTexture($"{folder}/AO.jpg", TextureKind.Linear);
            if (ao != null)
            {
                material.SetTexture("_OcclusionMap", ao);
                material.SetFloat("_OcclusionStrength", 1f);
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            var metallicSmoothness = PackMetallicSmoothness(set, folder, metallic);
            if (metallicSmoothness != null)
            {
                material.SetTexture("_MetallicGlossMap", metallicSmoothness);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            // With a metallic/smoothness map, _Smoothness scales the map's alpha.
            material.SetFloat("_Smoothness", metallicSmoothness != null ? smoothnessScale : 0.3f);

            float scale = 1f / tileMetres;
            material.SetTextureScale("_BaseMap", new Vector2(scale, scale));
            return Save(name, material);
        }

        static Material NewLit(string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new System.InvalidOperationException("URP Lit shader not found. Is URP the active pipeline?");
            return new Material(shader) { name = name, enableInstancing = true };
        }

        static Material Save(string name, Material material)
        {
            AssetDatabase.CreateAsset(material, $"{MaterialsDir}/{name}.mat");
            Cache[name] = material;
            return material;
        }

        enum TextureKind { Color, Normal, Linear }

        static Texture2D ImportTexture(string path, TextureKind kind, bool readable = false)
        {
            if (!File.Exists(path))
                return null;

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = kind == TextureKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = kind == TextureKind.Color;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.isReadable = readable;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// URP Lit reads metallic from R and smoothness from A of one map. CC0 sets ship separate Metalness and
        /// Roughness images, so combine them: R = metalness (or 0), A = 1 - roughness.
        /// </summary>
        static Texture2D PackMetallicSmoothness(string set, string folder, bool metallic)
        {
            string outputPath = $"{GeneratedTextureDir}/{set}_{(metallic ? "MetallicSmoothness" : "Smoothness")}.png";
            if (!File.Exists(outputPath))
            {
                var roughness = ImportTexture($"{folder}/Roughness.jpg", TextureKind.Linear, readable: true);
                if (roughness == null)
                    return null;
                var metalness = metallic
                    ? ImportTexture($"{folder}/Metalness.jpg", TextureKind.Linear, readable: true)
                    : null;

                int width = roughness.width, height = roughness.height;
                Color[] rough = roughness.GetPixels();
                Color[] metal = metalness != null && metalness.width == width && metalness.height == height
                    ? metalness.GetPixels()
                    : null;

                var packed = new Color[rough.Length];
                for (int i = 0; i < rough.Length; i++)
                    packed[i] = new Color(metal != null ? metal[i].r : 0f, 0f, 0f, 1f - rough[i].r);

                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                texture.SetPixels(packed);
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(outputPath);

                // The source maps no longer need to be CPU-readable.
                ImportTexture($"{folder}/Roughness.jpg", TextureKind.Linear);
                if (metalness != null)
                    ImportTexture($"{folder}/Metalness.jpg", TextureKind.Linear);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(outputPath);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        }

        /// <summary>Procedural grass blades on a transparent background.</summary>
        static Texture2D GrassBladeTexture()
        {
            string path = $"{GeneratedTextureDir}/GrassBlades_v2.png";
            if (!File.Exists(path))
            {
                const int size = 256;
                var pixels = new Color[size * size];
                var random = new System.Random(3);
                for (int blade = 0; blade < 28; blade++)
                {
                    float baseX = 20 + (float)random.NextDouble() * (size - 40);
                    float bladeHeight = size * (0.55f + (float)random.NextDouble() * 0.45f);
                    float lean = ((float)random.NextDouble() - 0.5f) * 70f;
                    float baseWidth = 4f + (float)random.NextDouble() * 4f;
                    var bottom = new Color(0.12f, 0.2f, 0.06f);
                    var top = Color.Lerp(new Color(0.34f, 0.46f, 0.15f), new Color(0.52f, 0.56f, 0.24f),
                        (float)random.NextDouble());

                    for (int y = 0; y < bladeHeight; y++)
                    {
                        float t = y / bladeHeight;
                        float centre = baseX + lean * t * t;
                        float halfWidth = baseWidth * (1f - t) * 0.5f + 0.5f;
                        Color colour = Color.Lerp(bottom, top, t);
                        for (int x = Mathf.FloorToInt(centre - halfWidth); x <= Mathf.CeilToInt(centre + halfWidth); x++)
                        {
                            if (x < 0 || x >= size)
                                continue;
                            pixels[y * size + x] = new Color(colour.r, colour.g, colour.b, 1f);
                        }
                    }
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.SetPixels(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.5f;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
