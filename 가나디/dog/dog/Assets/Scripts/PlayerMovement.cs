using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public enum PlayerControlMode
{
    Free,
    Interacting,
    CutsceneLocked,
    AutoMoving,
    LightRetreat
}

/// <summary>
/// A/D movement, Space jump, S crouch, and shared control for interaction,
/// cutscenes, automatic movement, and forced light retreats.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovement : MonoBehaviour
{
    [Header("이동")]
    [Min(0.1f)] public float moveSpeed = 4.4f;
    [Min(0.1f)] public float jumpForce = 6.8f;
    [Min(1f)] public float fallMultiplier = 2.5f;
    [Min(1f)] public float lowJumpMultiplier = 2f;

    [Header("입력")]
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode crouchKey = KeyCode.S;

    [Header("바닥 감지")]
    public Transform groundCheck;
    [Min(0.01f)] public float groundCheckRadius = 0.18f;
    public LayerMask groundLayer;

    [Header("침대 점프 제한")]
    public Collider2D jumpBlocker;
    [Min(0f)] public float jumpBlockerPadding = 0.12f;

    [Header("강한 빛 후퇴")]
    [Min(0.1f)] public float retreatDistance = 1.8f;
    [Min(0.1f)] public float retreatSpeed = 4.2f;
    [Min(0f)] public float retreatCooldown = 1.2f;

    [Header("엎드리기")]
    [Range(0.4f, 0.95f)] public float crouchHeightMultiplier = 0.72f;
    [Range(0.5f, 1f)] public float crouchVisualHeightMultiplier = 0.84f;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private BoxCollider2D boxCollider;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private LightExposureSensor exposureSensor;
    private PlayerInteraction interaction;
    private Vector2 standingColliderSize;
    private Vector2 standingColliderOffset;
    private Vector3 standingScale;
    private float horizontalInput;
    private float automaticTargetX;
    private float automaticSpeed;
    private float nextRetreatAllowedTime;
    private bool isFacingRight;
    private bool queuedJump;
    private bool isGrounded;
    private bool isCrouching;
    private bool automaticMoveIsRetreat;
    private PlayerControlMode resumeMode = PlayerControlMode.Free;
    private bool animatorHasSpeed;
    private bool animatorHasRunning;
    private bool animatorHasPushing;
    private bool animatorHasGrounded;
    private bool animatorHasJump;
    private bool animatorHasCrouching;
    private bool animatorHasBiting;
    private bool animatorHasTugSpeed;
    private readonly RaycastHit2D[] retreatHits = new RaycastHit2D[32];

    public Rigidbody2D Body => body;
    public Collider2D BodyCollider => bodyCollider;
    public float HorizontalInput => horizontalInput;
    public bool IsGrounded => isGrounded;
    public bool IsCrouching => isCrouching;
    public int FacingDirection => isFacingRight ? 1 : -1;
    public bool IsPushing { get; private set; }
    public bool IsAutoMoving => ControlMode == PlayerControlMode.AutoMoving ||
                                ControlMode == PlayerControlMode.LightRetreat;
    public bool AllowsPlayerInput => ControlMode == PlayerControlMode.Free ||
                                    ControlMode == PlayerControlMode.Interacting;
    public PlayerControlMode ControlMode { get; private set; } = PlayerControlMode.Free;
    public int JumpInputCount { get; private set; }
    public int JumpAcceptedCount { get; private set; }
    public bool LightRetreatEnabled { get; set; } = true;
    public LightExposureState ExposureState => exposureSensor != null
        ? exposureSensor.CurrentState
        : LightExposureState.Normal;

    public event Action RetreatCompleted;
    public event Action<PlayerControlMode> ControlModeChanged;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        boxCollider = bodyCollider as BoxCollider2D;
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        exposureSensor = GetComponent<LightExposureSensor>();
        interaction = GetComponent<PlayerInteraction>();
        standingScale = transform.localScale;
        isFacingRight = standingScale.x >= 0f;

        if (boxCollider != null)
        {
            standingColliderSize = boxCollider.size;
            standingColliderOffset = boxCollider.offset;
        }

        if (body != null)
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;

        if (animator != null)
        {
            animatorHasSpeed = HasParameter("Speed", AnimatorControllerParameterType.Float);
            animatorHasRunning = HasParameter("IsRunning", AnimatorControllerParameterType.Bool);
            animatorHasPushing = HasParameter("IsPushing", AnimatorControllerParameterType.Bool);
            animatorHasGrounded = HasParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            animatorHasJump = HasParameter("Jump", AnimatorControllerParameterType.Trigger);
            animatorHasCrouching = HasParameter("IsCrouching", AnimatorControllerParameterType.Bool);
            animatorHasBiting = HasParameter("IsBiting", AnimatorControllerParameterType.Bool);
            animatorHasTugSpeed = HasParameter("LeashTugSpeed", AnimatorControllerParameterType.Float);
        }
    }

    private void OnEnable()
    {
        if (exposureSensor != null)
            exposureSensor.StateChanged += HandleExposureChanged;
    }

    private void OnDisable()
    {
        if (exposureSensor != null)
            exposureSensor.StateChanged -= HandleExposureChanged;
        if (interaction != null)
            interaction.CancelCurrentInteraction();
    }

    private void Update()
    {
        if (body == null)
            return;

        if (SettingsMenuController.IsOpen)
        {
            horizontalInput = 0f;
            queuedJump = false;
            return;
        }

        horizontalInput = 0f;
        bool canReadInput = AllowsPlayerInput;
        if (canReadInput)
        {
            if (IsKeyHeld(leftKey)) horizontalInput -= 1f;
            if (IsKeyHeld(rightKey)) horizontalInput += 1f;

            bool jumpPressed = WasKeyPressedThisFrame(jumpKey);
            if (jumpPressed)
                JumpInputCount++;

            if (jumpPressed && isGrounded && (interaction == null || !interaction.IsBiting) && !IsJumpBlockedByObstacle())
            {
                queuedJump = true;
                if (animator != null && animatorHasJump)
                    animator.SetTrigger("Jump");
            }
        }

        if (interaction != null && interaction.IsBiting)
            isFacingRight = interaction.ActiveTarget.TetherPoint.position.x >= body.position.x;
        else if (Mathf.Abs(horizontalInput) > 0.01f)
            isFacingRight = horizontalInput > 0f;

        isGrounded = groundLayer.value == 0
            ? IsGroundedByColliderFallback()
            : Physics2D.OverlapCircle(GetGroundCheckPosition(), groundCheckRadius, groundLayer) != null;

        if (canReadInput)
            UpdateCrouch(IsKeyHeld(crouchKey) && (interaction == null || !interaction.IsBiting));
        else if (ControlMode == PlayerControlMode.CutsceneLocked)
            UpdateCrouch(false);

        if (interaction != null)
        {
            interaction.RefreshTarget(FacingDirection);
            if (canReadInput && WasPrimaryMousePressedThisFrame())
                interaction.HandleInteractionClick();
        }

        FlipSprite();
        if (animator != null)
        {
            float velocitySpeed = body != null
                ? Mathf.Abs(body.linearVelocity.x) / Mathf.Max(0.1f, moveSpeed)
                : 0f;
            float animationSpeed = Mathf.Max(Mathf.Abs(horizontalInput), velocitySpeed);
            if (animatorHasSpeed)
                animator.SetFloat("Speed", animationSpeed);
            if (animatorHasRunning)
                animator.SetBool("IsRunning", animationSpeed > 0.05f && isGrounded && !isCrouching);
            if (animatorHasPushing)
                animator.SetBool("IsPushing", IsPushing);
            if (animatorHasGrounded)
                animator.SetBool("IsGrounded", isGrounded && !queuedJump);
            if (animatorHasCrouching)
                animator.SetBool("IsCrouching", isCrouching);
            if (animatorHasBiting)
                animator.SetBool("IsBiting", interaction != null && interaction.IsBiting);
            if (animatorHasTugSpeed)
                animator.SetFloat("LeashTugSpeed", interaction != null && interaction.IsBiting &&
                    interaction.BiteMotionReady && horizontalInput * FacingDirection < -.05f &&
                    Mathf.Abs(body.linearVelocity.x) > .03f ? .8f : 0f);
        }
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        if (ControlMode == PlayerControlMode.AutoMoving ||
            ControlMode == PlayerControlMode.LightRetreat)
        {
            MoveAutomatically();
            ApplyBetterJumpPhysics();
            return;
        }

        if (!AllowsPlayerInput)
        {
            horizontalInput = 0f;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            queuedJump = false;
            IsPushing = false;
            ApplyBetterJumpPhysics();
            return;
        }

        float speed = moveSpeed * GetExposureMovementMultiplier();
        if (interaction != null && interaction.IsBiting)
            speed = interaction.BiteMotionReady ? Mathf.Min(speed, .85f) : 0f;
        Vector2 proposedVelocity = new Vector2(horizontalInput * speed,
            queuedJump ? jumpForce : body.linearVelocity.y);
        Vector2 movementDelta = proposedVelocity * Time.fixedDeltaTime;
        if (WouldMoveIntoExposedLight(movementDelta, out Vector2 lightSource))
        {
            body.linearVelocity = Vector2.zero;
            queuedJump = false;
            IsPushing = false;

            float retreatDirection = Mathf.Sign(body.position.x - lightSource.x);
            if (Mathf.Abs(retreatDirection) < 0.01f)
                retreatDirection = -Mathf.Sign(horizontalInput);
            if (Mathf.Abs(retreatDirection) < 0.01f)
                retreatDirection = -FacingDirection;
            BeginForcedRetreat(retreatDirection, retreatDistance, retreatSpeed);
            ApplyBetterJumpPhysics();
            return;
        }

        if (queuedJump)
        {
            queuedJump = false;
            JumpAcceptedCount++;
            isGrounded = false;
        }

        body.linearVelocity = proposedVelocity;
        IsPushing = interaction != null &&
                    interaction.ApplyInteraction(horizontalInput, Time.fixedDeltaTime, isGrounded);

        ApplyBetterJumpPhysics();
    }

    public void SetControlMode(PlayerControlMode mode)
    {
        if (ControlMode == mode)
            return;

        if (mode == PlayerControlMode.CutsceneLocked ||
            mode == PlayerControlMode.LightRetreat ||
            mode == PlayerControlMode.AutoMoving)
        {
            queuedJump = false;
            horizontalInput = 0f;
            if (interaction != null)
                interaction.CancelCurrentInteraction();
        }

        ControlMode = mode;
        ControlModeChanged?.Invoke(mode);
    }

    public void BeginAutoMoveToX(float targetX, float speed, PlayerControlMode modeOnArrival)
    {
        automaticTargetX = targetX;
        automaticSpeed = Mathf.Max(0.1f, speed);
        automaticMoveIsRetreat = false;
        resumeMode = modeOnArrival;
        SetControlMode(PlayerControlMode.AutoMoving);
    }

    public void BeginForcedRetreat(float direction, float distance, float speed)
    {
        if (!LightRetreatEnabled || Time.time < nextRetreatAllowedTime || ControlMode == PlayerControlMode.CutsceneLocked ||
            ControlMode == PlayerControlMode.LightRetreat)
            return;

        if (Mathf.Abs(direction) < 0.01f)
            direction = -FacingDirection;

        float retreatDirection = Mathf.Sign(direction);
        float reachableDistance = GetReachableRetreatDistance(retreatDirection, Mathf.Max(0.1f, distance));
        automaticTargetX = body.position.x + retreatDirection * reachableDistance;
        automaticSpeed = Mathf.Max(0.1f, speed);
        automaticMoveIsRetreat = true;
        resumeMode = PlayerControlMode.Free;
        nextRetreatAllowedTime = Time.time + retreatCooldown;
        SetControlMode(PlayerControlMode.LightRetreat);
    }

    public void SetInteractionActive(bool active)
    {
        if (ControlMode == PlayerControlMode.Free && active)
            SetControlMode(PlayerControlMode.Interacting);
        else if (ControlMode == PlayerControlMode.Interacting && !active)
            SetControlMode(PlayerControlMode.Free);
    }

    public static bool IsKeyHeld(KeyCode key)
    {
        KeyControl control = GetKeyboardControl(key);
        return control != null ? control.isPressed : Input.GetKey(key);
    }

    public static bool WasKeyPressedThisFrame(KeyCode key)
    {
        KeyControl control = GetKeyboardControl(key);
        return control != null ? control.wasPressedThisFrame : Input.GetKeyDown(key);
    }

    public static bool WasPrimaryMousePressedThisFrame()
    {
        return Mouse.current != null
            ? Mouse.current.leftButton.wasPressedThisFrame
            : Input.GetMouseButtonDown(0);
    }

    public void FaceTowards(float worldX)
    {
        if (Mathf.Abs(worldX - transform.position.x) > 0.01f)
            isFacingRight = worldX > transform.position.x;
        FlipSprite();
    }

    public static Vector2 GetPrimaryMouseScreenPosition()
    {
        return Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
    }

    private static KeyControl GetKeyboardControl(KeyCode key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return null;

        switch (key)
        {
            case KeyCode.A: return keyboard.aKey;
            case KeyCode.D: return keyboard.dKey;
            case KeyCode.S: return keyboard.sKey;
            case KeyCode.Space: return keyboard.spaceKey;
            case KeyCode.F1: return keyboard.f1Key;
            case KeyCode.R: return keyboard.rKey;
            default: return null;
        }
    }

    private void MoveAutomatically()
    {
        float distance = automaticTargetX - body.position.x;
        if (Mathf.Abs(distance) <= 0.09f)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            bool completedRetreat = automaticMoveIsRetreat;
            automaticMoveIsRetreat = false;
            SetControlMode(resumeMode);
            if (completedRetreat)
            {
                // A fresh approach after the retreat must react immediately, rather
                // than hitting the predictive boundary while the old cooldown runs.
                nextRetreatAllowedTime = Time.time;
                RetreatCompleted?.Invoke();
            }
            return;
        }

        float direction = Mathf.Sign(distance);
        if (automaticMoveIsRetreat)
        {
            // Re-evaluate obstacles during the retreat too: a wall must never keep
            // the completion event (and the tutorial's light-off timer) waiting.
            float reachable = GetReachableRetreatDistance(direction, Mathf.Abs(distance));
            automaticTargetX = body.position.x + direction * reachable;
            if (reachable <= 0.09f)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                automaticMoveIsRetreat = false;
                nextRetreatAllowedTime = Time.time;
                SetControlMode(resumeMode);
                RetreatCompleted?.Invoke();
                return;
            }
        }
        isFacingRight = direction > 0f;
        if (!automaticMoveIsRetreat && WouldMoveIntoExposedLight(
                new Vector2(direction * automaticSpeed * Time.fixedDeltaTime, 0f), out Vector2 source))
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            float away = Mathf.Sign(body.position.x - source.x);
            BeginForcedRetreat(Mathf.Abs(away) > .01f ? away : -direction, retreatDistance, retreatSpeed);
            return;
        }
        float stepSpeed = Mathf.Min(automaticSpeed,
            Mathf.Abs(automaticTargetX - body.position.x) / Time.fixedDeltaTime);
        body.linearVelocity = new Vector2(direction * stepSpeed, body.linearVelocity.y);
    }

    private float GetReachableRetreatDistance(float direction, float requestedDistance)
    {
        if (bodyCollider == null)
            return requestedDistance;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        filter.useTriggers = false;
        Vector2 axis = Vector2.right * direction;
        int count = bodyCollider.Cast(axis, filter, retreatHits, requestedDistance);
        float reachable = requestedDistance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = retreatHits[i];
            if (hit.collider == null || hit.collider.attachedRigidbody == body ||
                hit.collider.bounds.max.y <= bodyCollider.bounds.min.y + 0.06f ||
                Vector2.Dot(hit.normal, -axis) < 0.5f)
                continue; // A floor contact does not block a horizontal retreat.
            reachable = Mathf.Min(reachable, Mathf.Max(0f, hit.distance - 0.05f));
        }
        return reachable;
    }

    private bool WouldMoveIntoExposedLight(Vector2 movementDelta, out Vector2 lightSource)
    {
        lightSource = transform.position;
        if (exposureSensor == null || bodyCollider == null || movementDelta.sqrMagnitude <= 0f)
            return false;

        Bounds bounds = bodyCollider.bounds;
        Vector2 center = bounds.center;
        Vector2 sampleExtents = (Vector2)bounds.extents + Vector2.one * 0.05f;
        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                Vector2 currentSample = center + new Vector2(sampleExtents.x * (x * 0.5f),
                    sampleExtents.y * (y * 0.5f));
                Vector2 nextSample = currentSample + movementDelta;
                if (exposureSensor.WouldMoveIntoGameplayLight(currentSample, nextSample,
                        out lightSource))
                    return true;
            }
        }

        Vector2 sensorCurrent = (Vector2)transform.position +
                                exposureSensor.GetSensorOffsetFromActor();
        return exposureSensor.WouldMoveIntoGameplayLight(sensorCurrent,
            sensorCurrent + movementDelta, out lightSource);
    }

    private void UpdateCrouch(bool requested)
    {
        if (boxCollider == null)
            return;

        bool shouldCrouch = requested || (isCrouching && !HasStandingHeadroom());
        if (shouldCrouch == isCrouching)
            return;

        float oldBottom = bodyCollider.bounds.min.y;
        if (shouldCrouch)
        {
            Vector3 scale = standingScale;
            scale.y = Mathf.Sign(scale.y) * Mathf.Abs(standingScale.y) * crouchVisualHeightMultiplier;
            transform.localScale = scale;

            float localBottom = standingColliderOffset.y - standingColliderSize.y * 0.5f;
            Vector2 size = standingColliderSize;
            size.y *= crouchHeightMultiplier;
            Vector2 offset = standingColliderOffset;
            offset.y = localBottom / crouchVisualHeightMultiplier + size.y * 0.5f;
            boxCollider.size = size;
            boxCollider.offset = offset;
            isCrouching = true;
        }
        else
        {
            transform.localScale = standingScale;
            boxCollider.size = standingColliderSize;
            boxCollider.offset = standingColliderOffset;
            isCrouching = false;
        }

        float newBottom = bodyCollider.bounds.min.y;
        Vector2 position = body.position;
        position.y += oldBottom - newBottom;
        body.position = position;
    }

    private bool HasStandingHeadroom()
    {
        if (bodyCollider == null || boxCollider == null)
            return true;

        Bounds current = bodyCollider.bounds;
        float standingHeight = standingColliderSize.y * Mathf.Abs(standingScale.y);
        float bottom = current.min.y + 0.025f;
        Vector2 size = new Vector2(current.size.x * 0.88f, Mathf.Max(0.05f, standingHeight - 0.04f));
        Vector2 center = new Vector2(current.center.x, bottom + size.y * 0.5f);
        Collider2D[] overlaps = Physics2D.OverlapBoxAll(center, size, 0f);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider2D candidate = overlaps[i];
            if (candidate == null || candidate.isTrigger || candidate.transform.root == transform.root)
                continue;
            return false;
        }
        return true;
    }

    private bool IsJumpBlockedByObstacle()
    {
        if (jumpBlocker == null || bodyCollider == null)
            return false;

        Bounds playerBounds = bodyCollider.bounds;
        Bounds blockerBounds = jumpBlocker.bounds;
        float horizontalGap = 0f;
        if (playerBounds.max.x < blockerBounds.min.x)
            horizontalGap = blockerBounds.min.x - playerBounds.max.x;
        else if (playerBounds.min.x > blockerBounds.max.x)
            horizontalGap = playerBounds.min.x - blockerBounds.max.x;

        return horizontalGap <= jumpBlockerPadding;
    }

    private void HandleExposureChanged(LightExposureState previous, LightExposureState current)
    {
        if (current != LightExposureState.Exposed ||
            (ControlMode != PlayerControlMode.Free && ControlMode != PlayerControlMode.Interacting))
            return;

        Vector2 source = exposureSensor != null ? exposureSensor.ActiveLightSource : transform.position;
        float direction = Mathf.Sign(transform.position.x - source.x);
        if (Mathf.Abs(transform.position.x - source.x) < 0.08f)
            direction = -FacingDirection;
        BeginForcedRetreat(direction, retreatDistance, retreatSpeed);
    }

    private float GetExposureMovementMultiplier()
    {
        return ExposureState == LightExposureState.Exposed ? 0.55f : 1f;
    }

    private Vector2 GetGroundCheckPosition()
    {
        if (groundCheck != null && (groundCheck == transform || groundCheck.IsChildOf(transform)))
            return groundCheck.position;
        if (bodyCollider != null)
            return new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y + 0.025f);
        return transform.position + Vector3.down * 0.5f;
    }

    private bool IsGroundedByColliderFallback()
    {
        return bodyCollider != null &&
               Physics2D.OverlapCircle(bodyCollider.bounds.center + Vector3.down *
                   (bodyCollider.bounds.extents.y + 0.02f), groundCheckRadius) != null;
    }

    private void ApplyBetterJumpPhysics()
    {
        if (body == null)
            return;

        if (body.linearVelocity.y < 0f)
            body.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        else if (body.linearVelocity.y > 0f && !IsKeyHeld(jumpKey))
            body.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
    }

    private void FlipSprite()
    {
        if (Mathf.Abs(transform.localScale.x) < 0.0001f)
            return;

        bool currentlyFacingRight = transform.localScale.x > 0f;
        if (currentlyFacingRight != isFacingRight)
        {
            Vector3 scale = transform.localScale;
            scale.x = -scale.x;
            transform.localScale = scale;
            standingScale.x = -standingScale.x;
        }
    }

    private bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name && parameters[i].type == type)
                return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D collider = GetComponent<Collider2D>();
        Vector3 position = groundCheck != null
            ? groundCheck.position
            : collider != null
                ? new Vector3(collider.bounds.center.x, collider.bounds.min.y + 0.025f, transform.position.z)
                : transform.position + Vector3.down * 0.5f;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(position, groundCheckRadius);
    }
}
