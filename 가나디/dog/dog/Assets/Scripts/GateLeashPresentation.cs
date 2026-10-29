using UnityEngine;

/// <summary>A reachable hanging strap that follows the dog's mouth after contact.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(250)]
public sealed class GateLeashPresentation : MonoBehaviour
{
    public PushableObject gate;
    public Transform knot;
    public Transform grip;
    public LineRenderer strap;
    public LineRenderer knotLoop;
    private PlayerInteraction bitingActor;
    private float restingLength;

    public Vector3 GripPosition => grip.position;

    public void BeginBite(PlayerInteraction actor)
    {
        bitingActor = actor;
        restingLength = Vector2.Distance(knot.position, actor.MouthPosition);
        RenderPose(actor.MouthPosition);
    }

    public void EndBite()
    {
        bitingActor = null;
        RenderPose(null);
    }

    private void LateUpdate()
    {
        RenderPose(bitingActor != null && bitingActor.IsBiting ? bitingActor.MouthPosition : (Vector3?)null);
    }

    public void RenderPose(Vector3? mouth)
    {
        if (strap == null || knotLoop == null || knot == null || grip == null) return;
        bool visible = gate != null && gate.NeedsBiteToRelease;
        strap.enabled = knotLoop.enabled = visible;
        if (!visible) return;
        Vector3 start = knot.position;
        Vector3 end = mouth ?? grip.position;
        // Draw in front of the gate and at the muzzle, never at the body collider.
        start.z = end.z = -.55f;
        float extension = mouth.HasValue ? Mathf.Max(0f, Vector2.Distance(start, end) - restingLength) : 0f;
        float sag = Mathf.Lerp(.13f, .008f, Mathf.Clamp01(extension / .22f));
        const int segments = 12;
        strap.positionCount = segments + 5;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            strap.SetPosition(i, Vector3.Lerp(start, end, t) + Vector3.down * (4f * sag * t * (1f - t)));
        }
        // The free handle hangs before contact and folds around the mouth when held.
        float w = mouth.HasValue ? .045f : .055f;
        float h = mouth.HasValue ? .035f : .12f;
        strap.SetPosition(segments + 1, end + new Vector3(w, -h, 0f));
        strap.SetPosition(segments + 2, end + new Vector3(-w, -h, 0f));
        strap.SetPosition(segments + 3, end + new Vector3(-w, -.02f, 0f));
        strap.SetPosition(segments + 4, end);
        knotLoop.positionCount = 5;
        knotLoop.SetPosition(0, start + new Vector3(-.07f, .03f, 0f));
        knotLoop.SetPosition(1, start + new Vector3(.06f, .03f, 0f));
        knotLoop.SetPosition(2, start + new Vector3(.06f, -.035f, 0f));
        knotLoop.SetPosition(3, start + new Vector3(-.07f, -.035f, 0f));
        knotLoop.SetPosition(4, start + new Vector3(-.07f, .03f, 0f));
    }

    private void OnDisable()
    {
        bitingActor = null;
        if (strap != null) strap.enabled = false;
        if (knotLoop != null) knotLoop.enabled = false;
    }
}
