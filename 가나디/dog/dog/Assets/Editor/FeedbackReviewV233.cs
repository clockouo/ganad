using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Targeted visual review of the wake transition, exterior scale and left gate leaf.</summary>
public static class FeedbackReviewV233
{
    private const string Folder = "tmp/feedback-v2.34";
    private static AfterTheGlowStoryDirector story;
    private static float started, openedAt, touchedAt;
    private static int lastFrame;
    private static string tag;
    private static readonly HashSet<string> captured = new HashSet<string>();
    private static readonly List<string> notes = new List<string>();

    [MenuItem("Tools/After the Glow/Feedback v2.33/Inspect")]
    public static void Inspect()
    {
        var current = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        var output = new System.Text.StringBuilder();
        foreach (Transform item in current.transform)
        {
            var sprite = item.GetComponent<SpriteRenderer>();
            output.AppendLine(item.name + " p=" + item.position.ToString("F3") + " s=" + item.localScale.ToString("F3") +
                (sprite != null ? " bounds=" + sprite.bounds.size.ToString("F3") + " enabled=" + sprite.enabled : ""));
        }
        var wake = new SerializedObject(current.bedWakePuzzleUI);
        var root = (GameObject)wake.FindProperty("screenRoot").objectReferenceValue;
        output.AppendLine("WAKE " + root.name);
        foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            var image = rect.GetComponent<Image>();
            output.AppendLine(rect.name + " size=" + rect.rect.size + " scale=" + rect.localScale + " anchors=" + rect.anchorMin + "/" + rect.anchorMax +
                (image != null ? " sprite=" + (image.sprite != null ? AssetDatabase.GetAssetPath(image.sprite) : "null") + " preserve=" + image.preserveAspect : ""));
        }
        output.AppendLine("STORY " + EditorJsonUtility.ToJson(current));
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "inspection.txt"), output.ToString());
        Debug.Log("Visual feedback inspection saved to " + Folder);
    }

    [MenuItem("Tools/After the Glow/Feedback v2.33/Review Wake Before")]
    public static void WakeBefore() => StartWake("before");
    [MenuItem("Tools/After the Glow/Feedback v2.33/Review Wake After")]
    public static void WakeAfter() => StartWake("after");

    private static T Field<T>(object item, string name) =>
        (T)item.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item);

    [MenuItem("Tools/After the Glow/Feedback v2.33/Exterior Before")]
    public static void ExteriorBefore() => CaptureExterior("before");
    [MenuItem("Tools/After the Glow/Feedback v2.33/Exterior After")]
    public static void ExteriorAfter() => CaptureExterior("after");

    private static void CaptureExterior(string label)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var current = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        current.enabled = false;
        current.bedWakePuzzleUI.Close();
        current.cameraFollow.enabled = false;
        current.player.SetControlMode(PlayerControlMode.CutsceneLocked);
        var camera = current.cameraFollow.GetComponent<Camera>();
        var target = new RenderTexture(1600, 900, 24);
        var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        float oldAspect = camera.aspect;
        Directory.CreateDirectory(Folder);
        try
        {
            camera.targetTexture = target; camera.aspect = 16f / 9f;
            camera.orthographicSize = current.cameraFollow.homeOrthographicSize;
            void Capture(string name, float actorX, float cameraX)
            {
                var body = current.player.Body;
                body.position = new Vector2(actorX, current.streetEntryPlayerY);
                body.linearVelocity = Vector2.zero;
                current.player.transform.position = new Vector3(actorX, body.position.y, 0f);
                current.player.FaceTowards(actorX + 1f);
                Physics2D.SyncTransforms();
                camera.transform.position = new Vector3(cameraX, current.cameraFollow.exteriorLockedY, -10f);
                camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(Folder, label + "-" + name + ".png"), texture.EncodeToPNG());
            }
            Capture("interior", -4f, -2.5f);
            var panel = Field<SpriteRenderer>(current, "frontDoorRenderer");
            var pose = typeof(AfterTheGlowStoryDirector).GetMethod("ApplyFrontDoorPose", BindingFlags.Instance | BindingFlags.NonPublic);
            pose.Invoke(current, new object[] { 0f });
            float leftEdge = panel.bounds.min.x;
            float closedRight = panel.bounds.max.x;
            pose.Invoke(current, new object[] { 1f });
            if (label == "after" && (Mathf.Abs(panel.bounds.min.x - leftEdge) > .001f || panel.bounds.max.x >= closedRight))
                throw new Exception("House door must expose the RIGHT opening while keeping its left edge fixed");
            Capture("home-door-right-open", current.frontDoorPivot.position.x - 1.5f, 2f);
            float half = camera.orthographicSize * camera.aspect;
            Capture("street", current.streetEntryPlayerX, current.cameraFollow.exteriorMapMinX + half);
            var gate = current.parkGate.GetComponent<GateHingePresentation>();
            gate.SetReleased(true); gate.SetOpenProgress(1f);
            Capture("gate-left-open", current.parkGate.transform.position.x - 1.1f, current.parkGate.transform.position.x);
            Capture("join-left", AfterTheGlowWorldLayout.StreetEndX - 1f, AfterTheGlowWorldLayout.StreetEndX - 2f);
            Capture("join-center", AfterTheGlowWorldLayout.StreetEndX, AfterTheGlowWorldLayout.StreetEndX);
            Capture("join-right", AfterTheGlowWorldLayout.StreetEndX + 1f, AfterTheGlowWorldLayout.StreetEndX + 2f);
            bool leftOpened = gate.leftLeaf.sprite == gate.openedLeftSprite;
            bool rightClosed = gate.rightLeaf.sprite == gate.releasedRightSprite;
            if (label == "after" && (!leftOpened || !rightClosed)) throw new Exception("Expected left leaf open and right leaf closed");
            var fountain = current.GetComponent<ParkFountainExploration>();
            Capture("park", fountain.fountainWorldX - 1f, fountain.fountainWorldX);
            var connected = current.transform.Find("Connected Exterior Backdrop").GetComponent<SpriteRenderer>();
            if (connected.bounds.size.y > 8f || connected.bounds.size.x != 36f)
                throw new Exception("Exterior must stay bounded at 36 x 7.7 world units");
            foreach (string obsolete in new[] { "Street Sky Continuation", "Park Sky Continuation" })
                if (current.transform.Find(obsolete) != null) throw new Exception("Giant sky extension still exists");
            File.WriteAllText(Path.Combine(Folder, label + "-exterior.txt"),
                "Isolated scene render; not full story replay.\n" +
                "Street width=" + current.transform.Find("Street Night Backdrop").GetComponent<SpriteRenderer>().bounds.size.x +
                "\nPlayer scale=" + current.player.transform.localScale + "\nLeft leaf opened=" + leftOpened + " right leaf closed=" + rightClosed +
                "\nHome doorway opens on right=" + (Mathf.Abs(panel.bounds.min.x - leftEdge) < .001f && panel.bounds.max.x < closedRight));
        }
        finally
        {
            camera.targetTexture = oldTarget; camera.aspect = oldAspect; RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(texture); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void StartWake(string label)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        EditorApplication.update -= TickWake;
        story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story.Stage != AfterTheGlowStoryStage.WakeTheOwner)
            throw new InvalidOperationException("Restart Play Mode for the wake review.");
        tag = label; captured.Clear(); notes.Clear();
        Directory.CreateDirectory(Folder);
        started = Time.unscaledTime; openedAt = touchedAt = -1f; lastFrame = -1;
        Application.runInBackground = true;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        var player = story.player;
        var bed = story.bed.GetComponent<Collider2D>().bounds;
        player.Body.position = new Vector2(bed.max.x + player.BodyCollider.bounds.extents.x + .05f, -3.86f);
        player.transform.position = new Vector3(player.Body.position.x, player.Body.position.y, player.transform.position.z);
        player.Body.linearVelocity = Vector2.zero;
        player.FaceTowards(story.bed.transform.position.x);
        Physics2D.SyncTransforms();
        File.WriteAllText(Path.Combine(Folder, tag + "-wake.txt"), "RUNNING");
        EditorApplication.update += TickWake;
    }

    private static void Shot(string name)
    {
        if (!captured.Add(name)) return;
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Folder, tag + "-" + name + ".png")));
    }

    private static void TickWake()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= TickWake; return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (Time.unscaledTime - started > 15f) throw new Exception("Wake review timed out: " + story.Stage);
            var wake = story.bedWakePuzzleUI;
            if (story.Stage == AfterTheGlowStoryStage.WakeTheOwner && story.player.IsGrounded && Time.unscaledTime - started > .35f)
            {
                var interaction = story.player.GetComponent<PlayerInteraction>();
                interaction.RefreshTarget(story.player.FacingDirection);
                if (interaction.CurrentTarget != story.bed) throw new Exception("Bed not selected");
                interaction.HandleInteractionClick();
            }
            if (story.Stage == AfterTheGlowStoryStage.BedWakePuzzle)
            {
                if (openedAt < 0f) openedAt = Time.unscaledTime;
                float elapsed = Time.unscaledTime - openedAt;
                if (elapsed >= .12f) Shot("wake-reveal-early");
                if (elapsed >= .24f && !captured.Contains("wake-reveal-mid"))
                {
                    Shot("wake-reveal-mid");
                    var texture = Field<Texture2D>(wake, "spreadTexture");
                    float center = texture.GetPixel(texture.width / 2, texture.height / 2).a;
                    float corner = texture.GetPixel(0, 0).a;
                    notes.Add("Mid-reveal alpha center=" + center + " corner=" + corner);
                    if (tag == "after" && (center > .01f || corner < .99f))
                        throw new Exception("Radial reveal must show the center and cover the corners");
                }
                var button = Field<Button>(wake, "touchButton");
                if (button.interactable && Time.unscaledTime - touchedAt > .7f)
                {
                    Shot("wake-target-" + wake.TouchCount);
                    int before = wake.TouchCount;
                    button.onClick.Invoke(); button.onClick.Invoke();
                    if (!wake.IsAnimating || wake.TouchCount != before) throw new Exception("Touch must animate and reject duplicate clicks");
                    touchedAt = Time.unscaledTime;
                    notes.Add("Touch " + before + " started; duplicate rejected");
                }
                if (touchedAt > 0f && Time.unscaledTime - touchedAt >= .39f && Time.unscaledTime - touchedAt <= .48f)
                    Shot("wake-rings-aligned");
            }
            if (story.Stage == AfterTheGlowStoryStage.OwnerPreparing)
            {
                if (story.BedWakeCount != 1 || wake.TouchCount != 4) throw new Exception("Four touches must wake owner exactly once");
                notes.Add("PASS: four targets; wake once; no stuck overlay");
                EditorApplication.update -= TickWake;
                File.WriteAllLines(Path.Combine(Folder, tag + "-wake.txt"), notes);
            }
        }
        catch (Exception error)
        {
            EditorApplication.update -= TickWake;
            notes.Add("FAIL " + error);
            File.WriteAllLines(Path.Combine(Folder, tag + "-wake.txt"), notes);
            Debug.LogException(error);
        }
    }
}
