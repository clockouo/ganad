using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Context target with physics pushing and an optional bite-and-pull latch.
/// A released latch can be pushed open with the same mouse interaction.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class PushableObject : MonoBehaviour
{
    [Header("밀기")]
    [SerializeField] private bool canBePushed = true;
    [SerializeField] private bool interactionEnabled = true;
    [Min(0.1f)] [SerializeField] private float maximumPushSpeed = 2.2f;
    [Min(0.1f)] [SerializeField] private float pushAcceleration = 9f;
    [Min(0f)] [SerializeField] private float maximumContactGap = 0.22f;
    [Min(0f)] [SerializeField] private float completeAfterPushDistance;
    [SerializeField] private bool disableColliderAfterPushCompletion;
    [SerializeField] private UnityEvent onPushCompleted = new UnityEvent();

    [Header("고정 물체의 이불 상호작용")]
    [SerializeField] private bool animateVisualOnly;
    [SerializeField] private bool interactionOnlyTrigger;
    [SerializeField] private Transform pushVisual;
    [Min(0.1f)] [SerializeField] private float visualInteractionSeconds = 0.65f;
    [Min(0f)] [SerializeField] private float visualPushDistance = 0.24f;
    [Min(0f)] [SerializeField] private float visualLiftHeight = 0.14f;

    [Header("고정 철문 경첩 열림")]
    [SerializeField] private bool opensOnHinge;
    [SerializeField] private GateHingePresentation gateHinge;
    [Min(0.1f)] [SerializeField] private float gateOpenSeconds = 0.65f;
    private float gateOpenProgress;
    private float startY;

    [Header("물고 당기는 결박")]
    [SerializeField] private bool requiresBiteToRelease;
    [Min(0.05f)] [SerializeField] private float pullDistanceToRelease = 1.1f;
    [SerializeField] private Transform tetherPoint;
    [SerializeField] private GameObject tetherVisual;
    [SerializeField] private ParticleSystem releaseParticles;
    [SerializeField] private UnityEvent onTetherReleased = new UnityEvent();

    private Rigidbody2D body;
    private Collider2D objectCollider;
    private float lastPushTime = float.NegativeInfinity;
    private float startX;
    private float pullProgress;
    private bool hasCompletedPush;
    private bool hasReleasedTether;
    private bool interactionActive;
    private Vector3 visualStartLocalPosition;
    private float visualElapsed;
    private float visualDirection = 1f;
    private bool storyPulseActive;
    private float storyPulseElapsed;

    public bool CanBePushed => interactionEnabled && canBePushed && isActiveAndEnabled &&
                               (!requiresBiteToRelease || hasReleasedTether) &&
                               body != null && (animateVisualOnly || opensOnHinge || body.bodyType == RigidbodyType2D.Dynamic);
    public bool CanBeInteractedWith => interactionEnabled && isActiveAndEnabled &&
                                       canBePushed && !hasCompletedPush;
    public bool NeedsBiteToRelease => requiresBiteToRelease && !hasReleasedTether;
    public string InteractionLabel => animateVisualOnly ? "이불 흔들기" : NeedsBiteToRelease ? "물고 당기기" : opensOnHinge ? "문 열기" : "밀기";
    public bool IsVisualInteraction => animateVisualOnly;
    public bool OpensOnHinge => opensOnHinge;
    public float GateOpenProgress => gateOpenProgress;
    public float PullProgress => pullProgress;
    public float PullDistanceToRelease => pullDistanceToRelease;
    public Transform TetherPoint => tetherPoint != null ? tetherPoint : transform;
    public event Action<PushableObject> PullCompleted;
    public event Action<PushableObject> PushCompleted;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        objectCollider = GetComponent<Collider2D>();
        if (interactionOnlyTrigger && objectCollider != null)
            objectCollider.isTrigger = true;
        body.bodyType = animateVisualOnly || opensOnHinge ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        if (opensOnHinge) body.gravityScale = 0f;
        startX = body.position.x;
        startY = body.position.y;
        if (gateHinge != null) gateHinge.ResetPose();
        if (pushVisual != null)
            visualStartLocalPosition = pushVisual.localPosition;
        LockHorizontalMotion();
        if (!requiresBiteToRelease)
            hasReleasedTether = true;
        if (tetherVisual != null)
            tetherVisual.SetActive(!hasReleasedTether);
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        if (opensOnHinge)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        if (animateVisualOnly)
        {
            if (storyPulseActive && !hasCompletedPush)
            {
                storyPulseElapsed += Time.fixedDeltaTime;
                float pulseProgress = Mathf.Clamp01(storyPulseElapsed / Mathf.Max(0.1f, visualInteractionSeconds * 0.62f));
                float pulseEnvelope = Mathf.Sin(pulseProgress * Mathf.PI);
                if (pushVisual != null)
                {
                    Vector3 displacement = new Vector3(visualDirection * visualPushDistance * 0.34f * pulseEnvelope,
                        visualLiftHeight * 0.28f * pulseEnvelope, 0f);
                    pushVisual.localPosition = visualStartLocalPosition +
                        pushVisual.parent.InverseTransformVector(displacement);
                }
                if (pulseProgress >= 1f)
                {
                    storyPulseActive = false;
                    if (pushVisual != null)
                        pushVisual.localPosition = visualStartLocalPosition;
                }
            }

            if (interactionActive && !hasCompletedPush)
            {
                visualElapsed += Time.fixedDeltaTime;
                float progress = Mathf.Clamp01(visualElapsed / visualInteractionSeconds);
                if (pushVisual != null)
                {
                    Vector3 displacement = new Vector3(visualDirection * visualPushDistance * progress,
                        Mathf.Sin(progress * Mathf.PI) * visualLiftHeight, 0f);
                    pushVisual.localPosition = visualStartLocalPosition +
                        pushVisual.parent.InverseTransformVector(displacement);
                }
                if (progress >= 1f)
                    CompletePush();
            }
            return;
        }

        if (Time.time - lastPushTime > Time.fixedDeltaTime * 1.5f)
            LockHorizontalMotion();

        if (!hasCompletedPush && completeAfterPushDistance > 0f &&
            Mathf.Abs(body.position.x - startX) >= completeAfterPushDistance)
        {
            CompletePush();
        }
    }

    public void BeginInteraction(float direction = 1f)
    {
        if (!CanBeInteractedWith)
            return;
        interactionActive = true;
        visualDirection = direction < 0f ? -1f : 1f;
    }

    public void ConfigureVisualInteraction(Transform visual)
    {
        animateVisualOnly = true;
        pushVisual = visual;
        disableColliderAfterPushCompletion = false;
        Rigidbody2D rigidbody = GetComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    public void ConfigureInteractionOnlyTrigger(bool enabled)
    {
        interactionOnlyTrigger = enabled;
        if (objectCollider == null)
            objectCollider = GetComponent<Collider2D>();
        if (objectCollider != null)
            objectCollider.isTrigger = enabled;
    }

    public void PlayStoryVisualPulse(float direction = 1f)
    {
        if (!animateVisualOnly || hasCompletedPush)
            return;

        if (pushVisual != null && visualStartLocalPosition == Vector3.zero && pushVisual.localPosition != Vector3.zero)
            visualStartLocalPosition = pushVisual.localPosition;
        visualDirection = direction < 0f ? -1f : 1f;
        storyPulseElapsed = 0f;
        storyPulseActive = true;
    }

    public void CompleteStoryVisualInteraction()
    {
        if (animateVisualOnly && !hasCompletedPush)
        {
            storyPulseActive = false;
            if (pushVisual != null)
                pushVisual.localPosition = visualStartLocalPosition;
            CompletePush();
        }
    }

    public void SetStoryGateOpenProgress(float progress)
    {
        if (!opensOnHinge || !hasReleasedTether || hasCompletedPush)
            return;

        gateOpenProgress = Mathf.Clamp01(progress);
        if (gateHinge != null)
            gateHinge.SetOpenProgress(gateOpenProgress);
        if (gateOpenProgress >= 1f)
            CompletePush();
    }

    public void ConfigureHingedGate(GateHingePresentation presentation)
    {
        opensOnHinge = true;
        animateVisualOnly = false;
        gateHinge = presentation;
        disableColliderAfterPushCompletion = true;
        var rigidbody = GetComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.gravityScale = 0f;
        rigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (!enabled)
            EndInteraction();
    }

    public void EndInteraction()
    {
        interactionActive = false;
        EndPush();
    }

    public void AddPullDistance(float distance)
    {
        if (!interactionActive || !NeedsBiteToRelease || distance <= 0f)
            return;

        pullProgress += distance;
        if (pullProgress >= pullDistanceToRelease)
            ReleaseTether();
    }

    public void PlayTetherReleaseParticles()
    {
        if (releaseParticles != null)
            releaseParticles.Play(true);
    }

    public bool TryPush(Collider2D playerCollider, float direction, float fixedDeltaTime)
    {
        if (!CanBePushed || !interactionActive || body == null || objectCollider == null ||
            playerCollider == null || (!opensOnHinge && body.bodyType != RigidbodyType2D.Dynamic))
        {
            return false;
        }

        direction = Mathf.Sign(direction);
        if (Mathf.Abs(direction) < 0.01f || !IsTouchingPushSide(playerCollider, direction))
            return false;

        if (opensOnHinge)
        {
            gateOpenProgress = Mathf.Min(1f, gateOpenProgress + fixedDeltaTime / gateOpenSeconds);
            if (gateHinge != null)
            {
                gateHinge.SetOpenProgress(gateOpenProgress);
            }
            if (gateOpenProgress >= 1f) CompletePush();
            return true;
        }

        body.constraints &= ~RigidbodyConstraints2D.FreezePositionX;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        Vector2 velocity = body.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, direction * maximumPushSpeed,
            pushAcceleration * fixedDeltaTime);
        body.linearVelocity = velocity;
        lastPushTime = Time.time;
        return true;
    }

    public void EndPush()
    {
        lastPushTime = float.NegativeInfinity;
        LockHorizontalMotion();
    }

    public void ResetState()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (objectCollider == null)
            objectCollider = GetComponent<Collider2D>();

        hasCompletedPush = false;
        hasReleasedTether = !requiresBiteToRelease;
        pullProgress = 0f;
        visualElapsed = 0f;
        storyPulseElapsed = 0f;
        storyPulseActive = false;
        gateOpenProgress = 0f;
        if (gateHinge != null) gateHinge.ResetPose();
        interactionActive = false;
        if (pushVisual != null)
            pushVisual.localPosition = visualStartLocalPosition;
        if (body != null)
        {
            body.position = new Vector2(startX, opensOnHinge ? startY : body.position.y);
            body.linearVelocity = Vector2.zero;
            LockHorizontalMotion();
        }
        if (objectCollider != null)
        {
            objectCollider.enabled = true;
            if (interactionOnlyTrigger)
                objectCollider.isTrigger = true;
            else if (animateVisualOnly)
                objectCollider.isTrigger = false;
        }
        if (tetherVisual != null)
            tetherVisual.SetActive(!hasReleasedTether);
        if (releaseParticles != null)
            releaseParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private bool IsTouchingPushSide(Collider2D playerCollider, float direction)
    {
        Bounds playerBounds = playerCollider.bounds;
        Bounds objectBounds = objectCollider.bounds;
        float verticalOverlap = Mathf.Min(playerBounds.max.y, objectBounds.max.y) -
                                Mathf.Max(playerBounds.min.y, objectBounds.min.y);
        if (verticalOverlap <= 0.04f)
            return false;

        float gap = direction > 0f
            ? objectBounds.min.x - playerBounds.max.x
            : playerBounds.min.x - objectBounds.max.x;
        return gap <= maximumContactGap && gap >= -0.18f;
    }

    private void ReleaseTether()
    {
        if (hasReleasedTether)
            return;

        hasReleasedTether = true;
        pullProgress = pullDistanceToRelease;
        if (tetherVisual != null)
            tetherVisual.SetActive(false);
        if (releaseParticles != null)
            releaseParticles.Play(true);
        onTetherReleased?.Invoke();
        PullCompleted?.Invoke(this);
    }

    private void CompletePush()
    {
        hasCompletedPush = true;
        interactionActive = false;
        EndPush();
        // Once the owner wakes, the dog walks beside the stationary bed in the 2D lane.
        // Keep its bounds for the existing no-jump zone, without blocking the hallway.
        if (animateVisualOnly && objectCollider != null)
            objectCollider.isTrigger = true;
        if (disableColliderAfterPushCompletion && objectCollider != null)
            objectCollider.enabled = false;
        onPushCompleted?.Invoke();
        PushCompleted?.Invoke(this);
    }

    private void LockHorizontalMotion()
    {
        if (body == null)
            return;

        if (opensOnHinge)
        {
            body.linearVelocity = Vector2.zero;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            return;
        }

        Vector2 velocity = body.linearVelocity;
        velocity.x = 0f;
        body.linearVelocity = velocity;
        body.constraints |= RigidbodyConstraints2D.FreezePositionX |
                            RigidbodyConstraints2D.FreezeRotation;
    }

    private void OnValidate()
    {
        maximumPushSpeed = Mathf.Max(0.1f, maximumPushSpeed);
        pushAcceleration = Mathf.Max(0.1f, pushAcceleration);
        maximumContactGap = Mathf.Max(0f, maximumContactGap);
        pullDistanceToRelease = Mathf.Max(0.05f, pullDistanceToRelease);
        completeAfterPushDistance = Mathf.Max(0f, completeAfterPushDistance);
    }
}
