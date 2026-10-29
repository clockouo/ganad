using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AfterTheGlowStoryDirector))]
public sealed class FrontDoorStateSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject homeDoorClosed;
    [SerializeField] private GameObject homeDoorOpen;
    [SerializeField] private GameObject exitDoorClosed;
    [SerializeField] private GameObject exitDoorOpen;

    private AfterTheGlowStoryDirector storyDirector;
    private bool isOpen;

    private void Awake()
    {
        storyDirector = GetComponent<AfterTheGlowStoryDirector>();
        ApplyState(false);
    }

    private void Update()
    {
        if (storyDirector == null)
            return;

        bool shouldBeOpen = storyDirector.FrontDoorOpenness > 0.01f;
        if (shouldBeOpen != isOpen)
            ApplyState(shouldBeOpen);
    }

    private void ApplyState(bool open)
    {
        if (homeDoorClosed != null)
            homeDoorClosed.SetActive(!open);
        if (homeDoorOpen != null)
            homeDoorOpen.SetActive(open);
        if (exitDoorClosed != null)
            exitDoorClosed.SetActive(!open);
        if (exitDoorOpen != null)
            exitDoorOpen.SetActive(open);

        isOpen = open;
    }
}
