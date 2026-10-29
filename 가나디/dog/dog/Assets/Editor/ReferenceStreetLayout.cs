using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Places the user's original illustration without repainting its car, posts or scenery.</summary>
public static class ReferenceStreetLayout
{
    private const string SourcePath = "Assets/Art/AfterTheGlow/References/Map02_Original.png";
    private const string ParkSourcePath = "Assets/Art/AfterTheGlow/Backgrounds/Map03_Park.png";
    private const string SpriteFolder = "Assets/Art/AfterTheGlow/Backgrounds/";
    private const int HeaderRows = 108;
    private const float Left = AfterTheGlowWorldLayout.ExteriorLeftX;
    private const float Width = AfterTheGlowWorldLayout.IllustrationWidth;
    private const float Scale = AfterTheGlowWorldLayout.ExteriorScale;
    private const float Floor = -3.86f;
    private const float Pixel = Width / 2172f;
    private const float End = Left + Width;
    private const float ParkWidth = Width;
    private const float ParkEnd = End + ParkWidth;
    private static Vector3 FromPixel(float x, float y, float z = -.2f)
        => new Vector3(Left + x * Pixel, Floor + (541f - y) * Pixel, z);

    [MenuItem("Tools/After the Glow/Apply Original Street Illustration")]
    private static void ApplyAndSave()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before changing the layout.");
        var root = GameObject.Find("AfterTheGlowStoryDemo");
        if (root == null) throw new MissingReferenceException("Open SampleScene first.");
        Directory.CreateDirectory("tmp/feedback-v2.34");
        if (!File.Exists("tmp/feedback-v2.34/before-active.unity"))
            EditorSceneManager.SaveScene(root.scene, "tmp/feedback-v2.34/before-active.unity", true);
        Apply(root.transform);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
    }

    public static void Apply(Transform root)
    {
        if (!File.Exists(SourcePath)) return;
        var importer = (TextureImporter)AssetImporter.GetAtPath(SourcePath);
        if (importer == null) { AssetDatabase.ImportAsset(SourcePath); importer = (TextureImporter)AssetImporter.GetAtPath(SourcePath); }
        if (importer.textureType != TextureImporterType.Sprite || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled || importer.maxTextureSize < 4096 || importer.filterMode != FilterMode.Point)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SourcePath);
        if (sprite == null) throw new MissingReferenceException("Original street sprite could not be imported.");
        var story = root.GetComponent<AfterTheGlowStoryDirector>();
        var street = root.Find("Street Night Backdrop").GetComponent<SpriteRenderer>();
        Undo.RecordObject(street, "Use original street illustration");
        street.sprite = PlayableSprite(sprite, "Map02_Playable");
        street.color = Color.white;
        street.enabled = true;
        street.transform.position = new Vector3(Left + Width / 2, Floor + (541f - 362f) * Pixel, 1f);
        street.transform.localScale = new Vector3(Width / sprite.bounds.size.x, Width / sprite.bounds.size.x, 1f);
        EditorUtility.SetDirty(street);

        // The following park starts at the original's right edge, never over the entrance posts.
        var park = root.Find("Park Map Backdrop");
        Undo.RecordObject(park, "Join park after original street");
        // Match the road/sidewalk seam: source 02 row 555 joins source 03 row 544.
        float roadY = Floor + (541f - 555f) * Pixel;
        park.position = new Vector3(End + ParkWidth * .5f, roadY + (544f - 362f) * Pixel, park.position.z);
        var parkRenderer = park.GetComponent<SpriteRenderer>();
        var parkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ParkSourcePath);
        parkRenderer.sprite = PlayableSprite(parkSprite, "Map03_Playable");
        park.localScale = new Vector3(ParkWidth / parkSprite.bounds.size.x, (ParkWidth / 3f) / parkSprite.bounds.size.y, 1f);
        StitchExterior(root, street, parkRenderer, sprite, parkSprite);
        story.parkEntryX = AfterTheGlowWorldLayout.ParkEntryX;
        story.streetEntryPlayerX = Left + 225f * Pixel;
        story.ownerStreetStartX = story.streetEntryPlayerX + .8f;
        story.ownerStreetEndX = Left + (22.5f - Left) * Scale;
        var entry = root.Find("Park Entry Trigger");
        if (entry != null)
        {
            entry.position = new Vector3(story.parkEntryX, entry.position.y, entry.position.z);
            entry.localScale = new Vector3(Scale, 1f, 1f);
        }
        story.cameraFollow.mapMaxX = ParkEnd;
        story.cameraFollow.exteriorMapMinX = Left;
        story.cameraFollow.matchExteriorSizeToHome = true;
        story.cameraFollow.exteriorOrthographicSize = story.cameraFollow.homeOrthographicSize;
        var fountain = root.GetComponent<ParkFountainExploration>();
        if (fountain != null)
        {
            fountain.fountainWorldX = AfterTheGlowWorldLayout.FountainX;
            fountain.entryWorldX = AfterTheGlowWorldLayout.FountainEntryX;
            fountain.exitWorldX = AfterTheGlowWorldLayout.FountainExitX;
            fountain.approachDistance = 1f;
            EditorUtility.SetDirty(fountain);
        }
        // Legacy exterior door art is separate from the door painted in the original map.
        var oldDoor = root.Find("StoryDoor_Open_State")?.GetComponent<SpriteRenderer>();
        if (oldDoor != null) oldDoor.enabled = false;

        // Use the car already painted in the source. Only its bulbs and cones change at runtime.
        var car = root.Find("PixelSnapper Car No Lights");
        var presentation = car.GetComponent<CarHeadlightPresentation>();
        Undo.RecordObject(presentation, "Use original background car");
        presentation.useBackdropCar = true;
        presentation.backdropLampOffMasks = new SpriteRenderer[2];
        var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/CoreTestbed/CoreTestbedShape.png");
        float[] lamps = { 841.5f, 1022.5f };
        for (int i = 0; i < lamps.Length; i++)
        {
            string name = "Original Car Lamp Off " + i;
            var item = root.Find(name);
            if (item == null) { var go = new GameObject(name); go.transform.SetParent(root, false); item = go.transform; }
            var mask = item.GetComponent<SpriteRenderer>();
            if (mask == null) mask = Undo.AddComponent<SpriteRenderer>(item.gameObject);
            mask.sprite = square;
            mask.sharedMaterial = street.sharedMaterial;
            mask.color = new Color32(46, 72, 75, 255);
            mask.sortingLayerID = street.sortingLayerID;
            mask.sortingOrder = 9;
            item.position = FromPixel(lamps[i], 444.5f);
            item.localScale = new Vector3(30f * Pixel / square.bounds.size.x, 29f * Pixel / square.bounds.size.y, 1f);
            presentation.backdropLampOffMasks[i] = mask;
        }
        var light = presentation.lightRoot.transform;
        light.position = FromPixel(932f, 444.5f, -.1f);
        light.localScale = new Vector3((90.5f * Pixel) / 1.55f, Scale, 1f);
        presentation.Refresh();
        EditorUtility.SetDirty(presentation);

        // Original posts remain in the background; only the two moving leaves are drawn over it.
        var gate = story.parkGate.transform;
        float leftHinge = Left + 1582f * Pixel, rightHinge = Left + 1899f * Pixel;
        gate.position = new Vector3((leftHinge + rightHinge) * .5f, Floor, -.1f);
        gate.localScale = new Vector3(Scale, Scale, 1f);
        var view = gate.GetComponent<GateHingePresentation>();
        if (view != null)
        {
            // Gate sprites and colliders keep their reference local dimensions; the root
            // shrinks both rendering and physics once, at the same ratio as the backdrop.
            float span = (rightHinge - leftHinge) / Scale;
            float leafWidth = span * .5f;
            float boundScale = leafWidth / view.boundLeftSprite.bounds.size.x;
            view.closedScale = new Vector3(boundScale, 9f, 1f);
            view.releasedLeftScale.x = leafWidth / view.releasedLeftSprite.bounds.size.x;
            view.releasedRightScale.x = leafWidth / view.releasedRightSprite.bounds.size.x;
            view.openedLeftScale.x = leafWidth * view.openedWidthFraction / view.openedLeftSprite.bounds.size.x;
            view.openedRightScale.x = leafWidth * view.openedWidthFraction / view.openedRightSprite.bounds.size.x;
            view.leftLeaf.transform.localPosition = new Vector3(-span * .5f, 0f, -.2f);
            view.rightLeaf.transform.localPosition = new Vector3(span * .5f, 0f, -.2f);
            view.ResetPose();
            story.gateOpenSprite = view.openedLeftSprite;
            foreach (string postName in new[] { "Fixed Left Gate Post", "Fixed Right Gate Post" })
            {
                var post = gate.Find(postName)?.GetComponent<SpriteRenderer>();
                if (post != null) post.enabled = false;
            }
            var collider = gate.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(span, collider.size.y);
            collider.offset = new Vector2(0f, collider.offset.y);
            GateLeashSetup.Apply(story);
            EditorUtility.SetDirty(view);
        }

        var floor = root.Find("Street Ground Extension");
        if (floor != null)
        {
            float start = 4.95f;
            floor.position = new Vector3((start + ParkEnd) * .5f, floor.position.y, floor.position.z);
            floor.localScale = new Vector3(ParkEnd - start, floor.localScale.y, floor.localScale.z);
            var collider = floor.GetComponent<BoxCollider2D>();
            if (collider != null) collider.size = Vector2.one;
        }
        // Padding belongs to the visible camera band, not a 64-unit tall wall.
        BoundLegacyPadding(root);
        // The continuous street floor now reaches through the park. Its old overlapping
        // placeholder floor has a vertical edge at X=42 that catches the dog's feet.
        var oldParkGround = root.Find("Park Placeholder Ground")?.GetComponent<Collider2D>();
        if (oldParkGround != null)
        {
            oldParkGround.enabled = false;
            EditorUtility.SetDirty(oldParkGround);
        }
        EditorUtility.SetDirty(story);
        EditorUtility.SetDirty(story.cameraFollow);
    }

    // These are separate Sprite assets over the unchanged source textures. Keep the original
    // pivot so omitting the title strip cannot move the road, car lamps or entrance posts.
    private static Sprite PlayableSprite(Sprite source, string name)
    {
        string path = SpriteFolder + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        float height = source.texture.height - HeaderRows;
        var rect = new Rect(0f, 0f, source.texture.width, height);
        Vector2 pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
        var result = Sprite.Create(source.texture, rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        result.name = name;
        AssetDatabase.CreateAsset(result, path);
        return result;
    }

    public static void BoundLegacyPadding(Transform root)
    {
        foreach (string name in new[] { "Backdrop Sky Fill", "Backdrop Floor Fill" })
        {
            var fill = root.Find(name);
            if (fill == null) continue;
            bool sky = name.Contains("Sky");
            fill.position = new Vector3(15.4f, sky ? -1f : -4.9f, 20f);
            fill.localScale = new Vector3(54.4f, sky ? 6f : 1.8f, 1f);
        }
    }

    private static void StitchExterior(Transform root, SpriteRenderer street, SpriteRenderer park,
        Sprite streetSource, Sprite parkSource)
    {
        foreach (string name in new[] { "Street Sky Continuation", "Park Sky Continuation" })
        {
            var obsolete = root.Find(name);
            if (obsolete != null) Undo.DestroyObjectImmediate(obsolete.gameObject);
        }
        const string path = SpriteFolder + "ExteriorStitch.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("AfterTheGlow/ExteriorStitch"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_StreetTex", streetSource.texture);
        material.SetTexture("_ParkTex", parkSource.texture);
        material.SetVector("_Layout", new Vector4(Left, End, Width, street.transform.position.y));
        material.SetVector("_Heights", new Vector4(streetSource.bounds.size.y * street.transform.lossyScale.y,
            park.transform.position.y, parkSource.bounds.size.y * park.transform.lossyScale.y,
            // The title strip has an uneven antialiased lower edge through row 109.
            // Sample clean sky below it so it cannot become a long horizontal band.
            (streetSource.texture.height - 120f - .5f) / streetSource.texture.height));
        EditorUtility.SetDirty(material);
        var item = root.Find("Connected Exterior Backdrop");
        if (item == null) { var go = new GameObject("Connected Exterior Backdrop"); go.transform.SetParent(root, false); item = go.transform; }
        var renderer = item.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = item.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/CoreTestbed/CoreTestbedShape.png");
        renderer.sharedMaterial = material;
        renderer.color = Color.white;
        renderer.sortingLayerID = street.sortingLayerID;
        renderer.sortingOrder = street.sortingOrder;
        renderer.enabled = true;
        // Covers exactly the exterior and the fixed gameplay camera's vertical band.
        item.position = new Vector3((Left + ParkEnd) * .5f, -1.85f, 1f);
        item.localScale = new Vector3(ParkEnd - Left, 7.7f, 1f);
        street.enabled = false;
        park.enabled = false;
        EditorUtility.SetDirty(renderer);
    }

    [MenuItem("Tools/After the Glow/Preview Original Street Layout")]
    private static void Preview()
    {
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        var camera = story.cameraFollow.GetComponent<Camera>();
        Vector3 oldPosition = camera.transform.position;
        float oldSize = camera.orthographicSize;
        var oldTarget = camera.targetTexture;
        float oldAspect = camera.aspect;
        var oldActive = RenderTexture.active;
        var target = new RenderTexture(1600, 900, 24);
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        string folder = Path.GetFullPath("tmp/reference-street-20261021");
        Directory.CreateDirectory(folder);
        try
        {
            camera.targetTexture = target;
            camera.aspect = 1600f / 900f;
            foreach (var sample in new[] {
                (name: "layout", x: Left + Width * .5f, size: Width * 9f / 32f),
                (name: "entrance", x: Left + 1740.5f * Pixel, size: 3f),
                (name: "headlight", x: Left + 932f * Pixel, size: 1.5f) })
            {
                camera.orthographicSize = sample.size;
                float y = sample.name == "headlight" ? -2.7f :
                    sample.size > 4f ? Floor + (541f - 362f) * Pixel : story.cameraFollow.exteriorLockedY;
                camera.transform.position = new Vector3(sample.x, y, oldPosition.z);
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, sample.name + ".png"), pixels.EncodeToPNG());
            }
        }
        finally
        {
            camera.transform.position = oldPosition; camera.orthographicSize = oldSize;
            camera.aspect = oldAspect;
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    [MenuItem("Tools/After the Glow/Frame Connected Exterior")]
    private static void FrameConnectedExterior()
    {
        var renderer = GameObject.Find("AfterTheGlowStoryDemo/Connected Exterior Backdrop").GetComponent<SpriteRenderer>();
        var scene = SceneView.lastActiveSceneView;
        if (scene == null) scene = EditorWindow.GetWindow<SceneView>();
        scene.in2DMode = true;
        scene.Frame(renderer.bounds, true);
        scene.Repaint();
    }
}
