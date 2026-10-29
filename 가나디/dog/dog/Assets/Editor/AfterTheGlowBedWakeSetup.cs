using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies the separated bed layers and the direct-touch wake screen to the
/// existing story scene without rebuilding its other set dressing.
/// </summary>
public static class AfterTheGlowBedWakeSetup
{
    private const string BackgroundPath = "Assets/Art/AfterTheGlow/Backgrounds/Map01_Home.png";
    private const string BedFramePath = "Assets/Art/AfterTheGlow/Props/Bed_Frame_Separated.png";
    private const string BedDuvetPath = "Assets/Art/AfterTheGlow/Props/Bed_Duvet_Separated.png";
    private const string WakeCloseupPath = "Assets/Art/AfterTheGlow/Puzzles/Puzzle01/OwnerWakeCloseup_Reference.png";
    private const string CirclePath = "Assets/Art/Generated/Phase1Circle.png";
    private const string TouchRingPath = "Assets/Art/AfterTheGlow/Puzzles/Puzzle01/TouchRing.png";

    [MenuItem("Tools/After the Glow/Refresh Bed and Wake Puzzle")]
    private static void RefreshBedAndWakePuzzle()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Open the story scene before refreshing the wake puzzle.");

        ApplyToScene(scene);
    }

    public static void ApplyToScene(Scene scene)
    {
        EnsureTouchRingAsset();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Sprite backgroundSprite = LoadSprite(BackgroundPath);
        Sprite frameSprite = LoadSprite(BedFramePath);
        Sprite duvetSprite = LoadSprite(BedDuvetPath);
        Sprite wakeSprite = LoadSprite(WakeCloseupPath);
        Sprite circleSprite = LoadSprite(CirclePath);
        Sprite touchRingSprite = LoadSprite(TouchRingPath);

        GameObject backgroundObject = FindRoot(scene, "BackGround");
        GameObject storyObject = FindRootWithDirector(scene);
        if (backgroundObject == null || storyObject == null)
            throw new InvalidOperationException("The active scene must contain BackGround and AfterTheGlowStoryDirector.");

        SpriteRenderer backgroundRenderer = backgroundObject.GetComponent<SpriteRenderer>();
        AfterTheGlowStoryDirector director = storyObject.GetComponent<AfterTheGlowStoryDirector>();
        Transform bedRoot = storyObject.transform.Find("Bed Push Target");
        if (backgroundRenderer == null || director == null || bedRoot == null)
            throw new InvalidOperationException("The story background, director, or bed interaction object is missing.");

        Undo.RecordObject(backgroundRenderer, "Set bed-free home background");
        backgroundRenderer.sprite = backgroundSprite;
        backgroundRenderer.enabled = true;
        EditorUtility.SetDirty(backgroundRenderer);

        PushableObject bedInteraction = bedRoot.GetComponent<PushableObject>();
        if (bedInteraction == null)
            throw new InvalidOperationException("Bed Push Target needs its PushableObject component.");
        Undo.RecordObject(bedInteraction, "Allow walking in front of bed");
        bedInteraction.ConfigureInteractionOnlyTrigger(true);
        EditorUtility.SetDirty(bedInteraction);

        for (int i = bedRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = bedRoot.GetChild(i);
            if (child.GetComponentInChildren<SpriteRenderer>(true) != null)
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        Transform oldFrame = storyObject.transform.Find("Separated Bed Frame");
        if (oldFrame != null)
            Undo.DestroyObjectImmediate(oldFrame.gameObject);

        Transform frame = CreateBedLayer(bedRoot, "Separated Bed Frame", frameSprite,
            backgroundRenderer, 2);
        Transform duvet = CreateBedLayer(bedRoot, "Separated Bed Duvet", duvetSprite,
            backgroundRenderer, 3);

        Undo.RecordObject(bedInteraction, "Connect separated bed duvet interaction");
        bedInteraction.ConfigureVisualInteraction(duvet);
        EditorUtility.SetDirty(bedInteraction);

        EnsureEventSystem(scene);
        BedWakePuzzleUI wakeUI = CreateOrRefreshWakeCanvas(scene, wakeSprite, circleSprite, touchRingSprite);
        Undo.RecordObject(director, "Connect Puzzle 01 wake screen");
        director.bedWakePuzzleUI = wakeUI;
        EditorUtility.SetDirty(director);
        OwnerPresentationSetup.ApplyIfAvailable(storyObject.transform);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[After the Glow] Bed layers and the direct-touch wake screen are ready. Puzzle 01 advances on four face taps.");
    }

    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Missing image asset or importer: " + path);

        bool changed = importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
            importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed ||
            !importer.alphaIsTransparency;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        if (changed)
            importer.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException("Unity could not load the sprite: " + path);
        return sprite;
    }

    private static Transform CreateBedLayer(Transform bedRoot, string objectName, Sprite sprite,
        SpriteRenderer backgroundRenderer, int orderOffset)
    {
        GameObject layerObject = new GameObject(objectName, typeof(SpriteRenderer));
        Undo.RegisterCreatedObjectUndo(layerObject, "Create separated bed layer");
        layerObject.transform.SetParent(bedRoot, false);
        AlignToBackground(layerObject.transform, sprite, backgroundRenderer, bedRoot);

        SpriteRenderer renderer = layerObject.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = backgroundRenderer.sharedMaterial;
        renderer.color = Color.white;
        renderer.sortingLayerID = backgroundRenderer.sortingLayerID;
        renderer.sortingOrder = backgroundRenderer.sortingOrder + orderOffset;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.maskInteraction = SpriteMaskInteraction.None;
        EditorUtility.SetDirty(renderer);
        return layerObject.transform;
    }

    private static void AlignToBackground(Transform art, Sprite artSprite,
        SpriteRenderer backgroundRenderer, Transform artParent)
    {
        Transform background = backgroundRenderer.transform;
        Vector3 displayedSize = backgroundRenderer.bounds.size;
        Vector3 parentScale = artParent.lossyScale;
        art.position = background.position;
        art.rotation = background.rotation;
        art.localScale = new Vector3(
            displayedSize.x / artSprite.bounds.size.x / Mathf.Max(0.0001f, parentScale.x),
            displayedSize.y / artSprite.bounds.size.y / Mathf.Max(0.0001f, parentScale.y),
            1f);
    }

    private static BedWakePuzzleUI CreateOrRefreshWakeCanvas(Scene scene, Sprite wakeSprite,
        Sprite circleSprite, Sprite touchRingSprite)
    {
        GameObject canvasObject = FindRoot(scene, "Bed Wake Puzzle Canvas");
        if (canvasObject == null)
        {
            canvasObject = new GameObject("Bed Wake Puzzle Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(BedWakePuzzleUI));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Puzzle 01 wake canvas");
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Transform oldScreen = canvasObject.transform.Find("Wake Screen");
        if (oldScreen != null)
            Undo.DestroyObjectImmediate(oldScreen.gameObject);

        GameObject screenObject = new GameObject("Wake Screen", typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(screenObject, "Create Puzzle 01 screen");
        screenObject.transform.SetParent(canvasObject.transform, false);
        StretchToParent(screenObject.GetComponent<RectTransform>());
        CanvasGroup group = screenObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Image background = CreateImage(screenObject.transform, "Sleeping Owner Close-up", wakeSprite,
            Color.white);
        StretchToParent(background.rectTransform);
        background.raycastTarget = false;
        background.preserveAspect = false;

        Image[] dots = new Image[4];
        for (int i = 0; i < dots.Length; i++)
        {
            Image dot = CreateImage(screenObject.transform, "Wake Progress " + (i + 1), circleSprite,
                new Color(0.035f, 0.13f, 0.17f, 0.95f));
            RectTransform rect = dot.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(30f, 30f);
            rect.anchoredPosition = new Vector2((i - 1.5f) * 72f, -94f);
            dots[i] = dot;
        }

        GameObject buttonObject = new GameObject("Owner Cheek Touch Target", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(buttonObject, "Create Puzzle 01 touch target");
        buttonObject.transform.SetParent(screenObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.405f, 0.415f);
        buttonRect.anchorMax = new Vector2(0.405f, 0.415f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(188f, 188f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.sprite = circleSprite;
        buttonImage.color = new Color(1f, 1f, 1f, 0f);
        buttonImage.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = buttonImage;

        Image touchHint = CreateImage(buttonObject.transform, "Touch Hint", touchRingSprite,
            new Color(1f, 0.88f, 0.62f, 0.82f));
        RectTransform hintRect = touchHint.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0.5f);
        hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.sizeDelta = new Vector2(152f, 152f);
        hintRect.anchoredPosition = Vector2.zero;
        touchHint.raycastTarget = false;

        BedWakePuzzleUI wakeUI = canvasObject.GetComponent<BedWakePuzzleUI>();
        wakeUI.Configure(screenObject, group, button, dots, touchHint);
        EditorUtility.SetDirty(wakeUI);
        screenObject.SetActive(false);
        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(scaler);
        return wakeUI;
    }

    private static void EnsureEventSystem(Scene scene)
    {
        EventSystem system = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            EventSystem candidate = root.GetComponentInChildren<EventSystem>(true);
            if (candidate != null)
            {
                system = candidate;
                break;
            }
        }

        if (system == null)
        {
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventObject, scene);
            Undo.RegisterCreatedObjectUndo(eventObject, "Create Unity UI EventSystem");
            system = eventObject.GetComponent<EventSystem>();
        }

        Undo.RecordObject(system.gameObject, "Configure UI EventSystem");
        system.enabled = true;
        BaseInputModule[] modules = system.GetComponents<BaseInputModule>();
        for (int i = 0; i < modules.Length; i++)
        {
            if (!(modules[i] is InputSystemUIInputModule))
                Undo.DestroyObjectImmediate(modules[i]);
        }

        InputSystemUIInputModule inputModule = system.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            inputModule = Undo.AddComponent<InputSystemUIInputModule>(system.gameObject);
            inputModule.AssignDefaultActions();
        }
        else if (inputModule.actionsAsset == null)
        {
            Undo.RecordObject(inputModule, "Assign default UI input actions");
            inputModule.AssignDefaultActions();
        }
        EditorUtility.SetDirty(system.gameObject);
        EditorUtility.SetDirty(inputModule);
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(imageObject, "Create wake screen UI element");
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void EnsureTouchRingAsset()
    {
        if (File.Exists(Path.GetFullPath(TouchRingPath)))
            return;

        const int size = 128;
        const float outerRadius = 54f;
        const float innerRadius = 49f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.filterMode = FilterMode.Point;
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float edge = Mathf.Clamp01(Mathf.Min(outerRadius - distance, distance - innerRadius) + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, edge);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        string absolutePath = Path.GetFullPath(TouchRingPath);
        string directory = Path.GetDirectoryName(absolutePath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TouchRingPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }

    private static GameObject FindRootWithDirector(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponent<AfterTheGlowStoryDirector>() != null)
                return root;
        return null;
    }
}
