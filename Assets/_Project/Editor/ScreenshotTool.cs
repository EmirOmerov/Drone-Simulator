using System.IO;
using DroneSim.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DroneSim.EditorTools
{
    /// <summary>
    /// Renders the flight scene from a few fixed viewpoints to Logs/Screens/*.png (1920×1080), for reviewing
    /// visuals without opening the editor: -executeMethod DroneSim.EditorTools.ScreenshotTool.CaptureAll
    /// </summary>
    public static class ScreenshotTool
    {
        const string OutputDir = "Logs/Screens";

        [MenuItem("DroneSim/Capture Screenshots")]
        public static void CaptureAll()
        {
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(OutputDir);

            var camera = Camera.main;
            var drone = GameObject.Find("Drone").transform;
            var grass = Object.FindAnyObjectByType<GrassField>();

            // Compile every shader variant synchronously, then throw away a warm-up frame:
            // otherwise the first capture can show placeholder shaders / wrong colours.
            ShaderUtil.allowAsyncCompilation = false;
            Capture(camera, grass, "_warmup", drone.position + new Vector3(0f, 1.6f, -4.5f), drone.position, 70f);
            File.Delete($"{OutputDir}/_warmup.png");

            Capture(camera, grass, "1_chase_start", drone.position + new Vector3(0f, 1.6f, -4.5f), drone.position + Vector3.up * 0.3f, 70f);
            Capture(camera, grass, "2_overview", new Vector3(45f, 32f, -35f), new Vector3(0f, 4f, 45f), 60f);
            Capture(camera, grass, "3_hall_inside", new Vector3(2f, 3f, 18f), new Vector3(-6f, 2.5f, 60f), 75f);
            Capture(camera, grass, "4_fpv_door", new Vector3(0f, 2f, 4f), new Vector3(0f, 2.6f, 40f), 100f);
            Capture(camera, grass, "5_mezzanine", new Vector3(-12f, 2f, 44f), new Vector3(-12f, 2f, 72f), 80f);
            Capture(camera, grass, "6_yard", new Vector3(45f, 4f, 30f), new Vector3(20f, 2f, 55f), 65f);

            Debug.Log($"[DroneSim] Screenshots written to {Path.GetFullPath(OutputDir)}");
        }

        static void Capture(Camera camera, GrassField grass, string name, Vector3 position, Vector3 lookAt, float fov)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            camera.fieldOfView = fov;

            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            camera.targetTexture = target;
            if (grass != null)
                grass.Draw();
            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes($"{OutputDir}/{name}.png", image.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }
}
