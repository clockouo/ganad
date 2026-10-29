using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Pipeline.Commands;

/// <summary>Records the actual production departure coroutine in Play mode.</summary>
public static class OwnerDoorDepartureReview
{
    private static string result = "Not started";
    private static string folder;
    private static AfterTheGlowStoryDirector story;
    private static SpriteRenderer owner;
    private static float started, nextFrame;
    private static bool complete, background;
    private static int frame, imageNumber;
    private static readonly HashSet<string> captured = new HashSet<string>();
    private static readonly List<string> samples = new List<string>();

    [CliCommand("owner_door_departure", "Record the actual owner departure through the front door", MainThreadRequired = true)]
    public static object Command([CliArg("action", "before, after, status")] string action = "status")
    {
        if (action == "status") return result;
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        EditorApplication.update -= Tick;
        story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        story.StopAllCoroutines(); story.bedWakePuzzleUI.Close(); story.pettingPuzzleUI.Close();
        story.homeRoomLighting.SetLightImmediate(true);
        story.player.SetControlMode(PlayerControlMode.CutsceneLocked);
        story.player.Body.position = new Vector2(story.OwnerDoorStandX - 1.3f, story.streetEntryPlayerY);
        story.player.transform.position = new Vector3(story.player.Body.position.x, story.player.Body.position.y, story.player.transform.position.z);
        story.player.Body.linearVelocity = Vector2.zero;
        story.owner.gameObject.SetActive(true); story.owner.localScale = Vector3.one;
        story.owner.position = new Vector3(story.OwnerDoorStandX, story.streetEntryPlayerY, -.1f);
        owner = story.GetComponent<OwnerAnimationPresentation>().ownerRenderer;
        typeof(AfterTheGlowStoryDirector).GetMethod("SetOwnerSprite", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(story, new object[] { story.ownerWalkSprite });
        story.cameraFollow.enabled = false;
        var camera = story.cameraFollow.GetComponent<Camera>(); camera.orthographicSize = 3f;
        camera.transform.position = new Vector3(story.frontDoorPivot.position.x, story.cameraFollow.lockedY, -10f);
        folder = Path.GetFullPath("tmp/owner-door-departure/" + action + "/"); Directory.CreateDirectory(folder);
        captured.Clear(); samples.Clear(); frame = -1; imageNumber = 0;
        started = nextFrame = Time.unscaledTime; complete = false;
        background = Application.runInBackground; Application.runInBackground = true; EditorApplication.isPaused = false;
        result = "RUNNING";
        story.StartCoroutine(Run()); EditorApplication.update += Tick;
        return result;
    }

    private static IEnumerator Run()
    {
        yield return (IEnumerator)typeof(AfterTheGlowStoryDirector).GetMethod("OwnerLeavesHouse", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(story, null);
        complete = true;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) throw new Exception("Play mode ended during departure.");
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            float elapsed = Time.unscaledTime - started;
            float x = story.owner.position.x;
            float doorX = story.frontDoorPivot.position.x;
            samples.Add($"{elapsed:F3},{x:F4},{story.owner.position.y:F4},{story.FrontDoorOpenness:F3},{story.owner.gameObject.activeSelf}");
            if (Time.unscaledTime >= nextFrame)
            {
                nextFrame = Time.unscaledTime + .1f;
                Shot("frame-" + (imageNumber++).ToString("000"));
            }
            if (story.owner.gameObject.activeSelf && story.FrontDoorOpenness > .95f)
            {
                if (x >= doorX && x < doorX + .15f) Once("at-opening");
                if (x >= doorX + .7f && x < doorX + .85f) Once("passing-right-jamb");
                if (story.OwnerDepartureCount > 0) Once("last-active-frame");
            }
            if (complete)
            {
                Once("finished");
                bool passed = story.Stage == AfterTheGlowStoryStage.HouseExitReady && !story.owner.gameObject.activeSelf;
                Finish(passed, "Actual departure coroutine completed; door crossing and closure captured.");
            }
            else if (elapsed > 15f) throw new Exception("Departure timed out.");
        }
        catch (Exception error) { Finish(false, error.Message); }
    }

    private static void Once(string name) { if (captured.Add(name)) Shot(name); }
    private static void Shot(string name)
    {
        var camera = story.cameraFollow.GetComponent<Camera>();
        var target = new RenderTexture(1280, 720, 24); var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = camera.targetTexture; var active = RenderTexture.active; float aspect = camera.aspect;
        Vector3 position = camera.transform.position; float size = camera.orthographicSize;
        try
        {
            camera.targetTexture = target; camera.aspect = 16f / 9f; camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); File.WriteAllBytes(folder + name + ".png", pixels.EncodeToPNG());
            if (!name.StartsWith("frame-"))
            {
                camera.transform.position = new Vector3(story.frontDoorPivot.position.x, -2.1f, -10f); camera.orthographicSize = 2.25f;
                camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(folder + "detail-" + name + ".png", pixels.EncodeToPNG());
            }
        }
        finally
        {
            camera.transform.position = position; camera.orthographicSize = size;
            camera.targetTexture = previous; camera.aspect = aspect; RenderTexture.active = active;
            UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        result = $"{(passed ? "PASS" : "FAIL")}: {message} Recorded {samples.Count} live frames.";
        File.WriteAllText(folder + "result.txt", result);
        samples.Insert(0, "seconds,ownerX,ownerY,doorOpenness,ownerActive"); File.WriteAllLines(folder + "route.csv", samples);
        Application.runInBackground = background;
    }
}
