using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Pipeline.Commands;

/// <summary>Anchor the original light artwork to the actual car lamp pixels.</summary>
public static class HeadlightAlignmentSetup
{
    private const string Output = "tmp/headlight-alignment/";
    // Pixel centers in the unmodified source PNGs, measured from the top left.
    private static readonly Vector2[] CarLamps = { new Vector2(161.5f, 411.5f), new Vector2(521.5f, 411.5f) };
    private static readonly Vector2[] GlowLamps = { new Vector2(333f, 245f), new Vector2(557f, 245f) };

    [CliCommand("headlight_alignment", "Align and inspect the car headlight origins", MainThreadRequired = true)]
    public static object Command([CliArg("action", "before, apply, park_car, preview, runtime")] string action = "preview")
    {
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (action == "runtime") return CheckRuntime(story);
        if (action == "before") return Capture(story, "before");
        if (action == "apply" || action == "park_car")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            string folder = action == "park_car" ? "tmp/car-wall-gap-20261022/" : Output;
            Directory.CreateDirectory(folder);
            if (!File.Exists(folder + "before-SampleScene.unity")) EditorSceneManager.SaveScene(story.gameObject.scene, folder + "before-SampleScene.unity", true);
            ApplyToScene(story);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(story.gameObject.scene); EditorSceneManager.SaveScene(story.gameObject.scene);
            return "Car centered between the street wall posts; light origins aligned and scene saved.";
        }
        return Capture(story, "after");
    }

    private static object CheckRuntime(AfterTheGlowStoryDirector story)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        var world = story.GetComponent<JourneyWorldArtPresentation>();
        bool original = story.carHeadlight.activeSelf;
        try
        {
            foreach (bool on in new[] { false, true })
            {
                story.carHeadlight.SetActive(on);
                world.SendMessage("LateUpdate");
                if (world.leftHeadlight.enabled != on || world.rightHeadlight.enabled != on)
                    throw new InvalidOperationException("Headlight visibility did not follow the gameplay light.");
            }
            string result = "PASS: both aligned beams follow the gameplay light on and off.";
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "runtime-check.txt", result);
            return result;
        }
        finally
        {
            story.carHeadlight.SetActive(original);
            world.SendMessage("LateUpdate");
        }
    }

    private static Vector3 LocalPixel(Sprite sprite, Vector2 topLeftPixel)
    {
        Vector2 pixel = new Vector2(topLeftPixel.x, sprite.texture.height - topLeftPixel.y);
        Vector2 local = (pixel - sprite.rect.position - sprite.pivot) / sprite.pixelsPerUnit;
        return new Vector3(local.x, local.y, 0);
    }

    public static void ApplyToScene(AfterTheGlowStoryDirector story)
    {
        var car = story.GetComponentsInChildren<CarHeadlightPresentation>(true).Single();
        var carPosition = car.transform.position;
        carPosition.x = AfterTheGlowWorldLayout.CarX;
        car.transform.position = carPosition;
        var lightPosition = story.carHeadlight.transform.position;
        lightPosition.x = carPosition.x;
        story.carHeadlight.transform.position = lightPosition;
        car.Refresh();
        EditorUtility.SetDirty(car.transform);
        EditorUtility.SetDirty(story.carHeadlight.transform);
        var carRenderer = car.GetComponent<SpriteRenderer>();
        var world = story.GetComponent<JourneyWorldArtPresentation>();
        var legacy = story.transform.Find("Final Headlight Glow");
        if (legacy != null) { legacy.GetComponent<SpriteRenderer>().enabled = false; EditorUtility.SetDirty(legacy.GetComponent<SpriteRenderer>()); }
        world.headlights = null;
        var source = AssetDatabase.LoadAllAssetsAtPath(FinalJourneyArtSetup.Root + "Effects/Lights__c2r2.png")
            .OfType<Sprite>().Single(s => s.name == "Lights__c2r2_00");
        var emitters = new SpriteRenderer[2];
        Directory.CreateDirectory("Assets/Art/Generated");
        for (int i = 0; i < 2; i++)
        {
            string side = i == 0 ? "Left" : "Right";
            string path = "Assets/Art/Generated/HeadlightAligned" + side + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                const float split = 446f;
                Rect rect = new Rect(i == 0 ? source.rect.x : split, source.rect.y,
                    i == 0 ? split - source.rect.x : source.rect.xMax - split, source.rect.height);
                Vector2 pixel = new Vector2(GlowLamps[i].x, source.texture.height - GlowLamps[i].y);
                Vector2 pivot = (pixel - rect.position) / rect.size;
                sprite = Sprite.Create(source.texture, rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = "Headlight Aligned " + side;
                AssetDatabase.CreateAsset(sprite, path);
            }
            string name = "Aligned " + side + " Headlight";
            var child = car.transform.Find(name);
            if (child == null) { child = new GameObject(name, typeof(SpriteRenderer)).transform; child.SetParent(car.transform, false); }
            var renderer = child.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = carRenderer.sharedMaterial;
            renderer.sortingLayerID = carRenderer.sortingLayerID; renderer.sortingOrder = carRenderer.sortingOrder + 1;
            renderer.color = Color.white; renderer.enabled = story.carHeadlight.activeInHierarchy;
            child.localPosition = LocalPixel(carRenderer.sprite, CarLamps[i]) + Vector3.back * .01f;
            child.localScale = Vector3.one; child.localRotation = Quaternion.identity;
            emitters[i] = renderer; EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(child);
        }
        world.leftHeadlight = emitters[0]; world.rightHeadlight = emitters[1]; EditorUtility.SetDirty(world);
        var gameplay = story.carHeadlight.GetComponent<GameplayLight2D>();
        var origins = emitters.Select(r => (Vector2)gameplay.transform.InverseTransformPoint(r.transform.position)).ToArray();
        gameplay.ConfigureDirectionalBeams(origins, Vector2.down, 1.25f, .12f, .75f, .3f);
        EditorUtility.SetDirty(gameplay);
    }

    private static object Capture(AfterTheGlowStoryDirector story, string prefix)
    {
        Directory.CreateDirectory(Output);
        var camera = story.cameraFollow.GetComponent<Camera>();
        var car = story.GetComponentsInChildren<CarHeadlightPresentation>(true).Single();
        Vector3 position = camera.transform.position; float size = camera.orthographicSize, aspect = camera.aspect;
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = new RenderTexture(1200, 800, 24); var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        var world = story.GetComponent<JourneyWorldArtPresentation>();
        bool[] enabled = { world.headlights != null && world.headlights.enabled, world.leftHeadlight != null && world.leftHeadlight.enabled, world.rightHeadlight != null && world.rightHeadlight.enabled };
        try
        {
            if (world.headlights != null) world.headlights.enabled = true;
            if (world.leftHeadlight != null) world.leftHeadlight.enabled = true;
            if (world.rightHeadlight != null) world.rightHeadlight.enabled = true;
            camera.targetTexture = target; camera.aspect = 1.5f; camera.orthographicSize = 1.8f;
            camera.transform.position = new Vector3(car.transform.position.x, -3.25f, -10);
            camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); pixels.Apply();
            string path = Path.GetFullPath(Output + prefix + ".png"); File.WriteAllBytes(path, pixels.EncodeToPNG());
            var carSprite = car.GetComponent<SpriteRenderer>().sprite;
            float[] errors = new[] { world.leftHeadlight, world.rightHeadlight }.Select((r, i) => r == null ? -1f :
                Vector2.Distance(r.transform.position, car.transform.TransformPoint(LocalPixel(carSprite, CarLamps[i])))).ToArray();
            if (prefix == "after" && errors.Any(e => e < 0 || e > .001f)) throw new InvalidOperationException("Headlight origins are not aligned.");
            var result = new { image = path, emitterPositionErrors = errors };
            File.WriteAllText(Output + prefix + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            if (world.headlights != null) world.headlights.enabled = enabled[0];
            if (world.leftHeadlight != null) world.leftHeadlight.enabled = enabled[1];
            if (world.rightHeadlight != null) world.rightHeadlight.enabled = enabled[2];
            camera.transform.position = position; camera.orthographicSize = size; camera.aspect = aspect;
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
