using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The fountain's separate top view, entered from the side-scrolling park.</summary>
[DisallowMultipleComponent]
public sealed class ParkFountainExploration : MonoBehaviour
{
    private const float LeftEntryAngle = -180f;
    private const float RightExitAngle = 0f;

    public float fountainWorldX = AfterTheGlowWorldLayout.FountainX;
    [Header("Side-scrolling entrance and exit")]
    public float entryWorldX = AfterTheGlowWorldLayout.FountainEntryX;
    public float exitWorldX = AfterTheGlowWorldLayout.FountainExitX;
    [Min(.1f)] public float approachDistance = 1f;
    public float angularSpeed = 58f;
    [Min(.05f)] public float memoryFadeInSeconds = .45f;
    [Min(.1f)] public float memoryHoldSeconds = 1.6f;
    [Min(.05f)] public float memoryFadeOutSeconds = .7f;
    public Texture2D[] memories = new Texture2D[4];
    public bool IsOpen { get; private set; }
    public bool IsMemoryOpen => activeMemory >= 0;
    public float AngleDegrees => angle;
    public int VisitedPoints { get; private set; }
    public int ActiveMemoryIndex => activeMemory;
    public int NextMemoryIndex => VisitedPoints < PointAngles.Length ? VisitedPoints : -1;
    public bool CanExitRight => Mathf.Abs(Mathf.DeltaAngle(angle, RightExitAngle)) < 22f;

    private AfterTheGlowStoryDirector story;
    private GameObject screen, memoryScreen;
    private RectTransform frame, dog, viewportRect;
    private UnityEngine.UI.RawImage memoryImage;
    private UnityEngine.UI.Button exitButton;
    private readonly UnityEngine.UI.Image[] markers = new UnityEngine.UI.Image[4];
    private readonly UnityEngine.UI.Image[] glows = new UnityEngine.UI.Image[4];
    private readonly UnityEngine.UI.Image[] dots = new UnityEngine.UI.Image[4];
    private readonly bool[] visited = new bool[4];
    private static readonly float[] PointAngles = { -153f, 128f, 37f, -47f };
    private float angle = LeftEntryAngle, memoryOpenedAt;
    private float orbitDirection = 1f;
    private float sideScrollGroundY;
    private int activeMemory = -1;
    private bool entryArmed = true, savedCameraEnabled;
    private Material dogMaterial, memoryMaterial;
    private Sprite ringSprite, dotSprite;
    private CanvasGroup screenGroup;
    private RectTransform presentation;
    private Coroutine transition;
    private bool transitioning;
    public bool IsTransitioning => transitioning;

    private void Awake()
    {
        story = GetComponent<AfterTheGlowStoryDirector>();
        if (story == null) story = FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (memories == null || memories.Length != 4) memories = new Texture2D[4];
        string[] defaults = { "01_ApproachingShoes", "02_HandStops", "03_WaitingOnBench", "04_FirstSniff" };
        for (int i = 0; i < 4; i++)
            if (memories[i] == null) memories[i] = JourneyArt.Texture("Memories/" + defaults[i]);
    }

    private void Update()
    {
        if (SettingsMenuController.IsOpen)
            return;
        if (story == null || story.player == null) return;
        if (!IsOpen)
        {
            float distance = Mathf.Abs(story.player.transform.position.x - entryWorldX);
            if (distance > 2.5f) entryArmed = true;
            if (entryArmed && story.Stage == AfterTheGlowStoryStage.ParkPlaceholder &&
                story.player.AllowsPlayerInput && story.player.IsGrounded && distance <= approachDistance) Open();
            return;
        }

        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (transitioning) return;
        if (escape) { Close(); return; }
        if (IsMemoryOpen)
        {
            UpdateMemoryFade();
            return;
        }
        if (CanExitRight && PlayerMovement.WasKeyPressedThisFrame(KeyCode.S)) { Close(); return; }
        float direction = (PlayerMovement.IsKeyHeld(KeyCode.D) ? 1f : 0f) - (PlayerMovement.IsKeyHeld(KeyCode.A) ? 1f : 0f);
        float turn = MoveHorizontally(direction, Time.unscaledDeltaTime);
        bool moving = Mathf.Abs(turn) > .0001f;
        dog.anchoredPosition = Orbit(angle) + Vector2.up * (moving ? Mathf.Sin(Time.unscaledTime * 15f) * 1.5f : 0f);
        JourneyArt.Set(dog.GetComponent<UnityEngine.UI.RawImage>(), JourneyArt.Frame("Characters/Dog/DogTopFixed__c2r2",
            moving ? Mathf.FloorToInt(Time.unscaledTime * 8f) % 4 : 0));
        if (moving) dog.localRotation = Quaternion.Euler(0, 0, angle + (turn < 0 ? 180f : 0f));
        dog.localScale = Vector3.one * (1f + (moving ? Mathf.Sin(Time.unscaledTime * 15f) * .025f : 0f));
        exitButton.interactable = CanExitRight;
        RefreshMemoryMarkers();
        int next = NextMemoryIndex;
        if (next >= 0 && Mathf.Abs(Mathf.DeltaAngle(angle, PointAngles[next])) <= 9f)
            OpenMemory(next);
    }

