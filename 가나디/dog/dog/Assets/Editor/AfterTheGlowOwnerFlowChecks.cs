using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Replays the real story coroutines and pointer gestures in Play Mode.</summary>
public static class AfterTheGlowOwnerFlowChecks
{
    private static readonly List<string> Lines = new List<string>();
    private static AfterTheGlowStoryDirector story;
    private static Mouse mouse;
    private static Keyboard keyboard;
    private static float started, lastAction, lastSample, stageSince;
    private static AfterTheGlowStoryStage lastStage;
    private static bool moved, capturedWalk, clicked, drivingStreet, capturedBeam, lightOffObserved, alignmentPassed;
    private static int clickStep, swipeStep;
    private static bool oldBackground;
    private static InputSettings.BackgroundBehavior oldInputBackground;
    private static string folder;
    private static bool manualPetCheck;
    private static bool approachFromRight;
    private static bool exitApproachStarted;
    private static float petOpenedAt;
    private static float lastFocus = -1f;
    private static int invalidPetStep, focusCaptureMask;
    private static bool petMouseHeld;

    [MenuItem("Tools/After the Glow/Snapshot Story State")]
    private static void Snapshot()
    {
        var director = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        string output = "stage=" + director.Stage + " mode=" + director.player.ControlMode +
            " facing=" + director.player.FacingDirection + " timeScale=" + Time.timeScale +
            " owner=" + director.owner.position + " player=" + director.player.transform.position;
        foreach (string field in new[] { "waitingForPetPuzzleClick", "petStrokeCount", "petPuzzleComplete" })
            output += "\n" + field + "=" + typeof(AfterTheGlowStoryDirector).GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
        output += "\nmouse=" + Mouse.current?.name + " focused=" + Application.isFocused;
        output += "\nfocusProgress=" + director.pettingPuzzleUI.Progress + " blur=" + director.pettingPuzzleUI.BlurPixels;
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../tmp/runtime-review/live-state.txt"));
        File.WriteAllText(path, output);
        ScreenCapture.CaptureScreenshot(Path.ChangeExtension(path, ".png"));
    }

    [MenuItem("Tools/After the Glow/Prepare Physical Pet Check From Right")]
    private static void PreparePhysical()
    {
        manualPetCheck = approachFromRight = true;
        StartReplay();
    }

    [MenuItem("Tools/After the Glow/Replay Owner Flow")]
    private static void Run()
    {
        manualPetCheck = approachFromRight = false;
        StartReplay();
    }

