using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Runtime settings panel styled after the supplied parchment reference.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public sealed class SettingsMenuController : MonoBehaviour
{
    private static SettingsMenuController instance;
    private static Sprite circleSprite;
    private static readonly string[] KoreanFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };

    private readonly Color ink = ColorFromHex("#173D4A");
    private readonly Color parchment = ColorFromHex("#F3E8D2");
    private readonly Color sage = ColorFromHex("#9CAD98");
    private readonly Color coral = ColorFromHex("#C96650");
    private Font font;
    private RectTransform overlay;
    private Slider masterSlider;
    private Slider backgroundSlider;
    private Slider effectsSlider;
    private Text masterValue;
    private Text backgroundValue;
    private Text effectsValue;
    private bool isOpen;
    private float previousTimeScale = 1f;

    public static bool IsOpen => instance != null && instance.isOpen;

    public static SettingsMenuController EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<SettingsMenuController>();
        if (instance != null)
            return instance;

        var root = new GameObject("After the Glow Settings UI");
        if (GameAudioManager.Instance != null)
            root.transform.SetParent(GameAudioManager.Instance.transform, false);
        return root.AddComponent<SettingsMenuController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
        EnsureEventSystem();
        Build();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (isOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        if (GameAudioManager.Instance != null)
            eventSystem.transform.SetParent(GameAudioManager.Instance.transform, false);
        else
            DontDestroyOnLoad(eventSystem);
    }

    private void Build()
    {
        font = Font.CreateDynamicFontFromOSFont(KoreanFontNames, 30);
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasObject = new GameObject("Settings Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        overlay = CreateRect("Settings Overlay", canvasObject.transform);
        Stretch(overlay);
        Image dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color(.015f, .075f, .09f, .82f);
        dim.raycastTarget = true;

        Image panelShadow = CreateImage("Panel Shadow", overlay, null, new Color(.005f, .035f, .045f, .7f), false);
        SetRect(panelShadow.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, -10f), new Vector2(844f, 904f));

        Image panel = CreateImage("Parchment Panel", overlay, null, parchment, true);
        SetRect(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, 0f), new Vector2(820f, 880f));
        var panelOutline = panel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = ink;
        panelOutline.effectDistance = new Vector2(2f, -2f);

        BuildPawIcon(panel.transform, new Vector2(0f, 338f));
        CreateText("Settings Title", panel.transform, "설정", 58, ink, TextAnchor.MiddleCenter,
            new Vector2(0f, 258f), new Vector2(340f, 78f), FontStyle.Bold);

        Button closeButton = CreateButton("Close Settings", panel.transform,
            new Vector2(.5f, .5f), new Vector2(282f, 341f), new Vector2(184f, 54f),
            parchment, new Color(.91f, .86f, .77f), new Color(.86f, .80f, .69f), "ESC   닫기", 25,
            CloseMenu, FontStyle.Normal, ink);
        var closeOutline = closeButton.gameObject.AddComponent<Outline>();
        closeOutline.effectColor = ink;
        closeOutline.effectDistance = new Vector2(2f, -2f);

        CreateRule(panel.transform, 198f);
        CreateText("Sound Section", panel.transform, "소리", 36, ink, TextAnchor.MiddleLeft,
            new Vector2(-318f, 145f), new Vector2(640f, 52f), FontStyle.Bold);

        masterSlider = CreateSliderRow(panel.transform, "전체 음량", 66f, out masterValue);
        backgroundSlider = CreateSliderRow(panel.transform, "배경음", -22f, out backgroundValue);
        effectsSlider = CreateSliderRow(panel.transform, "효과음", -110f, out effectsValue);

        GameAudioManager audio = GameAudioManager.EnsureInstance();
        masterSlider.SetValueWithoutNotify(audio.MasterVolume);
        backgroundSlider.SetValueWithoutNotify(audio.BackgroundVolume);
        effectsSlider.SetValueWithoutNotify(audio.EffectsVolume);
        masterSlider.onValueChanged.AddListener(_ => ApplyVolumes());
        backgroundSlider.onValueChanged.AddListener(_ => ApplyVolumes());
        effectsSlider.onValueChanged.AddListener(_ => ApplyVolumes());
        RefreshValues();

        CreateRule(panel.transform, -168f);
        CreateButton("Continue", panel.transform, new Vector2(.5f, .5f),
            new Vector2(0f, -252f), new Vector2(704f, 92f),
            ink, new Color(.11f, .33f, .39f), new Color(.04f, .21f, .27f), "계속하기", 38,
            CloseMenu, FontStyle.Bold);
        Button quitButton = CreateButton("Quit Game", panel.transform, new Vector2(.5f, .5f),
            new Vector2(0f, -358f), new Vector2(704f, 82f),
            parchment, new Color(1f, .95f, .88f), new Color(.96f, .87f, .79f), "게임 나가기", 34,
            QuitGame, FontStyle.Bold, coral);
        var quitOutline = quitButton.gameObject.AddComponent<Outline>();
        quitOutline.effectColor = coral;
        quitOutline.effectDistance = new Vector2(2f, -2f);

        overlay.gameObject.SetActive(false);
    }

    private Slider CreateSliderRow(Transform parent, string label, float y, out Text valueText)
    {
        CreateText(label + " Label", parent, label, 31, ink, TextAnchor.MiddleLeft,
            new Vector2(-292f, y), new Vector2(205f, 54f), FontStyle.Bold);

        RectTransform root = CreateRect(label + " Slider", parent);
        SetRect(root, new Vector2(.5f, .5f), new Vector2(45f, y), new Vector2(424f, 38f));

        Image track = CreateImage(label + " Track", root, null, new Color(.09f, .25f, .30f), true);
        SetRect(track.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(424f, 13f));

        RectTransform fillArea = CreateRect("Fill Area", root);
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = new Vector2(2f, 0f);
        fillArea.offsetMax = new Vector2(-2f, 0f);
        Image fill = CreateImage("Fill", fillArea, null, sage, false);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        RectTransform handleArea = CreateRect("Handle Area", root);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = Vector2.zero;
        handleArea.offsetMax = Vector2.zero;
        Image handle = CreateImage("Handle", handleArea, null, parchment, true);
        handle.rectTransform.anchorMin = new Vector2(.5f, .5f);
        handle.rectTransform.anchorMax = new Vector2(.5f, .5f);
        handle.rectTransform.sizeDelta = new Vector2(38f, 42f);
        var handleOutline = handle.gameObject.AddComponent<Outline>();
        handleOutline.effectColor = ink;
        handleOutline.effectDistance = new Vector2(2f, -2f);

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.transition = Selectable.Transition.None;

        Text percentText = CreateText(label + " Percent", parent, "80%", 29, ink, TextAnchor.MiddleRight,
            new Vector2(335f, y), new Vector2(92f, 54f), FontStyle.Bold);
        slider.onValueChanged.AddListener(v => percentText.text = Mathf.RoundToInt(v * 100f) + "%");
        valueText = percentText;
        return slider;
    }

    private void BuildPawIcon(Transform parent, Vector2 position)
    {
        Sprite sprite = GetCircleSprite();
        Color pad = ColorFromHex("#B99162");
        Image center = CreateImage("Paw Pad", parent, sprite, pad, false);
        SetRect(center.rectTransform, new Vector2(.5f, .5f), position, new Vector2(28f, 23f));
        Vector2[] toes = { new Vector2(-19f, 20f), new Vector2(-7f, 31f), new Vector2(7f, 31f), new Vector2(19f, 20f) };
        for (int i = 0; i < toes.Length; i++)
        {
            Image toe = CreateImage("Paw Toe " + i, parent, sprite, pad, false);
            SetRect(toe.rectTransform, new Vector2(.5f, .5f), position + toes[i], new Vector2(12f, 14f));
        }
        CreateText("Paw Accent Left", parent, "·", 25, pad, TextAnchor.MiddleCenter,
            position + new Vector2(-45f, 0f), new Vector2(20f, 28f));
        CreateText("Paw Accent Right", parent, "·", 25, pad, TextAnchor.MiddleCenter,
            position + new Vector2(45f, 0f), new Vector2(20f, 28f));
    }

    private void CreateRule(Transform parent, float y)
    {
        Image line = CreateImage("Divider", parent, null, new Color(.58f, .66f, .59f, .9f), false);
        SetRect(line.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, y), new Vector2(724f, 3f));
    }

    private Button CreateButton(string objectName, Transform parent, Vector2 anchor, Vector2 position,
        Vector2 size, Color normal, Color highlighted, Color pressed, string label, int fontSize,
        Action action, FontStyle fontStyle = FontStyle.Normal, Color? textColor = null)
    {
        Image image = CreateImage(objectName, parent, null, normal, true);
        SetRect(image.rectTransform, anchor, position, size);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = pressed;
        colors.selectedColor = highlighted;
        colors.fadeDuration = .12f;
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            action?.Invoke();
        });
        CreateText(objectName + " Text", image.transform, label, fontSize,
            textColor ?? Color.white, TextAnchor.MiddleCenter, Vector2.zero, size, fontStyle, true);
        return button;
    }

    private Text CreateText(string objectName, Transform parent, string content, int size, Color color,
        TextAnchor alignment, Vector2 position, Vector2 dimensions, FontStyle style = FontStyle.Normal,
        bool stretch = false)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 4f);
            rect.offsetMax = new Vector2(-16f, -4f);
        }
        else
        {
            SetRect(rect, new Vector2(.5f, .5f), position, dimensions);
        }

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color, bool raycastTarget)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        var rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Paw Circle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f - size * .5f) / (size * .5f);
                float dy = (y + .5f - size * .5f) / (size * .5f);
                float alpha = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 12f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
        circleSprite.name = "Runtime Paw Circle";
        circleSprite.hideFlags = HideFlags.DontSave;
        return circleSprite;
    }

    private void ApplyVolumes()
    {
        GameAudioManager.EnsureInstance().SetVolumes(masterSlider.value, backgroundSlider.value, effectsSlider.value);
        RefreshValues();
    }

    private void RefreshValues()
    {
        masterValue.text = Mathf.RoundToInt(masterSlider.value * 100f) + "%";
        backgroundValue.text = Mathf.RoundToInt(backgroundSlider.value * 100f) + "%";
        effectsValue.text = Mathf.RoundToInt(effectsSlider.value * 100f) + "%";
    }

    private void OpenMenu()
    {
        if (isOpen)
            return;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isOpen = true;
        overlay.gameObject.SetActive(true);
    }

    private void CloseMenu()
    {
        if (!isOpen)
            return;
        isOpen = false;
        overlay.gameObject.SetActive(false);
        Time.timeScale = previousTimeScale;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static Color ColorFromHex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }
}
