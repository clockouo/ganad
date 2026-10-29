using System.Collections;
using UnityEngine;

/// <summary>Gentle upward entrances and downward exits shared by story close-ups.</summary>
public static class SoftPanelTransition
{
    public static IEnumerator Play(CanvasGroup group, RectTransform content, bool opening,
        float seconds = .7f, float travel = 52f)
    {
        group.interactable = false;
        group.blocksRaycasts = false;
        float startAlpha = group.alpha;
        Vector2 rest = content.anchoredPosition;
        Vector2 start = rest + (opening ? Vector2.down * travel : Vector2.zero);
        Vector2 end = rest + (opening ? Vector2.zero : Vector2.down * travel * .65f);
        content.anchoredPosition = start;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (SettingsMenuController.IsOpen) { yield return null; continue; }
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, seconds));
            float eased = t * t * t * (t * (t * 6f - 15f) + 10f);
            group.alpha = Mathf.Lerp(startAlpha, opening ? 1f : 0f, eased);
            content.anchoredPosition = Vector2.LerpUnclamped(start, end, eased);
            yield return null;
        }
        group.alpha = opening ? 1f : 0f;
        content.anchoredPosition = rest;
        group.interactable = opening;
        group.blocksRaycasts = opening;
    }
}
