using UnityEngine;

/// <summary>Clickable house doorway. Crossing a floor trigger never exits the house.</summary>
[DisallowMultipleComponent]
public sealed class HouseExitInteraction : MonoBehaviour
{
    public AfterTheGlowStoryDirector director;
    public SpriteRenderer clickArea;
    [Min(0.1f)] public float interactionRange = 1.6f;
    public BoxCollider2D boundaryBlocker;

    public Bounds ClickBounds => clickArea != null ? clickArea.bounds : new Bounds(transform.position, Vector3.zero);

    private void Awake()
    {
        if (boundaryBlocker != null)
            boundaryBlocker.isTrigger = false;
    }

    private void Update()
    {
        bool doorOpen = director != null && director.FrontDoorOpenness > 0.01f;
        if (boundaryBlocker != null && boundaryBlocker.enabled)
            boundaryBlocker.isTrigger = doorOpen;

        if (!doorOpen || director.Stage != AfterTheGlowStoryStage.HouseExitReady ||
            director.player == null || !director.player.AllowsPlayerInput ||
            !director.player.IsGrounded || director.player.BodyCollider == null)
            return;

        // Crossing the visible dark opening is the exit input. There is no click prompt.
        if (ClickBounds.Intersects(director.player.BodyCollider.bounds))
            director.BeginStreetDeparture();
    }

    public bool CanInteract(PlayerMovement actor)
    {
        if (!isActiveAndEnabled || director == null || actor == null ||
            director.player != actor || director.Stage != AfterTheGlowStoryStage.HouseExitReady ||
            director.FrontDoorOpenness <= 0.01f ||
            !actor.AllowsPlayerInput || !actor.IsGrounded || clickArea == null)
            return false;
        Vector3 position = actor.BodyCollider != null ? actor.BodyCollider.bounds.center : actor.transform.position;
        Vector3 nearest = ClickBounds.ClosestPoint(position);
        return Vector2.Distance(position, nearest) <= interactionRange;
    }

    public bool TryInteractAtScreenPosition(Vector2 screenPosition, PlayerMovement actor)
    {
        return false;
    }

    public bool TryInteractAtWorldPosition(Vector2 worldPosition, PlayerMovement actor)
    {
        return false;
    }
}
