using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public enum AfterTheGlowStoryStage
{
    WakeTheOwner,
    BedWakePuzzle,
    OwnerPreparing,
    OwnerAtTheDoor,
    OwnerPetPuzzle,
    OwnerDeparture,
    HouseExitReady,
    StreetDeparture,
    FollowOwner,
    HeadlightRetreat,
    HeadlightWait,
    GateBound,
    GateMemoryPuzzle,
    GateReleased,
    GatePushed,
    ParkPlaceholder,
    GateBitePull
}

/// <summary>
/// Connects the first playable story slice: wake, prepare, pet, leave, learn
/// the headlight retreat, pull the leash, push the park gate, and enter a stub.
/// </summary>
[DisallowMultipleComponent]
public sealed class AfterTheGlowStoryDirector : MonoBehaviour
{
    [Header("Actors and shared systems")]
    public PlayerMovement player;
    public CameraFollow cameraFollow;
    public Transform owner;
    public Light2D bathroomLight;
    public HomeRoomLighting homeRoomLighting;
    public PushableObject bed;
    public GameObject rollingBall;
    public PushableObject parkGate;
    public GameObject carHeadlight;
    public Transform frontDoorPivot;

    [Header("Puzzle 01 close-up touch screen")]
    public BedWakePuzzleUI bedWakePuzzleUI;
    public PettingPuzzleUI pettingPuzzleUI;

    [Header("PixelSnapper presentation states")]
    public Sprite ownerWalkSprite;
    public Sprite ownerPetSprite;
    public Sprite frontDoorClosedSprite;
    public Sprite frontDoorOpenSprite;
    public Sprite gateBoundSprite;
    public Sprite gateLooseSprite;
    public Sprite gateBrokenSprite;
    public Sprite gateOpenSprite;

    [Header("Puzzle 03 memory images (loaded from Resources/Puzzle03)")]
    public Texture2D[] gateMemoryFrames = new Texture2D[8];

    [Header("목줄 크기")]
    [Tooltip("1.0이 현재 크기입니다. 목줄을 같은 비율로 키웁니다. 1.05보다 크게 하면 화면 가장자리에서 잘릴 수 있습니다.")]
    [InspectorName("크기 배율")]
    [SerializeField, Range(0.5f, 1.5f)]
    private float gateMemoryLeashScale = 1f;
    [Tooltip("Scene 뷰 미리보기에서 마지막으로 끊어진 목줄 상태를 표시합니다.")]
    [InspectorName("마지막 파손 미리보기")]
    [SerializeField]
    private bool gateMemoryLeashPreviewBroken;

    public float GateMemoryLeashScale => gateMemoryLeashScale;
    public bool GateMemoryLeashPreviewBroken => gateMemoryLeashPreviewBroken;

    [Header("Owner size and contact with the playable dog")]
    [Min(0.1f)] public float ownerStandingHeight = 2.05f;
    [Min(0.1f)] public float ownerPetHeight = 1.75f;
    public float ownerVisualGroundOffset = -0.24f;
    public bool alignOwnerPetWithPlayer;
    public Vector2 ownerPetHandNormalized = new Vector2(0.94f, 0.44f);

    [Header("Temporary timing and positions")]
    [Min(0.1f)] public float petSeconds = 1f;
    [Min(0.1f)] public float headlightOffDelay = 2.5f;
    public float bathroomX = 6.3f;
    public float frontDoorX = 7.9f;
    [Min(0f)] public float ownerDoorStandOffset = 1f;
    [Min(0f)] public float ownerDoorExitOffset = 1.25f;
    public float streetEntryPlayerX = AfterTheGlowWorldLayout.StreetEntryX;
    public float streetEntryPlayerY = -3.86f;
    public float ownerPreparationTargetX = 11.1f;
    [Min(0f)] public float minimumOwnerPreparationSeconds = 4f;
    public float parkEntryX = AfterTheGlowWorldLayout.ParkEntryX;
    public float ownerStreetStartX = AfterTheGlowWorldLayout.StreetEntryX + .8f;
    public float ownerStreetEndX = 13.755f;
    public float actorMoveSpeed = 1.8f;

    [Header("Front door motion")]
    [Min(0.05f)] public float doorOpenSeconds = 0.35f;
    [Min(0.05f)] public float doorCloseSeconds = 0.28f;
    [Min(0.05f)] public float doorClosedHoldSeconds = 0.45f;
    [Min(0.05f)] public float doorReboundSeconds = 0.18f;
    [Range(0f, 1f)] public float doorAjarAmount = 0.2f;

    public AfterTheGlowStoryStage Stage { get; private set; } = AfterTheGlowStoryStage.WakeTheOwner;
    public string StatusMessage { get; private set; } = "A / D 이동 · Space 점프 · S 엎드리기\n침대 가까이에서 클릭해 이불을 흔들어보자.";
    public float FrontDoorOpenness { get; private set; }
    public bool BathroomDoorOpen { get; private set; }
    public int BedWakeCount { get; private set; }
    public int PetCount { get; private set; }
    public int OwnerDepartureCount { get; private set; }
    public int GateReleaseCount { get; private set; }
    public int GateOpenCount { get; private set; }
    public int HouseExitCount { get; private set; }
    public int HeadlightOffCount { get; private set; }
    public int GateMemoryClickCount { get; private set; }
    public bool GateMemoryAwaitingClick => Stage == AfterTheGlowStoryStage.GateMemoryPuzzle &&
        !gatePuzzleCompleting && gatePuzzlePage % 2 == 0 && gatePuzzlePage <= 6;
    private float OwnerDoorWorldX => frontDoorPivot != null ? frontDoorPivot.position.x : frontDoorX;
    private float HomeCameraFocusY => cameraFollow != null && cameraFollow.lockY
        ? cameraFollow.lockedY
        : cameraFollow != null ? cameraFollow.transform.position.y : 0f;
    private float HomeCameraSize => cameraFollow != null
        ? cameraFollow.homeOrthographicSize
        : 3f;
    public float OwnerDoorStandX => OwnerDoorWorldX - ownerDoorStandOffset;
    private float OwnerDoorExitTargetX
    {
        get
        {
            float target = OwnerDoorWorldX + ownerDoorExitOffset;
            if (frontDoorPivot == null || ownerRenderer == null || ownerRenderer.sprite == null)
                return target;

            float actorHalfWidth = ownerRenderer.sprite.bounds.extents.x *
                Mathf.Abs(ownerRenderer.transform.lossyScale.x);
            float doorRightEdge = FrontDoorwayBounds.max.x;
            return Mathf.Max(target, doorRightEdge + actorHalfWidth + 0.08f);
        }
    }
    private Bounds FrontDoorwayBounds
    {
        get
        {
            Transform parent = frontDoorRenderer.transform.parent;
            Vector3 center = parent != null ? parent.TransformPoint(doorPanelClosedPosition) : doorPanelClosedPosition;
            float height = frontDoorClosedSprite.bounds.size.y * Mathf.Abs(doorPanelClosedScale.y) *
                (parent != null ? Mathf.Abs(parent.lossyScale.y) : 1f);
            return new Bounds(center, new Vector3(doorPanelWorldWidth, height, 1f));
        }
    }
    public float OwnerPetPlayerTargetX
    {
        get
        {
            if (!alignOwnerPetWithPlayer || ownerPetSprite == null || player == null || owner == null)
                return OwnerDoorStandX - 0.8f;
            float handLocalX = (ownerPetHandNormalized.x * ownerPetSprite.rect.width - ownerPetSprite.pivot.x) /
                ownerPetSprite.pixelsPerUnit;
            float scale = ownerPetHeight / ownerPetSprite.bounds.size.y;
            Collider2D playerCollider = player.BodyCollider != null ? player.BodyCollider : player.GetComponent<Collider2D>();
            float dogHeadOffset = playerCollider != null ? playerCollider.bounds.extents.x * 0.8f : 0.48f;
            // The petting artwork faces right; flip it toward the dog standing on the left.
            return owner.position.x - handLocalX * scale - dogHeadOffset;
        }
    }
    public float OwnerPreparationStartedAt { get; private set; } = -1f;
    public float OwnerPreparationRemainingSeconds => OwnerPreparationStartedAt < 0f ? 0f :
        Mathf.Max(0f, OwnerPreparationStartedAt + minimumOwnerPreparationSeconds - Time.time);

