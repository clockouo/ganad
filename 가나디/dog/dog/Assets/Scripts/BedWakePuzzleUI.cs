using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Face-lick rhythm targets: press when the approaching ring meets the fixed ring.
/// Missed beats repeat the same target without removing previously earned progress.
/// </summary>
[DisallowMultipleComponent]
public sealed class BedWakePuzzleUI : MonoBehaviour
{
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private CanvasGroup screenGroup;
    [SerializeField] private Button touchButton;
    [SerializeField] private Image[] progressDots = new Image[4];
    [SerializeField] private Image touchHint;
    [Header("Face lick rhythm")]
    [Min(.3f)] [SerializeField] private float approachDuration = 1.1f;
    [Min(.03f)] [SerializeField] private float timingWindow = .16f;
    [Min(.1f)] [SerializeField] private float retryDelay = .32f;

    private AfterTheGlowStoryDirector director;
    private int touchCount;
    private bool isOpen;
    private float hintAlpha = 0.8f;
    private float targetAppearedAt;
    private Coroutine ringRoutine;
    private UnityEngine.UI.Image approachRing;
    private Coroutine presentationRoutine;
    private RectTransform closeupContent;
    private Vector2 closeupRest;
    private bool spreading;
    public bool IsAnimating => spreading || ringRoutine != null;
    public int TouchCount => touchCount;
    private static readonly Vector2[] FaceTargets = {
        // Local portrait positions follow the face when the bed fits the screen.
        new Vector2(.24f, .33f), new Vector2(.76f, .33f),
        new Vector2(.38f, .23f), new Vector2(.62f, .23f)
    };

    private void Awake()
    {
        BindRhythmInput();
        Close();
    }

    private void Update()
    {
        if (SettingsMenuController.IsOpen)
            return;

        if (isOpen && screenRoot != null)
        {
            var face = screenRoot.transform.Find("Sleeping Owner Close-up/Final Wake Face");
            if (face != null) face.GetComponent<UnityEngine.UI.Image>().sprite = JourneyArt.Frame("Puzzles/WakeFaces__c2r2", Mathf.Min(3, touchCount));
        }

        if (!isOpen || touchHint == null || touchButton == null || spreading || ringRoutine != null)
            return;

        float elapsed = Time.unscaledTime - targetAppearedAt;
        float appeared = Mathf.Clamp01(elapsed / .18f);
        touchButton.interactable = elapsed >= 0f;
        bool onBeat = Mathf.Abs(elapsed - approachDuration) <= timingWindow;
        touchHint.rectTransform.localScale = Vector3.one;
        Color color = onBeat ? new Color(1f, .94f, .74f, 1f) : new Color(.015f, .04f, .05f, hintAlpha);
        color.a *= appeared;
        touchHint.color = color;
        if (approachRing != null)
        {
            float progress = Mathf.Clamp(elapsed / approachDuration, 0f, 1f + timingWindow / approachDuration);
            approachRing.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(1.9f, 1f, progress);
            approachRing.color = color;
        }
        if (elapsed > approachDuration + timingWindow)
            ringRoutine = StartCoroutine(MissAndRetry());
    }

    public void Configure(GameObject root, CanvasGroup group, Button button,
        Image[] dots, Image hint)
    {
        screenRoot = root;
        screenGroup = group;
        touchButton = button;
        progressDots = dots;
        touchHint = hint;
        BindRhythmInput();
    }

    private void BindRhythmInput()
    {
        if (touchButton == null) return;
        // Rhythm is measured on the press, not after the mouse/finger is released.
        // Button submission is handled by the same adapter without an onClick double hit.
        touchButton.onClick.RemoveListener(RegisterDirectTouch);
        var input = touchButton.GetComponent<WakeRhythmTargetInput>();
        if (input == null) input = touchButton.gameObject.AddComponent<WakeRhythmTargetInput>();
        input.Bind(this);
    }

