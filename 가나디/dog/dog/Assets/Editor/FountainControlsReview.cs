using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.Pipeline.Commands;

/// <summary>Checks screen-space input direction and the complete fountain memory route.</summary>
public static class FountainControlsReview
{
    private const string Folder = "tmp/fountain-horizontal-controls/";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string result = "Not started";
    private static ParkFountainExploration fountain;
    private static Keyboard keyboard, previousKeyboard;
    private static InputSettings.BackgroundBehavior background;
    private static InputSettings.EditorInputBehaviorInPlayMode editorInput;
    private static bool runInBackground, exiting;
    private static float started, lastX;
    private static int frame, checks, previousDirection;
    private static readonly bool[] memories = new bool[4];

    [CliCommand("fountain_controls", "Verify A moves left and D moves right around the fountain", MainThreadRequired = true)]
    public static object Command([CliArg("action", "math, play, status")] string action = "status")
    {
        if (action == "math") return CheckDirections();
        if (action != "play") return result;
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        if (keyboard != null) throw new InvalidOperationException("A review is already running.");
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        fountain = story.GetComponent<ParkFountainExploration>();
        if (fountain.IsOpen) throw new InvalidOperationException("Use a fresh Play session.");
        story.bedWakePuzzleUI.Close(); story.pettingPuzzleUI.Close();
        typeof(AfterTheGlowStoryDirector).GetProperty("Stage").SetValue(story, AfterTheGlowStoryStage.ParkPlaceholder);
        previousKeyboard = Keyboard.current;
        background = InputSystem.settings.backgroundBehavior;
        editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        runInBackground = Application.runInBackground;
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>("FountainControlsReviewKeyboard"); keyboard.MakeCurrent();
        EditorApplication.isPaused = false;
        fountain.Open();
        started = Time.realtimeSinceStartup; frame = -1; checks = previousDirection = 0; exiting = false;
        lastX = Mathf.Cos(fountain.AngleDegrees * Mathf.Deg2Rad);
        Array.Clear(memories, 0, memories.Length);
        Directory.CreateDirectory(Folder); result = "RUNNING";
        EditorApplication.update += Tick;
        return result;
    }

    private static object CheckDirections()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run the direction check outside Play mode.");
        var gameObject = new GameObject("Temporary Fountain Direction Check");
        int assertions = 0;
        try
        {
            var component = gameObject.AddComponent<ParkFountainExploration>();
            var angle = typeof(ParkFountainExploration).GetField("angle", Private);
            var turn = typeof(ParkFountainExploration).GetField("orbitDirection", Private);
            var move = typeof(ParkFountainExploration).GetMethod("MoveHorizontally", Private);
            foreach (float startAngle in new[] { -180f, -179f, -135f, -90f, -45f, -1f, 0f, 1f, 45f, 90f, 135f, 179f, 180f })
            foreach (float lastTurn in new[] { -1f, 1f })
            foreach (float input in new[] { -1f, 0f, 1f })
            {
                angle.SetValue(component, startAngle); turn.SetValue(component, lastTurn);
                float previous = Mathf.Cos(startAngle * Mathf.Deg2Rad);
                for (int i = 0; i < 240; i++)
                {
                    move.Invoke(component, new object[] { input, 1f / 60f });
                    float current = Mathf.Cos((float)angle.GetValue(component) * Mathf.Deg2Rad);
                    if ((input < 0 && current > previous + .00001f) || (input > 0 && current < previous - .00001f) ||
                        (input == 0 && Mathf.Abs(current - previous) > .00001f))
                        throw new Exception($"Input {input} moved in the wrong screen direction from {startAngle}.");
                    previous = current; assertions++;
                }
                if (input != 0 && Mathf.Abs(previous - input) > .00001f)
                    throw new Exception("Holding a direction did not stop at its side of the fountain.");
            }
            Directory.CreateDirectory(Folder);
            string text = $"PASS: {assertions} screen-direction checks, all quadrants, both previous turn directions, idle and both endpoints.";
            File.WriteAllText(Folder + "direction-check.txt", text);
            return text;
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) { Finish(false, "Play mode ended during review."); return; }
            if (Time.realtimeSinceStartup - started > 90f) throw new Exception("Fountain route timed out.");
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            float x = Mathf.Cos(fountain.AngleDegrees * Mathf.Deg2Rad);
            if ((previousDirection < 0 && x > lastX + .00001f) || (previousDirection > 0 && x < lastX - .00001f))
                throw new Exception("Actual A/D input moved in the wrong screen direction.");
            checks++; lastX = x;
            if (exiting)
            {
                Keys(0);
                if (!fountain.IsOpen)
                {
                    for (int i = 0; i < memories.Length; i++) if (!memories[i]) throw new Exception("A memory was skipped.");
                    Finish(true, "A moves left, D moves right; all four memories reached in order, S exit completed.");
                }
                return;
            }
            if (fountain.IsTransitioning) { Keys(0); return; }
            if (fountain.IsMemoryOpen)
            {
                memories[fountain.ActiveMemoryIndex] = true; Keys(0); return;
            }
            if (fountain.VisitedPoints == 4 && fountain.CanExitRight)
            {
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "right-exit.png"));
                keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                previousDirection = 0; exiting = true; return;
            }
            int direction = fountain.VisitedPoints switch
            {
                0 => 1,
                1 => fountain.AngleDegrees < 0f ? 1 : -1,
                2 => 1,
                3 => fountain.AngleDegrees > 0f ? 1 : -1,
                _ => 1
            };
            Keys(direction);
        }
        catch (Exception error) { Finish(false, error.Message); }
    }

    private static void Keys(int direction)
    {
        keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, direction == 0 ? new KeyboardState() : new KeyboardState(direction < 0 ? Key.A : Key.D));
        previousDirection = direction;
    }

    private static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        result = $"{(passed ? "PASS" : "FAIL")}: {message} Frames checked={checks}, completed memories={fountain.VisitedPoints}.";
        File.WriteAllText(Folder + "play-check.txt", result);
        if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
        InputSystem.settings.backgroundBehavior = background;
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        Application.runInBackground = runInBackground;
    }
}
