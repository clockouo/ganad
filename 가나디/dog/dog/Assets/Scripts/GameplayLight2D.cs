using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Marks a URP 2D light as a source that affects gameplay and exposes the
/// same world-space radius to the light exposure sensor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Light2D))]
public sealed class GameplayLight2D : MonoBehaviour
{
    private static readonly List<GameplayLight2D> ActiveLightsInternal =
        new List<GameplayLight2D>();

    [SerializeField] private Light2D visualLight;
    [SerializeField] private bool affectsGameplay = true;
    [Min(0.1f)]
    [SerializeField] private float gameplayRadius = 5f;
    [Min(0f)]
    [SerializeField] private float gameplayIntensityMultiplier = 1f;
    [Min(0.1f)]
    [SerializeField] private float falloffExponent = 1f;
    [SerializeField] private bool syncRadiusToPointLight = true;

    [Header("Directional headlight footprint")]
    [SerializeField] private bool useDirectionalBeams;
    [SerializeField] private Vector2[] beamOrigins = new Vector2[0];
    [SerializeField] private Vector2 beamDirection = Vector2.down;
    [Min(0.1f)] [SerializeField] private float beamLength = 3.5f;
    [Min(0.01f)] [SerializeField] private float beamStartHalfWidth = 0.2f;
    [Min(0.01f)] [SerializeField] private float beamEndHalfWidth = 1.05f;
    [Min(0f)] [SerializeField] private float beamOutwardSpread;

    public static IReadOnlyList<GameplayLight2D> ActiveLights => ActiveLightsInternal;
    public bool AffectsGameplay => affectsGameplay;
    public Light2D VisualLight => visualLight;
    public bool UsesDirectionalBeams => useDirectionalBeams;
    public int BeamCount => useDirectionalBeams && beamOrigins != null ? beamOrigins.Length : 0;

    public void ConfigureDirectionalBeams(Vector2[] origins, Vector2 direction,
        float length, float nearHalfWidth, float farHalfWidth, float outwardSpread = 0f)
    {
        beamOrigins = origins;
        beamDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.down;
        beamLength = Mathf.Max(0.1f, length);
        beamStartHalfWidth = Mathf.Max(0.01f, nearHalfWidth);
        beamEndHalfWidth = Mathf.Max(0.01f, farHalfWidth);
        beamOutwardSpread = Mathf.Max(0f, outwardSpread);
        useDirectionalBeams = beamOrigins != null && beamOrigins.Length > 0;
    }

    public void GetBeamCrossSection(int index, float progress, out Vector2 left, out Vector2 right)
    {
        Vector2 direction = beamDirection.normalized;
        Vector2 side = new Vector2(-direction.y, direction.x);
        Vector2 center = beamOrigins[index] + direction * (beamLength * progress) +
            side * (Mathf.Sign(Vector2.Dot(beamOrigins[index], side)) * beamOutwardSpread * progress);
        float halfWidth = Mathf.Lerp(beamStartHalfWidth, beamEndHalfWidth, progress);
        left = center - side * halfWidth;
        right = center + side * halfWidth;
    }

    private void OnEnable()
    {
        CacheVisualLight();
        if (!ActiveLightsInternal.Contains(this))
            ActiveLightsInternal.Add(this);
    }

    private void OnDisable()
    {
        ActiveLightsInternal.Remove(this);
    }

    private void OnValidate()
    {
        CacheVisualLight();
        gameplayRadius = Mathf.Max(0.1f, gameplayRadius);
        falloffExponent = Mathf.Max(0.1f, falloffExponent);

        if (syncRadiusToPointLight &&
            visualLight != null &&
            visualLight.lightType == Light2D.LightType.Point)
        {
            visualLight.pointLightOuterRadius = gameplayRadius;
        }
    }

    public bool TryGetStrength(Vector2 worldPosition, out float strength, out Vector2 sourcePosition)
    {
        strength = 0f;
        sourcePosition = transform.position;

        if (!affectsGameplay ||
            !isActiveAndEnabled ||
            visualLight == null ||
            !visualLight.isActiveAndEnabled)
        {
            return false;
        }

        sourcePosition = visualLight.transform.position;
        if (useDirectionalBeams)
        {
            Vector2 sample = transform.InverseTransformPoint(worldPosition);
            Vector2 direction = beamDirection.normalized;
            Vector2 side = new Vector2(-direction.y, direction.x);
            for (int i = 0; i < BeamCount; i++)
            {
                Vector2 relative = sample - beamOrigins[i];
                float depth = Vector2.Dot(relative, direction);
                if (depth < 0f || depth > beamLength)
                    continue;
                float progress = depth / beamLength;
                float lateralCenter = Mathf.Sign(Vector2.Dot(beamOrigins[i], side)) * beamOutwardSpread * progress;
                if (Mathf.Abs(Vector2.Dot(relative, side) - lateralCenter) >
                    Mathf.Lerp(beamStartHalfWidth, beamEndHalfWidth, progress))
                    continue;
                sourcePosition = transform.TransformPoint(beamOrigins[i]);
                strength = visualLight.intensity * gameplayIntensityMultiplier * Mathf.Lerp(1f, 0.2f, progress);
                return true;
            }
            return false;
        }

        float scale = Mathf.Max(
            Mathf.Abs(visualLight.transform.lossyScale.x),
            Mathf.Abs(visualLight.transform.lossyScale.y));
        float radius = gameplayRadius * Mathf.Max(0.01f, scale);

        if (syncRadiusToPointLight && visualLight.lightType == Light2D.LightType.Point)
            radius = Mathf.Max(0.1f, visualLight.pointLightOuterRadius * Mathf.Max(0.01f, scale));

        float distance = Vector2.Distance(sourcePosition, worldPosition);
        if (distance >= radius)
            return false;

        float normalizedDistance = Mathf.Clamp01(distance / radius);
        float distanceFalloff = Mathf.Pow(1f - normalizedDistance, falloffExponent);
        strength = visualLight.intensity * gameplayIntensityMultiplier * distanceFalloff;
        return true;
    }

    private void CacheVisualLight()
    {
        if (visualLight == null)
            visualLight = GetComponent<Light2D>();
    }
}
