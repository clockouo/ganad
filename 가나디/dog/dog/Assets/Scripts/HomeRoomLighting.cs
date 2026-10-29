using System.Collections;
using UnityEngine;

/// <summary>House-only night tint and the owner's visible wall-switch action.</summary>
[DisallowMultipleComponent]
public sealed class HomeRoomLighting : MonoBehaviour
{
    public SpriteRenderer roomShade;
    public Transform wallSwitch;
    public SpriteRenderer switchRocker;
    public SpriteRenderer switchIndicator;
    public LineRenderer ownerReach;
    public SpriteRenderer ownerRenderer;
    public SpriteMask[] restingArmMasks;
    [Range(0f, .65f)] public float startingDarkness = .28f;
    [Min(.05f)] public float lightFadeSeconds = .35f;
    public float ownerStandOffset = .46f;
    public bool IsRoomLightOn { get; private set; }
    public float OwnerSwitchStandX => wallSwitch.position.x - ownerStandOffset;
    private bool ownerSwitchSequenceRunning;

    private void Awake() => SetLightImmediate(false);

    public void SetLightImmediate(bool on)
    {
        IsRoomLightOn = on;
        if (!on)
            ownerSwitchSequenceRunning = false;
        SetDarkness(on ? 0f : startingDarkness);
        UpdateSwitchPose();
        SetReachVisible(false);
    }

    private void SetDarkness(float alpha)
    {
        if (roomShade == null) return;
        roomShade.color = new Color(.035f, .065f, .11f, alpha);
        roomShade.enabled = alpha > .001f;
    }

    private void UpdateSwitchPose()
    {
        if (switchRocker != null)
        {
            switchRocker.transform.localPosition = new Vector3(0f, IsRoomLightOn ? -.018f : .018f, -.01f);
            switchRocker.color = IsRoomLightOn ? new Color(.84f, .72f, .46f) : new Color(.42f, .43f, .34f);
        }
        if (switchIndicator != null)
            switchIndicator.color = IsRoomLightOn ? new Color(1f, .87f, .46f) : new Color(.21f, .32f, .31f);
    }

    public void SetOwnerReach(Transform owner, float amount)
    {
        bool visible = ownerReach != null && owner != null && wallSwitch != null && amount > .001f;
        SetReachVisible(visible);
        if (!visible) return;
        float facing = wallSwitch.position.x >= owner.position.x ? 1f : -1f;
        Vector3 shoulder = owner.position + new Vector3(facing * .22f, 1.0f, 0f);
        Vector3 rest = shoulder + Vector3.down * .60f;
        Vector3 hand = Vector3.Lerp(rest, wallSwitch.position, Mathf.SmoothStep(0f, 1f, amount));
        Vector3 elbow = Vector3.Lerp(shoulder, hand, .5f) + Vector3.down * .08f;
        shoulder.z = elbow.z = hand.z = -.65f;
        ownerReach.positionCount = 3;
        ownerReach.SetPosition(0, shoulder);
        ownerReach.SetPosition(1, elbow);
        ownerReach.SetPosition(2, hand);
    }

    private void SetReachVisible(bool visible)
    {
        if (ownerReach != null) ownerReach.enabled = visible;
        if (ownerRenderer != null)
            ownerRenderer.maskInteraction = visible ? SpriteMaskInteraction.VisibleOutsideMask : SpriteMaskInteraction.None;
        if (restingArmMasks != null)
            foreach (var mask in restingArmMasks) if (mask != null) mask.enabled = visible;
    }

    public IEnumerator PlayOwnerSwitch(Transform owner)
    {
        if (IsRoomLightOn || ownerSwitchSequenceRunning || wallSwitch == null) yield break;
        ownerSwitchSequenceRunning = true;
        float elapsed = 0f;
        while (elapsed < .24f)
        {
            elapsed += Time.deltaTime;
            SetOwnerReach(owner, Mathf.Clamp01(elapsed / .24f));
            yield return null;
        }
        SetOwnerReach(owner, 1f);
        yield return new WaitForSeconds(.08f);
        // The click, rocker movement and visible room brightening start together.
        IsRoomLightOn = true;
        UpdateSwitchPose();
        GameAudioManager.Instance?.PlaySfx("LightSwitchOn", .5f, .98f);
        elapsed = 0f;
        while (elapsed < lightFadeSeconds)
        {
            elapsed += Time.deltaTime;
            SetDarkness(startingDarkness * (1f - Mathf.SmoothStep(0f, 1f, elapsed / lightFadeSeconds)));
            yield return null;
        }
        SetDarkness(0f);
        yield return new WaitForSeconds(.12f);
        elapsed = 0f;
        while (elapsed < .18f)
        {
            elapsed += Time.deltaTime;
            SetOwnerReach(owner, 1f - Mathf.Clamp01(elapsed / .18f));
            yield return null;
        }
        SetReachVisible(false);
        ownerSwitchSequenceRunning = false;
    }

    private void OnDisable()
    {
        ownerSwitchSequenceRunning = false;
        SetReachVisible(false);
    }
}
