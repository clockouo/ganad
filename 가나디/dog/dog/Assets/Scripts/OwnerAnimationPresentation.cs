using UnityEngine;

/// <summary>Plays the supplied owner strips without changing story or interaction timing.</summary>
[DisallowMultipleComponent]
public sealed class OwnerAnimationPresentation : MonoBehaviour
{
    public AfterTheGlowStoryDirector director;
    public SpriteRenderer ownerRenderer;
    public SpriteRenderer sleepingRenderer;
    public SpriteRenderer bedDuvetRenderer;
    public Sprite[] walkFrames;
    public Sprite[] petFrames;
    public Sprite[] sleepFrames;
    public float walkFrameSeconds = .09f;
    public float petFrameSeconds = .16f;
    public float sleepFrameSeconds = .32f;

    private bool petting;
    private float petStartedAt, walkTime, previousOwnerX;

    private void Start()
    {
        if (director == null) director = GetComponent<AfterTheGlowStoryDirector>();
        if (director != null && director.owner != null) previousOwnerX = director.owner.position.x;
    }

    public void SetPetting(bool value)
    {
        if (petting != value) petStartedAt = Time.time;
        petting = value;
    }

    private void LateUpdate()
    {
        if (director == null) return;
        bool asleep = director.Stage == AfterTheGlowStoryStage.WakeTheOwner ||
                      director.Stage == AfterTheGlowStoryStage.BedWakePuzzle;
        if (sleepingRenderer != null)
        {
            sleepingRenderer.enabled = asleep;
            if (asleep && sleepFrames != null && sleepFrames.Length > 0)
                sleepingRenderer.sprite = sleepFrames[Mathf.FloorToInt(Time.time / Mathf.Max(.03f, sleepFrameSeconds)) % sleepFrames.Length];
        }
        if (bedDuvetRenderer != null) bedDuvetRenderer.enabled = !asleep;
        if (director.owner == null || ownerRenderer == null) return;
        float x = director.owner.position.x;
        bool moving = Mathf.Abs(x - previousOwnerX) > .0001f;
        previousOwnerX = x;
        if (!director.owner.gameObject.activeInHierarchy) return;

        if (petting && petFrames != null && petFrames.Length > 0)
        {
            int beat = Mathf.FloorToInt((Time.time - petStartedAt) / Mathf.Max(.03f, petFrameSeconds));
            // Bend down once, then repeat the final two hand positions.
            int index = beat < petFrames.Length ? beat :
                Mathf.Max(0, petFrames.Length - 2) + (beat - petFrames.Length) % Mathf.Min(2, petFrames.Length);
            ownerRenderer.sprite = petFrames[index];
        }
        else if (moving && walkFrames != null && walkFrames.Length > 0)
        {
            walkTime += Time.deltaTime;
            ownerRenderer.sprite = walkFrames[Mathf.FloorToInt(walkTime / Mathf.Max(.03f, walkFrameSeconds)) % walkFrames.Length];
        }
        else
        {
            walkTime = 0f;
            if (petFrames != null && petFrames.Length > 0) ownerRenderer.sprite = petFrames[0];
        }
    }
}
