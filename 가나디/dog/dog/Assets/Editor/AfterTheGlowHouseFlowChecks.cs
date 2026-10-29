using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AfterTheGlowHouseFlowChecks
{
    [MenuItem("Tools/After the Glow/Verify House Click Exit")]
    private static void Run()
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("Enter Play Mode first.");
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        var player = story.player;
        var interaction = player.GetComponent<PlayerInteraction>();
        var exit = story.frontDoorPivot.GetComponent<HouseExitInteraction>();
        var body = player.GetComponent<Rigidbody2D>();
        var follow = story.cameraFollow;
        var camera = follow.GetComponent<Camera>();
        var owner = story.owner.GetComponentInChildren<SpriteRenderer>(true);
        var door = story.frontDoorPivot.Find("Front Door Panel").GetComponent<SpriteRenderer>();
        bool oldBackground = Application.runInBackground;
        float oldSize = follow.exteriorOrthographicSize;
        Application.runInBackground = true;
        var lines = new List<string>();
        bool passed = true;
        Action<bool, string> check = (condition, description) => {
            passed &= condition;
            lines.Add((condition ? "PASS " : "FAIL ") + description);
        };
        check(!exit.TryInteractAtWorldPosition(exit.ClickBounds.center, player),
            "Door clicks before owner departure are rejected");
        check(exit.boundaryBlocker != null && exit.boundaryBlocker.enabled && !exit.boundaryBlocker.isTrigger,
            "Walking boundary blocks direct house departure");
        var legacy = story.transform.Find("House To Street Trigger");
        check(legacy == null || !legacy.gameObject.activeSelf, "Legacy walking exit trigger is inactive");
        var bedCollider = story.bed.GetComponent<Collider2D>();
        body.position = new Vector2(bedCollider.bounds.min.x - player.BodyCollider.bounds.extents.x - 0.03f,
            player.transform.position.y);
        Physics2D.SyncTransforms();
        bool clickedBed = false, movedToOldTarget = false, checkedDelay = false;
        bool checkedOwner = false, checkedExit = false, clickedExit = false, checkedStreet = false, capturedWide = false;
        float earlyArrivalAt = -1f;
        float delayAt = -1f;
        float readyAt = -1f;
        float outsideAt = -1f;
        float started = Time.realtimeSinceStartup;
        EditorApplication.CallbackFunction tick = null;
        Action finish = () => {
            EditorApplication.update -= tick;
            follow.exteriorOrthographicSize = oldSize;
            Application.runInBackground = oldBackground;
            lines.Add("Result: " + (passed ? "PASS" : "FAIL"));
            lines.Add("Final stage: " + story.Stage);
            File.WriteAllLines(Path.Combine(Application.dataPath, "../AgentScripts/house-click-exit-verification-2026-10-15.txt"), lines);
        };
        tick = () => {
            try
            {
                if (!EditorApplication.isPlaying || Time.realtimeSinceStartup - started > 55f)
                {
                    check(false, "Scenario timed out or interrupted at " + story.Stage);
                    finish();
                    return;
                }
                if (!clickedBed && player.IsGrounded)
                {
                    interaction.RefreshTarget(1);
                    interaction.HandleInteractionClick();
                    clickedBed = true;
                }
                if (story.BedWakeCount == 1 && !movedToOldTarget)
                {
                    body.position = new Vector2(0f, body.position.y);
                    Physics2D.SyncTransforms();
                    movedToOldTarget = true;
                }
                if (story.OwnerPreparationStartedAt >= 0f && earlyArrivalAt < 0f)
                {
                    check(story.ownerPreparationTargetX > 1f && !story.ReachOwnerPreparationTarget(),
                        "Old X=0 preparation point no longer releases the owner");
                    delayAt = story.OwnerPreparationStartedAt;
                    body.position = new Vector2(story.ownerPreparationTargetX, body.position.y);
                    Physics2D.SyncTransforms();
                    earlyArrivalAt = Time.time;
                }
                if (earlyArrivalAt >= 0f && !checkedDelay && Time.time - earlyArrivalAt > 0.6f)
                {
                    check(story.Stage == AfterTheGlowStoryStage.OwnerPreparing && !story.owner.gameObject.activeSelf,
                        "Early arrival waits inside the bathroom for the minimum preparation time");
                    checkedDelay = true;
                }
                if (story.Stage == AfterTheGlowStoryStage.OwnerAtTheDoor && delayAt >= 0f)
                {
                    check(Time.time - delayAt >= story.minimumOwnerPreparationSeconds - 0.04f,
                        "Owner emerges after minimum preparation time: " + (Time.time - delayAt));
                    delayAt = -1f;
                }
                if (!checkedOwner && story.PetCount == 1 && story.OwnerDepartureCount == 0)
                {
                    check(story.owner.gameObject.activeInHierarchy && owner.enabled && owner.sortingOrder > door.sortingOrder,
                        "Owner stands fully in front of the closed door while petting");
                    ScreenCapture.CaptureScreenshot("Assets/Temp/owner-visible-at-door-2026-10-15.png");
                    checkedOwner = true;
                }
                if (story.Stage == AfterTheGlowStoryStage.HouseExitReady && readyAt < 0f)
                {
                    readyAt = Time.time;
                    check(story.HouseExitCount == 0 && !story.owner.gameObject.activeSelf,
                        "Owner hides after leaving; player remains in the house without a click");
                    Vector2 away = camera.WorldToScreenPoint(new Vector3(-10f, 0f, 0f));
                    interaction.HandleInteractionClickAtScreenPosition(away);
                    check(story.Stage == AfterTheGlowStoryStage.HouseExitReady && story.HouseExitCount == 0,
                        "Clicking away from the doorway does not exit");
                    if (legacy != null)
                        legacy.GetComponent<AfterTheGlowStoryTrigger>().SendMessage("OnTriggerEnter2D", player.BodyCollider,
                            SendMessageOptions.DontRequireReceiver);
                    check(story.Stage == AfterTheGlowStoryStage.HouseExitReady, "Old trigger cannot start departure even when invoked");
                    body.position = new Vector2(0f, body.position.y);
                    Physics2D.SyncTransforms();
                    Vector2 doorway = camera.WorldToScreenPoint(exit.ClickBounds.center);
                    interaction.HandleInteractionClickAtScreenPosition(doorway);
                    check(story.HouseExitCount == 0, "Door cannot be clicked remotely from across the room");
                    body.position = new Vector2(story.frontDoorX - 0.8f, body.position.y);
                    Physics2D.SyncTransforms();
                    checkedExit = true;
                }
                if (checkedExit && !clickedExit && Time.time - readyAt > 0.5f && player.IsGrounded)
                {
                    Vector2 doorway = camera.WorldToScreenPoint(exit.ClickBounds.center);
                    interaction.HandleInteractionClickAtScreenPosition(doorway);
                    clickedExit = true;
                    outsideAt = Time.time;
                    check(story.HouseExitCount == 1 && story.Stage == AfterTheGlowStoryStage.StreetDeparture,
                        "Clicking the nearby door starts departure once");
                    check(Mathf.Abs(player.transform.position.x - story.streetEntryPlayerX) < 0.01f,
                        "Player starts directly outside: X=" + player.transform.position.x);
                    float left = camera.transform.position.x - camera.orthographicSize * camera.aspect;
                    check(left >= follow.homeMapMaxX + 0.09f, "First exterior camera frame excludes house interior: left=" + left);
                    interaction.HandleInteractionClickAtScreenPosition(doorway);
                    check(story.HouseExitCount == 1, "Repeated clicks cannot duplicate the transition");
                    ScreenCapture.CaptureScreenshot("Assets/Temp/house-click-outside-start-2026-10-15.png");
                }
                if (story.Stage == AfterTheGlowStoryStage.FollowOwner && !checkedStreet)
                {
                    check(!story.rollingBall.activeSelf && follow.enabled && player.AllowsPlayerInput,
                        "Ball remains inside; exterior follow and free controls resume");
                    player.SetControlMode(PlayerControlMode.CutsceneLocked);
                    follow.exteriorOrthographicSize = 6.2f;
                    checkedStreet = true;
                    outsideAt = Time.time;
                }
                if (checkedStreet && !capturedWide && Time.time - outsideAt > 0.7f)
                {
                    float left = camera.transform.position.x - camera.orthographicSize * camera.aspect;
                    check(left >= follow.homeMapMaxX + 0.09f, "Zooming out still excludes house interior: left=" + left);
                    ScreenCapture.CaptureScreenshot("Assets/Temp/house-click-exterior-wide-2026-10-15.png");
                    capturedWide = true;
                    outsideAt = Time.time;
                }
                if (capturedWide && Time.time - outsideAt > 0.25f)
                {
                    finish();
                }
            }
            catch (Exception exception)
            {
                check(false, exception.ToString());
                finish();
            }
        };
        EditorApplication.update += tick;
    }
}
