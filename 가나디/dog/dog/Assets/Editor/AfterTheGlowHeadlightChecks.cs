using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AfterTheGlowHeadlightChecks
{
    [MenuItem("Tools/After the Glow/Verify Headlight Display")]
    private static void Run()
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("Enter Play Mode first.");
        AfterTheGlowStoryDirector story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        GameObject source = story.carHeadlight;
        MeshRenderer beam = source.transform.Find("Visible Headlight Beam").GetComponent<MeshRenderer>();
        if (beam == null)
            throw new MissingComponentException("Refresh Car Headlight Display before checking it.");
        GameplayLight2D gameplay = source.GetComponent<GameplayLight2D>();
        CameraFollow follow = story.cameraFollow;
        Camera camera = follow.GetComponent<Camera>();
        bool oldEnabled = follow.enabled;
        Vector3 oldCameraPosition = follow.transform.position;
        float oldSize = camera.orthographicSize;
        bool oldBackground = Application.runInBackground;
        bool oldLightActive = source.activeSelf;
        follow.enabled = false;
        follow.transform.position = new Vector3(11.4f, follow.exteriorLockedY, -10f);
        camera.orthographicSize = follow.exteriorOrthographicSize;
        story.player.SetControlMode(PlayerControlMode.CutsceneLocked);
        Application.runInBackground = true;
        source.SetActive(true);
        var lines = new List<string>();
        bool passed = true;
        Action<bool, string> check = (value, description) => {
            lines.Add((value ? "PASS " : "FAIL ") + description);
            passed &= value;
        };
        check(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset,
            "URP pipeline detected");
        check(beam.enabled && beam.gameObject.activeInHierarchy && beam.GetComponent<MeshFilter>().sharedMesh.vertexCount == 192,
            "Two front-facing headlight cones are active with 24 bands each");
        Vector2 sample = (Vector2)source.transform.position + new Vector2(-1.55f, -1f);
        check(gameplay.TryGetStrength(sample, out _, out _), "Gameplay light is active with visible beam");
        check(gameplay.UsesDirectionalBeams &&
            !gameplay.TryGetStrength((Vector2)source.transform.position + Vector2.left * 4f, out _, out _) &&
            !gameplay.TryGetStrength((Vector2)source.transform.position + Vector2.up, out _, out _),
            "No invisible circular light blocks the left side or the space behind the lamps");
        float started = Time.realtimeSinceStartup;
        bool capturedOn = false, switchedOff = false, capturedOff = false;
        EditorApplication.CallbackFunction tick = null;
        tick = () => {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= tick;
                Application.runInBackground = oldBackground;
                return;
            }
            float elapsed = Time.realtimeSinceStartup - started;
            if (!capturedOn && elapsed > 0.25f)
            {
                ScreenCapture.CaptureScreenshot("Assets/Temp/headlight-visible-on-2026-10-15.png");
                capturedOn = true;
            }
            if (!switchedOff && elapsed > 0.75f)
            {
                source.SetActive(false);
                check(!beam.gameObject.activeInHierarchy && !gameplay.TryGetStrength(sample, out _, out _),
                    "Switching off the source hides the display and disables gameplay exposure together");
                switchedOff = true;
            }
            if (!capturedOff && elapsed > 1.1f)
            {
                ScreenCapture.CaptureScreenshot("Assets/Temp/headlight-visible-off-2026-10-15.png");
                capturedOff = true;
            }
            if (elapsed > 1.5f)
            {
                EditorApplication.update -= tick;
                source.SetActive(oldLightActive);
                follow.transform.position = oldCameraPosition;
                camera.orthographicSize = oldSize;
                follow.enabled = oldEnabled;
                story.player.SetControlMode(PlayerControlMode.Free);
                Application.runInBackground = oldBackground;
                lines.Add("Result: " + (passed ? "PASS" : "FAIL"));
                File.WriteAllLines(Path.Combine(Application.dataPath, "../AgentScripts/headlight-display-verification-2026-10-15.txt"), lines);
            }
        };
        EditorApplication.update += tick;
    }
}
