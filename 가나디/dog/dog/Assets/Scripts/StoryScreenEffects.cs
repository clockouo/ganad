using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Short damped impacts and warm edge glare, independent of camera follow.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class StoryScreenEffects : MonoBehaviour
{
    private Camera view;
    private float impactStarted, impactSeconds, impactStrength;
    private float glareStarted = -10f;
    private Matrix4x4 restingProjection;
    private bool projectionChanged;
    private UnityEngine.UI.RawImage glare;
    private Texture2D edgeTexture;
    public bool ImpactActive => Time.time - impactStarted < impactSeconds;
    public float GlareAlpha => glare != null ? glare.color.a : 0f;

    private void Awake()
    {
        view = GetComponent<Camera>();
        var canvasObject = new GameObject("Headlight Edge Glare", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1500;
        var overlay = new GameObject("Warm Light At Screen Edges", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
        overlay.transform.SetParent(canvasObject.transform, false);
        glare = overlay.GetComponent<UnityEngine.UI.RawImage>();
        glare.raycastTarget = false;
        glare.color = Color.clear;
        var rect = glare.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        edgeTexture = new Texture2D(96, 64, TextureFormat.RGBA32, false);
        edgeTexture.filterMode = FilterMode.Bilinear;
        edgeTexture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[96 * 64];
        for (int y = 0; y < 64; y++) for (int x = 0; x < 96; x++)
        {
            float edge = Mathf.Max(Mathf.Abs((x + .5f) / 48f - 1f), Mathf.Abs((y + .5f) / 32f - 1f));
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.42f, 1f, edge));
            pixels[y * 96 + x] = new Color(1f, 1f, 1f, alpha);
        }
        edgeTexture.SetPixels(pixels); edgeTexture.Apply(false, true);
        glare.texture = edgeTexture;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        RestoreProjection();
        if (glare != null) glare.color = Color.clear;
    }
    public void DoorImpact() => Impact(.012f, .3f);
    public void HeadlightShock()
    {
        Impact(.008f, .42f);
        glareStarted = Time.time;
    }
    private void Impact(float strength, float seconds)
    {
        impactStarted = Time.time; impactSeconds = seconds; impactStrength = strength;
    }
    private void LateUpdate()
    {
        float elapsed = Time.time - glareStarted;
        float alpha = elapsed < .12f ? Mathf.SmoothStep(0f, .42f, elapsed / .12f)
            : .42f * (1f - Mathf.SmoothStep(0f, 1f, (elapsed - .12f) / .85f));
        glare.color = new Color(1f, .96f, .76f, alpha);
    }
    private void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera != view || !ImpactActive || SettingsMenuController.IsOpen) return;
        RestoreProjection();
        restingProjection = camera.projectionMatrix;
        Matrix4x4 projection = restingProjection;
        float elapsed = Time.time - impactStarted;
        float damping = Mathf.Pow(1f - Mathf.Clamp01(elapsed / impactSeconds), 2f) * impactStrength;
        projection.m03 += Mathf.Sin(elapsed * 93f) * damping;
        projection.m13 += Mathf.Cos(elapsed * 77f) * damping * .7f;
        camera.projectionMatrix = projection;
        projectionChanged = true;
    }
    private void EndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera == view) RestoreProjection();
    }
    private void RestoreProjection()
    {
        // Reset also restores automatic orthographic projection, so later story
        // zooms continue to follow orthographicSize after the impact ends.
        if (projectionChanged && view != null) view.ResetProjectionMatrix();
        projectionChanged = false;
    }
    private void OnDestroy() { if (edgeTexture != null) Destroy(edgeTexture); }
}