    private SpriteRenderer ownerRenderer;
    private SpriteRenderer frontDoorRenderer;
    private Vector3 doorPanelClosedPosition;
    private Vector3 doorPanelClosedScale;
    private float doorPanelWidth;
    private float doorPanelWorldWidth;
    private SpriteRenderer gateArtworkRenderer;
    private bool cutsceneCameraWasEnabled;
    private Vector3 lockedCameraPosition;
    private float lockedCameraOrthographicSize;
    private bool cameraSnapshotCaptured;
    private bool waitingForPetPuzzleClick;
    private bool petPuzzleComplete;
    private int bedLickCount;
    private int petStrokeCount;
    private int gatePuzzlePage;
    private float gatePuzzlePageTime;
    private float memory2ShakeUntil;
    private float gateBitePullDistance;
    private LeashBreakUI leashBreakUI;
    private bool gatePuzzleCompleting;
    private Texture2D activeGatePuzzleFrame;
    private Texture2D gateMemoryCleanFenceFrame;
    private Texture2D gateMemoryTornLeashFrame;
    private float fearUntil;
    private AudioClip doorSlamClip;
    private Coroutine activeRoutine;
    private Coroutine headlightRoutine;
    private bool ownerPreparationTargetReached;
    private bool waitingForPlayerProgress;
    private StoryScreenEffects screenEffects;

    private void Awake()
    {
        GameAudioManager.EnsureInstance();
        if (homeRoomLighting == null) homeRoomLighting = GetComponent<HomeRoomLighting>();
        if (pettingPuzzleUI == null)
            pettingPuzzleUI = GetComponent<PettingPuzzleUI>();
        if (pettingPuzzleUI == null)
            pettingPuzzleUI = gameObject.AddComponent<PettingPuzzleUI>();
        LoadGateMemoryFrames();
        leashBreakUI = GetComponent<LeashBreakUI>();
        if (leashBreakUI == null) leashBreakUI = gameObject.AddComponent<LeashBreakUI>();
        if (GetComponent<ParkFountainExploration>() == null) gameObject.AddComponent<ParkFountainExploration>();
        if (bedWakePuzzleUI == null)
            bedWakePuzzleUI = FindAnyObjectByType<BedWakePuzzleUI>();
        if (player == null)
            player = FindAnyObjectByType<PlayerMovement>();
        if (cameraFollow == null)
            cameraFollow = FindAnyObjectByType<CameraFollow>();
        if (cameraFollow != null)
        {
            screenEffects = cameraFollow.GetComponent<StoryScreenEffects>();
            if (screenEffects == null) screenEffects = cameraFollow.gameObject.AddComponent<StoryScreenEffects>();
        }
        if (owner != null)
            ownerRenderer = owner.GetComponentInChildren<SpriteRenderer>(true);
        if (frontDoorPivot != null)
        {
            Transform panel = frontDoorPivot.Find("Front Door Panel");
            frontDoorRenderer = panel != null ? panel.GetComponent<SpriteRenderer>() :
                frontDoorPivot.GetComponentInChildren<SpriteRenderer>(true);
            if (frontDoorRenderer != null)
            {
                doorPanelClosedPosition = frontDoorRenderer.transform.localPosition;
                doorPanelClosedScale = frontDoorRenderer.transform.localScale;
                doorPanelWidth = frontDoorRenderer.sprite != null
                    ? frontDoorRenderer.sprite.bounds.size.x * doorPanelClosedScale.x : 1f;
                doorPanelWorldWidth = frontDoorRenderer.sprite != null
                    ? frontDoorRenderer.sprite.bounds.size.x * Mathf.Abs(doorPanelClosedScale.x) *
                        Mathf.Abs(frontDoorRenderer.transform.parent != null
                            ? frontDoorRenderer.transform.parent.lossyScale.x : 1f)
                    : 1f;
            }
        }
        if (parkGate != null)
        {
            Transform artwork = parkGate.transform.Find("Gate Artwork");
            if (artwork != null)
                gateArtworkRenderer = artwork.GetComponent<SpriteRenderer>();
        }
    }

    private void Start()
    {
        if (bed != null)
            bed.PushCompleted += HandleBedPushed;
        if (parkGate != null)
        {
            parkGate.PullCompleted += HandleGateReleased;
            parkGate.PushCompleted += HandleGatePushed;
            parkGate.SetInteractionEnabled(false);
        }
        if (player != null)
        {
            player.RetreatCompleted += HandleRetreatCompleted;
            player.ControlModeChanged += HandlePlayerControlModeChanged;
        }

        if (bathroomLight != null)
            bathroomLight.gameObject.SetActive(false);
        SetBathroomDoorOpen(false);
        if (homeRoomLighting != null) homeRoomLighting.SetLightImmediate(false);
        SetOwnerSprite(ownerWalkSprite);
        SetFrontDoorSprite(frontDoorClosedSprite);
        if (frontDoorRenderer != null && frontDoorRenderer.sprite != null)
        {
            doorPanelWorldWidth = frontDoorRenderer.sprite.bounds.size.x * Mathf.Abs(doorPanelClosedScale.x) *
                Mathf.Abs(frontDoorRenderer.transform.parent != null
                    ? frontDoorRenderer.transform.parent.lossyScale.x : 1f);
        }
        ApplyFrontDoorPose(0f);
        SetGateSprite(gateBoundSprite);
        if (owner != null)
            owner.gameObject.SetActive(false);
        if (rollingBall != null)
        {
            rollingBall.SetActive(true);
        }
        if (carHeadlight != null)
            carHeadlight.SetActive(true);
        GameAudioManager.Instance?.PlayAmbience("RoomTone", .8f);
        GameAudioManager.Instance?.PlayMusic("AtmosphericEmotionalPiano", .30f);
    }

    private void Update()
    {
        if (Stage == AfterTheGlowStoryStage.OwnerPreparing && waitingForPlayerProgress &&
            !ownerPreparationTargetReached && player != null &&
            player.transform.position.x >= ownerPreparationTargetX)
        {
            ReachOwnerPreparationTarget();
        }

        bool clicked = !SettingsMenuController.IsOpen && PlayerMovement.WasPrimaryMousePressedThisFrame();
        if (Stage == AfterTheGlowStoryStage.OwnerPetPuzzle && pettingPuzzleUI != null)
            petStrokeCount = Mathf.FloorToInt(pettingPuzzleUI.Progress * 4f);
        if (Stage == AfterTheGlowStoryStage.GateMemoryPuzzle)
            UpdateGateMemoryPuzzleInput(clicked);
        if (Stage == AfterTheGlowStoryStage.GateBitePull)
        {
            var interaction = player != null ? player.GetComponent<PlayerInteraction>() : null;
            if (interaction == null || interaction.ActiveTarget != parkGate)
            {
                gateBitePullDistance = 0f;
                Stage = AfterTheGlowStoryStage.GateBound;
            }
        }
    }

