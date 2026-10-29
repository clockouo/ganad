using UnityEngine;

/// <summary>Keeps final art layers in step with the existing gameplay state.</summary>
[DefaultExecutionOrder(100)]
public sealed class JourneyWorldArtPresentation : MonoBehaviour
{
    public AfterTheGlowStoryDirector story;
    public SpriteRenderer headlights, lampGlow, waterJet, bathroomDoor, leashKnot;
    public SpriteRenderer leftHeadlight, rightHeadlight;
    public SpriteRenderer dog;
    [HideInInspector] public bool bathroomArtAligned;
    [HideInInspector] public Vector3 bathroomClosedPosition, bathroomClosedScale;
    private void LateUpdate()
    {
        if (story == null) return;
        if (headlights != null) headlights.enabled = story.carHeadlight != null && story.carHeadlight.activeInHierarchy;
        bool headlightsOn = story.carHeadlight != null && story.carHeadlight.activeInHierarchy;
        if (leftHeadlight != null) leftHeadlight.enabled = headlightsOn;
        if (rightHeadlight != null) rightHeadlight.enabled = headlightsOn;
        if (lampGlow != null) lampGlow.enabled = story.homeRoomLighting != null && story.homeRoomLighting.IsRoomLightOn;
        if (waterJet != null) waterJet.sprite = JourneyArt.Frame("Effects/Water__c2r2", 2 + Mathf.FloorToInt(Time.time * 5f) % 2);
        if (bathroomDoor != null) SetBathroomDoor(story.BathroomDoorOpen);
        if (leashKnot != null) leashKnot.enabled = story.parkGate != null && story.parkGate.NeedsBiteToRelease;
        if (dog != null && story.player != null)
        {
            var interaction = story.player.GetComponent<PlayerInteraction>();
            if (interaction != null && interaction.IsBiting)
                dog.sprite = JourneyArt.Frame("Characters/Dog/" + (Mathf.Abs(story.player.Body.linearVelocity.x) > .05f ? "bite_pull" : "bite"), Mathf.FloorToInt(Time.time * 10f) % 4);
        }
    }

    public void SetBathroomDoor(bool open)
    {
        if (bathroomDoor == null) return;
        var closed = JourneyArt.Frame("Props/Doors__c2r2", 0);
        var desired = JourneyArt.Frame("Props/Doors__c2r2", open ? 1 : 0);
        bathroomDoor.sprite = desired;
        if (!bathroomArtAligned) return;
        // The open drawing has a taller source rectangle. Keep the same world
        // height and left hinge instead of letting it grow outside the doorway.
        float ratio = closed.bounds.size.y / desired.bounds.size.y;
        Vector3 scale = bathroomClosedScale;
        scale.x *= ratio; scale.y *= ratio;
        float closedWidth = closed.bounds.size.x * bathroomClosedScale.x;
        float width = desired.bounds.size.x * scale.x;
        bathroomDoor.transform.localScale = scale;
        bathroomDoor.transform.localPosition = bathroomClosedPosition + Vector3.left * ((closedWidth - width) * .5f);
    }
}