    private static void StartReplay()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story.Stage != AfterTheGlowStoryStage.WakeTheOwner)
            throw new InvalidOperationException("Restart Play Mode before replaying the story.");
        EditorApplication.update -= Tick;
        Lines.Clear();
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../tmp/runtime-review"));
        Directory.CreateDirectory(folder);
        oldBackground = Application.runInBackground;
        Application.runInBackground = true;
        oldInputBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        mouse = InputSystem.AddDevice<Mouse>("StoryReplayMouse");
        keyboard = InputSystem.AddDevice<Keyboard>("StoryReplayKeyboard");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        moved = capturedWalk = clicked = drivingStreet = capturedBeam = lightOffObserved = false;
        alignmentPassed = true;
        clickStep = swipeStep = 0;
        exitApproachStarted = false;
        petOpenedAt = -1f;
        lastFocus = -1f;
        invalidPetStep = focusCaptureMask = 0;
        petMouseHeld = false;
        started = lastAction = lastSample = stageSince = Time.realtimeSinceStartup;
        lastStage = story.Stage;
        story.TryBeginStoryPuzzle(story.bed);
        Button button = story.bedWakePuzzleUI.GetComponentInChildren<Button>(true);
        for (int i = 0; i < 4; i++) button.onClick.Invoke();
        EditorApplication.update += Tick;
        Log("Replay started with the actual bed-touch button.");
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || story == null) { Finish("INTERRUPTED"); return; }
            float now = Time.realtimeSinceStartup;
            if (now - started > 65f) { Finish("FAIL timeout at " + story.Stage); return; }
            if (story.Stage != lastStage)
            {
                lastStage = story.Stage;
                stageSince = now;
                Log("STAGE " + story.Stage);
                if (story.Stage == AfterTheGlowStoryStage.OwnerDeparture)
                    QueueMouse(new Vector2(Screen.width * .5f, Screen.height * .55f), false);
            }
            if (now - lastSample > 0.5f)
            {
                lastSample = now;
                var renderer = story.owner.GetComponentInChildren<SpriteRenderer>(true);
                Log("sample owner=" + story.owner.position.ToString("F3") + " feet=" +
                    renderer.bounds.min.y.ToString("F3") + " player=" + story.player.transform.position.ToString("F3") +
                    " mode=" + story.player.ControlMode + " camera=" + story.cameraFollow.transform.position.ToString("F3"));
            }
            if (!capturedWalk && story.Stage == AfterTheGlowStoryStage.OwnerPreparing &&
                story.owner.gameObject.activeSelf && story.owner.position.x > story.bed.transform.position.x + 1f)
            {
                capturedWalk = true;
                Capture("owner-walking.png");
            }
            if (!moved && story.OwnerPreparationStartedAt >= 0f)
            {
                moved = true;
                if (approachFromRight)
                {
                    story.player.Body.position = new Vector2(4.5f, story.streetEntryPlayerY);
                    story.player.transform.position = story.player.Body.position;
                    story.player.SetControlMode(PlayerControlMode.Free);
                    Physics2D.SyncTransforms();
                }
                else story.player.BeginAutoMoveToX(story.ownerPreparationTargetX + 0.1f, 4f, PlayerControlMode.Free);
            }
            bool waiting = (bool)typeof(AfterTheGlowStoryDirector).GetField("waitingForPetPuzzleClick",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(story);
            if (!clicked && story.Stage == AfterTheGlowStoryStage.OwnerPetPuzzle)
            {
                clicked = true;
                QueueMouse(new Vector2(Screen.width * .5f, Screen.height * .55f), false);
                lastAction = now;
            }
            if (!clicked && waiting && now - lastAction > 0.2f)
            {
                if (clickStep == 0)
                {
                    float error = Mathf.Abs(story.player.transform.position.x - story.OwnerPetPlayerTargetX);
                    alignmentPassed = error < 0.12f;
                    alignmentPassed &= story.player.FacingDirection == 1;
                    Log((alignmentPassed ? "PASS " : "FAIL ") + "door dog alignment error=" + error);
                    Capture("owner-at-door.png");
                    if (manualPetCheck)
                    {
                        Snapshot();
                        Finish("READY for physical pointer check; dog approached from right and faces owner");
                        return;
                    }
                }
                QueuePetEvent(clickStep == 1 ? EventType.MouseDown : EventType.MouseUp,
                    new Vector2(Screen.width * .5f, Screen.height * .55f));
                clickStep++;
                lastAction = now;
                if (clickStep >= 3) clicked = true;
            }
            if (clicked && story.Stage == AfterTheGlowStoryStage.OwnerPetPuzzle && story.pettingPuzzleUI.IsOpen)
            {
                var ui = story.pettingPuzzleUI;
                Vector3[] corners = new Vector3[4];
                ui.PortraitRect.GetWorldCorners(corners);
                float width = corners[3].x - corners[0].x;
                Vector2 center = (corners[0] + corners[2]) * .5f;
                if (petOpenedAt < 0f)
                {
                    petOpenedAt = lastAction = now;
                    Capture("pet-focus-0.png");
                    Log("Focus start progress=" + ui.Progress + " blur=" + ui.BlurPixels);
                }
                if (ui.DisplayProgress < lastFocus - .0001f) throw new Exception("Focus regressed");
                lastFocus = ui.DisplayProgress;
                if (now - petOpenedAt < 1f)
                {
                    if (ui.Progress != 0f) throw new Exception("Idle advanced petting");
                }
                else if (invalidPetStep < 9 && now - lastAction > .15f)
                {
                    if (invalidPetStep == 0) Log("PASS idle does not advance focus");
                    if (invalidPetStep == 2 || invalidPetStep == 5 || invalidPetStep == 8)
                    {
                        if (ui.Progress != 0f) throw new Exception("Non-petting input advanced focus at step " + invalidPetStep);
                        Log("PASS rejected " + (invalidPetStep == 2 ? "stationary click" : invalidPetStep == 5 ? "outside drag" : "vertical drag"));
                    }
                    switch (invalidPetStep)
                    {
                        case 0: QueueMouse(center, true); break;
                        case 1: QueueMouse(center, false); break;
                        case 2: QueueMouse(new Vector2(20, 20), true); break;
                        case 3: QueueMouse(new Vector2(120, 20), true); break;
                        case 4: QueueMouse(new Vector2(120, 20), false); break;
                        case 5: QueueMouse(center, true); break;
                        case 6: QueueMouse(center + Vector2.up * 60f, true); break;
                        case 7: QueueMouse(center + Vector2.up * 60f, false); break;
                    }
                    invalidPetStep++;
                    lastAction = now;
                }
                else if (invalidPetStep >= 9 && now - lastAction > .45f && swipeStep < 7)
                {
                    // The real EventSystem receives Input System mouse down/move/up.
                    float x = center.x + (swipeStep % 2 == 0 ? -.2f : .2f) * width;
                    QueueMouse(new Vector2(x, center.y), true);
                    swipeStep++;
                    lastAction = now;
                }
                if (ui.DisplayProgress >= .49f && ui.DisplayProgress < .52f && (focusCaptureMask & 1) == 0)
                {
                    focusCaptureMask |= 1;
                    Capture("pet-focus-50.png");
                    Log("Focus midpoint progress=" + ui.DisplayProgress + " blur=" + ui.BlurPixels);
                }
                if (ui.DisplayProgress >= 1f && (focusCaptureMask & 2) == 0)
                {
                    focusCaptureMask |= 2;
                    if (ui.BlurPixels != 0f) throw new Exception("Final portrait is still blurred");
                    Capture("pet-focus-100.png");
                    Log("PASS clear portrait; blur=0, final hold begins");
                }
                if (now - petOpenedAt > 12f) throw new Exception("UGUI petting stalled at " + ui.Progress);
            }
            if (story.Stage == AfterTheGlowStoryStage.HouseExitReady)
            {
                if (story.player.ControlMode != PlayerControlMode.Free && !exitApproachStarted)
                    throw new Exception("House controls did not resume");
                if (!story.rollingBall.activeSelf || !story.cameraFollow.enabled)
                    throw new Exception("House exit readiness disabled home systems");
                if (now - stageSince > 2f && !exitApproachStarted)
                {
                    Log("PASS owner departure waits for an explicit door selection; player is Free");
                    story.player.GetComponent<PlayerInteraction>().HandleInteractionClickAtScreenPosition(Vector2.zero);
                    if (story.Stage != AfterTheGlowStoryStage.HouseExitReady)
                        throw new Exception("Blank click exited house");
                    exitApproachStarted = true;
                    story.player.BeginAutoMoveToX(story.OwnerDoorStandX, 2.8f, PlayerControlMode.Free);
                }
                var exit = story.frontDoorPivot.GetComponent<HouseExitInteraction>();
                if (exitApproachStarted && exit.CanInteract(story.player))
                {
                    Vector2 screen = story.cameraFollow.GetComponent<Camera>().WorldToScreenPoint(exit.ClickBounds.center);
                    story.player.GetComponent<PlayerInteraction>().HandleInteractionClickAtScreenPosition(screen);
                    if (story.HouseExitCount != 1) throw new Exception("Door selection did not exit exactly once");
                    Log("PASS selected open door starts exterior once");
                }
            }
            if (!capturedBeam && story.Stage == AfterTheGlowStoryStage.FollowOwner)
            {
                capturedBeam = true;
                Camera camera = story.cameraFollow.GetComponent<Camera>();
                float left = camera.transform.position.x - camera.orthographicSize * camera.aspect;
                bool exteriorClear = left >= 6.5f;
                alignmentPassed &= exteriorClear;
                Log((exteriorClear ? "PASS " : "FAIL ") + "exterior excludes house; left=" + left);
                GameplayLight2D light = story.carHeadlight.GetComponent<GameplayLight2D>();
                Vector2 source = story.carHeadlight.transform.position;
                Mesh mesh = story.carHeadlight.GetComponentInChildren<MeshFilter>().sharedMesh;
                bool beamsMatch = light.UsesDirectionalBeams && mesh.vertexCount == 192 &&
                    mesh.bounds.max.y <= 0.001f && mesh.bounds.min.y < -3f &&
                    light.TryGetStrength(source + new Vector2(-1.55f, -1f), out _, out _) &&
                    !light.TryGetStrength(source + Vector2.left * 4f, out _, out _) &&
                    !light.TryGetStrength(source + Vector2.up, out _, out _);
                alignmentPassed &= beamsMatch;
                Log((beamsMatch ? "PASS " : "FAIL ") + "two downward cones; exposure inside beams only");
                Capture("headlights-on.png");
            }
            if (!drivingStreet && story.Stage == AfterTheGlowStoryStage.FollowOwner)
            {
                drivingStreet = true;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            }
            if (story.Stage == AfterTheGlowStoryStage.HeadlightRetreat || story.Stage == AfterTheGlowStoryStage.HeadlightWait)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            if (!lightOffObserved && story.Stage == AfterTheGlowStoryStage.GateBound)
            {
                lightOffObserved = true;
                if (story.carHeadlight.activeSelf) { Finish("FAIL headlight stayed on"); return; }
                Log("PASS headlight switched off after retreat");
                Capture("headlights-off.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            }
            if (lightOffObserved && story.player.transform.position.x > 18.3f)
            {
                bool singleEvents = story.PetCount == 1 && story.OwnerDepartureCount == 1 && story.HeadlightOffCount == 1;
                Log((singleEvents ? "PASS " : "FAIL ") + "single pet, departure and light-off events");
                Finish((alignmentPassed && singleEvents ? "PASS " : "FAIL checks; ") +
                    "continuous rubbing completed, owner left, exterior resumed, retreat completed, light switched off, dog passed the car");
            }
            if (now - stageSince > 18f && story.Stage != AfterTheGlowStoryStage.OwnerPreparing)
                Finish("FAIL stalled at " + story.Stage);
        }
        catch (Exception exception) { Finish("FAIL " + exception); }
    }

    private static void QueueMouse(Vector2 position, bool held)
    {
        if (story != null && story.Stage == AfterTheGlowStoryStage.OwnerPetPuzzle && story.pettingPuzzleUI.IsOpen)
        {
            QueuePetEvent(held ? (petMouseHeld ? EventType.MouseDrag : EventType.MouseDown) : EventType.MouseUp,
                new Vector2(position.x, Screen.height - position.y));
            petMouseHeld = held;
        }
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, held));
    }

    private static void QueuePetEvent(EventType type, Vector2 position)
    {
        var enqueue = typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (enqueue == null) throw new MissingMethodException("QueueGameViewInputEvent");
        enqueue.Invoke(null, new object[] { new Event { type = type, button = 0, mousePosition = position } });
    }

    private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(folder, name));
    private static void Log(string line)
    {
        Lines.Add((Time.realtimeSinceStartup - started).ToString("F2") + " " + line);
        File.WriteAllLines(Path.Combine(folder, "owner-flow-report.txt"), Lines);
    }
    private static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        Log(result);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        Application.runInBackground = oldBackground;
        InputSystem.settings.backgroundBehavior = oldInputBackground;
        Debug.Log("Owner flow replay: " + result);
    }
}
