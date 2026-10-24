using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DroneSim.EditorTools
{
    /// <summary>
    /// Windows build: menu "DroneSim/Build Windows", or batchmode
    /// -executeMethod DroneSim.EditorTools.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        const string OutputPath = "Builds/Windows/DroneSimulator.exe";

        [MenuItem("DroneSim/Build Windows")]
        public static void BuildWindows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            bool succeeded = summary.result == BuildResult.Succeeded;

            Debug.Log($"[DroneSim] Build {summary.result}: {OutputPath}, " +
                      $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalTime}");

            if (Application.isBatchMode)
                EditorApplication.Exit(succeeded ? 0 : 1);
        }
    }
}
