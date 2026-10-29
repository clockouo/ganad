using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Builds the playable scene while restoring the Editor's full scene list afterward.</summary>
[InitializeOnLoad]
public static class WindowsPlayerBuild
{
    private const string GameScene = "Assets/Scenes/SampleScene.unity";
    private const string PendingBuildKey = "AfterTheGlow.WindowsPlayerBuild.Pending";

    static WindowsPlayerBuild()
    {
        // Leaving Play Mode may reload the domain. Keep the request through that reload.
        if (SessionState.GetBool(PendingBuildKey, false))
            EditorApplication.update += Run;
    }

    [Serializable]
    private sealed class Report
    {
        public string status;
        public string result;
        public string outputPath;
        public string platform = "StandaloneWindows64";
        public string[] scenes = { GameScene };
        public string startedUtc;
        public double durationSeconds;
        public ulong totalSizeBytes;
        public int totalErrors;
        public int totalWarnings;
        public string[] errors;
        public string[] warnings;
    }

    [MenuItem("Tools/After the Glow/Build Windows Game")]
    private static void Queue()
    {
        if (BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("A Player build is already running.");
        if (!File.Exists(GameScene)) throw new FileNotFoundException(GameScene);
        if (SessionState.GetBool(PendingBuildKey, false)) return;
        SessionState.SetBool(PendingBuildKey, true);
        EditorApplication.update -= Run;
        EditorApplication.update += Run;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.Log("Windows build queued. Leaving Play Mode before saving and building.");
            EditorApplication.isPlaying = false;
        }
    }

    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
        EditorApplication.update -= Run;
        if (!SessionState.GetBool(PendingBuildKey, false)) return;
        SessionState.SetBool(PendingBuildKey, false);
        if (!EditorSceneManager.SaveOpenScenes())
        {
            Debug.Log("Windows build cancelled because the open scenes were not saved.");
            return;
        }
        AssetDatabase.SaveAssets();
        string workspace = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string latestPath = Path.Combine(workspace, ".document_work/latest-build-path.txt");
        string directory = File.Exists(latestPath) ? File.ReadAllText(latestPath).Trim() :
            Path.Combine(workspace, "Builds", "AfterTheGlow-Windows-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(latestPath, directory);
        string reportPath = Path.Combine(directory, "BuildReport.json");
        var record = new Report {
            status = "building",
            outputPath = Path.Combine(directory, "AfterTheGlow.exe"),
            startedUtc = DateTime.UtcNow.ToString("o")
        };
        File.WriteAllText(reportPath, JsonUtility.ToJson(record, true));
        var originalScenes = EditorBuildSettings.scenes;
        try
        {
            // URP's light prefilter scans EditorBuildSettings, including scenes outside build options.
            // Only the playable scene is enabled for this build; the testbed stays in the project.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScene, true) };
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { GameScene },
                locationPathName = record.outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.DetailedBuildReport
            });
            record.status = "completed";
            record.result = report.summary.result.ToString();
            record.durationSeconds = report.summary.totalTime.TotalSeconds;
            record.totalSizeBytes = report.summary.totalSize;
            record.totalErrors = report.summary.totalErrors;
            record.totalWarnings = report.summary.totalWarnings;
            var messages = report.steps.SelectMany(step => step.messages).ToArray();
            record.errors = messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                .Select(m => m.content).Distinct().ToArray();
            record.warnings = messages.Where(m => m.type == LogType.Warning)
                .Select(m => m.content).Distinct().ToArray();
            Debug.Log("Windows game build: " + record.result + " — " + record.outputPath);
        }
        catch (Exception exception)
        {
            record.status = "completed";
            record.result = "Failed";
            record.totalErrors++;
            record.errors = new[] { exception.ToString() };
            Debug.LogException(exception);
        }
        finally
        {
            EditorBuildSettings.scenes = originalScenes;
            File.WriteAllText(reportPath, JsonUtility.ToJson(record, true));
        }
    }
}
