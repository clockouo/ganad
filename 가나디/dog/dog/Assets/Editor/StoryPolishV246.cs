using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Pipeline.Commands;

/// <summary>Persist the feedback without rebuilding the scene or replacing supplied artwork.</summary>
public static class StoryPolishV246
{
    public const string Evidence = "tmp/feedback-v2.46";
    [CliCommand("story_polish", "Inspect, apply, and playtest story transitions", MainThreadRequired = true)]
    public static object Command([CliArg("action", "inspect, apply, review, report")] string action = "inspect")
    {
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story == null) throw new MissingReferenceException("Open SampleScene first.");
        if (action == "apply") return Apply(story);
        if (action == "preview_park") return PreviewPark(story);
        if (action == "review") return StoryFlowReviewV246.Main();
        if (action == "report") return File.Exists(Evidence + "/result.txt") ? File.ReadAllText(Evidence + "/result.txt") : "No review yet.";
        var fountain = story.GetComponent<ParkFountainExploration>();
        if (action == "fountain_portals" || action == "fountain_exit")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before saving fountain portals.");
            string folder = action == "fountain_exit" ? "tmp/fountain-exit-bench-20261022" : "tmp/fountain-portals-v2.48";
            Directory.CreateDirectory(folder);
            if (!File.Exists(folder + "/before-active.unity"))
                EditorSceneManager.SaveScene(story.gameObject.scene, folder + "/before-active.unity", true);
            var previous = new { entry = fountain.entryWorldX, exit = fountain.exitWorldX };
            if (action == "fountain_portals") fountain.entryWorldX = AfterTheGlowWorldLayout.FountainEntryX;
            fountain.exitWorldX = AfterTheGlowWorldLayout.FountainExitX;
            EditorUtility.SetDirty(fountain);
            EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
            EditorSceneManager.SaveScene(story.gameObject.scene);
            var result = new { previous, entry = fountain.entryWorldX, exit = fountain.exitWorldX, radius = fountain.approachDistance };
            File.WriteAllText(folder + "/applied.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        var gate = story.parkGate.GetComponent<GateHingePresentation>();
        return new {
            playing = EditorApplication.isPlaying, frame = Time.frameCount,
            stage = story.Stage.ToString(), player = story.player.transform.position.ToString(),
            camera = story.cameraFollow.transform.position.ToString(),
            bathroomX = story.bathroomX, owner = story.owner.position.ToString(),
            gate = EditorJsonUtility.ToJson(gate), fountain = EditorJsonUtility.ToJson(fountain),
            renderers = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include)
                .Where(r => r.gameObject.scene.IsValid() && (r.name.Contains("Gate") || r.name.Contains("Dog") || r.name.Contains("Backdrop") || r.name.Contains("Walls")))
                .Select(r => new { r.name, r.enabled, layer = r.sortingLayerName, order = r.sortingOrder,
                    position = r.transform.position.ToString(), path = r.sprite == null ? "" : AssetDatabase.GetAssetPath(r.sprite) }).ToArray()
        };
    }

    public static string Apply(AfterTheGlowStoryDirector story)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before saving.");
        Directory.CreateDirectory(Evidence);
        if (!File.Exists(Evidence + "/before-active.unity"))
            EditorSceneManager.SaveScene(story.gameObject.scene, Evidence + "/before-active.unity", true);
        var fountain = story.GetComponent<ParkFountainExploration>();
        fountain.fountainWorldX = AfterTheGlowWorldLayout.FountainX;
        fountain.entryWorldX = AfterTheGlowWorldLayout.FountainEntryX;
        fountain.exitWorldX = AfterTheGlowWorldLayout.FountainExitX;
        fountain.approachDistance = .65f;
        if (fountain.memories == null || fountain.memories.Length != 4) fountain.memories = new Texture2D[4];
        string[] names = { "01_ApproachingShoes", "02_HandStops", "03_WaitingOnBench", "04_FirstSniff" };
        for (int i = 0; i < names.Length; i++)
        {
            if (fountain.memories[i] == null || fountain.memories[i].name.Contains("ParkMemoryVignettes"))
                fountain.memories[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(FinalJourneyArtSetup.Root + "Memories/" + names[i] + ".png");
            if (fountain.memories[i] == null) throw new MissingReferenceException(names[i]);
        }
        var hinge = story.parkGate.GetComponent<GateHingePresentation>();
        var dog = story.player.transform.Find("PixelSnapper Dog Visual").GetComponent<SpriteRenderer>();
        const string materialPath = "Assets/Art/Generated/GateInwardPolish.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Resources.Load<Shader>("GateForwardOpening"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        hinge.forwardOpenMaterial = material;
        hinge.opensIntoGarden = true;
        hinge.closedSortingLayerID = dog.sortingLayerID;
        hinge.closedSortingOrder = dog.sortingOrder + 2;
        hinge.foregroundSortingLayerID = dog.sortingLayerID;
        hinge.foregroundSortingOrder = dog.sortingOrder + 2;
        hinge.ResetPose();
        // The right leaf keeps its foreground order; the left leaf is behind the dog.
        hinge.rightLeaf.sortingLayerID = dog.sortingLayerID;
        hinge.rightLeaf.sortingOrder = hinge.closedSortingOrder;
        var frontWalls = story.transform.Find("Final Park Front Walls");
        if (frontWalls != null)
        {
            // These walls are already painted into the supplied park plate.
            // The separate, differently shaped copy produced doubled stone edges.
            var renderer = frontWalls.GetComponent<SpriteRenderer>();
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
        var leash = story.parkGate.GetComponent<GateLeashPresentation>();
        if (leash != null && leash.grip != null)
        {
            // The supplied final dog is taller than the earlier action sheet.
            // Keep the same tug mechanics and align the reachable end to its lips.
            Vector3 position = leash.grip.position;
            position.y = story.parkGate.transform.position.y + .42f;
            leash.grip.position = position;
            EditorUtility.SetDirty(leash.grip);
        }
        GateReleaseParticleSetup.Apply(story.parkGate.GetComponentInChildren<ParticleSystem>(true));
        LayeringArtworkSetup.ApplyToScene(story);
        foreach (var item in new UnityEngine.Object[] { fountain, hinge, hinge.leftLeaf, hinge.rightLeaf, story }) EditorUtility.SetDirty(item);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
        EditorSceneManager.SaveScene(story.gameObject.scene);
        string result = "Saved v2.46: soft wake/pet/fountain transitions; continuous bathroom camera; door recoil and impact; headlight glare; inward gate behind dog; fountain entry=" + fountain.entryWorldX + "; individual supplied memories.";
        File.WriteAllText(Evidence + "/applied.txt", result);
        return result;
    }

    private static string PreviewPark(AfterTheGlowStoryDirector story)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture the saved scene outside Play Mode.");
        var camera = story.cameraFollow.GetComponent<Camera>();
        Vector3 position = camera.transform.position;
        float size = camera.orthographicSize;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
        string path = Evidence + "/park-source-confirmed.png";
        try
        {
            camera.transform.position = new Vector3(AfterTheGlowWorldLayout.FountainX, story.cameraFollow.exteriorLockedY, -10f);
            camera.orthographicSize = story.cameraFollow.exteriorOrthographicSize;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.transform.position = position; camera.orthographicSize = size;
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
        return Path.GetFullPath(path);
    }
}
