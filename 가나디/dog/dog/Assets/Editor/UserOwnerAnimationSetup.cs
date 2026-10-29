using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Native importer preparation for the user's unmodified owner sprite strips.</summary>
public static class UserOwnerAnimationSetup
{
    public const string Folder = "Assets/Art/AfterTheGlow/Characters/Owner/";
    public const string PetPath = Folder + "Owner_User_Pet_5Frames.png";
    public const string SleepPath = Folder + "Owner_User_Sleep_6Frames.png";
    public const string WalkPath = Folder + "Owner_User_Walk_12Frames.png";
    public static bool HasSourceSheets => File.Exists(PetPath) && File.Exists(SleepPath) && File.Exists(WalkPath);

    public static string ApplyAndSave()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before saving owner artwork.");
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story == null) throw new MissingReferenceException("Open SampleScene first.");
        const string folder = "tmp/owner-user-v2.40/";
        Directory.CreateDirectory(folder);
        if (!File.Exists(folder + "before-active.unity")) EditorSceneManager.SaveScene(story.gameObject.scene, folder + "before-active.unity", true);
        OwnerPresentationSetup.ApplyIfAvailable(story.transform);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
        EditorSceneManager.SaveScene(story.gameObject.scene);
        var animation = story.GetComponent<OwnerAnimationPresentation>();
        File.WriteAllLines(folder + "applied.txt", new[] {
            "Pet=" + animation.petFrames.Length + "; Sleep=" + animation.sleepFrames.Length + "; Walk=" + animation.walkFrames.Length,
            "Pet bends once then loops final hand positions; walking loops only while moving; sleep is visible until waking.",
            "Standing and pet source canvas height=216; floor pivot=0; actor height=2.05; source RGBA files preserved.",
            "Sleeping sheet aligns with separated duvet bounds; old duvet returns after waking. Dedicated frontal rhythm portrait is preserved.",
            "Scene and source arrays saved; static authoring previews only, not a playtest."
        });
        CapturePoses(story, animation, folder);
        EditorSceneManager.SaveScene(story.gameObject.scene);
        return "Applied owner Pet 5 / Sleep 6 / Walk 12 and saved scene. Static previews: " + folder;
    }

    private static void CapturePoses(AfterTheGlowStoryDirector story, OwnerAnimationPresentation animation, string folder)
    {
        var camera = story.cameraFollow.GetComponent<Camera>();
        var renderer = animation.ownerRenderer;
        Vector3 ownerPosition = story.owner.position, ownerScale = story.owner.localScale;
        Vector3 playerPosition = story.player.transform.position, cameraPosition = camera.transform.position;
        Sprite oldSprite = renderer.sprite; bool oldFlip = renderer.flipX, oldActive = story.owner.gameObject.activeSelf;
        bool sleeping = animation.sleepingRenderer.enabled, duvet = animation.bedDuvetRenderer.enabled;
        var oldTarget = camera.targetTexture; var oldRender = RenderTexture.active;
        float size = camera.orthographicSize, aspect = camera.aspect;
        var target = new RenderTexture(1600, 900, 24); var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = 16f / 9f; camera.orthographicSize = 2f;
            void Shot(string name, float centerX)
            {
                float half = camera.orthographicSize * camera.aspect;
                camera.transform.position = new Vector3(Mathf.Clamp(centerX, story.cameraFollow.mapMinX + half,
                    story.cameraFollow.homeMapMaxX - half), story.cameraFollow.lockedY, -10f);
                Physics2D.SyncTransforms(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes(folder + name + ".png", pixels.EncodeToPNG());
            }
            story.owner.gameObject.SetActive(false); animation.sleepingRenderer.enabled = true; animation.bedDuvetRenderer.enabled = false;
            Shot("sleeping", story.bed.transform.position.x);
            animation.sleepingRenderer.enabled = false; animation.bedDuvetRenderer.enabled = true;
            story.owner.gameObject.SetActive(true); story.owner.localScale = Vector3.one;
            story.owner.position = new Vector3(story.OwnerDoorStandX, -3.86f, ownerPosition.z);
            renderer.sprite = animation.petFrames[4]; renderer.flipX = true;
            story.player.transform.position = new Vector3(story.OwnerPetPlayerTargetX, -3.86f, playerPosition.z);
            Shot("petting", (story.owner.position.x + story.player.transform.position.x) * .5f);
            renderer.sprite = animation.walkFrames[3]; renderer.flipX = false;
            story.owner.position = new Vector3(story.OwnerDoorStandX - 2f, -3.86f, ownerPosition.z);
            Shot("walking", story.owner.position.x);
        }
        finally
        {
            story.owner.position = ownerPosition; story.owner.localScale = ownerScale; story.owner.gameObject.SetActive(oldActive);
            story.player.transform.position = playerPosition; renderer.sprite = oldSprite; renderer.flipX = oldFlip;
            animation.sleepingRenderer.enabled = sleeping; animation.bedDuvetRenderer.enabled = duvet;
            camera.transform.position = cameraPosition; camera.orthographicSize = size; camera.aspect = aspect;
            camera.targetTexture = oldTarget; RenderTexture.active = oldRender;
            Physics2D.SyncTransforms(); UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    public static void EnsureSprites()
    {
        Slice(PetPath, "Owner_User_Pet", 5, 216);
        Slice(SleepPath, "Owner_User_Sleep", 6, 68);
        Slice(WalkPath, "Owner_User_Walk", 12, 216);
    }

    private static void Slice(string path, string prefix, int count, int height)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (importer == null) throw new InvalidOperationException("Could not import " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.maxTextureSize = 4096;
        importer.filterMode = FilterMode.Point;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        importer = AssetImporter.GetAtPath(path) as TextureImporter;
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("No Sprite Editor provider for " + path);
        provider.InitSpriteEditorDataProvider();
        if (!provider.HasDataProvider(typeof(ISpriteFrameEditCapability)) ||
            !provider.HasDataProvider(typeof(ISpriteNameFileIdDataProvider)))
            throw new InvalidOperationException("Missing owner sprite edit providers.");
        var editing = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (editing == null) throw new InvalidOperationException("Owner sprite capabilities unavailable.");
        var cap = editing.GetEditCapability();
        if (!cap.HasCapability(EEditCapability.CreateAndDeleteSprite) || !cap.HasCapability(EEditCapability.EditSpriteName) ||
            !cap.HasCapability(EEditCapability.EditSpriteRect) || !cap.HasCapability(EEditCapability.EditPivot))
            throw new InvalidOperationException("Owner sprite creation, naming, slicing or pivots not supported.");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null || texture.width != count * 256 || texture.height != 256)
            throw new InvalidDataException("Unexpected owner sheet dimensions: " + path);
        var previous = provider.GetSpriteRects();
        var rects = new SpriteRect[count];
        var pairs = new SpriteNameFileIdPair[count];
        for (int i = 0; i < count; i++)
        {
            string name = prefix + "_" + (i + 1).ToString("00");
            var old = previous.FirstOrDefault(p => p.name == name);
            GUID id = old != null ? old.spriteID : GUID.Generate();
            rects[i] = new SpriteRect { name = name, spriteID = id, rect = new Rect(i * 256, 20, 256, height),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(.5f, 0f), border = Vector4.zero };
            pairs[i] = new SpriteNameFileIdPair(name, id);
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
        provider.Apply(); importer.SaveAndReimport();
    }

    public static void Apply(Transform root)
    {
        EnsureSprites();
        var story = root.GetComponent<AfterTheGlowStoryDirector>();
        if (story == null || story.owner == null) throw new MissingReferenceException("Owner story actor missing.");
        Sprite[] Frames(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        var walk = Frames(WalkPath); var pet = Frames(PetPath); var sleep = Frames(SleepPath);
        story.ownerWalkSprite = walk[0]; story.ownerPetSprite = pet[pet.Length - 1];
        story.ownerStandingHeight = story.ownerPetHeight = 2.05f;
        story.ownerVisualGroundOffset = -.24f;
        story.alignOwnerPetWithPlayer = true;
        story.ownerPetHandNormalized = new Vector2(.8f, .22f);
        var visual = story.owner.GetComponentInChildren<SpriteRenderer>(true);
        visual.sprite = walk[0]; visual.flipX = false; visual.sortingOrder = 30;
        visual.transform.localPosition = new Vector3(0, story.ownerVisualGroundOffset, -.2f);
        float scale = story.ownerStandingHeight / walk[0].bounds.size.y;
        Vector3 parent = visual.transform.parent.lossyScale;
        visual.transform.localScale = new Vector3(scale / Mathf.Abs(parent.x), scale / Mathf.Abs(parent.y), 1f);

        var bedRoot = story.bed.transform;
        var duvet = bedRoot.Find("Separated Bed Duvet")?.GetComponent<SpriteRenderer>();
        if (duvet == null) throw new MissingReferenceException("Separated bed duvet is required for the sleeping owner.");
        var item = bedRoot.Find("Owner Sleeping Animation");
        if (item == null) { var go = new GameObject("Owner Sleeping Animation", typeof(SpriteRenderer)); go.transform.SetParent(bedRoot, false); item = go.transform; }
        var sleeping = item.GetComponent<SpriteRenderer>();
        Bounds mattress = VisibleBounds(duvet);
        sleeping.sprite = sleep[0]; sleeping.sharedMaterial = duvet.sharedMaterial;
        sleeping.color = Color.white; sleeping.sortingLayerID = duvet.sortingLayerID; sleeping.sortingOrder = duvet.sortingOrder + 1;
        // The three supplied sheets use the same pixel scale. Preserve the owner's
        // head/body size instead of enlarging the sleeping frame to fill the whole bed.
        float sleepScale = scale;
        item.position = new Vector3(mattress.center.x, mattress.min.y, duvet.transform.position.z - .01f);
        Vector3 bedScale = bedRoot.lossyScale;
        item.localScale = new Vector3(sleepScale / Mathf.Abs(bedScale.x), sleepScale / Mathf.Abs(bedScale.y), 1f);
        sleeping.enabled = true; duvet.enabled = false;
        story.bed.ConfigureVisualInteraction(item);
        var animation = root.GetComponent<OwnerAnimationPresentation>();
        if (animation == null) animation = Undo.AddComponent<OwnerAnimationPresentation>(root.gameObject);
        animation.director = story; animation.ownerRenderer = visual; animation.sleepingRenderer = sleeping;
        animation.bedDuvetRenderer = duvet; animation.walkFrames = walk; animation.petFrames = pet; animation.sleepFrames = sleep;
        foreach (UnityEngine.Object changed in new UnityEngine.Object[] { story, visual, sleeping, duvet, story.bed, animation }) EditorUtility.SetDirty(changed);
    }

    private static Bounds VisibleBounds(SpriteRenderer renderer)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(AssetDatabase.GetAssetPath(renderer.sprite.texture))))
                throw new InvalidDataException("Could not inspect bed artwork bounds.");
            var pixels = texture.GetPixels32();
            Rect rect = renderer.sprite.rect;
            int minX = (int)rect.xMax, minY = (int)rect.yMax, maxX = -1, maxY = -1;
            for (int y = (int)rect.yMin; y < rect.yMax; y++)
            for (int x = (int)rect.xMin; x < rect.xMax; x++)
                if (pixels[y * texture.width + x].a > 20) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            if (maxX < minX) throw new InvalidDataException("Bed artwork has no visible pixels.");
            Vector2 pivot = renderer.sprite.pivot; float ppu = renderer.sprite.pixelsPerUnit;
            Vector3 a = renderer.transform.TransformPoint(new Vector3((minX - rect.xMin - pivot.x) / ppu, (minY - rect.yMin - pivot.y) / ppu, 0));
            Vector3 b = renderer.transform.TransformPoint(new Vector3((maxX + 1 - rect.xMin - pivot.x) / ppu, (maxY + 1 - rect.yMin - pivot.y) / ppu, 0));
            var bounds = new Bounds(); bounds.SetMinMax(Vector3.Min(a, b), Vector3.Max(a, b)); return bounds;
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
}