    private void OnDestroy()
    {
        if (bed != null)
            bed.PushCompleted -= HandleBedPushed;
        if (parkGate != null)
        {
            parkGate.PullCompleted -= HandleGateReleased;
            parkGate.PushCompleted -= HandleGatePushed;
        }
        if (player != null)
        {
            player.RetreatCompleted -= HandleRetreatCompleted;
            player.ControlModeChanged -= HandlePlayerControlModeChanged;
            player.LightRetreatEnabled = true;
        }
        if (doorSlamClip != null)
            Destroy(doorSlamClip);
    }

    internal bool BeginStreetDeparture()
    {
        if (Stage != AfterTheGlowStoryStage.HouseExitReady || activeRoutine != null)
            return false;

        HouseExitCount++;
        activeRoutine = StartCoroutine(StreetDepartureSequence());
        return true;
    }

    public bool TryBeginStoryPuzzle(PushableObject target)
    {
        if (target == null || player == null)
            return false;

        if (target == bed && Stage == AfterTheGlowStoryStage.WakeTheOwner)
        {
            GameAudioManager.Instance?.PlaySfx("BedBlanket", .62f, Random.Range(.94f, 1.04f));
            Stage = AfterTheGlowStoryStage.BedWakePuzzle;
            bedLickCount = 0;
            player.SetControlMode(PlayerControlMode.CutsceneLocked);
            LockCamera();
            if (bedWakePuzzleUI != null)
                bedWakePuzzleUI.Open(this);
            else
                Debug.LogError("Puzzle 01 touch screen is missing from the active scene.", this);
            StatusMessage = string.Empty;
            return true;
        }

        if (target == parkGate && Stage == AfterTheGlowStoryStage.GateBound)
        {
            gateBitePullDistance = 0f;
            Stage = AfterTheGlowStoryStage.GateBitePull;
            // Let PlayerInteraction latch the leash and draw its mouth connection.
            return false;
        }

        return false;
    }

    public bool CaptureGateBitePull(PushableObject target, float distance)
    {
        if (target != parkGate || Stage != AfterTheGlowStoryStage.GateBitePull)
            return false;
        gateBitePullDistance += Mathf.Max(0f, distance);
        if (gateBitePullDistance >= .45f)
        {
            player.GetComponent<PlayerInteraction>().CancelCurrentInteraction();
            Stage = AfterTheGlowStoryStage.GateMemoryPuzzle;
            player.SetControlMode(PlayerControlMode.CutsceneLocked);
            Vector3 focus = player.transform.position;
            if (parkGate != null)
                focus.x = (player.transform.position.x + parkGate.transform.position.x) * 0.5f;
            FocusCameraOn(focus, 2.8f);
            gatePuzzleCompleting = false;
            GateMemoryClickCount = 0;
            SetGateMemoryPage(0);
            gatePuzzlePageTime = Time.time;
            StatusMessage = "철창에 묶인 목줄을 가까이에서 살펴본다.";
        }
        return true;
    }

    private void LoadGateMemoryFrames()
    {
        if (gateMemoryFrames == null || gateMemoryFrames.Length != 8)
            gateMemoryFrames = new Texture2D[8];

        string[] names =
        {
            "01_목줄_첫번째클릭", "02_회상1_다가오는신발",
            "03_목줄_두번째클릭", "04_회상2_손을멈추는주인",
            "05_목줄_세번째클릭", "06_회상3_벤치에서기다리는주인",
            "07_목줄_마지막당기기로끊기", "08_회상4_손끝냄새를맡기"
        };

        for (int i = 0; i < names.Length; i++)
        {
            if (gateMemoryFrames[i] == null)
                gateMemoryFrames[i] = Resources.Load<Texture2D>("Puzzle03/" + names[i]);
        }

        gateMemoryCleanFenceFrame = Resources.Load<Texture2D>("Puzzle03/LeashGateClean");
        gateMemoryTornLeashFrame = Resources.Load<Texture2D>("Puzzle03/LeashTornHalves");
    }

    public int AdvanceBedWakePuzzleFromTouch()
    {
        if (Stage != AfterTheGlowStoryStage.BedWakePuzzle || bed == null)
            return bedLickCount;

        bedLickCount = Mathf.Min(4, bedLickCount + 1);
        bed.PlayStoryVisualPulse(player != null ? player.FacingDirection : 1);
        if (bedLickCount < 4)
            return bedLickCount;

        UnlockCameraAndControls();
        bed.CompleteStoryVisualInteraction();
        return bedLickCount;
    }

    private void BeginOwnerPetPuzzle()
    {
        waitingForPetPuzzleClick = false;
        Stage = AfterTheGlowStoryStage.OwnerPetPuzzle;
        petPuzzleComplete = false;
        petStrokeCount = 0;
        if (player != null && owner != null)
            player.FaceTowards(owner.position.x);
        if (owner != null)
            SetOwnerSprite(ownerPetSprite);
        // Keep the preceding eased shot while the portrait rises into view.
        pettingPuzzleUI.Open(() =>
        {
            if (Stage != AfterTheGlowStoryStage.OwnerPetPuzzle) return;
            petStrokeCount = 4;
            petPuzzleComplete = true;
        });
        StatusMessage = string.Empty;
    }

    private void HandleOwnerPetPointerEvent(Event pointerEvent)
    {
        if (SettingsMenuController.IsOpen || pointerEvent == null || pointerEvent.button != 0)
            return;
        if (Stage == AfterTheGlowStoryStage.OwnerAtTheDoor && waitingForPetPuzzleClick &&
            pointerEvent.type == EventType.MouseDown)
        {
            BeginOwnerPetPuzzle();
            pointerEvent.Use();
            return;
        }
    }

    private void UpdateGateMemoryPuzzleInput(bool clicked)
    {
        if (gatePuzzleCompleting) return;
        float elapsed = Time.time - gatePuzzlePageTime;
        if (gatePuzzlePage % 2 == 1)
        {
            float duration = gatePuzzlePage == 3 ? 2.6f : gatePuzzlePage == 7 ? 2f : 1.6f;
            if (elapsed < duration) return;
            if (gatePuzzlePage == 7) BreakLeashAndOpenGate();
            else SetGateMemoryPage(gatePuzzlePage + 1);
            return;
        }
        if (!clicked || elapsed < .25f) return;
        GateMemoryClickCount++;
        SetGateMemoryPage(gatePuzzlePage + 1);
    }
    private void SetGateMemoryPage(int page)
    {
        gatePuzzlePage = page;
        gatePuzzlePageTime = Time.time;
        int frameIndex = page switch
        {
            0 => 0,
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 5,
            6 => 4,
            7 => 7,
            8 => 4,
            9 => 6,
            _ => 0
        };
        activeGatePuzzleFrame = page == 9 && gateMemoryCleanFenceFrame != null
            ? gateMemoryCleanFenceFrame
            : gateMemoryFrames != null && frameIndex < gateMemoryFrames.Length
                ? gateMemoryFrames[frameIndex]
                : null;
        if (page == 7)
            GameAudioManager.Instance?.PlaySfx("DogSniff", .52f, .92f);
        if (page == 3)
            memory2ShakeUntil = Time.time + 0.22f;
    }

