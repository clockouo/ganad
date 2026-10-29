using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Pipeline.Commands;

/// <summary>Fits the separate door leaves inside the painted doorway openings.</summary>
public static class DoorArtworkSetup
{
    private const string Output = "tmp/door-alignment/";
    private static readonly Rect FrontPixels = new Rect(1920, 185, 166, 341);
    private static readonly Rect BathroomPixels = new Rect(848, 185, 164, 342);
    private static string exitReview = "Not started";

    [CliCommand("door_art", "Align door leaves with the supplied background openings", MainThreadRequired = true)]
    public static object Command([CliArg("action", "before, apply, preview, runtime, exit-start, exit-status, gate-front, owner-route-preview, gate-front-preview")] string action = "preview")
    {
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story == null) throw new MissingReferenceException("Open SampleScene first.");
        if (action == "owner-route-preview")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before taking the route preview.");
            return CaptureOwnerExitRoute(story);
        }
        if (action == "gate-front-preview")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before taking the gate preview.");
            return CaptureDogAtGate(story);
        }
        if (action == "gate-front")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before saving gate sorting.");
            const string folder = "tmp/gate-front-v2.49/";
            Directory.CreateDirectory(folder);
            if (!File.Exists(folder + "before-SampleScene.unity"))
                EditorSceneManager.SaveScene(story.gameObject.scene, folder + "before-SampleScene.unity", true);

            var hinge = story.parkGate.GetComponent<GateHingePresentation>();
            if (hinge.leftLeaf == null)
                hinge.leftLeaf = story.parkGate.transform.Find("Left Hinged Gate Leaf")?.GetComponent<SpriteRenderer>();
            if (hinge.rightLeaf == null)
                hinge.rightLeaf = story.parkGate.transform.Find("Right Hinged Gate Leaf")?.GetComponent<SpriteRenderer>();
            var dog = story.player.transform.Find("PixelSnapper Dog Visual").GetComponent<SpriteRenderer>();
            int previousLeft = hinge.leftLeaf.sortingOrder;
            int previousRight = hinge.rightLeaf.sortingOrder;
            hinge.closedSortingLayerID = dog.sortingLayerID;
            hinge.closedSortingOrder = dog.sortingOrder + 2;
            hinge.foregroundSortingLayerID = dog.sortingLayerID;
            hinge.foregroundSortingOrder = dog.sortingOrder + 2;
            hinge.leftLeaf.sortingLayerID = dog.sortingLayerID;
            hinge.rightLeaf.sortingLayerID = dog.sortingLayerID;
            hinge.leftLeaf.sortingOrder = hinge.closedSortingOrder;
            hinge.rightLeaf.sortingOrder = hinge.closedSortingOrder;
            EditorUtility.SetDirty(hinge);
            EditorUtility.SetDirty(hinge.leftLeaf);
            EditorUtility.SetDirty(hinge.rightLeaf);
            EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
            EditorSceneManager.SaveScene(story.gameObject.scene);
            return new { previousLeft, previousRight, dogOrder = dog.sortingOrder,
                gateOrder = hinge.closedSortingOrder, saved = true };
        }
        if (action == "exit-status") return exitReview;
        if (action == "exit-start")
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            story.StopAllCoroutines();
            story.bedWakePuzzleUI.Close(); story.pettingPuzzleUI.Close();
            story.player.SetControlMode(PlayerControlMode.CutsceneLocked);
            typeof(AfterTheGlowStoryDirector).GetProperty("Stage").SetValue(story, AfterTheGlowStoryStage.OwnerAtTheDoor);
            story.homeRoomLighting.SetLightImmediate(true);
            story.bathroomLight.gameObject.SetActive(true);
            typeof(AfterTheGlowStoryDirector).GetMethod("SetBathroomDoorOpen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(story, new object[] { true });
            exitReview = "Running";
            story.StartCoroutine(ReviewBathroomExit(story));
            return exitReview;
        }
        if (action == "before") return Capture(story, "before", false);
        if (action == "runtime") return VerifyRuntime(story);
        if (action == "apply")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Directory.CreateDirectory(Output);
            if (!File.Exists(Output + "before-SampleScene.unity"))
                EditorSceneManager.SaveScene(story.gameObject.scene, Output + "before-SampleScene.unity", true);
            ApplyToScene(story);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
            EditorSceneManager.SaveScene(story.gameObject.scene);
            return "Doors fitted to painted openings; legacy dark opening disabled and scene saved.";
        }
        return Capture(story, "after", true);
    }

    private static System.Collections.IEnumerator ReviewBathroomExit(AfterTheGlowStoryDirector story)
    {
        var routine = (System.Collections.IEnumerator)typeof(AfterTheGlowStoryDirector)
            .GetMethod("OwnerLeavesBathroom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(story, null);
        bool complete = false, closedBeforeFrontDoor = false;
        float closedAtX = float.NaN;
        System.Collections.IEnumerator Run()
        {
            yield return routine;
            complete = true;
        }
        story.StartCoroutine(Run());
        float start = Time.time;
        while (!complete && Time.time - start < 30f)
        {
            if (!story.BathroomDoorOpen && float.IsNaN(closedAtX))
            {
                closedAtX = story.owner.position.x;
                closedBeforeFrontDoor = Mathf.Abs(story.owner.position.x - story.OwnerDoorStandX) > .1f;
            }
            yield return null;
        }
        var world = story.GetComponent<JourneyWorldArtPresentation>();
        bool closed = !story.BathroomDoorOpen && world.bathroomDoor.sprite == JourneyArt.Frame("Props/Doors__c2r2", 0);
        bool lightOff = !story.bathroomLight.gameObject.activeSelf;
        // Turning a room light on must no longer reopen the door.
        story.bathroomLight.gameObject.SetActive(true); world.SendMessage("LateUpdate");
        bool independent = world.bathroomDoor.sprite == JourneyArt.Frame("Props/Doors__c2r2", 0);
        story.bathroomLight.gameObject.SetActive(false);
        bool passed = complete && closedBeforeFrontDoor && closed && lightOff && independent;
        exitReview = $"{(passed ? "PASS" : "FAIL")}: complete={complete}, closedBeforeFrontDoor={closedBeforeFrontDoor}, closed={closed}, lightOff={lightOff}, independentOfLight={independent}, closedAtX={closedAtX}, finalX={story.owner.position.x}, frames={Time.frameCount}, seconds={Time.time - start}";
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "bathroom-exit-check.txt", exitReview);
        Capture(story, "exit", false);
    }

    public static void ApplyToScene(AfterTheGlowStoryDirector story)
    {
        var background = story.gameObject.scene.GetRootGameObjects().First(g => g.name == "BackGround").GetComponent<SpriteRenderer>();
        if (background.sprite == null || background.sprite.texture.name != "01_House")
            throw new InvalidOperationException("Door pixel coordinates require the final 01_House background.");
        foreach (string name in new[] { "Front Door Dark Opening", "Front Door Frame Left", "Front Door Frame Right", "Front Door Frame Header",
            "Front Door Handle", "HingedDoor_Closed", "FrontDoor_Closed", "FrontDoor_Open", "StoryDoor_Open_State",
            "Bathroom Door Left", "Bathroom Door Right", "Bathroom Door Header" })
        {
            foreach (var renderer in story.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == name))
            { renderer.enabled = false; EditorUtility.SetDirty(renderer); }
        }
        var front = story.frontDoorPivot.Find("Front Door Panel").GetComponent<SpriteRenderer>();
        front.sprite = story.frontDoorClosedSprite;
        Fit(background, front, FrontPixels);
        var world = story.GetComponent<JourneyWorldArtPresentation>();
        world.bathroomDoor.sprite = JourneyArt.Frame("Props/Doors__c2r2", 0);
        Fit(background, world.bathroomDoor, BathroomPixels);
        world.bathroomClosedPosition = world.bathroomDoor.transform.localPosition;
        world.bathroomClosedScale = world.bathroomDoor.transform.localScale;
        world.bathroomArtAligned = true;
        EditorUtility.SetDirty(world);
    }

    private static Bounds Opening(SpriteRenderer background, Rect pixels)
    {
        var sprite = background.sprite;
        Vector2 pixelCenter = new Vector2(pixels.center.x, sprite.texture.height - pixels.center.y);
        Vector2 local = (pixelCenter - sprite.rect.position - sprite.pivot) / sprite.pixelsPerUnit;
        Vector3 center = background.transform.TransformPoint(new Vector3(local.x, local.y, 0));
        Vector3 scale = background.transform.lossyScale;
        return new Bounds(center, new Vector3(pixels.width / sprite.pixelsPerUnit * Mathf.Abs(scale.x),
            pixels.height / sprite.pixelsPerUnit * Mathf.Abs(scale.y), .1f));
    }

    private static void Fit(SpriteRenderer background, SpriteRenderer door, Rect pixels)
    {
        var opening = Opening(background, pixels);
        Vector3 parent = door.transform.parent != null ? door.transform.parent.lossyScale : Vector3.one;
        door.transform.position = new Vector3(opening.center.x, opening.center.y, door.transform.position.z);
        door.transform.localScale = new Vector3(opening.size.x / door.sprite.bounds.size.x / Mathf.Abs(parent.x),
            opening.size.y / door.sprite.bounds.size.y / Mathf.Abs(parent.y), 1);
        door.enabled = true;
        EditorUtility.SetDirty(door); EditorUtility.SetDirty(door.transform);
    }

    private static string[] Capture(AfterTheGlowStoryDirector story, string prefix, bool includeOpen)
    {
        Directory.CreateDirectory(Output);
        var camera = story.cameraFollow.GetComponent<Camera>();
        var world = story.GetComponent<JourneyWorldArtPresentation>();
        var front = story.frontDoorPivot.Find("Front Door Panel").GetComponent<SpriteRenderer>();
        var bath = world.bathroomDoor;
        Vector3 position = camera.transform.position;
        float size = camera.orthographicSize, aspect = camera.aspect;
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        Vector3 frontPosition = front.transform.localPosition, frontScale = front.transform.localScale;
        Vector3 bathPosition = bath.transform.localPosition, bathScale = bath.transform.localScale;
        Sprite bathSprite = bath.sprite;
        var target = new RenderTexture(900, 900, 24);
        var pixels = new Texture2D(900, 900, TextureFormat.RGB24, false);
        var paths = new System.Collections.Generic.List<string>();
        try
        {
            camera.targetTexture = target; camera.aspect = 1; camera.orthographicSize = 1.85f;
            void Shot(string name, SpriteRenderer door)
            {
                camera.transform.position = new Vector3(door.bounds.center.x, door.bounds.center.y, -10);
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); pixels.Apply();
                string path = Path.GetFullPath(Output + prefix + "-" + name + ".png");
                File.WriteAllBytes(path, pixels.EncodeToPNG()); paths.Add(path);
            }
            Shot("front-closed", front); Shot("bathroom-closed", bath);
            if (includeOpen)
            {
                // Same width and hinge transform used by the story director.
                float width = front.sprite.bounds.size.x * frontScale.x;
                front.transform.localScale = new Vector3(frontScale.x * .3f, frontScale.y, frontScale.z);
                front.transform.localPosition = frontPosition + Vector3.left * width * .35f;
                Shot("front-open", front);
                world.SetBathroomDoor(true); Shot("bathroom-open", bath);
            }
        }
        finally
        {
            front.transform.localPosition = frontPosition; front.transform.localScale = frontScale;
            bath.transform.localPosition = bathPosition; bath.transform.localScale = bathScale; bath.sprite = bathSprite;
            camera.transform.position = position; camera.orthographicSize = size; camera.aspect = aspect;
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
        return paths.ToArray();
    }

    private static object CaptureOwnerExitRoute(AfterTheGlowStoryDirector story)
    {
        const string folder = "tmp/owner-door-review-v2.49/";
        Directory.CreateDirectory(folder);
        var background = GameObject.Find("BackGround").GetComponent<SpriteRenderer>();
        var front = story.frontDoorPivot.Find("Front Door Panel").GetComponent<SpriteRenderer>();
        var owner = story.owner;
        var ownerVisual = owner.GetComponentInChildren<SpriteRenderer>(true);
        var camera = story.cameraFollow.GetComponent<Camera>();
        Vector3 ownerPosition = owner.position;
        bool ownerActive = owner.gameObject.activeSelf;
        Sprite ownerSprite = ownerVisual.sprite;
        bool ownerEnabled = ownerVisual.enabled;
        bool ownerFlip = ownerVisual.flipX;
        Vector3 visualPosition = ownerVisual.transform.localPosition;
        Vector3 visualScale = ownerVisual.transform.localScale;
        Vector3 frontPosition = front.transform.localPosition;
        Vector3 frontScale = front.transform.localScale;
        Sprite frontSprite = front.sprite;
        Vector3 cameraPosition = camera.transform.position;
        float cameraSize = camera.orthographicSize;
        float cameraAspect = camera.aspect;
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        bool followWasEnabled = story.cameraFollow.enabled;
        var target = new RenderTexture(900, 900, 24);
        var pixels = new Texture2D(900, 900, TextureFormat.RGB24, false);
        var paths = new System.Collections.Generic.List<string>();
        float doorX = story.frontDoorPivot.position.x;
        Bounds opening = Opening(background, FrontPixels);
        try
        {
            owner.gameObject.SetActive(true);
            if (story.ownerWalkSprite != null) ownerVisual.sprite = story.ownerWalkSprite;
            ownerVisual.enabled = true;
            ownerVisual.flipX = false;
            if (ownerVisual.sprite != null)
            {
                Vector3 parentScale = ownerVisual.transform.parent != null
                    ? ownerVisual.transform.parent.lossyScale : Vector3.one;
                float scale = story.ownerStandingHeight / ownerVisual.sprite.bounds.size.y;
                ownerVisual.transform.localScale = new Vector3(scale / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                    scale / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1f);
                ownerVisual.transform.localPosition = new Vector3(0f, story.ownerVisualGroundOffset, -.2f);
            }

            front.sprite = story.frontDoorClosedSprite;
            float closedPanelWidth = front.sprite.bounds.size.x * Mathf.Abs(frontScale.x);
            front.transform.localScale = new Vector3(frontScale.x * .12f, frontScale.y, frontScale.z);
            front.transform.localPosition = frontPosition + Vector3.left * (closedPanelWidth * .44f);

            float actorHalfWidth = ownerVisual.sprite.bounds.extents.x *
                Mathf.Abs(ownerVisual.transform.lossyScale.x);
            float doorWidth = front.sprite.bounds.size.x * Mathf.Abs(frontScale.x) *
                Mathf.Abs(front.transform.parent != null ? front.transform.parent.lossyScale.x : 1f);
            float exitX = Mathf.Max(doorX + story.ownerDoorExitOffset,
                doorX + doorWidth * .5f + actorHalfWidth + .08f);
            float[] routeXs = { story.OwnerDoorStandX, doorX, exitX };
            string[] names = { "owner-at-door-start", "owner-crossing-door", "owner-clear-of-door" };

            camera.targetTexture = target;
            camera.aspect = 1f;
            camera.orthographicSize = 2.7f;
            story.cameraFollow.enabled = false;
            for (int i = 0; i < routeXs.Length; i++)
            {
                owner.position = new Vector3(routeXs[i], ownerPosition.y, ownerPosition.z);
                camera.transform.position = new Vector3(doorX, opening.center.y, cameraPosition.z);
                Physics2D.SyncTransforms();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                pixels.Apply();
                string path = Path.GetFullPath(folder + names[i] + ".png");
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                paths.Add(path);
            }

            Bounds crossingBounds = ownerVisual.bounds;
            owner.position = new Vector3(doorX, ownerPosition.y, ownerPosition.z);
            Physics2D.SyncTransforms();
            crossingBounds = ownerVisual.bounds;
            bool crossesOpening = crossingBounds.min.x <= doorX && crossingBounds.max.x >= doorX &&
                crossingBounds.min.y < opening.max.y && crossingBounds.max.y > opening.min.y;
            owner.position = new Vector3(exitX, ownerPosition.y, ownerPosition.z);
            Physics2D.SyncTransforms();
            bool clearsOpening = ownerVisual.bounds.min.x > opening.max.x - .01f;
            return new
            {
                doorX,
                opening = new { left = opening.min.x, right = opening.max.x },
                startX = routeXs[0],
                crossingX = routeXs[1],
                exitX,
                crossesOpening,
                clearsOpening,
                screenshots = paths.ToArray()
            };
        }
        finally
        {
            owner.position = ownerPosition;
            ownerVisual.transform.localPosition = visualPosition;
            ownerVisual.transform.localScale = visualScale;
            ownerVisual.sprite = ownerSprite;
            ownerVisual.flipX = ownerFlip;
            ownerVisual.enabled = ownerEnabled;
            owner.gameObject.SetActive(ownerActive);
            front.transform.localPosition = frontPosition;
            front.transform.localScale = frontScale;
            front.sprite = frontSprite;
            camera.transform.position = cameraPosition;
            camera.orthographicSize = cameraSize;
            camera.aspect = cameraAspect;
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            story.cameraFollow.enabled = followWasEnabled;
            UnityEngine.Object.DestroyImmediate(pixels);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static object CaptureDogAtGate(AfterTheGlowStoryDirector story)
    {
        const string folder = "tmp/gate-front-v2.49/";
        Directory.CreateDirectory(folder);
        var camera = story.cameraFollow.GetComponent<Camera>();
        var dog = story.player.transform.Find("PixelSnapper Dog Visual").GetComponent<SpriteRenderer>();
        var hinge = story.parkGate.GetComponent<GateHingePresentation>();
        if (hinge.leftLeaf == null)
            hinge.leftLeaf = story.parkGate.transform.Find("Left Hinged Gate Leaf")?.GetComponent<SpriteRenderer>();
        if (hinge.rightLeaf == null)
            hinge.rightLeaf = story.parkGate.transform.Find("Right Hinged Gate Leaf")?.GetComponent<SpriteRenderer>();

        Vector3 dogPosition = dog.transform.position;
        Vector3 cameraPosition = camera.transform.position;
        float cameraSize = camera.orthographicSize;
        float cameraAspect = camera.aspect;
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        int leftOrder = hinge.leftLeaf.sortingOrder, rightOrder = hinge.rightLeaf.sortingOrder;
        var target = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        string path = Path.GetFullPath(folder + "dog-crossing-front-gate.png");
        try
        {
            dog.transform.position = new Vector3(story.parkGate.transform.position.x,
                story.player.transform.position.y, dogPosition.z);
            camera.transform.position = new Vector3(story.parkGate.transform.position.x, -2.95f, cameraPosition.z);
            camera.orthographicSize = 2.2f;
            camera.aspect = 1280f / 720f;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            return new
            {
                dogOrder = dog.sortingOrder,
                leftGateOrder = hinge.leftLeaf.sortingOrder,
                rightGateOrder = hinge.rightLeaf.sortingOrder,
                gateIsInFront = hinge.leftLeaf.sortingOrder > dog.sortingOrder &&
                    hinge.rightLeaf.sortingOrder > dog.sortingOrder,
                screenshot = path
            };
        }
        finally
        {
            dog.transform.position = dogPosition;
            hinge.leftLeaf.sortingOrder = leftOrder;
            hinge.rightLeaf.sortingOrder = rightOrder;
            camera.transform.position = cameraPosition;
            camera.orthographicSize = cameraSize;
            camera.aspect = cameraAspect;
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            Physics2D.SyncTransforms();
            UnityEngine.Object.DestroyImmediate(pixels);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static object VerifyRuntime(AfterTheGlowStoryDirector story)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode for the runtime check.");
        var background = GameObject.Find("BackGround").GetComponent<SpriteRenderer>();
        var front = story.frontDoorPivot.Find("Front Door Panel").GetComponent<SpriteRenderer>();
        var method = typeof(AfterTheGlowStoryDirector).GetMethod("ApplyFrontDoorPose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        float openness = story.FrontDoorOpenness;
        var world = story.GetComponent<JourneyWorldArtPresentation>();
        var results = new System.Collections.Generic.List<string>();
        void Check(SpriteRenderer leaf, Bounds opening, string label)
        {
            Bounds bounds = leaf.bounds;
            bool contained = bounds.min.x >= opening.min.x - .005f && bounds.max.x <= opening.max.x + .005f &&
                bounds.min.y >= opening.min.y - .005f && bounds.max.y <= opening.max.y + .005f;
            if (!contained) throw new InvalidOperationException(label + " extends outside the doorway.");
            results.Add(label + ": PASS");
        }
        try
        {
            foreach (float value in new[] { 0f, .2f, 1f })
            { method.Invoke(story, new object[] { value }); Check(front, Opening(background, FrontPixels), "Front " + value); }
            world.SetBathroomDoor(false); Check(world.bathroomDoor, Opening(background, BathroomPixels), "Bathroom closed");
            world.SetBathroomDoor(true); Check(world.bathroomDoor, Opening(background, BathroomPixels), "Bathroom open");
        }
        finally
        {
            method.Invoke(story, new object[] { openness });
            world.SetBathroomDoor(story.BathroomDoorOpen);
        }
        File.WriteAllLines(Output + "runtime-checks.txt", results);
        return results.ToArray();
    }
}
