using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Identifies colliders that block gameplay light rays. Add it to fixed walls
/// and movable shadow blockers; ordinary floor and decoration colliders do not
/// block gameplay light unless they carry this component.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(ShadowCaster2D))]
public sealed class LightOccluder2D : MonoBehaviour
{
    private static readonly RaycastHit2D[] RaycastBuffer = new RaycastHit2D[32];

    [SerializeField] private bool blocksGameplayLight = true;
    private Collider2D occlusionCollider;

    public bool BlocksGameplayLight =>
        blocksGameplayLight &&
        isActiveAndEnabled &&
        occlusionCollider != null &&
        occlusionCollider.enabled &&
        !occlusionCollider.isTrigger;

    private void Awake()
    {
        CacheCollider();
    }

    private void OnValidate()
    {
        CacheCollider();
    }

    private void CacheCollider()
    {
        if (occlusionCollider == null)
            occlusionCollider = GetComponent<Collider2D>();
    }

    public static bool IsBlocked(
        Vector2 source,
        Vector2 target,
        Transform ignoredRoot,
        LayerMask occluderLayers)
    {
        Vector2 ray = target - source;
        float distance = ray.magnitude;
        if (distance <= 0.01f)
            return false;

        int hitCount = Physics2D.RaycastNonAlloc(
            source,
            ray / distance,
            RaycastBuffer,
            Mathf.Max(0f, distance - 0.02f),
            occluderLayers);

        Transform rootToIgnore = ignoredRoot != null ? ignoredRoot.root : null;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = RaycastBuffer[i].collider;
            if (hitCollider == null || hitCollider.isTrigger)
                continue;

            if (rootToIgnore != null && hitCollider.transform.root == rootToIgnore)
                continue;

            LightOccluder2D occluder = hitCollider.GetComponentInParent<LightOccluder2D>();
            if (occluder != null && occluder.BlocksGameplayLight)
                return true;
        }

        return false;
    }
}
