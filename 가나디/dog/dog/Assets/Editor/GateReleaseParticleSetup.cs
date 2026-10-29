using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Pipeline.Commands;

/// <summary>Give the gate dust an explicit, build-referenced URP particle material.</summary>
public static class GateReleaseParticleSetup
{
    public const string MaterialPath = "Assets/Art/Generated/GateReleaseDust.mat";
    private const string Evidence = "tmp/gate-particles-v2.47";

    [CliCommand("gate_particles", "Inspect, repair, and render the gate release dust", MainThreadRequired = true)]
    public static object Command([CliArg("action", "inspect, before, apply, verify, play_check")] string action = "inspect")
    {
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story == null) throw new MissingReferenceException("Open SampleScene first.");
        var particles = story.parkGate.GetComponentInChildren<ParticleSystem>(true);
        if (particles == null) throw new MissingReferenceException("Gate release particles are missing.");
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        Directory.CreateDirectory(Evidence);
        if (action == "play_check")
        {
            if (!EditorApplication.isPlaying || story.Stage != AfterTheGlowStoryStage.WakeTheOwner)
                throw new InvalidOperationException("Use a fresh Play Mode session for the focused particle check.");
            story.StartCoroutine(CheckOpeningInPlayMode(story, particles));
            return "Rendering the gate opening and actual release-particle call in Play Mode.";
        }
        if (action == "before" || action == "verify")
            return Verify(story, particles, action == "before");
        if (action == "apply")
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before saving the repair.");
            if (!File.Exists(Evidence + "/before-active.unity"))
                EditorSceneManager.SaveScene(story.gameObject.scene, Evidence + "/before-active.unity", true);
            Apply(particles);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
            EditorSceneManager.SaveScene(story.gameObject.scene);
        }
        var material = renderer.sharedMaterial;
        var result = new {
            name = particles.name, material = material != null ? AssetDatabase.GetAssetPath(material) : "MISSING",
            shader = material != null && material.shader != null ? material.shader.name : "MISSING",
            supported = material != null && material.shader != null && material.shader.isSupported,
            shaderError = material != null && material.shader != null && ShaderUtil.ShaderHasError(material.shader)
        };
        File.WriteAllText(Evidence + "/" + action + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        return result;
    }

    public static void Apply(ParticleSystem particles)
    {
        if (particles == null) return;
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            const string templatePath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
            var template = AssetDatabase.LoadAssetAtPath<Material>(templatePath);
            if (template == null) throw new MissingReferenceException("URP ParticlesUnlit material is missing.");
            material = new Material(template) { name = "GateReleaseDust" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        // A white square keeps the existing pixel dust and vertex tint. No
        // external texture or scene-default particle material is required.
        material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        material.SetColor("_BaseColor", Color.white);
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        if (particles.trails.enabled) renderer.trailMaterial = material;
        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(renderer);
    }

    private static System.Collections.IEnumerator CheckOpeningInPlayMode(AfterTheGlowStoryDirector story, ParticleSystem particles)
    {
        var follow = story.cameraFollow;
        var camera = follow.GetComponent<Camera>();
        var hinge = story.parkGate.GetComponent<GateHingePresentation>();
        Vector3 savedPosition = camera.transform.position;
        float savedSize = camera.orthographicSize;
        bool savedFollow = follow.enabled;
        var savedControl = story.player.ControlMode;
        var samples = new List<object>();
        bool passed = true;
        int startFrame = Time.frameCount;
        try
        {
            follow.enabled = false;
            story.player.SetControlMode(PlayerControlMode.CutsceneLocked);
            camera.transform.position = particles.transform.position + new Vector3(0f, 0f, -10f);
            camera.orthographicSize = 1.25f;
            hinge.SetReleased(true);
            story.parkGate.PlayTetherReleaseParticles();
            float started = Time.time;
            int next = 0;
            float[] targets = { .04f, .16f, .32f };
            while (Time.time - started < .8f)
            {
                hinge.SetOpenProgress(Mathf.Clamp01((Time.time - started) / .65f));
                yield return new WaitForEndOfFrame();
                if (next >= targets.Length || Time.time - started < targets[next]) continue;
                Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
                int magenta = capture.GetPixels32().Count(c => c.r > 180 && c.b > 180 && c.g < 100);
                int alive = particles.particleCount;
                passed &= alive > 0 && magenta == 0;
                samples.Add(new { frame = Time.frameCount, elapsed = Time.time - started, alive, magentaPixels = magenta });
                File.WriteAllBytes(Evidence + "/play-opening-" + next + ".png", capture.EncodeToPNG());
                UnityEngine.Object.Destroy(capture);
                next++;
            }
            passed &= next == targets.Length && particles.particleCount == 0 && Time.frameCount > startFrame;
            File.WriteAllText(Evidence + "/play-verification.json", Newtonsoft.Json.JsonConvert.SerializeObject(
                new { passed, startFrame, endFrame = Time.frameCount, samples }, Newtonsoft.Json.Formatting.Indented));
        }
        finally
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            hinge.ResetPose();
            camera.transform.position = savedPosition; camera.orthographicSize = savedSize;
            follow.enabled = savedFollow; story.player.SetControlMode(savedControl);
        }
    }

    private static object Verify(AfterTheGlowStoryDirector story, ParticleSystem particles, bool before)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Render the saved particle setup outside Play Mode.");
        var camera = story.cameraFollow.GetComponent<Camera>();
        Vector3 savedPosition = camera.transform.position;
        float savedSize = camera.orthographicSize;
        var savedTarget = camera.targetTexture;
        var savedActive = RenderTexture.active;
        bool savedAutoSeed = particles.useAutoRandomSeed;
        uint savedSeed = particles.randomSeed;
        const int width = 1280, height = 720;
        var target = new RenderTexture(width, height, 24);
        var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var frames = new List<object>();
        bool passed = true;
        try
        {
            camera.transform.position = particles.transform.position + new Vector3(0f, 0f, -10f);
            camera.orthographicSize = 1.25f;
            camera.targetTexture = target;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false; particles.randomSeed = 247;
            Color32[] Capture()
            {
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                return pixels.GetPixels32();
            }
            var baseline = Capture();
            foreach (float time in new[] { .04f, .16f, .32f, .7f })
            {
                particles.Simulate(time, true, true, true);
                var colors = Capture();
                int changed = 0, magenta = 0;
                for (int i = 0; i < colors.Length; i++)
                {
                    var c = colors[i]; var b = baseline[i];
                    if (Mathf.Abs(c.r-b.r)+Mathf.Abs(c.g-b.g)+Mathf.Abs(c.b-b.b) <= 24) continue;
                    changed++;
                    if (c.r > 180 && c.b > 180 && c.g < 100) magenta++;
                }
                int alive = particles.particleCount;
                frames.Add(new { time, alive, changedPixels = changed, magentaPixels = magenta });
                if (time == .16f) passed &= alive > 0 && changed > 0;
                if (time == .7f) passed &= alive == 0;
                passed &= magenta == 0;
                File.WriteAllBytes(Evidence + "/" + (before ? "before" : "fixed") + "-" + time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".png", pixels.EncodeToPNG());
            }
            var material = particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            bool validMaterial = material != null && material.shader != null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader);
            passed &= validMaterial;
            var result = new { passed, validMaterial, frames = frames.ToArray() };
            File.WriteAllText(Evidence + "/" + (before ? "before" : "verification") + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.randomSeed = savedSeed; particles.useAutoRandomSeed = savedAutoSeed;
            camera.transform.position = savedPosition; camera.orthographicSize = savedSize;
            camera.targetTexture = savedTarget; RenderTexture.active = savedActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
