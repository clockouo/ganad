using System;
using System.Collections;
using UnityEngine;

/// <summary>Separated leash ends recoil and fall over a clean fence plate.</summary>
public sealed class LeashBreakUI : MonoBehaviour
{
    public bool IsPlaying { get; private set; }
    public float LeashScale { get; set; } = 1f;
    private GameObject screen;
    private UnityEngine.UI.RawImage left, right;
    private readonly UnityEngine.UI.Image[] fibers = new UnityEngine.UI.Image[14];
    private Material cutout;

    private void Build()
    {
        if (screen != null) return;
        Texture2D plate = JourneyArt.Texture("Backgrounds/07_LeashFence");
        Sprite broken = JourneyArt.Frame("Puzzles/LeashStatesFixed__c2r3", 4);
        Texture2D ends = broken.texture;
        if (plate == null || ends == null) throw new InvalidOperationException("Leash break layers are missing.");
        cutout = new Material(Resources.Load<Shader>("Puzzle02/PettingFocus"));
        cutout.SetFloat("_Cutout", .8f);
        cutout.SetFloat("_BlurPixels", 0f);
        screen = new GameObject("Leash Break Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        screen.transform.SetParent(transform, false);
        var canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;
        var scaler = screen.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        var black = Image("Letterbox", screen.transform);
        black.color = Color.black;
        Stretch(black.rectTransform);
        var art = new GameObject("Fence Aspect Frame", typeof(RectTransform), typeof(UnityEngine.UI.AspectRatioFitter));
        art.transform.SetParent(screen.transform, false);
        var fit = art.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = (float)plate.width / plate.height;
        art.AddComponent<UnityEngine.UI.RectMask2D>();
        var background = Raw("Clean Fence", art.transform, plate);
        Stretch(background.rectTransform);
        left = Raw("Left Torn Leash", art.transform, ends);
        right = Raw("Right Torn Leash", art.transform, ends);
        left.material = right.material = cutout;
        Rect uv = JourneyArt.UV(broken);
        left.uvRect = new Rect(uv.x, uv.y, uv.width * .5f, uv.height);
        right.uvRect = new Rect(uv.x + uv.width * .5f, uv.y, uv.width * .5f, uv.height);
        foreach (var item in new[] { left, right })
        {
            item.rectTransform.anchorMin = item.rectTransform.anchorMax = new Vector2(.5f, .5f);
            item.rectTransform.sizeDelta = new Vector2(350f, 90f);
        }
        left.rectTransform.pivot = new Vector2(1f, .5f);
        right.rectTransform.pivot = new Vector2(0f, .5f);
        for (int i = 0; i < fibers.Length; i++)
        {
            fibers[i] = Image("Loose Fiber " + i, art.transform);
            fibers[i].rectTransform.sizeDelta = new Vector2(4 + i % 3 * 2, 3);
        }
        screen.SetActive(false);
    }

    public IEnumerator Play(Action onSnap)
    {
        Build();
        float scale = Mathf.Max(.1f, LeashScale);
        left.rectTransform.sizeDelta = right.rectTransform.sizeDelta =
            new Vector2(350f * scale, 90f * scale);
        IsPlaying = true;
        screen.SetActive(true);
        float elapsed = 0f;
        bool snapped = false;
        try
        {
            while (elapsed < 1.35f)
            {
                if (SettingsMenuController.IsOpen)
                {
                    yield return null;
                    continue;
                }
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= .2f && !snapped) { snapped = true; onSnap?.Invoke(); }
                float t = Mathf.Clamp01((elapsed - .2f) / .95f);
                float recoil = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 3f));
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, 1f, t));
                left.rectTransform.anchoredPosition = new Vector2(-115f * recoil, -135f * t * t);
                right.rectTransform.anchoredPosition = new Vector2(115f * recoil, -135f * t * t);
                left.rectTransform.localScale = right.rectTransform.localScale = new Vector3(Mathf.Lerp(1.1f, 1f, recoil), 1, 1);
                left.rectTransform.localRotation = Quaternion.Euler(0, 0, t * 11f);
                right.rectTransform.localRotation = Quaternion.Euler(0, 0, -t * 11f);
                left.color = right.color = new Color(1, 1, 1, alpha);
                for (int i = 0; i < fibers.Length; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    fibers[i].color = new Color(1f, .8f, .56f, snapped ? Mathf.Sin(t * Mathf.PI) : 0f);
                    fibers[i].rectTransform.anchoredPosition = new Vector2(side * t * (45f + i * 9f),
                        -15f + Mathf.Sin(i * 2.3f) * 60f * t - t * t * 170f);
                    fibers[i].rectTransform.localRotation = Quaternion.Euler(0, 0, side * t * (90f + i * 17f));
                }
                yield return null;
            }
        }
        finally { IsPlaying = false; if (screen != null) screen.SetActive(false); }
    }

    private static void Half(RectTransform rect, bool rightHalf)
    {
        rect.anchorMin = new Vector2(rightHalf ? .5f : 0f, 0f);
        rect.anchorMax = new Vector2(rightHalf ? 1f : .5f, 1f);
        rect.pivot = new Vector2(rightHalf ? 1f : 0f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static UnityEngine.UI.RawImage Raw(string name, Transform parent, Texture texture)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<UnityEngine.UI.RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }
    private static UnityEngine.UI.Image Image(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = false;
        return image;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private void OnDestroy() { if (cutout != null) Destroy(cutout); }
}
