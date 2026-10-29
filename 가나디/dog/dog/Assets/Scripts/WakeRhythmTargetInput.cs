using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Uses the initial press timestamp for mouse, touch and UI submit.</summary>
[DisallowMultipleComponent]
public sealed class WakeRhythmTargetInput : MonoBehaviour, IPointerDownHandler, ISubmitHandler
{
    private BedWakePuzzleUI owner;
    public void Bind(BedWakePuzzleUI puzzle) => owner = puzzle;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && owner != null)
            owner.PressRhythmTarget();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (owner != null) owner.PressRhythmTarget();
    }
}
