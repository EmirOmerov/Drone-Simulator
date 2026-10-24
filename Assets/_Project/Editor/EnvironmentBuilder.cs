using DroneSim.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DroneSim.EditorTools
{
    /// <summary>Outdoor world: grass field, concrete apron, road, container yard, sun, sky, fog, post-processing.</summary>
    static class EnvironmentBuilder
    {
        // Layout (metres, XZ). The factory sits on the apron; see FactoryBuilder.
        public static readonly Rect Apron = new Rect(-35f, -20f, 70f, 120f);
        public static readonly Rect Road = new Rect(35f, 39f, 170f, 8f);

        const string PostProfilePath = "Assets/_Project/Settings/PostProcessProfile.asset";

        public static void Build()
        {
            var root = new GameObject("Environment").transform;

            CreateLighting();
            CreateGround(root);
            CreateGrass(root);
            CreateContainerYard(root);
            CreateBarrels(root, new Vector3(-21f, 0f, 12f));
            CreateBarrels(root, new Vector3(21f, 0f, 80f));
            CreatePostProcessing(root);
        }

        static void CreateLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.skybox = MaterialLibrary.Sky();
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.8f, 0.86f);
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 600f;
        }

        static void CreateGround(Transform root)
        {
            // Large enough that its edge is always lost in the fog.
            Geo.Box(root, "Ground", new Vector3(0f, -0.25f, 40f), new Vector3(3000f, 0.5f, 3000f), MaterialLibrary.Grass);

            // Slab tops sit a few cm above the grass so they never z-fight.
            Geo.Box(root, "ConcreteApron", new Vector3(Apron.center.x, -0.01f, Apron.center.y),
                new Vector3(Apron.width, 0.1f, Apron.height), MaterialLibrary.Concrete);

            var road = Geo.Box(root, "Road", new Vector3(Road.center.x, -0.02f, Road.center.y),
                new Vector3(Road.width, 0.1f, Road.height), MaterialLibrary.Asphalt);

            var markings = Geo.Group(root, "RoadMarkings");
            var white = MaterialLibrary.Plain("RoadMarkingWhite", new Color(0.92f, 0.92f, 0.88f), 0.3f);
            for (float x = Road.xMin + 3f; x < Road.xMax - 2f; x += 6f)
                Geo.Box(markings, "Dash", new Vector3(x, 0.035f, Road.center.y), new Vector3(3f, 0.01f, 0.15f),
                    white, collider: false);
            GameObjectUtility.SetStaticEditorFlags(road, StaticEditorFlags.BatchingStatic);

            // Yellow safety lines in front of the factory doors.
            var yellow = MaterialLibrary.Plain("SafetyYellow", new Color(0.95f, 0.78f, 0.1f), 0.3f);
            Geo.Box(markings, "DoorLineSouth", new Vector3(0f, 0.045f, 13.5f), new Vector3(14f, 0.01f, 0.25f), yellow, collider: false);
            Geo.Box(markings, "DoorLineNorth", new Vector3(0f, 0.045f, 76.5f), new Vector3(14f, 0.01f, 0.25f), yellow, collider: false);
        }

        static void CreateGrass(Transform root)
        {
            var field = new GameObject("GrassField").AddComponent<GrassField>();
            field.transform.SetParent(root, false);

            Rect apron = Apron, road = Road;
            apron.xMin -= 0.5f; apron.yMin -= 0.5f; apron.xMax += 0.5f; apron.yMax += 0.5f;
            road.yMin -= 0.5f; road.yMax += 0.5f;

            field.Configure(MeshLibrary.GrassClump(), MaterialLibrary.GrassBlades(),
                new Vector2(-70f, -50f), new Vector2(80f, 130f), 60000, new[] { apron, road });
        }

        static void CreateContainerYard(Transform root)
        {
            var yard = Geo.Group(root, "ContainerYard");
            Material[] colours =
            {
                MaterialLibrary.Container(new Color(0.85f, 0.3f, 0.22f), "Red"),
                MaterialLibrary.Container(new Color(0.3f, 0.45f, 0.8f), "Blue"),
                MaterialLibrary.Container(new Color(0.35f, 0.62f, 0.38f), "Green"),
                MaterialLibrary.Container(new Color(0.95f, 0.6f, 0.2f), "Orange"),
            };

            var size = new Vector3(2.44f, 2.6f, 6.06f);
            float[] columns = { 24f, 27f, 30.5f, 33.5f };
            float[] rows = { 20f, 27f, 56f, 63f, 70f }; // keeps the side-door lane (z ≈ 39..47) clear
            var random = new System.Random(11);
            foreach (float z in rows)
            foreach (float x in columns)
            {
                if (random.NextDouble() < 0.2)
                    continue;
                int stack = random.NextDouble() < 0.4 ? 2 : 1;
                for (int level = 0; level < stack; level++)
                {
                    var position = new Vector3(x, 0.04f + size.y * (level + 0.5f), z + (float)(random.NextDouble() - 0.5) * 0.6f);
                    Geo.Box(yard, "Container", position, size, colours[random.Next(colours.Length)],
                        Quaternion.Euler(0f, (float)(random.NextDouble() - 0.5) * 4f, 0f));
                }
            }
        }

        static void CreateBarrels(Transform root, Vector3 centre)
        {
            var group = Geo.Group(root, "Barrels", centre);
            var random = new System.Random((int)(centre.x * 13 + centre.z));
            for (int i = 0; i < 7; i++)
            {
                var offset = new Vector3((float)random.NextDouble() * 2.4f, 0.49f, (float)random.NextDouble() * 2.4f);
                Geo.Cylinder(group, "Barrel", offset, 0.3f, 0.9f, MaterialLibrary.RedPaintedMetal);
            }
        }

        static void CreatePostProcessing(Transform root)
        {
            AssetDatabase.DeleteAsset(PostProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProfilePath);

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = Add<Bloom>(profile);
            bloom.intensity.Override(0.45f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.65f);

            var colour = Add<ColorAdjustments>(profile);
            colour.postExposure.Override(0.25f);
            colour.contrast.Override(12f);
            colour.saturation.Override(10f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.5f);

            AssetDatabase.SaveAssets();

            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.transform.SetParent(root, false);
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
