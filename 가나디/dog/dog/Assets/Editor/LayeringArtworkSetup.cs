using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Pipeline.Commands;

/// <summary>Keep the left gate and painted house furniture behind their actors.</summary>
public static class LayeringArtworkSetup
{
    private const string Folder = "tmp/actor-layering/";

    [CliCommand("actor_layering", "Apply and preview left gate and house furniture draw order", MainThreadRequired = true)]
    public static object Command([CliArg("action", "apply, preview")] string action = "preview")
    {
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before saving layering.");
        Directory.CreateDirectory(Folder);
        if (action == "apply")
        {
            if (!File.Exists(Folder + "before-SampleScene.unity")) EditorSceneManager.SaveScene(story.gameObject.scene, Folder + "before-SampleScene.unity", true);
            Capture(story, "before");
            ApplyToScene(story);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(story.gameObject.scene); EditorSceneManager.SaveScene(story.gameObject.scene);
        }
        Capture(story, "after");
        var hinge = story.parkGate.GetComponent<GateHingePresentation>();
        var dog = story.player.transform.Find("PixelSnapper Dog Visual").GetComponent<SpriteRenderer>();
        var owner = story.GetComponent<OwnerAnimationPresentation>().ownerRenderer;
        var background = GameObject.Find("BackGround").GetComponent<SpriteRenderer>();
        var result = new { leftLayer = hinge.leftLeaf.sortingLayerName, leftOrder = hinge.leftLeaf.sortingOrder,
            dogLayer = dog.sortingLayerName, dogOrder = dog.sortingOrder, rightOrder = hinge.rightLeaf.sortingOrder,
            ownerLayer = owner.sortingLayerName, ownerOrder = owner.sortingOrder,
            furnitureLayer = background.sortingLayerName, furnitureOrder = background.sortingOrder };
        File.WriteAllText(Folder + "applied.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        return result;
    }

    public static void ApplyToScene(AfterTheGlowStoryDirector story)
    {
        var dog = story.player.transform.Find("PixelSnapper Dog Visual").GetComponent<SpriteRenderer>();
        var hinge = story.parkGate.GetComponent<GateHingePresentation>();
        hinge.keepLeftLeafBehindActors = true;
        hinge.leftLeafSortingLayerID = dog.sortingLayerID;
        hinge.leftLeafSortingOrder = dog.sortingOrder - 2;
        hinge.SetOpenProgress(hinge.OpenProgress);
        var owner = story.GetComponent<OwnerAnimationPresentation>().ownerRenderer;
        owner.sortingLayerID = dog.sortingLayerID;
        owner.sortingOrder = Mathf.Max(owner.sortingOrder, dog.sortingOrder + 2);
        // The table is painted into 01_House. Keep that plate behind the owner.
        var background = GameObject.Find("BackGround").GetComponent<SpriteRenderer>();
        background.sortingLayerID = SortingLayer.NameToID("BackGround"); background.sortingOrder = -100;
        HomeRoomLightingSetup.SortSwitchBehindOwner(story);
        var lighting = story.homeRoomLighting;
        if (lighting != null)
        {
            if (lighting.ownerReach != null)
            {
                lighting.ownerReach.sortingLayerID = owner.sortingLayerID;
                lighting.ownerReach.sortingOrder = owner.sortingOrder + 1;
                EditorUtility.SetDirty(lighting.ownerReach);
            }
            if (lighting.restingArmMasks != null)
                foreach (var mask in lighting.restingArmMasks)
                {
                    if (mask == null) continue;
                    mask.frontSortingLayerID = mask.backSortingLayerID = owner.sortingLayerID;
                    mask.frontSortingOrder = owner.sortingOrder; mask.backSortingOrder = owner.sortingOrder - 1;
                    EditorUtility.SetDirty(mask);
                }
        }
        foreach (var item in new UnityEngine.Object[] { hinge, hinge.leftLeaf, hinge.rightLeaf, owner, background }) EditorUtility.SetDirty(item);
    }

    private static void Capture(AfterTheGlowStoryDirector story, string prefix)
    {
        var camera = story.cameraFollow.GetComponent<Camera>();
        var hinge = story.parkGate.GetComponent<GateHingePresentation>();
        var owner = story.GetComponent<OwnerAnimationPresentation>().ownerRenderer;
        Vector3 cameraPosition = camera.transform.position, ownerPosition = story.owner.position, playerPosition = story.player.transform.position;
        float cameraSize = camera.orthographicSize, aspect = camera.aspect, progress = hinge.OpenProgress;
        var releasedField = typeof(GateHingePresentation).GetField("released", BindingFlags.Instance | BindingFlags.NonPublic);
        bool released = (bool)releasedField.GetValue(hinge), ownerActive = story.owner.gameObject.activeSelf;
        Sprite ownerSprite = owner.sprite;
        bool shade = story.homeRoomLighting.roomShade.enabled;
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = new RenderTexture(1200, 800, 24); var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = 1.5f; camera.orthographicSize = 2.5f;
            story.homeRoomLighting.roomShade.enabled = false;
            void Shot(string name, float centerX)
            {
                camera.transform.position = new Vector3(centerX, -1.8f, -10f); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); pixels.Apply();
                File.WriteAllBytes(Folder + prefix + "-" + name + ".png", pixels.EncodeToPNG());
            }
            story.owner.gameObject.SetActive(false);
            story.player.transform.position = new Vector3(hinge.leftLeaf.transform.position.x, story.streetEntryPlayerY, playerPosition.z);
            hinge.SetReleased(true);
            foreach (float open in new[] { 0f, .5f, 1f })
            {
                hinge.SetOpenProgress(open); Shot("left-gate-" + open.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), story.parkGate.transform.position.x);
            }
            story.player.transform.position = new Vector3(-4f, story.streetEntryPlayerY, playerPosition.z);
            story.owner.gameObject.SetActive(true); owner.sprite = story.ownerWalkSprite;
            story.owner.position = new Vector3(0f, story.streetEntryPlayerY, ownerPosition.z);
            Shot("owner-table", 0f);
        }
        finally
        {
            hinge.SetReleased(released); hinge.SetOpenProgress(progress);
            story.owner.position = ownerPosition; story.owner.gameObject.SetActive(ownerActive); owner.sprite = ownerSprite;
            story.player.transform.position = playerPosition; story.homeRoomLighting.roomShade.enabled = shade;
            camera.transform.position = cameraPosition; camera.orthographicSize = cameraSize; camera.aspect = aspect;
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