    private void BreakLeashAndOpenGate()
    {
        if (gatePuzzleCompleting || parkGate == null)
            return;

        gatePuzzleCompleting = true;
        player.SetControlMode(PlayerControlMode.CutsceneLocked);
        activeGatePuzzleFrame = null;
        activeRoutine = StartCoroutine(FinishLeashMemoryAndOpenGate());
    }

    private IEnumerator FinishLeashMemoryAndOpenGate()
    {
        leashBreakUI.LeashScale = gateMemoryLeashScale;
        yield return leashBreakUI.Play(() => { parkGate.BeginInteraction(); parkGate.AddPullDistance(parkGate.PullDistanceToRelease); });
        activeGatePuzzleFrame = null;
        if (player != null)
            player.SetControlMode(PlayerControlMode.CutsceneLocked);
        parkGate.PlayTetherReleaseParticles();

        Vector3 gateStart = parkGate.transform.localPosition;
        for (int i = 0; i < 4; i++)
        {
            parkGate.transform.localPosition = gateStart + Vector3.right * (i % 2 == 0 ? 0.035f : -0.035f);
            yield return new WaitForSeconds(0.055f);
        }
        parkGate.transform.localPosition = gateStart;

        UnlockCameraAndControls();
        parkGate.SetInteractionEnabled(true);
        StatusMessage = "목줄이 끊어졌다. 직접 문을 밀고 공원으로 들어갈 수 있다.";
        activeRoutine = null;
    }

