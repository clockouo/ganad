using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>The PPT's front-facing dog, revealed by horizontal petting rather than a color veil.</summary>
[DisallowMultipleComponent]
public sealed class PettingPuzzleUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler,
    IInitializePotentialDragHandler
{
    public const float RequiredTravelWidths = 1.6f;
    public float Progress { get; private set; }
    public float DisplayProgress { get; private set; }
    public bool IsOpen { get; private set; }
    public float BlurPixels => portraitMaterial != null ? portraitMaterial.GetFloat("_BlurPixels") : 0f;
    public RectTransform PortraitRect => portrait != null ? portrait.rectTransform : null;

    private GameObject screen;
    private UnityEngine.UI.RawImage portrait;
    private UnityEngine.UI.RawImage backdrop;
    [SerializeField] private UnityEngine.UI.Image[] dots = new UnityEngine.UI.Image[4];
    private Material portraitMaterial, backdropMaterial;
    private RenderTexture backdropTexture;
    private Action completed;
    private Coroutine opening;
    private CanvasGroup screenGroup;
    private RectTransform content;
    private bool transitioning;
    public bool IsTransitioning => transitioning;
    private float lastX, clearSince = -1f;
    private float nextSwipeSoundAt;
    private int pointerId = int.MinValue;
    private bool dragging;
    private bool wasInside;
    private readonly Vector3[] corners = new Vector3[4];
    private const int MouseGestureId = int.MinValue + 1;
    private RectTransform interactionHint;
    private UnityEngine.UI.Image heldButtonHint;
    private UnityEngine.UI.RawImage dogLayer;
    private RectTransform swipeArrow;
    private Material dogMaterial;
    private float strokeEnergy, strokeDirection;
    public const float InitialBlurPixels = 10f;

    private void Awake() => Build();

    private void Build()
    {
        if (screen != null)
        {
            for (int i = 0; i < dots.Length; i++)
                if (dots[i] == null && content != null)
                    dots[i] = content.Find("Progress " + (i + 1))?.GetComponent<UnityEngine.UI.Image>();
            return;
        }
        Texture2D art = JourneyArt.Texture("Backgrounds/06_FirstMeetingPark");
        Texture2D dogArt = JourneyArt.Texture("Puzzles/PetDog__c2r2");
        Texture2D backgroundArt = art;
        Shader shader = Resources.Load<Shader>("Puzzle02/PettingFocus");
        if (art == null || dogArt == null || backgroundArt == null || shader == null)
            throw new InvalidOperationException("Puzzle02 requires its reference portrait and focus shader.");
        art.filterMode = FilterMode.Point;
        art.wrapMode = TextureWrapMode.Clamp;
        portraitMaterial = new Material(shader);
        dogMaterial = new Material(shader);
        dogMaterial.SetFloat("_Cutout", .8f);
        backdropMaterial = new Material(shader);
        backdropMaterial.SetFloat("_BlurPixels", 4f);

        screen = new GameObject("Petting Puzzle Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        screen.transform.SetParent(transform, false);
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

        portrait = Create<UnityEngine.UI.RawImage>("PPT Dog Portrait", content);
        portrait.texture = backgroundArt;
        portrait.material = portraitMaterial;
        portrait.raycastTarget = true;
        portrait.rectTransform.anchorMin = new Vector2(.5f, .5f);
        portrait.rectTransform.anchorMax = new Vector2(.5f, .5f);
        portrait.rectTransform.sizeDelta = new Vector2(800f, 450f);
        portrait.rectTransform.anchoredPosition = new Vector2(0f, 16f);
        // EventTrigger forwards only the portrait's gestures to this component.
        var trigger = portrait.gameObject.AddComponent<EventTrigger>();
        AddEvent(trigger, EventTriggerType.PointerDown, e => OnPointerDown((PointerEventData)e));
        AddEvent(trigger, EventTriggerType.InitializePotentialDrag, e => OnInitializePotentialDrag((PointerEventData)e));
        AddEvent(trigger, EventTriggerType.Drag, e => OnDrag((PointerEventData)e));
        AddEvent(trigger, EventTriggerType.PointerUp, e => OnPointerUp((PointerEventData)e));

        dogLayer = Create<UnityEngine.UI.RawImage>("Separated Petting Dog", portrait.transform);
        dogLayer.texture = dogArt;
        JourneyArt.Set(dogLayer, JourneyArt.Frame("Puzzles/PetDog__c2r2"));
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
        screen.SetActive(false);
    }

    private void BuildSwipeArrow()
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

    private void ArrowPart(string name, float x, float y, float width, float height, Color color)
    {
        var part = Create<UnityEngine.UI.Image>(name, swipeArrow);
        part.color = color;
        part.raycastTarget = false;
        part.rectTransform.sizeDelta = new Vector2(width, height);
        part.rectTransform.anchoredPosition = new Vector2(x, y);
    }

    private void BuildInteractionHint()
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

    private UnityEngine.UI.Image HintLine(string name, float x, float y, float w, float h, Color color)
    {
        var item = Create<UnityEngine.UI.Image>(name, interactionHint);
        item.color = color;
        item.raycastTarget = false;
        item.rectTransform.sizeDelta = new Vector2(w, h);
        item.rectTransform.anchoredPosition = new Vector2(x, y);
        return item;
    }

    public void Open(Action onCompleted)
    {
        Build();
        Close();
        completed = onCompleted;
        Progress = DisplayProgress = 0f;
        clearSince = -1f;
        nextSwipeSoundAt = 0f;
        strokeEnergy = strokeDirection = 0f;
        transitioning = true;
        opening = StartCoroutine(CaptureAndOpen());
    }

    private IEnumerator CaptureAndOpen()
    {
        // Capture before showing the overlay, never recursively capture the card itself.
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        backdropTexture = new RenderTexture(Mathf.Max(1, Screen.width / 4), Mathf.Max(1, Screen.height / 4), 0);
        backdropTexture.filterMode = FilterMode.Bilinear;
        capture.filterMode = FilterMode.Bilinear;
        Graphics.Blit(capture, backdropTexture);
        Destroy(capture);
        backdrop.texture = backdropTexture;
        IsOpen = true;
        Refresh();
        screenGroup.alpha = 0f;
        screen.SetActive(true);
        yield return SoftPanelTransition.Play(screenGroup, content, true, .72f);
        transitioning = false;
        opening = null;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (SettingsMenuController.IsOpen)
        {
            StopRubbingSound();
            return;
        }
        float time = Time.unscaledTime;
        if (transitioning) return;
        strokeEnergy = Mathf.MoveTowards(strokeEnergy, 0f, Time.unscaledDeltaTime * 2.5f);
        swipeArrow.anchoredPosition = new Vector2(Mathf.Sin(time * 2.4f) * 12f, 175f);
        // Keep the card and hit region still; only the separated puppy reacts.
        dogLayer.rectTransform.anchoredPosition = new Vector2(strokeDirection * strokeEnergy * 7f,
            -65f + Mathf.Sin(time * 1.8f) * 1.5f + Mathf.Sin(time * 12f) * strokeEnergy * 3f);
        JourneyArt.Set(dogLayer, JourneyArt.Frame("Puzzles/PetDog__c2r2", Mathf.Min(3, Mathf.FloorToInt(DisplayProgress * 4f))));
        dogLayer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -strokeDirection * strokeEnergy * 2.2f);
        dogLayer.rectTransform.localScale = Vector3.one * (1f + strokeEnergy * .012f);
        interactionHint.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 2.4f) * 9f, -272f);
        var hintColor = heldButtonHint.color;
        hintColor.a = .65f + Mathf.Sin(Time.unscaledTime * 3f) * .3f;
        heldButtonHint.color = hintColor;
        DisplayProgress = Mathf.MoveTowards(DisplayProgress, Progress, Time.unscaledDeltaTime * 2.5f);
        Refresh();
        if (DisplayProgress < 1f) return;
        if (clearSince < 0f) clearSince = Time.unscaledTime;
        if (Time.unscaledTime - clearSince < .65f) return;
        transitioning = true;
        dragging = false;
        StopRubbingSound();
        opening = StartCoroutine(FinishSoftly());
    }

    private IEnumerator FinishSoftly()
    {
        Action callback = completed;
        yield return SoftPanelTransition.Play(screenGroup, content, false, .56f);
        opening = null;
        Close();
        callback?.Invoke();
    }

    private void Refresh()
    {
        if (portrait == null || portrait.texture == null || portraitMaterial == null) return;
        portrait.texture.filterMode = DisplayProgress < 1f ? FilterMode.Bilinear : FilterMode.Point;
        portraitMaterial.SetFloat("_BlurPixels", InitialBlurPixels * portrait.texture.width / 475f * (1f - DisplayProgress));
        dogLayer.texture.filterMode = DisplayProgress < 1f ? FilterMode.Bilinear : FilterMode.Point;
        dogMaterial.SetFloat("_BlurPixels", InitialBlurPixels * dogLayer.texture.width / 475f * (1f - DisplayProgress));
        for (int i = 0; i < dots.Length; i++)
            if (dots[i] != null) dots[i].color = DisplayProgress >= (i + 1) / 4f ? new Color(.98f, .9f, .68f) : new Color(.11f, .24f, .28f);
    }

    public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

    public void OnPointerDown(PointerEventData e)
    {
        // Desktop input is handled from individual GUI pointer events so fast
        // down/drag/up packets are not lost between EventSystem updates.
        // Touch still uses uGUI's pointer capture and finger identity.
        if (e.pointerId != MouseGestureId &&
            !(e is ExtendedPointerEventData touch && touch.pointerType == UIPointerType.Touch)) return;
        if (!IsOpen || transitioning || SettingsMenuController.IsOpen ||
            e.button != PointerEventData.InputButton.Left || !Inside(e)) return;
        if (dragging && pointerId != e.pointerId) return;
        dragging = true;
        wasInside = true;
        pointerId = e.pointerId;
        lastX = e.position.x;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!IsOpen || transitioning || SettingsMenuController.IsOpen || !dragging || e.pointerId != pointerId) return;
        float deltaX = e.position.x - lastX;
        float distance = Mathf.Abs(deltaX);
        lastX = e.position.x;
        bool inside = Inside(e);
        if (!inside || !wasInside)
        {
            wasInside = inside;
            if (!inside) StopRubbingSound();
            return;
        }
        if (distance > .1f)
        {
            strokeDirection = Mathf.Sign(deltaX);
            strokeEnergy = Mathf.Clamp01(strokeEnergy + distance / 80f);
            if (distance >= 14f && Time.unscaledTime >= nextSwipeSoundAt)
            {
                GameAudioManager.Instance?.PlaySfxIfIdle("ClothRubbing", .42f, .78f);
                nextSwipeSoundAt = Time.unscaledTime + .2f;
            }
        }
        PortraitRect.GetWorldCorners(corners);
        float screenWidth = Mathf.Max(1f, Vector3.Distance(corners[0], corners[3]));
        Progress = Mathf.Clamp01(Progress + distance / (screenWidth * RequiredTravelWidths));
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId == pointerId)
        {
            dragging = false;
        }
    }

    public void HandleMouseEvent(Event e)
    {
        if (e != null && e.type == EventType.MouseLeaveWindow && pointerId == MouseGestureId)
        {
            dragging = false;
            StopRubbingSound();
        }
        if (!IsOpen || e == null || e.button != 0) return;
        if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag && e.type != EventType.MouseUp) return;
        var pointer = new PointerEventData(EventSystem.current)
        {
            pointerId = MouseGestureId,
            button = PointerEventData.InputButton.Left,
            position = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y)
        };
        switch (e.type)
        {
            case EventType.MouseDown: OnPointerDown(pointer); break;
            case EventType.MouseDrag: OnDrag(pointer); break;
            case EventType.MouseUp: OnPointerUp(pointer); break;
        }
        e.Use();
    }

    private bool Inside(PointerEventData e) => RectTransformUtility.RectangleContainsScreenPoint(PortraitRect, e.position);
    private void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        dragging = false;
        StopRubbingSound();
    }
    private void OnDisable() => Close();

    private void StopRubbingSound() => GameAudioManager.Instance?.StopGatedSfx("ClothRubbing");

    public void Close()
    {
        StopRubbingSound();
        if (opening != null) { StopCoroutine(opening); opening = null; }
        IsOpen = dragging = false;
        transitioning = false;
        if (content != null) content.anchoredPosition = Vector2.zero;
        if (screenGroup != null) screenGroup.alpha = 0f;
        pointerId = int.MinValue;
        completed = null;
        if (screen != null) screen.SetActive(false);
        if (backdrop != null) backdrop.texture = null;
        if (backdropTexture != null) { backdropTexture.Release(); Destroy(backdropTexture); backdropTexture = null; }
    }

    private void OnDestroy()
    {
        Close();
        if (portraitMaterial != null) Destroy(portraitMaterial);
        if (dogMaterial != null) Destroy(dogMaterial);
        if (backdropMaterial != null) Destroy(backdropMaterial);
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
    private static void AddEvent(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(e => action(e));
        trigger.triggers.Add(entry);
    }
}
