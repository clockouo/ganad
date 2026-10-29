using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scene-agnostic goal trigger. It emits one completion event and leaves
/// transitions, story flow, and level-specific reactions to the scene.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class GoalTrigger2D : MonoBehaviour
{
    [SerializeField] private bool completeOnce = true;
    [SerializeField] private UnityEvent onGoalCompleted = new UnityEvent();

    private bool hasCompleted;

    public static event Action<GoalTrigger2D, PlayerMovement> GoalReached;
    public bool HasCompleted => hasCompleted;
    public UnityEvent OnGoalCompleted => onGoalCompleted;

    private void Awake()
    {
        Collider2D goalCollider = GetComponent<Collider2D>();
        if (goalCollider != null)
            goalCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (completeOnce && hasCompleted)
            return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null)
            return;

        hasCompleted = true;
        onGoalCompleted?.Invoke();
        GoalReached?.Invoke(this, player);
    }

    private void OnValidate()
    {
        Collider2D goalCollider = GetComponent<Collider2D>();
        if (goalCollider != null)
            goalCollider.isTrigger = true;
    }
}
