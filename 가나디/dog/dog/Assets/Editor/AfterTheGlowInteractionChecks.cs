using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Editor-driven integration check. Positioning prepares the scenario;
// the real interaction component and story coroutines perform the actions.
public static class AfterTheGlowInteractionChecks
{
    [MenuItem("Tools/After the Glow/Verify Blanket and Door")]
    public static void Run()
    {
        Debug.Log(Main());
    }

    public static string Main()
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("Enter Play Mode first.");
        var director = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        bool oldBackground = Application.runInBackground;
        Application.runInBackground = true;
        var player = director.player;
        var bed = director.bed;
        var collider = bed.GetComponent<Collider2D>();
        var blanket = bed.transform.Find("PixelSnapper Bed Art/Bed Blanket");
        var camera = director.cameraFollow;
        if (camera.target == null || blanket == null)
            throw new InvalidOperationException("Camera target or blanket missing.");
        var bedStart = bed.transform.position;
        var blanketStart = blanket.position;
        var oldOffset = camera.exteriorOffset;
        float oldSize = camera.exteriorOrthographicSize;
        float oldY = camera.exteriorLockedY;
        float oldSmooth = camera.exteriorSmoothTime;
        bool oldLockY = camera.exteriorLockY;
        var lines = new List<string>();
        var failures = new List<string>();
        float started = Time.realtimeSinceStartup;
        float maximumBedTravel = 0f;
        float maximumBlanketTravel = 0f;
        float maximumBlanketLift = 0f;
        bool clicked = false, prepared = false, sawOpen = false, sawClosed = false;
        bool checkedAjar = false, exteriorStarted = false, tuningStarted = false;
        float closedStart = -1f, closedDuration = 0f, tuningStart = 0f;
        EditorApplication.CallbackFunction update = null;
        Action<bool> finish = success => {
            EditorApplication.update -= update;
            Application.runInBackground = oldBackground;
            camera.exteriorOffset = oldOffset;
            camera.exteriorOrthographicSize = oldSize;
            camera.exteriorLockedY = oldY;
            camera.exteriorSmoothTime = oldSmooth;
            camera.exteriorLockY = oldLockY;
            lines.Add("Result: " + (success && failures.Count == 0 ? "PASS" : "FAIL"));
            lines.AddRange(failures);
            lines.Add("Final state: " + director.Stage + ", time=" + Time.time +
                ", grounded=" + player.IsGrounded + ", target=" +
                player.GetComponent<PlayerInteraction>().CurrentTargetName);
            File.WriteAllLines(Path.Combine(Application.dataPath, "../AgentScripts/blanket-door-verification-2026-10-15.txt"), lines);
        };
        Action<bool, string> check = (condition, label) => {
            lines.Add((condition ? "PASS " : "FAIL ") + label);
            if (!condition) failures.Add(label);
        };
        check(!blanket.parent.GetComponent<SpriteRenderer>().enabled,
            "Whole-bed sprite is replaced by separate stationary frame layers");
        var legacyDoor = director.frontDoorPivot.Find("HingedDoor_Closed");
        check(legacyDoor == null || !legacyDoor.GetComponent<SpriteRenderer>().enabled,
            "Legacy closed-door duplicate is hidden");
        player.GetComponent<Rigidbody2D>().position = new Vector2(
            collider.bounds.min.x - player.BodyCollider.bounds.extents.x - 0.03f,
            player.transform.position.y);
        Physics2D.SyncTransforms();
        update = () => {
            try
            {
                if (!EditorApplication.isPlaying || Time.realtimeSinceStartup - started > 45f)
                {
                    failures.Add("Scenario interrupted or timed out at " + director.Stage);
                    finish(false);
                    return;
                }
                maximumBedTravel = Mathf.Max(maximumBedTravel, Vector3.Distance(bed.transform.position, bedStart));
                maximumBlanketTravel = Mathf.Max(maximumBlanketTravel, Mathf.Abs(blanket.position.x - blanketStart.x));
                maximumBlanketLift = Mathf.Max(maximumBlanketLift, blanket.position.y - blanketStart.y);
                if (!clicked && player.IsGrounded)
                {
                    var interaction = player.GetComponent<PlayerInteraction>();
                    interaction.RefreshTarget(1);
                    check(interaction.CurrentTarget == bed, "Bed found by normal nearby-target query");
                    interaction.HandleInteractionClick();
                    clicked = true;
                }
                if (director.BedWakeCount == 1 && !prepared)
                {
                    check(maximumBedTravel < 0.001f, "Bed remains stationary: " + maximumBedTravel);
                    check(maximumBlanketTravel >= 0.23f && maximumBlanketLift > 0.1f,
                        "Blanket moves and lifts: " + maximumBlanketTravel + ", " + maximumBlanketLift);
                    check(collider.isTrigger, "Bed allows side passage after waking");
                    ScreenCapture.CaptureScreenshot("Assets/Temp/blanket-moved-2026-10-15.png");
                    player.GetComponent<Rigidbody2D>().position = new Vector2(director.ownerPreparationTargetX, player.transform.position.y);
                    Physics2D.SyncTransforms();
                    prepared = true;
                }
                if (!sawOpen && director.FrontDoorOpenness >= 0.999f)
                {
                    check(director.frontDoorPivot.gameObject.activeInHierarchy,
                        "Door artwork hierarchy is active while opening");
                    check(director.Stage == AfterTheGlowStoryStage.OwnerAtTheDoor,
                        "Door fully opens before owner departure completes");
                    sawOpen = true;
                    ScreenCapture.CaptureScreenshot("Assets/Temp/front-door-open-2026-10-15.png");
                }
                if (sawOpen && director.OwnerDepartureCount == 1 && director.FrontDoorOpenness < 0.001f)
                {
                    if (!sawClosed)
                    {
                        sawClosed = true;
                        closedStart = Time.time;
                        ScreenCapture.CaptureScreenshot("Assets/Temp/front-door-closed-2026-10-15.png");
                    }
                    closedDuration = Time.time - closedStart;
                }
                if (director.Stage == AfterTheGlowStoryStage.HouseExitReady && !checkedAjar)
                {
                    check(sawOpen && sawClosed && closedDuration >= 0.35f,
                        "Open then closed, visible closed hold: " + closedDuration);
                    check(Mathf.Abs(director.FrontDoorOpenness - director.doorAjarAmount) < 0.001f,
                        "Rebound follows closed hold: " + director.FrontDoorOpenness);
                    check(director.BedWakeCount == 1 && director.PetCount == 1 && director.OwnerDepartureCount == 1,
                        "Wake, pet, and departure each run once");
                    check(maximumBedTravel < 0.001f, "Bed stays fixed through whole house sequence");
                    checkedAjar = true;
                }
                if (checkedAjar && !exteriorStarted)
                {
                    HouseExitInteraction exit = director.frontDoorPivot.GetComponent<HouseExitInteraction>();
                    exteriorStarted = exit.TryInteractAtWorldPosition(exit.ClickBounds.center, player);
                }
                if (director.Stage == AfterTheGlowStoryStage.FollowOwner && !tuningStarted)
                {
                    check(camera.enabled && camera.target == player.transform, "Exterior follow targets Player");
                    // Isolate camera tuning from the headlight's automatic retreat.
                    player.SetControlMode(PlayerControlMode.CutsceneLocked);
                    camera.exteriorOffset = new Vector3(1f, oldOffset.y, oldOffset.z);
                    camera.exteriorOrthographicSize = 3.8f;
                    camera.exteriorLockedY = oldY + 0.25f;
                    camera.exteriorSmoothTime = 0.05f;
                    camera.exteriorLockY = true;
                    tuningStarted = true;
                    tuningStart = Time.time;
                }
                if (tuningStarted && Time.time - tuningStart > 0.6f)
                {
                    check(Mathf.Abs(camera.GetComponent<Camera>().orthographicSize - 3.8f) < 0.001f,
                        "Exterior zoom updates live");
                    check(Mathf.Abs(camera.transform.position.y - (oldY + 0.25f)) < 0.001f,
                        "Exterior fixed height updates live");
                    float expectedX = Mathf.Max(player.transform.position.x + 1f,
                        camera.exteriorMapMinX + 3.8f * camera.GetComponent<Camera>().aspect);
                    check(Mathf.Abs(camera.transform.position.x - expectedX) < 0.08f,
                        "Exterior offset and follow smoothing update live: camera=" +
                        camera.transform.position.x + ", desired=" + expectedX);
                    finish(true);
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
                finish(false);
            }
        };
        EditorApplication.update += update;
        return "Integration check started; results are written to AgentScripts/blanket-door-verification-2026-10-15.txt";
    }
}