    private void FixedUpdate()
    {
        if (Stage != AfterTheGlowStoryStage.GateReleased || activeRoutine != null ||
            player == null || !player.AllowsPlayerInput || !player.IsGrounded ||
            Mathf.Abs(player.HorizontalInput) < .05f) return;
        var interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null && interaction.IsInteractionActive) return;
        parkGate.BeginInteraction(player.FacingDirection);
        parkGate.TryPush(player.BodyCollider, player.HorizontalInput, Time.fixedDeltaTime);
    }
    private static bool IsPrimaryMouseHeld()
    {
        return Mouse.current != null ? Mouse.current.leftButton.isPressed : Input.GetMouseButton(0);
    }

    public bool ReachOwnerPreparationTarget()
    {
        if (Stage != AfterTheGlowStoryStage.OwnerPreparing || !waitingForPlayerProgress ||
            ownerPreparationTargetReached || player == null ||
            player.transform.position.x < ownerPreparationTargetX)
            return false;

        ownerPreparationTargetReached = true;
        waitingForPlayerProgress = false;
        StatusMessage = "복도를 따라 주인이 있는 쪽으로 간다. 공은 길에 놓인 생활 물체다.";
        return true;
    }

    public bool EnterParkPlaceholder()
    {
        if (Stage != AfterTheGlowStoryStage.GatePushed)
            return false;

        Stage = AfterTheGlowStoryStage.ParkPlaceholder;
        StatusMessage = "공원 입구에 도착했다. 이 지점에서 다음 맵을 연결할 수 있다.";
        GameAudioManager.Instance?.PlayAmbience("ParkBirds", .68f);
        GameAudioManager.Instance?.PlaySecondaryAmbience(null);
        if (player != null)
            player.SetControlMode(PlayerControlMode.Free);
        if (cameraFollow != null)
            cameraFollow.enabled = true;
        return true;
    }

    private void HandleBedPushed(PushableObject _)
    {
        if (Stage != AfterTheGlowStoryStage.WakeTheOwner &&
            Stage != AfterTheGlowStoryStage.BedWakePuzzle)
            return;

        BedWakeCount++;
        activeRoutine = StartCoroutine(BedAndOwnerSequence());
    }

    private IEnumerator BedAndOwnerSequence()
    {
        Stage = AfterTheGlowStoryStage.OwnerPreparing;
        ownerPreparationTargetReached = false;
        waitingForPlayerProgress = false;
        StatusMessage = "주인이 일어나 벽 스위치로 방의 불을 켠다.";
        if (player != null) player.SetControlMode(PlayerControlMode.CutsceneLocked);

        if (owner != null)
        {
            if (bed != null)
                owner.position = new Vector3(bed.transform.position.x, owner.position.y, owner.position.z);
            owner.gameObject.SetActive(true);
            SetOwnerSprite(ownerWalkSprite);
            if (homeRoomLighting != null && homeRoomLighting.wallSwitch != null)
            {
                float focusX = (owner.position.x + homeRoomLighting.OwnerSwitchStandX) * .5f;
                yield return PanCameraTo(new Vector3(focusX, HomeCameraFocusY, 0f), HomeCameraSize, .6f);
                // Keep the room dim while the owner stands up and walks to the switch.
                yield return new WaitForSeconds(.35f);
                yield return MoveActorToX(owner, homeRoomLighting.OwnerSwitchStandX, actorMoveSpeed);
                yield return new WaitForSeconds(.12f);
                yield return homeRoomLighting.PlayOwnerSwitch(owner);
            }
        }

        UnlockCameraAndControls();
        StatusMessage = "불을 켠 주인이 화장실로 들어간다.";
        if (owner != null)
        {
            yield return MoveActorToX(owner, bathroomX - 0.45f, actorMoveSpeed);
            owner.gameObject.SetActive(false);
        }
        if (bathroomLight != null) bathroomLight.gameObject.SetActive(true);
        SetBathroomDoorOpen(true);

        waitingForPlayerProgress = true;
        OwnerPreparationStartedAt = Time.time;
        StatusMessage = "주인이 준비하는 동안 복도를 따라간다. 공은 지나가며 건드려도 진행 조건이 아니다.";
        // The owner comes out after a short preparation beat, regardless of whether
        // the player crossed the old hallway progress marker or touched the ball.
        yield return new WaitUntil(() => OwnerPreparationRemainingSeconds <= 0f);

        Stage = AfterTheGlowStoryStage.OwnerAtTheDoor;
        StatusMessage = "주인이 화장실에서 나와 현관으로 향한다.";
        if (player != null)
            player.SetControlMode(PlayerControlMode.CutsceneLocked);

        yield return OwnerLeavesBathroom();

        waitingForPetPuzzleClick = false;
        if (player != null && owner != null)
        {
            player.BeginAutoMoveToX(OwnerPetPlayerTargetX, 2.8f, PlayerControlMode.CutsceneLocked);
            yield return WaitForPlayerAutoMove(PlayerControlMode.CutsceneLocked);
            player.FaceTowards(owner.position.x);
        }

        // Show the interaction pose before waiting, instead of freezing a walk frame.
        if (owner != null)
            SetOwnerSprite(ownerPetSprite);

        petPuzzleComplete = false;
        Vector3 petFocus = owner != null ? owner.position : player.transform.position;
        if (owner != null && player != null)
            petFocus.x = (player.transform.position.x + owner.position.x) * 0.5f;
        petFocus.y = HomeCameraFocusY;
        yield return PanCameraTo(petFocus, Mathf.Min(HomeCameraSize, 2.65f), 0.28f);
        waitingForPetPuzzleClick = true;
        StatusMessage = "화면이 강아지 가까이로 다가온다. 클릭하면 쓰다듬기 퍼즐이 시작된다.";
        yield return new WaitUntil(() => petPuzzleComplete);

        yield return OwnerLeavesHouse();
    }

    private IEnumerator OwnerLeavesHouse()
    {
        Stage = AfterTheGlowStoryStage.OwnerDeparture;
        PetCount++;
        yield return new WaitForSeconds(petSeconds * 0.45f);

        StatusMessage = "주인이 집을 나간다.";
        yield return PanCameraTo(new Vector3(OwnerDoorWorldX, HomeCameraFocusY, 0f),
            HomeCameraSize, 0.35f);
        SetOwnerSprite(ownerWalkSprite);
        yield return AnimateFrontDoor(1f, doorOpenSeconds);
        if (owner != null)
            yield return WalkOwnerThroughFrontDoor();

        OwnerDepartureCount++;
        yield return new WaitForSeconds(0.16f);
        if (owner != null)
            owner.gameObject.SetActive(false);
        yield return AnimateFrontDoor(0f, doorCloseSeconds);
        PlayDoorSlam();
        screenEffects?.DoorImpact();
        yield return ReboundFrontDoor();
        yield return new WaitForSeconds(0.2f);
        if (owner != null)
            owner.gameObject.SetActive(false);

        Stage = AfterTheGlowStoryStage.HouseExitReady;
        StatusMessage = "열린 현관문을 직접 선택해 주인을 따라 나간다.";
        activeRoutine = null;
        UnlockCameraAndControls();
    }

    private IEnumerator WalkOwnerThroughFrontDoor()
    {
        if (frontDoorRenderer == null || ownerRenderer == null)
        {
            yield return MoveActorToX(owner, OwnerDoorExitTargetX, actorMoveSpeed * 1.25f);
            owner.gameObject.SetActive(false);
            yield break;
        }

        Bounds doorway = FrontDoorwayBounds;
        float groundY = owner.position.y;
        float doorwayY = groundY + doorway.min.y + .015f - ownerRenderer.bounds.min.y;
        float openLeft = doorway.min.x + doorway.size.x * .12f;
        float openCenter = (openLeft + doorway.max.x) * .5f;
        // Only the departing owner goes behind the wall to the right of the jamb.
        // The dog and the room keep their normal foreground ordering.
        var previousMaterial = ownerRenderer.sharedMaterial;
        var doorwayMaterial = new Material(Resources.Load<Shader>("OwnerDoorwayExit"));
        doorwayMaterial.SetFloat("_DoorRight", doorway.max.x);
        ownerRenderer.sharedMaterial = doorwayMaterial;
        try
        {
            var walk = MoveActorToX(owner, OwnerDoorExitTargetX, actorMoveSpeed * 1.25f);
            while (walk.MoveNext())
            {
                Vector3 position = owner.position;
                float depth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(openLeft, openCenter, position.x));
                position.y = Mathf.Lerp(groundY, doorwayY, depth);
                owner.position = position;
                yield return walk.Current;
            }
            // Remain hidden before releasing the wall mask, including the closing beat.
            owner.gameObject.SetActive(false);
        }
        finally
        {
            ownerRenderer.sharedMaterial = previousMaterial;
            Destroy(doorwayMaterial);
            Vector3 position = owner.position; position.y = groundY; owner.position = position;
        }
    }

    private void SetBathroomDoorOpen(bool open)
    {
        BathroomDoorOpen = open;
        var artwork = GetComponent<JourneyWorldArtPresentation>();
        if (artwork != null) artwork.SetBathroomDoor(open);
    }

    private IEnumerator OwnerLeavesBathroom()
    {
        if (owner != null)
        {
            owner.gameObject.SetActive(true);
            SetOwnerSprite(ownerWalkSprite);
            owner.position = new Vector3(bathroomX, owner.position.y, owner.position.z);

            float routeCenterX = (bathroomX + OwnerDoorStandX) * 0.5f;
            yield return PanCameraTo(new Vector3(routeCenterX, HomeCameraFocusY, 0f), HomeCameraSize, .65f);

            // Close behind the owner as soon as the whole sprite clears the doorway.
            var artwork = GetComponent<JourneyWorldArtPresentation>();
            float clearance = artwork != null && artwork.bathroomDoor != null && ownerRenderer != null
                ? artwork.bathroomDoor.bounds.extents.x + ownerRenderer.bounds.extents.x + .05f : .65f;
            float exitX = Mathf.MoveTowards(bathroomX, OwnerDoorStandX, clearance);
            yield return MoveActorToX(owner, exitX, actorMoveSpeed);
        }

        SetBathroomDoorOpen(false);
        if (bathroomLight != null && bathroomLight.gameObject.activeSelf)
        {
            bathroomLight.gameObject.SetActive(false);
            GameAudioManager.Instance?.PlaySfx("LightSwitchOff", .5f, .98f);
        }

        if (owner != null)
            yield return MoveActorToX(owner, OwnerDoorStandX, actorMoveSpeed);
    }

    private IEnumerator StreetDepartureSequence()
    {
        Stage = AfterTheGlowStoryStage.StreetDeparture;
        GameAudioManager.Instance?.PlayAmbience("StreetTraffic", .60f);
        GameAudioManager.Instance?.PlaySecondaryAmbience("DieselIdle", .23f);
        if (rollingBall != null)
            rollingBall.SetActive(false);
        StatusMessage = "화면 끝에서 주인이 사라지는 것을 지켜본다.";
        if (player != null)
        {
            player.SetControlMode(PlayerControlMode.CutsceneLocked);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Vector2 entry = new Vector2(streetEntryPlayerX, streetEntryPlayerY);
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = entry;
            }
            player.transform.position = new Vector3(entry.x, entry.y, player.transform.position.z);
            Physics2D.SyncTransforms();
        }
        if (cameraFollow != null)
        {
            // The home close-up snapshot must not be restored after switching areas.
            cameraSnapshotCaptured = false;
            cameraFollow.enabled = true;
            cameraFollow.SwitchToExteriorFraming();
        }
        LockCamera();

        if (owner != null)
        {
            owner.gameObject.SetActive(true);
            SetOwnerSprite(ownerWalkSprite);
            float cameraRightEdge = GetExteriorCameraRightEdge();
            float ownerStartX = cameraRightEdge - 0.8f;
            float ownerEndX = cameraRightEdge + 4f;
            owner.position = new Vector3(ownerStartX, owner.position.y, owner.position.z);
            yield return MoveActorToX(owner, ownerEndX, actorMoveSpeed * 1.4f);
            owner.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.45f);
        Stage = AfterTheGlowStoryStage.FollowOwner;
        StatusMessage = "주인을 따라간다. 차량의 쌍라이트를 정면으로 받지 않도록 살펴보자.";
        UnlockCameraAndControls();

        activeRoutine = null;
    }

    private void HandleRetreatCompleted()
    {
        if ((Stage != AfterTheGlowStoryStage.FollowOwner &&
             Stage != AfterTheGlowStoryStage.HeadlightRetreat &&
             Stage != AfterTheGlowStoryStage.HeadlightWait) || carHeadlight == null ||
            !carHeadlight.activeInHierarchy)
            return;

        Stage = AfterTheGlowStoryStage.HeadlightWait;
        player.LightRetreatEnabled = true;
        StatusMessage = "뒤로 물러났다. 잠시 기다리면 차량 불빛이 꺼진다.";
        if (headlightRoutine == null)
            headlightRoutine = StartCoroutine(TurnOffHeadlightAfterDelay());
    }

    private void HandlePlayerControlModeChanged(PlayerControlMode mode)
    {
        if (mode == PlayerControlMode.LightRetreat &&
            (Stage == AfterTheGlowStoryStage.FollowOwner || Stage == AfterTheGlowStoryStage.HeadlightWait) &&
            carHeadlight != null && carHeadlight.activeInHierarchy)
        {
            Stage = AfterTheGlowStoryStage.HeadlightRetreat;
            StatusMessage = "강한 빛 앞에서 뒤로 물러난다.";
            fearUntil = Time.unscaledTime + 1.45f;
            screenEffects?.HeadlightShock();
        }
    }

    private float GetExteriorCameraRightEdge()
    {
        Camera camera = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : null;
        if (camera != null && camera.orthographic)
            return camera.transform.position.x + camera.orthographicSize * camera.aspect;

        return ownerStreetEndX - 0.5f;
    }

    private IEnumerator TurnOffHeadlightAfterDelay()
    {
        yield return new WaitForSeconds(headlightOffDelay);
        if (carHeadlight != null)
            carHeadlight.SetActive(false);
        GameAudioManager.Instance?.PlaySecondaryAmbience(null);
        HeadlightOffCount++;
        if (player != null)
            player.LightRetreatEnabled = true;
        if (Stage == AfterTheGlowStoryStage.HeadlightWait || Stage == AfterTheGlowStoryStage.HeadlightRetreat)
        {
            Stage = AfterTheGlowStoryStage.GateBound;
            if (parkGate != null)
                parkGate.SetInteractionEnabled(true);
            StatusMessage = "전조등이 꺼졌다. 공원 문을 묶은 목줄 가까이로 가자.";
        }
        headlightRoutine = null;
    }

    private void HandleGateReleased(PushableObject _)
    {
        if (Stage != AfterTheGlowStoryStage.GateBound &&
            Stage != AfterTheGlowStoryStage.GateMemoryPuzzle)
            return;

        GateReleaseCount++;
        Stage = AfterTheGlowStoryStage.GateReleased;
        SetGateSprite(gateLooseSprite);
        StatusMessage = "철그덕! 회상이 끝나고 목줄이 끊어졌다.";
        GameAudioManager.Instance?.PlaySfx("GateRattle", .36f, .94f);
        GameAudioManager.Instance?.PlaySfx("GateClank", .32f, .86f);
    }

    private void HandleGatePushed(PushableObject _)
    {
        if (Stage != AfterTheGlowStoryStage.GateReleased)
            return;

        GateOpenCount++;
        Stage = AfterTheGlowStoryStage.GatePushed;
        SetGateSprite(gateOpenSprite != null ? gateOpenSprite : gateBrokenSprite);
        StatusMessage = "강아지가 철문 한쪽을 밀어 공원 입구를 연다.";
        GameAudioManager.Instance?.PlaySfx("IronDoor", .56f, .9f);
    }

    private void SetOwnerSprite(Sprite sprite)
    {
        var animation = GetComponent<OwnerAnimationPresentation>();
        if (animation != null) animation.SetPetting(sprite == ownerPetSprite);
        if (ownerRenderer != null && sprite != null)
        {
            ownerRenderer.sprite = sprite;
            if (alignOwnerPetWithPlayer)
            {
                bool petting = sprite == ownerPetSprite;
                float scale = (petting ? ownerPetHeight : ownerStandingHeight) / sprite.bounds.size.y;
                Vector3 parentScale = ownerRenderer.transform.parent.lossyScale;
                ownerRenderer.transform.localScale = new Vector3(scale / Mathf.Abs(parentScale.x),
                    scale / Mathf.Abs(parentScale.y), 1f);
                ownerRenderer.transform.localPosition = new Vector3(0f, ownerVisualGroundOffset, -0.2f);
                ownerRenderer.flipX = petting;
            }
        }
    }

    private void SetFrontDoorSprite(Sprite sprite)
    {
        if (frontDoorRenderer != null && sprite != null)
            frontDoorRenderer.sprite = sprite;
    }

    private IEnumerator AnimateFrontDoor(float openness, float seconds)
    {
        float start = FrontDoorOpenness;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, seconds));
            float eased = openness < start ? t * t : Mathf.SmoothStep(0f, 1f, t);
            ApplyFrontDoorPose(Mathf.Lerp(start, openness, eased));
            yield return null;
        }
        ApplyFrontDoorPose(openness);
    }

    private IEnumerator ReboundFrontDoor()
    {
        float duration = Mathf.Max(.38f, doorReboundSeconds * 3.4f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float spring = 1f - Mathf.Exp(-6f * t) * Mathf.Cos(10f * t);
            ApplyFrontDoorPose(doorAjarAmount * spring);
            yield return null;
        }
        ApplyFrontDoorPose(doorAjarAmount);
    }

    private void ApplyFrontDoorPose(float openness)
    {
        FrontDoorOpenness = Mathf.Clamp01(openness);
        if (frontDoorPivot != null)
            frontDoorPivot.localRotation = Quaternion.identity;
        if (frontDoorRenderer == null)
            return;
        // Transform one continuous leaf through the slam and recoil. Swapping
        // silhouettes at 1% openness made the impact look like a second opening.
        SetFrontDoorSprite(frontDoorClosedSprite);
        float widthFraction = Mathf.Lerp(1f, 0.12f, FrontDoorOpenness);
        Vector3 scale = doorPanelClosedScale;
        scale.x *= widthFraction;
        frontDoorRenderer.transform.localScale = scale;
        // House door: approved opening is on the RIGHT. The garden gate has its
        // own independent left-leaf presentation; do not mirror this with that gate.
        frontDoorRenderer.transform.localPosition = doorPanelClosedPosition +
            Vector3.left * (doorPanelWidth * 0.5f * (1f - widthFraction));
    }

    private void SetGateSprite(Sprite sprite)
    {
        GateHingePresentation hinge = parkGate != null ? parkGate.GetComponent<GateHingePresentation>() : null;
        if (hinge != null)
        {
            if (sprite == gateBoundSprite) hinge.ResetPose();
            else if (sprite == gateLooseSprite) hinge.SetReleased(true);
            else if (sprite == gateOpenSprite) hinge.SetOpenProgress(1f);
            return;
        }
        if (gateArtworkRenderer != null && sprite != null)
            gateArtworkRenderer.sprite = sprite;
    }

    private IEnumerator WaitForPlayerAutoMove(PlayerControlMode modeOnTimeout = PlayerControlMode.Free, float timeoutSeconds = 4.5f)
    {
        float deadline = Time.time + timeoutSeconds;
        while (player != null && player.IsAutoMoving && Time.time < deadline)
            yield return null;
        if (player != null && player.IsAutoMoving)
            player.SetControlMode(modeOnTimeout);
    }

    private IEnumerator MoveActorToX(Transform actor, float targetX, float speed)
    {
        if (actor == null)
            yield break;

        float nextStepAt = Time.time;
        while (Mathf.Abs(actor.position.x - targetX) > 0.035f)
        {
            float direction = Mathf.Sign(targetX - actor.position.x);
            Vector3 position = actor.position;
            position.x = Mathf.MoveTowards(position.x, targetX, speed * Time.deltaTime);
            actor.position = position;
            Vector3 scale = actor.localScale;
            scale.x = Mathf.Abs(scale.x) * direction;
            actor.localScale = scale;
            if (actor == owner && Time.time >= nextStepAt)
            {
                GameAudioManager.Instance?.PlaySfx("WoodSteps", .42f, Random.Range(.94f, 1.04f));
                nextStepAt = Time.time + .36f;
            }
            yield return null;
        }

        Vector3 finalPosition = actor.position;
        finalPosition.x = targetX;
        actor.position = finalPosition;
    }

    private void LockCamera()
    {
        if (cameraFollow == null)
            return;

        if (!cameraSnapshotCaptured)
        {
            lockedCameraPosition = cameraFollow.transform.position;
            cutsceneCameraWasEnabled = cameraFollow.enabled;
            Camera attachedCamera = cameraFollow.GetComponent<Camera>();
            lockedCameraOrthographicSize = attachedCamera != null && attachedCamera.orthographic
                ? attachedCamera.orthographicSize
                : 4.2f;
            cameraSnapshotCaptured = true;
        }
        cameraFollow.enabled = false;
    }

    private void FocusCameraOn(Vector3 focus, float orthographicSize)
    {
        if (cameraFollow == null)
            return;

        LockCamera();
        Camera attachedCamera = cameraFollow.GetComponent<Camera>();
        float fittedSize = ClampHomeOrthographicSize(orthographicSize, attachedCamera);
        if (attachedCamera != null && attachedCamera.orthographic)
            attachedCamera.orthographicSize = fittedSize;
        Vector3 position = cameraFollow.transform.position;
        position.x = ClampHomeCameraX(focus.x, fittedSize, attachedCamera);
        position.y = focus.y;
        cameraFollow.transform.position = position;
    }

    private IEnumerator PanCameraTo(Vector3 focus, float orthographicSize, float duration)
    {
        if (cameraFollow == null)
            yield break;

        LockCamera();
        Camera attachedCamera = cameraFollow.GetComponent<Camera>();
        Vector3 startPosition = cameraFollow.transform.position;
        float startSize = attachedCamera != null && attachedCamera.orthographic
            ? attachedCamera.orthographicSize
            : orthographicSize;
        startSize = ClampHomeOrthographicSize(startSize, attachedCamera);
        startPosition.x = ClampHomeCameraX(startPosition.x, startSize, attachedCamera);
        cameraFollow.transform.position = startPosition;
        if (attachedCamera != null && attachedCamera.orthographic)
            attachedCamera.orthographicSize = startSize;

        float targetSize = ClampHomeOrthographicSize(orthographicSize, attachedCamera);
        float targetX = ClampHomeCameraX(focus.x, targetSize, attachedCamera);
        Vector3 targetPosition = new Vector3(targetX, focus.y, startPosition.z);

        if (duration <= 0f)
        {
            cameraFollow.transform.position = targetPosition;
            if (attachedCamera != null && attachedCamera.orthographic)
                attachedCamera.orthographicSize = targetSize;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress * (3f - 2f * progress);
            float frameSize = ClampHomeOrthographicSize(Mathf.Lerp(startSize, targetSize, eased), attachedCamera);
            Vector3 framePosition = Vector3.Lerp(startPosition, targetPosition, eased);
            framePosition.x = ClampHomeCameraX(framePosition.x, frameSize, attachedCamera);
            cameraFollow.transform.position = framePosition;
            if (attachedCamera != null && attachedCamera.orthographic)
                attachedCamera.orthographicSize = frameSize;
            yield return null;
        }

        cameraFollow.transform.position = targetPosition;
        if (attachedCamera != null && attachedCamera.orthographic)
            attachedCamera.orthographicSize = targetSize;
    }

    private float ClampHomeOrthographicSize(float orthographicSize, Camera attachedCamera)
    {
        if (cameraFollow == null || attachedCamera == null || !attachedCamera.orthographic)
            return orthographicSize;

        float mapWidth = cameraFollow.homeMapMaxX - cameraFollow.mapMinX;
        if (mapWidth <= 0f)
            return orthographicSize;

        float maximumSize = mapWidth / (2f * Mathf.Max(.01f, attachedCamera.aspect));
        return Mathf.Min(Mathf.Max(.01f, orthographicSize), maximumSize);
    }

    private float ClampHomeCameraX(float worldX, float orthographicSize, Camera attachedCamera)
    {
        if (cameraFollow == null || attachedCamera == null || !attachedCamera.orthographic)
            return worldX;

        float halfWidth = orthographicSize * attachedCamera.aspect;
        float minimumCenter = cameraFollow.mapMinX + halfWidth;
        float maximumCenter = cameraFollow.homeMapMaxX - halfWidth;
        if (maximumCenter <= minimumCenter)
            return (cameraFollow.mapMinX + cameraFollow.homeMapMaxX) * 0.5f;
        return Mathf.Clamp(worldX, minimumCenter, maximumCenter);
    }

    private void RestoreCameraPoseOnly(bool restoreFollow)
    {
        if (cameraFollow == null || !cameraSnapshotCaptured)
            return;

        cameraFollow.transform.position = lockedCameraPosition;
        Camera attachedCamera = cameraFollow.GetComponent<Camera>();
        if (attachedCamera != null && attachedCamera.orthographic)
            attachedCamera.orthographicSize = lockedCameraOrthographicSize;
        cameraFollow.enabled = restoreFollow && cutsceneCameraWasEnabled;
        cameraSnapshotCaptured = false;
    }

    private void UnlockCameraAndControls()
    {
        if (player != null)
            player.SetControlMode(PlayerControlMode.Free);

        if (cameraFollow != null && cameraSnapshotCaptured &&
            (Stage == AfterTheGlowStoryStage.BedWakePuzzle ||
             Stage == AfterTheGlowStoryStage.OwnerPreparing || Stage == AfterTheGlowStoryStage.HouseExitReady))
        {
            cameraSnapshotCaptured = false;
            if (cutsceneCameraWasEnabled) cameraFollow.ResumeFromCurrentPose();
        }
        else RestoreCameraPoseOnly(true);
    }

    private void PlayDoorSlam()
    {
        if (!GameAudioManager.NonMusicAudioEnabled)
            return;
        if (doorSlamClip == null)
            doorSlamClip = CreateDoorCloseClip();
        GameAudioManager.Instance?.PlayClip(doorSlamClip, GameAudioBus.Effects, .44f);
    }

    private AudioClip CreateDoorCloseClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.62f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        uint randomState = 0x9E3779B9u;
        float filteredNoise = 0f;
        float peak = 0.0001f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            float whiteNoise = (randomState & 0x00FFFFFFu) / 8388607.5f - 1f;
            filteredNoise = Mathf.Lerp(filteredNoise, whiteNoise, 0.16f);

            // Low wooden panel resonances give the impact body without a held synth tone.
            float body = Mathf.Exp(-t * 10f) * (
                Mathf.Sin(2f * Mathf.PI * 74f * t) * 0.48f +
                Mathf.Sin(2f * Mathf.PI * 119f * t) * 0.28f +
                Mathf.Sin(2f * Mathf.PI * 187f * t) * 0.12f);

            // A soft broadband hit, followed by a small latch click and frame rattle.
            float impact = (filteredNoise * 0.5f + whiteNoise * 0.12f) * Mathf.Exp(-t * 68f);
            float latchTime = t - 0.095f;
            float latch = latchTime >= 0f
                ? (Mathf.Sin(2f * Mathf.PI * 720f * latchTime) * 0.045f +
                   (whiteNoise - filteredNoise) * 0.1f) * Mathf.Exp(-latchTime * 115f)
                : 0f;
            float rattleTime = t - 0.12f;
            float rattleEnvelope = rattleTime >= 0f ? Mathf.Exp(-rattleTime * 18f) : 0f;
            float rattleGate = 0.42f + 0.58f * Mathf.Sin(2f * Mathf.PI * 29f * t);
            float rattle = (filteredNoise * 0.17f + whiteNoise * 0.035f) *
                rattleEnvelope * rattleGate;
            float reboundTime = t - 0.235f;
            float rebound = reboundTime >= 0f
                ? Mathf.Sin(2f * Mathf.PI * 91f * reboundTime) *
                  Mathf.Exp(-reboundTime * 38f) * 0.055f
                : 0f;

            float sample = (body + impact + latch + rattle + rebound) * 0.36f;
            samples[i] = sample;
            peak = Mathf.Max(peak, Mathf.Abs(sample));
        }

        float normalize = 0.68f / peak;
        for (int i = 0; i < samples.Length; i++)
            samples[i] *= normalize;

        AudioClip clip = AudioClip.Create("Door Close - Wood Impact", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnGUI()
    {
        if (!SettingsMenuController.IsOpen)
            HandleOwnerPetPointerEvent(Event.current);
        if (!SettingsMenuController.IsOpen && Stage == AfterTheGlowStoryStage.OwnerPetPuzzle && pettingPuzzleUI != null)
            pettingPuzzleUI.HandleMouseEvent(Event.current);
        if (activeGatePuzzleFrame != null)
        {
            GUI.color = Color.white;
            Rect imageRect = new Rect(0f, 0f, Screen.width, Screen.height);
            if (gatePuzzlePage == 3 && Time.time < memory2ShakeUntil)
            {
                imageRect.x += Mathf.Sin(Time.time * 91f) * 3f;
                imageRect.y += Mathf.Cos(Time.time * 73f) * 2f;
            }
            float memoryElapsed = Time.time - gatePuzzlePageTime;
            float zoom = gatePuzzlePage == 1 ? Mathf.Lerp(1.07f, 1f, Mathf.Clamp01(memoryElapsed / 1.6f)) :
                gatePuzzlePage == 7 ? Mathf.Lerp(1f, 1.035f, Mathf.Clamp01(memoryElapsed / 2f)) : 1f;
            if (gatePuzzlePage == 5)
                imageRect.y -= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(memoryElapsed / 1.6f)) * Screen.height * .008f;
            Vector2 center = imageRect.center;
            imageRect.size *= zoom;
            imageRect.center = center;
            GUI.DrawTexture(imageRect,
                activeGatePuzzleFrame, ScaleMode.ScaleToFit, false);
            if (gatePuzzlePage == 9 && gateMemoryTornLeashFrame != null)
            {
                Rect tornRect = imageRect;
                tornRect.size *= gateMemoryLeashScale;
                tornRect.center = imageRect.center;
                GUI.DrawTexture(tornRect, gateMemoryTornLeashFrame, ScaleMode.ScaleToFit, true);
            }
            else if (gatePuzzlePage % 2 == 0)
            {
                int state = Mathf.Min(3, gatePuzzlePage / 2);
                float leashWidth = imageRect.width * .95f * gateMemoryLeashScale;
                float leashHeight = imageRect.height * .38f * gateMemoryLeashScale;
                JourneyArt.Draw(new Rect(imageRect.center.x - leashWidth * .5f,
                    imageRect.center.y - leashHeight * .5f, leashWidth, leashHeight),
                    JourneyArt.Frame("Puzzles/LeashStatesFixed__c2r3", state));
            }
            // Memory artwork already contains the owner's connected arm and hand.
            return;
        }

        if (CanShowBedInteractionHint() ||
            (Stage == AfterTheGlowStoryStage.OwnerAtTheDoor && waitingForPetPuzzleClick))
            DrawInteractionGestureHint(false);

        DrawHeadlightFearCue();
    }

    private void DrawHeadlightFearCue()
    {
        if (Time.unscaledTime >= fearUntil || player == null || cameraFollow == null)
            return;

        Camera camera = cameraFollow.GetComponent<Camera>();
        if (camera == null)
            return;

        Vector3 head = camera.WorldToScreenPoint(player.transform.position + Vector3.up * 1.2f);
        float scale = Mathf.Max(0.65f, Screen.height / 720f);
        float shake = Mathf.Sin(Time.unscaledTime * 34f) * 2.5f * scale;
        JourneyArt.Draw(new Rect(head.x - 24f * scale + shake,
            Screen.height - head.y - 48f * scale, 48f * scale, 54f * scale), JourneyArt.Frame("UI/Indicators__c4r2", 6));
    }

    private bool CanShowBedInteractionHint()
    {
        if (Stage != AfterTheGlowStoryStage.WakeTheOwner || player == null || bed == null ||
            !player.AllowsPlayerInput || !player.IsGrounded || !bed.CanBeInteractedWith)
            return false;

        // Share the click target selection so the hint never promises an unavailable action.
        var interaction = player.GetComponent<PlayerInteraction>();
        return interaction != null && interaction.CurrentTarget == bed && !interaction.IsInteractionActive;
    }

    private void DrawInteractionGestureHint(bool swipe)
    {
        float unit = Screen.height / 720f * 1.35f;
        Vector3 head = player != null && cameraFollow != null
            ? cameraFollow.GetComponent<Camera>().WorldToScreenPoint(player.transform.position + Vector3.up * 0.9f)
            : new Vector3(Screen.width * .5f, Screen.height * .5f, 0f);
        float x = head.x + (swipe ? Mathf.Sin(Time.unscaledTime * 2.8f) * 28f * unit : 0f);
        float y = Mathf.Clamp(Screen.height - head.y - 68f * unit, 85f * unit, Screen.height - 100f * unit);
        GUI.color = new Color(.05f, .15f, .18f, .85f);
        GUI.DrawTexture(new Rect(x - 30f * unit, y - 8f * unit, 60f * unit, 51f * unit), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.93f, 0.72f, 0.75f + Mathf.Sin(Time.unscaledTime * 3f) * 0.15f);
        float w = 24f * unit, h = 34f * unit, stroke = 2f * unit;
        GUI.DrawTexture(new Rect(x - w * .5f, y, stroke, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + w * .5f - stroke, y, stroke, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - w * .5f, y, w, stroke), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - w * .5f, y + h - stroke, w, stroke), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - w * .5f, y + h * .42f, w, stroke), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - stroke * .5f, y, stroke, h * .42f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - w * .5f + stroke * 2f, y + stroke * 2f,
            w * .5f - stroke * 3f, h * .42f - stroke * 3f), Texture2D.whiteTexture);
        if (swipe)
        {
            float center = head.x, arrowY = y + h + 12f * unit;
            GUI.DrawTexture(new Rect(center - 48f * unit, arrowY, 96f * unit, stroke), Texture2D.whiteTexture);
            for (int side = -1; side <= 1; side += 2)
                for (int step = 0; step < 5; step++)
                    GUI.DrawTexture(new Rect(center + side * (48f - step * 2f) * unit,
                        arrowY - step * unit, stroke, (step * 2f + 2f) * unit), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    private static void DrawPuzzleProgressDots(int progress, int total)
    {
        const float diameter = 15f;
        const float gap = 21f;
        float width = total * diameter + (total - 1) * gap;
        float startX = (Screen.width - width) * 0.5f;
        float y = Screen.height * 0.1f;
        for (int i = 0; i < total; i++)
        {
            GUI.color = i < progress ? new Color(0.96f, 0.88f, 0.61f, 0.95f) :
                new Color(0.04f, 0.11f, 0.14f, 0.68f);
            GUI.DrawTexture(new Rect(startX + i * (diameter + gap), y, diameter, diameter),
                Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }
}
