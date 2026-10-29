using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AfterTheGlowBackdropChecks
{
    [MenuItem("Tools/After the Glow/Verify Background Padding")]
    private static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before checking static background framing.");
        CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        Camera camera = follow.GetComponent<Camera>();
        Vector3 oldPosition = camera.transform.position;
        float oldSize = camera.orthographicSize;
        float oldAspect = camera.aspect;
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        var lines = new List<string>();
        bool passed = true;
        Action<bool, string> check = (condition, description) => {
            passed &= condition;
            lines.Add((condition ? "PASS " : "FAIL ") + description);
        };
        try
        {
            check(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset,
                "URP pipeline detected");
            foreach (string name in new[] { "Backdrop Sky Fill", "Backdrop Floor Fill", "Home Ceiling Fill" })
            {
                GameObject item = GameObject.Find("AfterTheGlowStoryDemo/" + name);
                check(item != null && item.GetComponent<SpriteRenderer>().enabled && item.GetComponents<Collider2D>().Length == 0,
                    name + " renders without gameplay colliders");
            }
            Vector3[] positions = {
                new Vector3(-3.6f, follow.lockedY, -10f),
                new Vector3(11.4f, follow.exteriorLockedY, -10f),
                new Vector3(11.4f, follow.exteriorLockedY, -10f)
            };
            float[] sizes = { follow.homeOrthographicSize, follow.exteriorOrthographicSize, 6.2f };
            string[] names = { "home", "street", "street-zoom-out" };
            for (int i = 0; i < positions.Length; i++)
            {
                RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
                Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                try
                {
                    camera.transform.position = positions[i];
                    camera.orthographicSize = sizes[i];
                    camera.aspect = 16f / 9f;
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
                    image.Apply();
                    int bluePixels = 0;
                    foreach (Color32 pixel in image.GetPixels32())
                    {
                        if (Mathf.Abs(pixel.r - 49) <= 1 && Mathf.Abs(pixel.g - 77) <= 1 && Mathf.Abs(pixel.b - 121) <= 1)
                            bluePixels++;
                    }
                    check(bluePixels == 0, names[i] + " has no old blue backdrop pixels: " + bluePixels);
                    File.WriteAllBytes(Path.Combine(Application.dataPath, "Temp/background-" + names[i] + "-2026-10-15.png"), image.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = oldTarget;
                    RenderTexture.active = oldActive;
                    UnityEngine.Object.DestroyImmediate(image);
                    RenderTexture.ReleaseTemporary(target);
                }
            }
        }
        finally
        {
            camera.transform.position = oldPosition;
            camera.orthographicSize = oldSize;
            camera.aspect = oldAspect;
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            lines.Add("Result: " + (passed ? "PASS" : "FAIL"));
            File.WriteAllLines(Path.Combine(Application.dataPath, "../AgentScripts/background-padding-verification-2026-10-15.txt"), lines);
        }
    }
}
