using UnityEngine;

/// <summary>The left leaf opens about its outer hinge; the right leaf stays closed.</summary>
[DisallowMultipleComponent]
public sealed class GateHingePresentation : MonoBehaviour
{
    public SpriteRenderer leftLeaf;
    public SpriteRenderer rightLeaf;
    public Sprite boundLeftSprite;
    public Sprite boundRightSprite;
    public Sprite releasedLeftSprite;
    public Sprite releasedRightSprite;
    public Sprite openedLeftSprite;
    public Sprite openedRightSprite;
    public Vector3 closedScale = Vector3.one;
    public Vector3 releasedLeftScale = Vector3.one;
    public Vector3 releasedRightScale = Vector3.one;
    public Vector3 openedLeftScale = Vector3.one;
    public Vector3 openedRightScale = Vector3.one;
    [Range(0.1f, 0.8f)] public float openedWidthFraction = 0.4f;
    public bool separateLeashVisual;
    [Header("Left leaf opening perspective")]
    public bool opensIntoGarden = true;
    public Material closedMaterial;
    public Material forwardOpenMaterial;
    public int closedSortingLayerID;
    public int closedSortingOrder = 14;
    public int foregroundSortingLayerID;
    public int foregroundSortingOrder = 20;
    [Header("Left leaf behind the walking lane")]
    public bool keepLeftLeafBehindActors;
    public int leftLeafSortingLayerID;
    public int leftLeafSortingOrder = 8;
    [Min(0f)] public float forwardDepth = .42f;
    [Min(0f)] public float forwardPerspective = .18f;
    public float OpenProgress { get; private set; }
    private bool released;
    private MaterialPropertyBlock leafProperties;

    public void SetReleased(bool value)
    {
        released = value;
        SetOpenProgress(OpenProgress);
    }

    public void ResetPose()
    {
        released = false;
        SetOpenProgress(0f);
    }

    public void SetOpenProgress(float value)
    {
        OpenProgress = Mathf.Clamp01(value);
        ApplyLeaf(leftLeaf, boundLeftSprite, releasedLeftSprite, openedLeftSprite,
            releasedLeftScale, openedLeftScale, true);
        ApplyLeaf(rightLeaf, boundRightSprite, releasedRightSprite, openedRightSprite,
            releasedRightScale, openedRightScale, false);
    }

    private void ApplyLeaf(SpriteRenderer leaf, Sprite bound, Sprite unbound, Sprite opened,
        Vector3 unboundScale, Vector3 openScale, bool shouldOpen)
    {
        if (leaf == null) return;
        if (shouldOpen && (released || separateLeashVisual) && forwardOpenMaterial != null && unbound != null)
        {
            leaf.sprite = unbound;
            leaf.transform.localScale = unboundScale;
            leaf.sharedMaterial = forwardOpenMaterial;
            ApplySorting(leaf, shouldOpen, !opensIntoGarden);
            if (leafProperties == null) leafProperties = new MaterialPropertyBlock();
            leafProperties.Clear();
            leafProperties.SetFloat("_OpenAmount", Mathf.SmoothStep(0f, 1f, OpenProgress));
            leafProperties.SetFloat("_LeafWidth", unbound.bounds.size.x);
            leafProperties.SetFloat("_HingeX", unbound.bounds.min.x);
            leafProperties.SetFloat("_HingeY", unbound.bounds.min.y);
            leafProperties.SetFloat("_WidthFraction", openedWidthFraction);
            float direction = opensIntoGarden ? -1f : 1f;
            leafProperties.SetFloat("_ForwardDepth", direction * forwardDepth / Mathf.Max(.001f, Mathf.Abs(leaf.transform.lossyScale.y)));
            leafProperties.SetFloat("_Perspective", direction * forwardPerspective);
            leaf.SetPropertyBlock(leafProperties);
            leaf.enabled = true;
            return;
        }
        leaf.SetPropertyBlock(null);
        if (closedMaterial != null) leaf.sharedMaterial = closedMaterial;
        ApplySorting(leaf, shouldOpen, false);
        if (OpenProgress >= 1f && shouldOpen)
        {
            leaf.sprite = opened;
            leaf.transform.localScale = openScale;
        }
        else
        {
            leaf.sprite = released || separateLeashVisual ? unbound : bound;
            float fraction = shouldOpen
                ? Mathf.Lerp(1f, openedWidthFraction, Mathf.SmoothStep(0f, 1f, OpenProgress))
                : 1f;
            Vector3 scale = released || separateLeashVisual ? unboundScale : closedScale;
            leaf.transform.localScale = new Vector3(scale.x * fraction, scale.y, scale.z);
        }
        leaf.enabled = true;
    }

    private void ApplySorting(SpriteRenderer leaf, bool isLeft, bool foreground)
    {
        if (isLeft && keepLeftLeafBehindActors)
        {
            leaf.sortingLayerID = leftLeafSortingLayerID;
            leaf.sortingOrder = leftLeafSortingOrder;
            return;
        }
        leaf.sortingLayerID = foreground ? foregroundSortingLayerID : closedSortingLayerID;
        leaf.sortingOrder = foreground ? foregroundSortingOrder : closedSortingOrder;
    }
}
