using UnityEngine;

/// <summary>
/// Selects one nearby context target. The left mouse button starts or releases
/// the action; A/D then push toward a target or pull away while biting it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public sealed class PlayerInteraction : MonoBehaviour
{
    [Header("대상 탐색")]
    [Min(0.1f)] [SerializeField] private float interactionRange = 1.6f;
    [Min(0.1f)] [SerializeField] private float interactionHeight = 1.35f;
    [SerializeField] private float verticalOffset = 0.05f;
    [SerializeField] private LayerMask interactionLayers = Physics2D.DefaultRaycastLayers;
    [Min(0.5f)] [SerializeField] private float activeTargetReleaseRange = 3.2f;
    [SerializeField] private HouseExitInteraction houseExit;

    private readonly Collider2D[] overlapBuffer = new Collider2D[32];
    private PlayerMovement player;
    private AfterTheGlowStoryDirector storyDirector;
    private PushableObject currentTarget;
    private PushableObject activeTarget;
    private LineRenderer biteLine;
    private Material lineMaterial;
    private float previousPullActorX;
    private PushableObject approachingTarget;
    private GateLeashPresentation heldLeash;
    private SpriteRenderer mouthRenderer;
    private float approachDeadline;
    private int approachStartFrame;
    private float initialTetherLength;
    private float maximumPullExtension;
    private float biteReadyTime;

    public PushableObject CurrentTarget => activeTarget != null ? activeTarget : currentTarget;
    public PushableObject ActiveTarget => activeTarget;
    public string CurrentTargetName => CurrentTarget != null ? CurrentTarget.gameObject.name : "없음";
    public string CurrentActionName => CurrentTarget != null ? CurrentTarget.InteractionLabel : "";
    public bool IsInteractionActive => activeTarget != null;
    public bool IsBiting => activeTarget != null && activeTarget.NeedsBiteToRelease;
    public bool IsApproachingLeash => approachingTarget != null;
    public bool BiteMotionReady => !IsBiting || Time.time >= biteReadyTime;
    public Vector3 MouthPosition
    {
        get
        {
            if (mouthRenderer == null)
                mouthRenderer = transform.Find("PixelSnapper Dog Visual")?.GetComponent<SpriteRenderer>();
            if (mouthRenderer == null || mouthRenderer.sprite == null)
                return transform.TransformPoint(new Vector3(.27f, .07f, 0f));
            Sprite sprite = mouthRenderer.sprite;
            // Coordinates in the unchanged 128px source cells, measured at the lips.
            Vector2 pixel = new Vector2(107f, 46f);
            if (sprite.name.EndsWith("19")) pixel = new Vector2(107f, 43f);
            else if (sprite.name.EndsWith("20") || sprite.name.EndsWith("22")) pixel = new Vector2(105f, 43f);
            else if (sprite.name.EndsWith("21")) pixel = new Vector2(106f, 37f);
            Vector2 local = (pixel - sprite.pivot) / sprite.pixelsPerUnit;
            if (mouthRenderer.flipX) local.x = -local.x;
            if (mouthRenderer.flipY) local.y = -local.y;
            return mouthRenderer.transform.TransformPoint(local);
        }
    }

    private void Awake()
    {
        player = GetComponent<PlayerMovement>();
        if (houseExit == null)
            houseExit = FindAnyObjectByType<HouseExitInteraction>();
        storyDirector = FindAnyObjectByType<AfterTheGlowStoryDirector>();
        CreateBiteLine();
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
            Destroy(lineMaterial);
    }

    private void LateUpdate()
    {
        if (approachingTarget != null)
        {
            var leash = approachingTarget.GetComponent<GateLeashPresentation>();
            bool cancel = leash == null || !approachingTarget.CanBeInteractedWith ||
                          !approachingTarget.NeedsBiteToRelease || Time.time > approachDeadline ||
                          (!player.IsAutoMoving && !player.AllowsPlayerInput) ||
                          (Time.frameCount != approachStartFrame && PlayerMovement.WasPrimaryMousePressedThisFrame());
            if (cancel)
            {
                approachingTarget = null;
                if (player.ControlMode == PlayerControlMode.AutoMoving)
                {
                    player.SetControlMode(PlayerControlMode.Free);
                    player.Body.linearVelocity = new Vector2(0f, player.Body.linearVelocity.y);
                }
            }
            else if (player.AllowsPlayerInput)
            {
                player.FaceTowards(leash.GripPosition.x);
                var target = approachingTarget;
                approachingTarget = null;
                if (player.IsGrounded && Vector2.Distance(MouthPosition, leash.GripPosition) <= .15f)
                    BeginInteraction(target);
            }
        }
        UpdateBiteLine();
    }

    public void RefreshTarget(int facingDirection)
    {
        if (player == null)
            player = GetComponent<PlayerMovement>();

        if (player != null && !player.AllowsPlayerInput)
        {
            if (activeTarget != null)
                CancelCurrentInteraction();
            currentTarget = null;
            UpdateBiteLine();
            return;
        }

        if (activeTarget != null)
        {
            var targetCollider = activeTarget.GetComponent<Collider2D>();
            float activeDistance = Vector2.Distance(player.BodyCollider.bounds.center,
                targetCollider.ClosestPoint(player.BodyCollider.bounds.center));
            if (!activeTarget.isActiveAndEnabled || !activeTarget.CanBeInteractedWith ||
                activeDistance > activeTargetReleaseRange)
                CancelCurrentInteraction();
        }

        int direction = facingDirection >= 0 ? 1 : -1;
        Vector2 center = (Vector2)transform.position +
                         new Vector2(direction * interactionRange * 0.48f, verticalOffset);
        Vector2 size = new Vector2(interactionRange, interactionHeight);
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.SetLayerMask(interactionLayers);
        int count = Physics2D.OverlapBox(center, size, 0f, filter, overlapBuffer);

        PushableObject nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider2D candidateCollider = overlapBuffer[i];
            if (candidateCollider == null || candidateCollider.transform.root == transform.root)
                continue;

            PushableObject candidate = candidateCollider.GetComponentInParent<PushableObject>();
            if (candidate == null || !candidate.CanBeInteractedWith)
                continue;

            float directionToTarget =
                (candidate.transform.position.x - transform.position.x) * direction;
            if (directionToTarget < -0.05f)
                continue;

            float distanceSquared =
                ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = candidate;
            }
        }

        currentTarget = activeTarget != null ? activeTarget : nearest;
        UpdateBiteLine();
    }

    public void HandleInteractionClick()
    {
        HandleInteractionClickAtScreenPosition(PlayerMovement.GetPrimaryMouseScreenPosition());
    }

    public void HandleInteractionClickAtScreenPosition(Vector2 screenPosition)
    {
        if (player == null || !player.AllowsPlayerInput)
            return;

        if (houseExit != null && houseExit.TryInteractAtScreenPosition(screenPosition, player))
            return;

        if (activeTarget != null)
        {
            CancelCurrentInteraction();
            return;
        }

        if (currentTarget == null || !currentTarget.CanBeInteractedWith || !player.IsGrounded)
            return;

        var leash = currentTarget.NeedsBiteToRelease ? currentTarget.GetComponent<GateLeashPresentation>() : null;
        if (leash != null)
        {
            var target = currentTarget;
            player.FaceTowards(leash.GripPosition.x);
            float mouthOffsetX = MouthPosition.x - player.Body.position.x;
            float targetX = leash.GripPosition.x - mouthOffsetX;
            // Set the pending target AFTER AutoMoving cancels any previous interaction.
            player.BeginAutoMoveToX(targetX, 1.6f, PlayerControlMode.Free);
            approachingTarget = target;
            approachDeadline = Time.time + 3f;
            approachStartFrame = Time.frameCount;
            return;
        }
        BeginInteraction(currentTarget);
    }

    private void BeginInteraction(PushableObject target)
    {
        if (storyDirector != null && storyDirector.TryBeginStoryPuzzle(target))
            return;

        activeTarget = target;
        previousPullActorX = player.Body.position.x;
        initialTetherLength = Vector2.Distance(MouthPosition, target.TetherPoint.position) + .06f;
        maximumPullExtension = 0f;
        biteReadyTime = Time.time + .18f;
        heldLeash = target.GetComponent<GateLeashPresentation>();
        if (heldLeash != null) heldLeash.BeginBite(this);
        activeTarget.PullCompleted += HandleTetherReleased;
        activeTarget.BeginInteraction(player.FacingDirection);
        player.SetInteractionActive(true);
        UpdateBiteLine();
    }

    public bool CanPush(float horizontalInput, bool grounded)
    {
        if (!grounded || activeTarget == null || !activeTarget.CanBePushed)
            return false;

        float directionToTarget = Mathf.Sign(activeTarget.transform.position.x - transform.position.x);
        if (Mathf.Abs(directionToTarget) < 0.01f)
            directionToTarget = player != null ? player.FacingDirection : 1;

        return Mathf.Abs(horizontalInput) > 0.05f &&
               Mathf.Sign(horizontalInput) == directionToTarget;
    }

    public bool ApplyInteraction(float horizontalInput, float fixedDeltaTime, bool grounded)
    {
        if (activeTarget == null || player == null || !activeTarget.isActiveAndEnabled)
        {
            CancelCurrentInteraction();
            return false;
        }

        if (activeTarget.NeedsBiteToRelease)
        {
            float actualDelta = player.Body.position.x - previousPullActorX;
            previousPullActorX = player.Body.position.x;
            if (grounded && Time.time >= biteReadyTime && Mathf.Abs(horizontalInput) > 0.05f)
            {
                float towardTarget = Mathf.Sign(activeTarget.transform.position.x - transform.position.x);
                bool movingBackward = Mathf.Sign(horizontalInput) == -towardTarget;
                if (movingBackward && actualDelta * towardTarget < 0f)
                {
                    // Only new extension after the slack is taken up counts. Walking
                    // forward and back cannot farm progress from the same slack.
                    float extension = Mathf.Max(0f,
                        Vector2.Distance(MouthPosition, activeTarget.TetherPoint.position) - initialTetherLength);
                    float distance = Mathf.Min(Mathf.Abs(actualDelta), Mathf.Max(0f, extension - maximumPullExtension));
                    maximumPullExtension = Mathf.Max(maximumPullExtension, extension);
                    var target = activeTarget;
                    if (distance > 0f && (storyDirector == null || !storyDirector.CaptureGateBitePull(target, distance)))
                        target.AddPullDistance(distance);
                }
            }
            return false;
        }

        if (activeTarget.IsVisualInteraction)
            return true;

        if (!CanPush(horizontalInput, grounded))
        {
            activeTarget.EndPush();
            return false;
        }

        return activeTarget.TryPush(player.BodyCollider, Mathf.Sign(horizontalInput), fixedDeltaTime);
    }

    public void CancelCurrentInteraction()
    {
        approachingTarget = null;
        if (heldLeash != null) heldLeash.EndBite();
        heldLeash = null;
        if (activeTarget != null)
        {
            activeTarget.PullCompleted -= HandleTetherReleased;
            activeTarget.EndInteraction();
        }
        activeTarget = null;
        if (player != null)
            player.SetInteractionActive(false);
        UpdateBiteLine();
    }

    private void HandleTetherReleased(PushableObject target)
    {
        if (target == activeTarget) CancelCurrentInteraction();
    }

    private void CreateBiteLine()
    {
        GameObject lineObject = new GameObject("Bite Connection");
        lineObject.transform.SetParent(transform, false);
        biteLine = lineObject.AddComponent<LineRenderer>();
        biteLine.useWorldSpace = true;
        biteLine.positionCount = 2;
        biteLine.startWidth = 0.045f;
        biteLine.endWidth = 0.035f;
        biteLine.numCapVertices = 3;
        biteLine.sortingOrder = 8;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            lineMaterial = new Material(shader);
            lineMaterial.color = new Color(0.73f, 0.55f, 0.33f, 0.95f);
            biteLine.material = lineMaterial;
        }
        biteLine.enabled = false;
    }

    private void UpdateBiteLine()
    {
        if (biteLine == null)
            return;

        bool show = activeTarget != null && activeTarget.NeedsBiteToRelease && heldLeash == null;
        biteLine.enabled = show;
        if (!show)
            return;

        Vector3 mouth = MouthPosition;
        Vector3 tiePoint = activeTarget.TetherPoint != null
            ? activeTarget.TetherPoint.position
            : activeTarget.transform.position;
        biteLine.SetPosition(0, mouth);
        biteLine.SetPosition(1, tiePoint);
    }

    private void OnDisable()
    {
        approachingTarget = null;
        if (heldLeash != null) heldLeash.EndBite();
        heldLeash = null;
        if (activeTarget != null)
        {
            activeTarget.PullCompleted -= HandleTetherReleased;
            activeTarget.EndInteraction();
        }
        activeTarget = null;
        if (player != null)
            player.SetInteractionActive(false);
        if (biteLine != null)
            biteLine.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        int direction = player != null ? player.FacingDirection : 1;
        Vector3 center = transform.position + new Vector3(direction * interactionRange * 0.48f, verticalOffset, 0f);
        Gizmos.color = CurrentTarget != null ? Color.green : Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(interactionRange, interactionHeight, 0f));
    }
}
