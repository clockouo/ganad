using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PreviewUserDogSheet
{
    public static string Main()
    {
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        var renderer = player.transform.Find("PixelSnapper Dog Visual").GetComponent<SpriteRenderer>();
        var camera = story.cameraFollow.GetComponent<Camera>();
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var oldPosition = camera.transform.position;
        var oldSprite = renderer.sprite;
        float oldSize = camera.orthographicSize, oldAspect = camera.aspect;
        var target = new RenderTexture(1600, 900, 24);
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/AfterTheGlow/Characters/Dog_User_Actions_5Rows.png")
            .OfType<Sprite>().OrderBy(s => s.name).ToArray();
        const string folder = "tmp/feedback-v2.37/";
        try
        {
            camera.targetTexture = target;
            camera.aspect = 16f / 9f;
            camera.orthographicSize = 3f;
            camera.transform.position = new Vector3(player.transform.position.x + 2f, story.cameraFollow.lockedY, -10f);
            foreach (int index in new[] { 0, 2, 7, 13, 20 })
            {
                renderer.sprite = sprites[index];
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(folder + "pose-" + (index + 1).ToString("00") + ".png", pixels.EncodeToPNG());
            }
            var animator = player.GetComponent<Animator>();
            File.WriteAllLines(folder + "animation-bindings.txt", animator.runtimeAnimatorController.animationClips.Select(clip =>
                clip.name + ": " + string.Join(",", AnimationUtility.GetObjectReferenceCurveBindings(clip).SelectMany(binding =>
                    AnimationUtility.GetObjectReferenceCurve(clip, binding)).Select(frame =>
                        (frame.value != null ? AssetDatabase.GetAssetPath(frame.value) + ":" + frame.value.name : "NULL")))));
        }
        finally
        {
            renderer.sprite = oldSprite;
            camera.transform.position = oldPosition;
            camera.orthographicSize = oldSize;
            camera.aspect = oldAspect;
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(pixels);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
        return "Saved static scene previews and animation asset bindings to " + folder;
    }
}
