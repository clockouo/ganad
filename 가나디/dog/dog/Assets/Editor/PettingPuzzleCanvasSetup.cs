using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;

/// <summary>One-time authoring tool. Runtime never constructs the puzzle hierarchy.</summary>
public static class PettingPuzzleCanvasSetup
{
    private static GameObject screen;
    private static UnityEngine.UI.RawImage portrait, backdrop, dogLayer;
    private static UnityEngine.UI.Image[] dots = new UnityEngine.UI.Image[4];
    private static CanvasGroup screenGroup;
    private static RectTransform content, swipeArrow, interactionHint;
    private static UnityEngine.UI.Image heldButtonHint;
    private static Material portraitMaterial, dogMaterial, backdropMaterial;

    [MenuItem("Tools/After the Glow/Create Editable Petting Canvas")]
    public static void CreateInOpenScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before editing the Canvas.");
        var controller = UnityEngine.Object.FindAnyObjectByType<PettingPuzzleUI>();
        if (controller == null) throw new InvalidOperationException("Open SampleScene first.");
        var serialized = new SerializedObject(controller);
        if (serialized.FindProperty("screen").objectReferenceValue != null)
        {
            Selection.activeObject = serialized.FindProperty("screen").objectReferenceValue;
            return;
        }
        CreateView(controller.transform);
        const string folder = "Assets/UI/Petting";
        System.IO.Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(portraitMaterial, folder + "/Portrait Focus.mat");
        AssetDatabase.CreateAsset(dogMaterial, folder + "/Dog Focus.mat");
        AssetDatabase.CreateAsset(backdropMaterial, folder + "/Backdrop Focus.mat");
        PrefabUtility.SaveAsPrefabAssetAndConnect(screen, folder + "/PettingPuzzleCanvas.prefab", InteractionMode.AutomatedAction);
        serialized.Update();
        serialized.FindProperty("screen").objectReferenceValue = screen;
        serialized.FindProperty("portrait").objectReferenceValue = portrait;
        serialized.FindProperty("backdrop").objectReferenceValue = backdrop;
        serialized.FindProperty("dogLayer").objectReferenceValue = dogLayer;
        serialized.FindProperty("screenGroup").objectReferenceValue = screenGroup;
        serialized.FindProperty("content").objectReferenceValue = content;
        serialized.FindProperty("swipeArrow").objectReferenceValue = swipeArrow;
        serialized.FindProperty("interactionHint").objectReferenceValue = interactionHint;
        serialized.FindProperty("heldButtonHint").objectReferenceValue = heldButtonHint;
        var dotProperty = serialized.FindProperty("dots");
        dotProperty.arraySize = dots.Length;
        for (int i = 0; i < dots.Length; i++) dotProperty.GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        EditorSceneManager.SaveScene(controller.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = screen;
        Debug.Log("Editable petting Canvas saved under AfterTheGlowStoryDemo, with a prefab in Assets/UI/Petting.");
    }
    private static void CreateView(Transform parent)
    {
        Texture2D art = JourneyArt.Texture("Backgrounds/06_FirstMeetingPark");
        Texture2D dogArt = JourneyArt.Texture("Puzzles/PettingDogPoses__c3r2");
        Texture2D pettingBackgroundArt = Resources.Load<Texture2D>("Puzzle02/PettingBackground_Separated");
        Texture2D backgroundArt = art;
        Shader shader = Resources.Load<Shader>("Puzzle02/PettingFocus");
        if (art == null || dogArt == null || pettingBackgroundArt == null || backgroundArt == null || shader == null)
            throw new InvalidOperationException("Puzzle02 requires its petting background, reference portrait and focus shader.");
        art.filterMode = FilterMode.Point;
        art.wrapMode = TextureWrapMode.Clamp;
        portraitMaterial = new Material(shader);
        dogMaterial = new Material(shader);
        dogMaterial.SetFloat("_Cutout", .8f);
        backdropMaterial = new Material(shader);
        backdropMaterial.SetFloat("_BlurPixels", 4f);

        screen = new GameObject("Petting Puzzle Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        screen.transform.SetParent(parent, false);
        Canvas canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;
        screenGroup = screen.AddComponent<CanvasGroup>();
        screenGroup.alpha = 0f;
        var scaler = screen.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        backdrop = Create<UnityEngine.UI.RawImage>("Blurred House", screen.transform);
        Stretch(backdrop.rectTransform);
        backdrop.material = backdropMaterial;
        backdrop.color = new Color(.88f, .91f, .88f, 1f);
        // Backdrop consumes raycasts, but does not receive petting gestures.
        backdrop.raycastTarget = true;

        var contentObject = new GameObject("Petting Close-up Content", typeof(RectTransform));
        contentObject.transform.SetParent(screen.transform, false);
        content = (RectTransform)contentObject.transform;
        Stretch(content);

        var pettingBackground = Create<UnityEngine.UI.RawImage>("Petting Background Frame", content);
        pettingBackground.texture = pettingBackgroundArt;
        pettingBackground.raycastTarget = false;
        Stretch(pettingBackground.rectTransform);

        portrait = Create<UnityEngine.UI.RawImage>("PPT Dog Portrait", content);
        portrait.texture = backgroundArt;
        portrait.material = portraitMaterial;
        portrait.raycastTarget = true;
        portrait.rectTransform.anchorMin = new Vector2(.5f, .5f);
        portrait.rectTransform.anchorMax = new Vector2(.5f, .5f);
        portrait.rectTransform.sizeDelta = new Vector2(800f, 450f);
        portrait.rectTransform.anchoredPosition = new Vector2(0f, 16f);
        dogLayer = Create<UnityEngine.UI.RawImage>("Separated Petting Dog", portrait.transform);
        dogLayer.texture = dogArt;
        dogLayer.material = dogMaterial;
        dogLayer.raycastTarget = false;
        Stretch(dogLayer.rectTransform);
        dogLayer.rectTransform.anchorMin = dogLayer.rectTransform.anchorMax = new Vector2(.5f, .5f);
        dogLayer.rectTransform.sizeDelta = new Vector2(300f, 300f);
        dogLayer.rectTransform.anchoredPosition = new Vector2(0f, -65f);
        dogLayer.rectTransform.pivot = new Vector2(.5f, .15f);
        BuildSwipeArrow();

        for (int i = 0; i < dots.Length; i++)
        {
            dots[i] = Create<UnityEngine.UI.Image>("Progress " + (i + 1), content);
            dots[i].raycastTarget = false;
            dots[i].rectTransform.sizeDelta = new Vector2(12f, 12f);
            dots[i].rectTransform.anchoredPosition = new Vector2((i - 1.5f) * 26f, -221f);
        }
        BuildInteractionHint();
        dogLayer.uvRect = new Rect(0f, .5f, 1f / 3f, .5f);
        dogMaterial.SetVector("_SampleRect", new Vector4(.5f / dogArt.width, .5f + .5f / dogArt.height,
            1f / 3f - .5f / dogArt.width, 1f - .5f / dogArt.height));
        screenGroup.alpha = 1f;
        screen.SetActive(true);
    }

    private static void BuildSwipeArrow()
    {
        var root = new GameObject("Independent Petting Arrow", typeof(RectTransform));
        root.transform.SetParent(content, false);
        swipeArrow = (RectTransform)root.transform;
        swipeArrow.sizeDelta = new Vector2(150f, 28f);
        swipeArrow.anchoredPosition = new Vector2(0f, 175f);
        var icon = Create<UnityEngine.UI.Image>("Swipe Indicator", swipeArrow);
        icon.sprite = JourneyArt.Frame("UI/Indicators__c4r2", 5);
        icon.preserveAspect = true; icon.raycastTarget = false;
        icon.rectTransform.sizeDelta = new Vector2(150f, 28f);
    }

    private static void ArrowPart(string name, float x, float y, float width, float height, Color color)
    {
        var part = Create<UnityEngine.UI.Image>(name, swipeArrow);
        part.color = color;
        part.raycastTarget = false;
        part.rectTransform.sizeDelta = new Vector2(width, height);
        part.rectTransform.anchoredPosition = new Vector2(x, y);
    }

    private static void BuildInteractionHint()
    {
        var plate = Create<UnityEngine.UI.Image>("Petting Interaction Hint", content);
        plate.sprite = JourneyArt.Frame("UI/CloseupPanel");
        plate.raycastTarget = false;
        plate.color = new Color(.07f, .18f, .21f, .9f);
        interactionHint = plate.rectTransform;
        interactionHint.sizeDelta = new Vector2(180f, 52f);
        interactionHint.anchoredPosition = new Vector2(0f, -272f);
        Color cream = Color.white;
        heldButtonHint = HintLine("Hold Left Mouse Button", 0, 0, 30, 38, cream);
        heldButtonHint.sprite = JourneyArt.Frame("UI/Indicators__c4r2", 4);
        heldButtonHint.preserveAspect = true;
        foreach (int side in new[] { -1, 1 })
        {
            var arrow = HintLine("Swipe Direction " + side, side * 55, 0, 45, 18, cream);
            arrow.sprite = JourneyArt.Frame("UI/Indicators__c4r2", 5);
            arrow.preserveAspect = true;
        }
    }

    private static UnityEngine.UI.Image HintLine(string name, float x, float y, float w, float h, Color color)
    {
        var item = Create<UnityEngine.UI.Image>(name, interactionHint);
        item.color = color;
        item.raycastTarget = false;
        item.rectTransform.sizeDelta = new Vector2(w, h);
        item.rectTransform.anchoredPosition = new Vector2(x, y);
        return item;
    }

    private static T Create<T>(string name, Transform parent) where T : Component
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(T));
        item.transform.SetParent(parent, false);
        return item.GetComponent<T>();
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