    private float MoveHorizontally(float horizontal, float deltaTime)
    {
        if (Mathf.Abs(horizontal) < .01f) return 0f;
        float previous = angle;
        float step = Mathf.Min(180f, Mathf.Abs(horizontal) * angularSpeed * Mathf.Max(0f, deltaTime));
        if (Mathf.Abs(angle) < .0001f)
        {
            // At the right end D stops. A continues around the next half of the ring.
            if (horizontal < 0f) angle = orbitDirection * step;
        }
        else if (Mathf.Abs(angle) >= 180f - .0001f)
        {
            // At the left end A stops. D continues around the next half of the ring.
            if (horizontal > 0f) angle = orbitDirection > 0f ? -180f + step : 180f - step;
        }
        else if (angle < 0f)
            angle = Mathf.Clamp(angle + horizontal * angularSpeed * deltaTime, -180f, 0f);
        else
            angle = Mathf.Clamp(angle - horizontal * angularSpeed * deltaTime, 0f, 180f);

        float turn = Mathf.DeltaAngle(previous, angle);
        if (Mathf.Abs(turn) > .0001f) orbitDirection = Mathf.Sign(turn);
        return turn;
    }

    private void RefreshMemoryMarkers()
    {
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] == null) continue;
            bool available = i == NextMemoryIndex;
            float pulse = available ? 1f + Mathf.Sin(Time.unscaledTime * 2.3f + i) * .06f : 1f;
            markers[i].rectTransform.localScale = Vector3.one * pulse;
            markers[i].color = visited[i] ? new Color(.6f, .85f, .81f, .75f)
                : available ? new Color(1f, .91f, .6f, .95f) : new Color(.48f, .63f, .63f, .38f);
            if (glows[i] != null)
                glows[i].color = available ? new Color(1f, .9f, .56f, .14f) : Color.clear;
        }
    }

    private static Vector2 Orbit(float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a) * 240f, 14f + Mathf.Sin(a) * 225f);
    }

    private void LateUpdate()
    {
        if (IsOpen && viewportRect != null) frame.localScale = Vector3.one * (viewportRect.rect.width / 1280f);
    }

    public void Open()
    {
        if (IsOpen || story == null || story.Stage != AfterTheGlowStoryStage.ParkPlaceholder) return;
        Build();
        IsOpen = true; entryArmed = false; angle = LeftEntryAngle; orbitDirection = 1f;
        exitButton.interactable = false;
        sideScrollGroundY = story.player.Body.position.y;
        savedCameraEnabled = story.cameraFollow.enabled;
        story.cameraFollow.enabled = false;
        story.player.SetControlMode(PlayerControlMode.CutsceneLocked);
        dog.anchoredPosition = Orbit(angle);
        dog.localRotation = Quaternion.Euler(0, 0, angle + 180f);
        memoryScreen.SetActive(false);
        activeMemory = -1;
        screen.SetActive(true);
        screenGroup.alpha = 0f;
        transitioning = true;
        transition = StartCoroutine(EnterSoftly());
        RefreshMemoryMarkers();
    }

    private System.Collections.IEnumerator EnterSoftly()
    {
        yield return SoftPanelTransition.Play(screenGroup, presentation, true, .8f, 34f);
        transitioning = false;
        transition = null;
    }

    public void Close()
    {
        if (!IsOpen || transitioning) return;
        transitioning = true;
        transition = StartCoroutine(LeaveSoftly());
    }

    private System.Collections.IEnumerator LeaveSoftly()
    {
        // Conceal the side-scroll teleport below the outgoing top view first.
        MoveToSideScrollExit();
        yield return SoftPanelTransition.Play(screenGroup, presentation, false, .65f, 34f);
        IsOpen = false;
        transitioning = false;
        transition = null;
        story.player.SetControlMode(PlayerControlMode.Free);
        story.cameraFollow.enabled = savedCameraEnabled;
        screen.SetActive(false);
    }

    private void MoveToSideScrollExit()
    {
        // Leaving during a reflection does not skip that scene. Completed scenes
        // remain remembered, so the same next point is available on re-entry.
        SetMemoryAlpha(0f);
        memoryScreen.SetActive(false);
        activeMemory = -1; entryArmed = false;
        var player = story.player;
        // Return beside the fountain's right rim at the entrance's grounded height.
        Vector2 position = new Vector2(exitWorldX, sideScrollGroundY);
        player.Body.position = position;
        player.transform.position = new Vector3(position.x, position.y, player.transform.position.z);
        player.Body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        player.FaceTowards(exitWorldX + 1f);
        // Frame the return position below the outgoing view before revealing it.
        story.cameraFollow.SwitchToExteriorFraming();
    }

    private void OpenMemory(int index)
    {
        // Guard the entry itself too: a later point must never bypass the order.
        if (!IsOpen || IsMemoryOpen || index < 0 || index >= PointAngles.Length ||
            index != NextMemoryIndex || visited[index] || memories[index] == null ||
            Mathf.Abs(Mathf.DeltaAngle(angle, PointAngles[index])) > 9f) return;
        activeMemory = index; memoryOpenedAt = Time.unscaledTime;
        memoryImage.texture = memories[index];
        memoryImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        SetMemoryAlpha(0f);
        memoryScreen.SetActive(true);
    }

    private void UpdateMemoryFade()
    {
        float elapsed = Time.unscaledTime - memoryOpenedAt;
        float fadeInEnd = memoryFadeInSeconds;
        float holdEnd = fadeInEnd + memoryHoldSeconds;
        float fadeOutEnd = holdEnd + memoryFadeOutSeconds;
        float alpha;

        if (elapsed < fadeInEnd)
            alpha = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.05f, memoryFadeInSeconds));
        else if (elapsed < holdEnd)
            alpha = 1f;
        else if (elapsed < fadeOutEnd)
            alpha = 1f - Mathf.SmoothStep(0f, 1f,
                (elapsed - holdEnd) / Mathf.Max(.05f, memoryFadeOutSeconds));
        else
        {
            CloseMemory();
            return;
        }

        SetMemoryAlpha(alpha);
    }

    private void SetMemoryAlpha(float alpha)
    {
        if (memoryImage == null)
            return;

        Color color = memoryImage.color;
        color.a = .68f * Mathf.Clamp01(alpha);
        memoryImage.color = color;
    }

    private void CloseMemory()
    {
        // Advance only after fade-in, hold and fade-out have all finished.
        if (activeMemory >= 0 && activeMemory == NextMemoryIndex)
        {
            visited[activeMemory] = true;
            dots[activeMemory].color = new Color(1f, .92f, .65f);
            VisitedPoints++;
        }
        SetMemoryAlpha(0f);
        activeMemory = -1;
        memoryScreen.SetActive(false);
        RefreshMemoryMarkers();
    }

    private void Build()
    {
        if (screen != null) return;
        Texture2D background = JourneyArt.Texture("Backgrounds/04_ParkTop");
        Texture2D dogArt = JourneyArt.Texture("Characters/Dog/DogTopFixed__c2r2");
        ringSprite = JourneyArt.Frame("UI/Indicators__c4r2", 0);
        dotSprite = JourneyArt.Frame("UI/Indicators__c4r2", 3);
        if (background == null || dogArt == null || ringSprite == null || dotSprite == null)
            throw new System.InvalidOperationException("Park fountain layers are missing.");
        screen = new GameObject("Park Fountain Top View Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        screen.transform.SetParent(transform, false);
        var canvas = screen.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1200;
        screenGroup = screen.AddComponent<CanvasGroup>();
        var scaler = screen.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        var presentationObject = new GameObject("Fountain Transition Content", typeof(RectTransform));
        presentationObject.transform.SetParent(screen.transform, false);
        presentation = (RectTransform)presentationObject.transform; Stretch(presentation);
        var black = Image("Letterbox", screen.transform, null, Color.black); Stretch(black.rectTransform);
        black.transform.SetAsFirstSibling();
        var viewport = new GameObject("Fountain Viewport", typeof(RectTransform), typeof(UnityEngine.UI.AspectRatioFitter));
        viewport.transform.SetParent(presentation, false);
        var fit = viewport.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 16f / 9f;
        // A stable 1280x720 art coordinate space inside any display aspect ratio.
        var space = new GameObject("Fountain Art Space", typeof(RectTransform));
        space.transform.SetParent(viewport.transform, false); frame = (RectTransform)space.transform;
        frame.sizeDelta = new Vector2(1280, 720);
        viewportRect = (RectTransform)viewport.transform;
        var bg = Raw("Clean Top View", frame, background); Stretch(bg.rectTransform);
        for (int i = 0; i < 4; i++)
        {
            var glow = Image("Memory Glow " + i, frame, dotSprite, new Color(1f, .9f, .56f, .14f));
            glows[i] = glow;
            glow.rectTransform.sizeDelta = new Vector2(46, 46); glow.rectTransform.anchoredPosition = Orbit(PointAngles[i]);
            markers[i] = Image("Memory Point " + i, frame, ringSprite, Color.white);
            markers[i].rectTransform.sizeDelta = new Vector2(34, 34); markers[i].rectTransform.anchoredPosition = Orbit(PointAngles[i]);
            dots[i] = Image("Visited Point " + i, frame, dotSprite, visited[i] ? new Color(1f,.92f,.65f) : new Color(.14f,.28f,.29f));
            dots[i].rectTransform.sizeDelta = new Vector2(10, 10); dots[i].rectTransform.anchoredPosition = new Vector2((i-1.5f)*22f, 324f);
        }
        dogMaterial = new Material(Resources.Load<Shader>("Puzzle02/PettingFocus")); dogMaterial.SetFloat("_Cutout", .85f);
        var dogImage = Raw("Fountain Player Dog", frame, dogArt); dogImage.material = dogMaterial;
        JourneyArt.Set(dogImage, JourneyArt.Frame("Characters/Dog/DogTopFixed__c2r2"));
        dog = dogImage.rectTransform; dog.sizeDelta = new Vector2(48, 78);
        KeyBadge("A", new Vector2(-315, 10), false);
        KeyBadge("D", new Vector2(315, 10), false);
        exitButton = KeyBadge("S", new Vector2(292, -178), true);
        exitButton.onClick.AddListener(Close);
        var waterMask = Image("Fountain Water Memory", frame, dotSprite, Color.white);
        memoryScreen = waterMask.gameObject;
        waterMask.rectTransform.sizeDelta = new Vector2(230f, 110f);
        waterMask.rectTransform.anchoredPosition = new Vector2(0f, -45f);
        waterMask.enabled = false;
        memoryImage = Raw("Memory Reflection", memoryScreen.transform, null);
        memoryImage.rectTransform.sizeDelta = new Vector2(240f, 135f);
        memoryMaterial = new Material(Resources.Load<Shader>("ParkFountain/WaterMemory"));
        memoryImage.material = memoryMaterial;
        memoryImage.color = new Color(.62f, .91f, .88f, .68f);
        memoryImage.raycastTarget = false;
        memoryScreen.SetActive(false); screen.SetActive(false);
    }

    private UnityEngine.UI.Button KeyBadge(string label, Vector2 position, bool clickable)
    {
        var plate = Image("Key " + label, frame, null, new Color(.06f,.19f,.22f,.86f));
        plate.rectTransform.sizeDelta = new Vector2(34, 34); plate.rectTransform.anchoredPosition = position;
        var textObject = new GameObject("Glyph", typeof(RectTransform), typeof(UnityEngine.UI.Text)); textObject.transform.SetParent(plate.transform, false);
        var text = textObject.GetComponent<UnityEngine.UI.Text>(); text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 23; text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f,.94f,.75f); text.raycastTarget = false; Stretch((RectTransform)text.transform);
        var button = plate.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = plate;
        plate.raycastTarget = clickable; button.enabled = clickable;
        return button;
    }
    private static UnityEngine.UI.Image Image(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<UnityEngine.UI.Image>(); image.sprite = sprite; image.color = color; image.raycastTarget = false; return image;
    }
    private static UnityEngine.UI.RawImage Raw(string name, Transform parent, Texture texture)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.RawImage)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<UnityEngine.UI.RawImage>(); image.texture = texture; image.raycastTarget = false; return image;
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
    private void OnDestroy() { if (dogMaterial != null) Destroy(dogMaterial); if (memoryMaterial != null) Destroy(memoryMaterial); }

    private void OnDrawGizmosSelected()
    {
        const float floorY = -3.86f;
        Gizmos.color = new Color(1f, .2f, .15f, .9f);
        Gizmos.DrawWireCube(new Vector3(entryWorldX, floorY + .8f, 0f), new Vector3(approachDistance * 2f, 1.6f, 0f));
        Gizmos.color = new Color(.15f, .35f, 1f, .9f);
        Gizmos.DrawWireCube(new Vector3(exitWorldX, floorY + .8f, 0f), new Vector3(1.8f, 1.6f, 0f));
    }
}
