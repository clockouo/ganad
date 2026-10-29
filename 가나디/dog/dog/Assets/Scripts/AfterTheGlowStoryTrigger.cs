using UnityEngine;

public enum AfterTheGlowTriggerKind
{
    HouseExit = 0,
    ParkEntry = 1,
    OwnerPreparationTarget = 2
}

/// <summary>One-shot trigger that advances the playable house/street story.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class AfterTheGlowStoryTrigger : MonoBehaviour
{
    public AfterTheGlowStoryDirector director;
    public AfterTheGlowTriggerKind triggerKind;
    private bool consumed;

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || director == null || other.GetComponentInParent<PlayerMovement>() == null)
            return;

        bool accepted;
        switch (triggerKind)
        {
            case AfterTheGlowTriggerKind.OwnerPreparationTarget:
                accepted = director.ReachOwnerPreparationTarget();
                break;
            case AfterTheGlowTriggerKind.HouseExit:
                // House departure is accepted only by the clickable doorway.
                accepted = false;
                break;
            case AfterTheGlowTriggerKind.ParkEntry:
                accepted = director.EnterParkPlaceholder();
                break;
            default:
                accepted = false;
                break;
        }
        consumed = accepted;
    }
}
