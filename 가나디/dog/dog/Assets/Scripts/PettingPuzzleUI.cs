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

    [SerializeField] private GameObject screen;
    [SerializeField] private UnityEngine.UI.RawImage portrait;
    [SerializeField] private UnityEngine.UI.RawImage backdrop;
    [SerializeField] private UnityEngine.UI.Image[] dots = new UnityEngine.UI.Image[4];
    private Material portraitMaterial, backdropMaterial;
    private RenderTexture backdropTexture;
    private Action completed;
    private Coroutine opening;
    [SerializeField] private CanvasGroup screenGroup;
    [SerializeField] private RectTransform content;
    private bool transitioning;
    public bool IsTransitioning => transitioning;
    private float lastX, clearSince = -1f;
    private float nextSwipeSoundAt;
    private int pointerId = int.MinValue;
    private bool dragging;
    private bool wasInside;
    private readonly Vector3[] corners = new Vector3[4];
    private const int MouseGestureId = int.MinValue + 1;
    [SerializeField] private RectTransform interactionHint;
    [SerializeField] private UnityEngine.UI.Image heldButtonHint;
    [SerializeField] private UnityEngine.UI.RawImage dogLayer;
    [SerializeField] private RectTransform swipeArrow;
    private Material dogMaterial;
    private float strokeEnergy, strokeDirection;
    public const float InitialBlurPixels = 10f;

    private bool viewInitialized;
    private Vector2 contentRest, dogRest, arrowRest, hintRest;
    private Quaternion dogRestRotation;
    private Vector3 dogRestScale;

    private void Awake()
    {
        InitializeView();
        Close();
    }

    private bool InitializeView()
    {
        if (viewInitialized) return true;
        if (screen == null || portrait == null || backdrop == null || dogLayer == null ||
            screenGroup == null || content == null || swipeArrow == null || interactionHint == null || heldButtonHint == null)
        {
            Debug.LogError("Petting Canvas references are missing. Assign the Canvas in the Inspector.", this);
            return false;
        }
        portraitMaterial = new Material(portrait.material);
        dogMaterial = new Material(dogLayer.material);
        backdropMaterial = new Material(backdrop.material);
        portrait.material = portraitMaterial;
        dogLayer.material = dogMaterial;
        backdrop.material = backdropMaterial;
        contentRest = content.anchoredPosition;
        dogRest = dogLayer.rectTransform.anchoredPosition;
        dogRestRotation = dogLayer.rectTransform.localRotation;
        dogRestScale = dogLayer.rectTransform.localScale;
        arrowRest = swipeArrow.anchoredPosition;
        hintRest = interactionHint.anchoredPosition;
        var trigger = portrait.GetComponent<EventTrigger>();
        if (trigger == null) trigger = portrait.gameObject.AddComponent<EventTrigger>();
        AddEvent(trigger, EventTriggerType.PointerDown, e => OnPointerDown((PointerEventData)e));
        AddEvent(trigger, EventTriggerType.InitializePotentialDrag, e => OnInitializePotentialDrag((PointerEventData)e));
        AddEvent(trigger, EventTriggerType.Drag, e => OnDrag((PointerEventData)e));
        AddEvent(trigger, EventTriggerType.PointerUp, e => OnPointerUp((PointerEventData)e));
        viewInitialized = true;
        return true;
    }

    public void Open(Action onCompleted)
    {
        if (!InitializeView()) return;
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
        swipeArrow.anchoredPosition = arrowRest + new Vector2(Mathf.Sin(time * 2.4f) * 12f, 0f);
        // Keep the card and hit region still; only the separated puppy reacts.
        dogLayer.rectTransform.anchoredPosition = dogRest + new Vector2(strokeDirection * strokeEnergy * 7f,
            Mathf.Sin(time * 1.8f) * 1.5f + Mathf.Sin(time * 12f) * strokeEnergy * 3f);
        dogLayer.rectTransform.localRotation = dogRestRotation * Quaternion.Euler(0f, 0f, -strokeDirection * strokeEnergy * 2.2f);
        dogLayer.rectTransform.localScale = dogRestScale * (1f + strokeEnergy * .012f);
        interactionHint.anchoredPosition = hintRest + new Vector2(Mathf.Sin(Time.unscaledTime * 2.4f) * 9f, 0f);
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
        // Read the five occupied grid cells directly. Imported automatic slices
        // include tiny alpha fragments and are not the pose frames.
        int pose = Mathf.Clamp(Mathf.FloorToInt(DisplayProgress * 5f), 0, 4);
        Rect poseUV = new Rect((pose % 3) / 3f, (1 - pose / 3) / 2f, 1f / 3f, .5f);
        dogLayer.uvRect = poseUV;
        float insetX = .5f / dogLayer.texture.width;
        float insetY = .5f / dogLayer.texture.height;
        dogMaterial.SetVector("_SampleRect", new Vector4(poseUV.xMin + insetX, poseUV.yMin + insetY,
            poseUV.xMax - insetX, poseUV.yMax - insetY));
        dogLayer.texture.filterMode = DisplayProgress < 1f ? FilterMode.Bilinear : FilterMode.Point;
        dogMaterial.SetFloat("_BlurPixels", InitialBlurPixels * (dogLayer.texture.width / 3f) / 475f * (1f - DisplayProgress));
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
        if (content != null) content.anchoredPosition = contentRest;
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

    private static void AddEvent(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(e => action(e));
        trigger.triggers.Add(entry);
    }
}
