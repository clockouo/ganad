using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Pipeline.Commands;

/// <summary>Connects the supplied street and park paintings in world space.</summary>
public static class FinalExteriorBlendSetup
{
    private const string MaterialPath = "Assets/Art/AfterTheGlow/Backgrounds/JourneyExteriorBlend.mat";
    private const string Output = "tmp/exterior-blend/";
    private static Transform Root => UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>().transform;
    private static SpriteRenderer Renderer(string name) => Root.Find(name).GetComponent<SpriteRenderer>();

    [CliCommand("exterior_blend", "Apply or preview the street to park background join", MainThreadRequired = true)]
    public static object Command([CliArg("action", "before, apply, preview, report")] string action = "report")
    {
        if (action == "before") return Capture("before");
        if (action == "preview") return Capture("after");
        if (action == "apply")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Directory.CreateDirectory(Output);
            if (!File.Exists(Output + "before-SampleScene.unity"))
                EditorSceneManager.SaveScene(Root.gameObject.scene, Output + "before-SampleScene.unity", true);
            ApplyToScene();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(Root.gameObject.scene);
            EditorSceneManager.SaveScene(Root.gameObject.scene);
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        return new {
            connected = Renderer("Connected Exterior Backdrop").enabled,
            separateStreet = Renderer("Street Night Backdrop").enabled,
            separatePark = Renderer("Park Map Backdrop").enabled,
            streetTexture = material == null ? "" : AssetDatabase.GetAssetPath(material.GetTexture("_StreetTex")),
            parkTexture = material == null ? "" : AssetDatabase.GetAssetPath(material.GetTexture("_ParkTex")),
            shaderErrors = material == null ? new string[0] : ShaderUtil.GetShaderMessages(material.shader)
                .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m => m.message).ToArray()
        };
    }

    public static void ApplyToScene()
    {
        var street = Renderer("Street Night Backdrop");
        var park = Renderer("Park Map Backdrop");
        if (street.sprite == null || park.sprite == null) throw new MissingReferenceException("Street and park artwork required.");
        var shader = Shader.Find("AfterTheGlow/JourneyExteriorBlend");
        if (shader == null) throw new MissingReferenceException("JourneyExteriorBlend shader has not imported.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        material.shader = shader;
        float height = street.sprite.bounds.size.y * Mathf.Abs(street.transform.lossyScale.y);
        float parkHeight = park.sprite.bounds.size.y * Mathf.Abs(park.transform.lossyScale.y);
        float width = street.sprite.bounds.size.x * Mathf.Abs(street.transform.lossyScale.x);
        float left = street.bounds.min.x, seam = park.bounds.min.x;
        // Measured first road rows in the original, unmodified 724px paintings.
        // Align their curb bottoms rather than their differently sized canvases.
        float roadY = street.transform.position.y + (.5f - 554f / 724f) * height;
        float parkCenterY = roadY - (.5f - 548f / 724f) * parkHeight;
        float shift = parkCenterY - park.transform.position.y;
        park.transform.position += Vector3.up * shift;
        var foreground = Root.Find("Final Park Front Walls");
        if (foreground != null) foreground.position += Vector3.up * shift;
        material.SetTexture("_StreetTex", street.sprite.texture);
        material.SetTexture("_ParkTex", park.sprite.texture);
        material.SetVector("_Layout", new Vector4(left, seam, width, street.transform.position.y));
        material.SetVector("_Heights", new Vector4(height, parkCenterY, parkHeight, .65f));
        material.SetVector("_Sky", new Vector4(1f - 111f / 724f, .999f, 0, 0));
        var connected = Renderer("Connected Exterior Backdrop");
        connected.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/CoreTestbed/CoreTestbedShape.png");
        connected.sharedMaterial = material; connected.color = Color.white;
        connected.sortingLayerID = street.sortingLayerID; connected.sortingOrder = street.sortingOrder;
        float end = park.bounds.max.x;
        connected.transform.position = new Vector3((left + end) * .5f, -1f, 1f);
        connected.transform.localScale = new Vector3((end - left) / connected.sprite.bounds.size.x,
            20f / connected.sprite.bounds.size.y, 1f);
        connected.enabled = true; street.enabled = park.enabled = false;
        foreach (var changed in new UnityEngine.Object[] { material, connected, street, park }) EditorUtility.SetDirty(changed);
    }

    private static string[] Capture(string prefix)
    {
        Directory.CreateDirectory(Output);
        var follow = Root.GetComponent<AfterTheGlowStoryDirector>().cameraFollow;
        var camera = follow.GetComponent<Camera>();
        Vector3 position = camera.transform.position;
        float size = camera.orthographicSize, aspect = camera.aspect;
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = new RenderTexture(1600, 900, 24);
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        string[] paths = new string[4];
        try
        {
            camera.targetTexture = target; camera.aspect = 16f / 9f;
            float[] centers = { 15.6f, 24.6f, 33.6f, 24.6f };
            string[] views = { "street", "join", "park", "panorama" };
            for (int i = 0; i < centers.Length; i++)
            {
                camera.orthographicSize = i == 3 ? 10.125f : follow.homeOrthographicSize;
                camera.transform.position = new Vector3(centers[i], follow.exteriorLockedY, -10);
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                paths[i] = Path.GetFullPath(Output + prefix + "-" + views[i] + ".png");
                File.WriteAllBytes(paths[i], pixels.EncodeToPNG());
            }
        }
        finally
        {
            camera.transform.position = position; camera.orthographicSize = size; camera.aspect = aspect;
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
        return paths;
    }
}