    public void Open(AfterTheGlowStoryDirector storyDirector)
    {
        Close();
        BindRhythmInput();
        director = storyDirector;
        touchCount = 0;
        isOpen = true;
        if (closeupContent == null && screenRoot != null)
        {
            closeupContent = screenRoot.transform.Find("Sleeping Owner Close-up") as RectTransform;
            if (closeupContent != null) closeupRest = closeupContent.anchoredPosition;
        }
        if (screenRoot != null)
            screenRoot.SetActive(true);
        if (screenGroup != null)
        {
            screenGroup.alpha = 0f;
            screenGroup.interactable = false;
            screenGroup.blocksRaycasts = false;
        }
        if (touchHint != null)
        {
            hintAlpha = .82f;
            touchHint.color = new Color(.015f, .04f, .05f, .95f);
            if (approachRing == null)
            {
                var go = new GameObject("Wake Approach Ring", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(touchButton.transform, false);
                approachRing = go.GetComponent<UnityEngine.UI.Image>();
                approachRing.sprite = JourneyArt.Frame("UI/Indicators__c4r2", 1);
                approachRing.raycastTarget = false;
                approachRing.rectTransform.sizeDelta = touchHint.rectTransform.sizeDelta;
            }
        }
        ShowFaceTarget();
        RefreshProgress();
        if (screenGroup != null && closeupContent != null)
        {
            spreading = true;
            presentationRoutine = StartCoroutine(OpenSoftly());
        }
        else
        {
            EnablePuzzleInput();
        }
    }

    public void Close()
    {
        if (ringRoutine != null) { StopCoroutine(ringRoutine); ringRoutine = null; }
        if (presentationRoutine != null) { StopCoroutine(presentationRoutine); presentationRoutine = null; }
        if (closeupContent != null) closeupContent.anchoredPosition = closeupRest;
        spreading = false;
        isOpen = false;
        if (screenGroup != null)
        {
            screenGroup.alpha = 0f;
            screenGroup.interactable = false;
            screenGroup.blocksRaycasts = false;
        }
        if (screenRoot != null && screenRoot.activeSelf)
            screenRoot.SetActive(false);
    }

    private IEnumerator OpenSoftly()
    {
        yield return SoftPanelTransition.Play(screenGroup, closeupContent, true, .72f);
        spreading = false;
        presentationRoutine = null;
        EnablePuzzleInput();
    }

    private void EnablePuzzleInput()
    {
        if (screenGroup != null)
        {
            screenGroup.interactable = true;
            screenGroup.blocksRaycasts = true;
        }
        ShowFaceTarget();
    }

    private void RegisterDirectTouch()
    {
        if (!isOpen || spreading || SettingsMenuController.IsOpen || director == null ||
            ringRoutine != null || !touchButton.interactable)
            return;

        float error = Mathf.Abs(Time.unscaledTime - targetAppearedAt - approachDuration);
        ringRoutine = StartCoroutine(error <= timingWindow ? MatchRingsAndAdvance() : MissAndRetry());
    }

    public void PressRhythmTarget() => RegisterDirectTouch();

    private void ShowFaceTarget()
    {
        if (touchButton == null) return;
        RectTransform rect = (RectTransform)touchButton.transform;
        rect.anchorMin = rect.anchorMax = FaceTargets[Mathf.Clamp(touchCount, 0, 3)];
        rect.anchoredPosition = Vector2.zero;
        touchButton.interactable = false;
        targetAppearedAt = Time.unscaledTime + .15f;
        touchHint.rectTransform.localScale = Vector3.one;
        touchHint.color = new Color(.015f, .04f, .05f, 0f);
        if (approachRing != null)
        {
            approachRing.rectTransform.localScale = Vector3.one * 1.9f;
            approachRing.color = new Color(.015f, .04f, .05f, 0f);
        }
    }

    private IEnumerator MatchRingsAndAdvance()
    {
        touchButton.interactable = false;
        if (approachRing != null)
        {
            approachRing.rectTransform.localScale = Vector3.one;
            approachRing.color = new Color(1f, .94f, .74f, 1f);
        }
        touchHint.color = new Color(1f, .94f, .74f, 1f);
        yield return new WaitForSecondsRealtime(.18f);
        if (touchCount >= 3)
        {
            touchCount = 4;
            RefreshProgress();
            isOpen = false;
            if (screenGroup != null && closeupContent != null)
                yield return SoftPanelTransition.Play(screenGroup, closeupContent, false, .56f);
            ringRoutine = null;
            Close();
            director.AdvanceBedWakePuzzleFromTouch();
        }
        else
        {
            ringRoutine = null;
            touchCount = director.AdvanceBedWakePuzzleFromTouch();
            RefreshProgress();
            ShowFaceTarget();
        }
    }

    private IEnumerator MissAndRetry()
    {
        touchButton.interactable = false;
        float elapsed = 0f;
        while (elapsed < retryDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / retryDelay);
            var missColor = new Color(.82f, .24f, .19f, alpha);
            touchHint.color = missColor;
            if (approachRing != null) approachRing.color = missColor;
            yield return null;
        }
        ringRoutine = null;
        ShowFaceTarget();
    }

    private void RefreshProgress()
    {
        if (progressDots == null)
            return;

        for (int i = 0; i < progressDots.Length; i++)
        {
            if (progressDots[i] == null)
                continue;

            progressDots[i].color = i < touchCount
                ? new Color(0.98f, 0.89f, 0.66f, 1f)
                : new Color(0.035f, 0.13f, 0.17f, 0.95f);
        }
    }
}
