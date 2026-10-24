using System.IO;
using DroneSim.CameraRig;
using DroneSim.Controls;
using DroneSim.DebugTools;
using DroneSim.Flight;
using DroneSim.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DroneSim.EditorTools
{
    /// <summary>
    /// Builds the flight scene entirely from code, so it is reproducible and diff-free:
    /// menu "DroneSim/Rebuild Flight Test Scene", or batchmode
    /// -executeMethod DroneSim.EditorTools.SceneBuilder.BuildFlightTestScene
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/FlightTest.unity";
        const string SettingsDir = "Assets/_Project/Settings";
        static readonly string[] LegacyAssets =
        {
            "Assets/_Project/Art/Textures/Grid.png",
            "Assets/_Project/Art/Textures/Generated/GrassBlades.png",
        };
        const int IgnoreRaycastLayer = 2;

        [MenuItem("DroneSim/Rebuild Flight Test Scene")]
        public static void BuildFlightTestScene()
        {
            EnsureFolder(SettingsDir);
            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            foreach (string legacy in LegacyAssets)
                AssetDatabase.DeleteAsset(legacy);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MeshLibrary.Begin();
            MaterialLibrary.Begin();

            EnvironmentBuilder.Build();
            FactoryBuilder.Build();
            FactoryBuilder.BuildCourse(new GameObject("Gameplay").transform);

            // Load the tuning assets only now: the hundreds of texture imports above can unload assets that were
            // loaded earlier, and a stale reference would be serialized into the scene as "None".
            var normalTuning = LoadOrCreateAsset<FlightTuning>(SettingsDir + "/DefaultFlightTuning.asset", null);
            var sportTuning = LoadOrCreateAsset<FlightTuning>(SettingsDir + "/SportFlightTuning.asset", ApplySportDefaults);

            GameObject drone = CreateDrone(normalTuning, sportTuning);
            var input = drone.GetComponent<FlightInputProvider>();
            var controller = drone.GetComponent<AngleModeController>();
            DroneCamera droneCamera = CreateCamera(drone.transform, input);
            CreateGroundMarker(drone.transform);

            var ui = new GameObject("UI");
            ui.AddComponent<PauseMenu>().Configure(input, drone.GetComponent<DroneRespawn>());
            ui.AddComponent<FlightHud>().Configure(controller, droneCamera);
            ui.AddComponent<InputDebugOverlay>().Configure(input, controller);

            MeshLibrary.End();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[DroneSim] Flight scene written to {ScenePath}");
        }

        /// <summary>Sport mode: more tilt, speed, yaw rate and climb. Top speed ≈ g·tan(45°) / 0.35 ≈ 28 m/s.</summary>
        static void ApplySportDefaults(FlightTuning tuning)
        {
            tuning.maxTiltDegrees = 45f;
            tuning.attitudeP = 220f;
            tuning.attitudeD = 29f;
            tuning.maxYawRateDegrees = 170f;
            tuning.yawRateP = 10f;
            tuning.linearDrag = 0.35f;
            tuning.maxClimbRate = 6f;
            tuning.maxDescentRate = 4f;
            tuning.climbRateP = 4f;
            tuning.maxThrustAcceleration = 35f;
        }

        // ---------------------------------------------------------------- drone

        static GameObject CreateDrone(FlightTuning normalTuning, FlightTuning sportTuning)
        {
            var drone = new GameObject("Drone");
            float halfHeight = 0.06f;
            drone.transform.position = FactoryBuilder.StartPadPosition + Vector3.up * (FactoryBuilder.PadTop + halfHeight + 0.02f);

            var collider = drone.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.5f, halfHeight * 2f, 0.5f);

            var body = drone.AddComponent<Rigidbody>();
            body.mass = normalTuning.mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var bodyMaterial = MaterialLibrary.Plain("DroneBody", new Color(0.12f, 0.13f, 0.15f), 0.7f, 0.3f);
            var frontMaterial = MaterialLibrary.Plain("DroneFront", new Color(0.9f, 0.2f, 0.15f), 0.6f);
            var rearMaterial = MaterialLibrary.Plain("DroneRear", new Color(0.9f, 0.9f, 0.9f), 0.6f);
            var propMaterial = MaterialLibrary.Plain("DroneProp", new Color(0.25f, 0.25f, 0.27f), 0.4f);
            var ledMaterial = MaterialLibrary.Emissive("DroneLed", new Color(0.2f, 0.9f, 1f), 3f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(drone.transform, false);

            Part(visual, PrimitiveType.Cube, "Body", Vector3.zero, Quaternion.identity,
                new Vector3(0.18f, 0.06f, 0.28f), bodyMaterial);
            Part(visual, PrimitiveType.Cube, "Nose", new Vector3(0f, 0.01f, 0.15f), Quaternion.identity,
                new Vector3(0.1f, 0.04f, 0.05f), frontMaterial);
            Part(visual, PrimitiveType.Cube, "Led", new Vector3(0f, 0.032f, -0.1f), Quaternion.identity,
                new Vector3(0.06f, 0.01f, 0.04f), ledMaterial);
            Part(visual, PrimitiveType.Cube, "ArmA", Vector3.zero, Quaternion.Euler(0f, 45f, 0f),
                new Vector3(0.62f, 0.02f, 0.035f), bodyMaterial);
            Part(visual, PrimitiveType.Cube, "ArmB", Vector3.zero, Quaternion.Euler(0f, -45f, 0f),
                new Vector3(0.62f, 0.02f, 0.035f), bodyMaterial);

            // Rotor order alternates spin direction: FL, FR, RR, RL.
            Vector3[] corners =
            {
                new Vector3(-0.22f, 0f, 0.22f), new Vector3(0.22f, 0f, 0.22f),
                new Vector3(0.22f, 0f, -0.22f), new Vector3(-0.22f, 0f, -0.22f),
            };
            var rotors = new Transform[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                bool front = corners[i].z > 0f;
                Part(visual, PrimitiveType.Cylinder, "Motor", corners[i] + Vector3.up * 0.02f, Quaternion.identity,
                    new Vector3(0.05f, 0.025f, 0.05f), front ? frontMaterial : rearMaterial);

                var rotor = new GameObject("Rotor").transform;
                rotor.SetParent(visual, false);
                rotor.localPosition = corners[i] + Vector3.up * 0.05f;
                Part(rotor, PrimitiveType.Cube, "Blade", Vector3.zero, Quaternion.identity,
                    new Vector3(0.24f, 0.004f, 0.025f), propMaterial);
                rotors[i] = rotor;
            }

            var input = drone.AddComponent<FlightInputProvider>();
            var controller = drone.AddComponent<AngleModeController>();
            SetSerialized(controller, "tuning", normalTuning);
            SetSerialized(controller, "sportTuning", sportTuning);
            SetSerialized(controller, "inputProvider", input);

            SetSerialized(drone.AddComponent<DroneRespawn>(), "inputProvider", input);
            SetSerialized(drone.AddComponent<FlightModeToggle>(), "inputProvider", input);
            drone.AddComponent<PropSpinner>().Configure(controller, rotors);

            // Camera wall-avoidance and the ground marker raycasts must not hit the drone itself.
            foreach (Transform t in drone.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = IgnoreRaycastLayer;
            return drone;
        }

        static void Part(Transform parent, PrimitiveType type, string name, Vector3 position, Quaternion rotation,
            Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>()); // the root BoxCollider is the physics shape
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        // ---------------------------------------------------------------- camera & helpers

        static DroneCamera CreateCamera(Transform drone, FlightInputProvider input)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 800f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = drone.position + new Vector3(0f, 1.6f, -4.5f);
            cameraObject.transform.LookAt(drone);

            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            var droneCamera = cameraObject.AddComponent<DroneCamera>();
            droneCamera.Configure(drone, input);
            return droneCamera;
        }

        static void CreateGroundMarker(Transform drone)
        {
            var markerObject = new GameObject("GroundMarker");
            var line = markerObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.015f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = MaterialLibrary.Unlit("MarkerLine", new Color(1f, 1f, 1f));

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "MarkerDisc";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(markerObject.transform, false);
            disc.transform.localScale = new Vector3(0.35f, 0.004f, 0.35f);
            var discRenderer = disc.GetComponent<Renderer>();
            discRenderer.sharedMaterial = MaterialLibrary.Unlit("MarkerDisc", new Color(1f, 0.85f, 0.1f));
            discRenderer.shadowCastingMode = ShadowCastingMode.Off;

            markerObject.AddComponent<GroundMarker>().Configure(drone, disc.transform);
        }

        static T LoadOrCreateAsset<T>(string path, System.Action<T> initialise) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            initialise?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void SetSerialized(Object target, string field, Object value)
        {
            // Unity's == null also catches destroyed/unloaded objects, which would otherwise save as "None".
            if (value == null)
                throw new System.ArgumentNullException(nameof(value), $"{target.GetType().Name}.{field} would be empty");
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
                throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
