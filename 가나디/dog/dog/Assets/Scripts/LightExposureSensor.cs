using System;
using UnityEngine;

public enum LightExposureState
{
    Normal,
    Exposed,
    ShadowSafe
}

/// <summary>
/// Evaluates gameplay light range and occlusion independently from rendered
/// pixels, then debounces state changes at light boundaries.
/// </summary>
[DisallowMultipleComponent]
public sealed class LightExposureSensor : MonoBehaviour
{
    [Header("판정 위치")]
    [SerializeField] private Transform sensorPoint;
    [SerializeField] private Vector2 sensorOffset = new Vector2(0f, 0.25f);
    [SerializeField] private LayerMask occluderLayers = Physics2D.DefaultRaycastLayers;

    [Header("빛 판정")]
    [Min(0f)]
    [SerializeField] private float minimumRangeStrength = 0.02f;
    [Min(0f)]
    [SerializeField] private float minimumExposureStrength = 0.2f;
    [Min(0f)]
    [SerializeField] private float exposedEnterDelay = 0.08f;
    [Min(0f)]
    [SerializeField] private float stateExitDelay = 0.1f;

    private LightExposureState candidateState = LightExposureState.Normal;
    private float candidateSince;
    private string activeLightName = "없음";
    private float activeLightStrength;

    public LightExposureState CurrentState { get; private set; } = LightExposureState.Normal;
    public LightExposureState EvaluatedState { get; private set; } = LightExposureState.Normal;
    public string ActiveLightName => activeLightName;
    public Vector2 ActiveLightSource { get; private set; }
    public float ActiveLightStrength => activeLightStrength;

    /// <summary>
    /// Checks a movement sample against each gameplay light without the state-change debounce.
    /// This lets the player stop at the outer gameplay-light boundary before entering the light.
    /// </summary>
    public bool WouldMoveIntoGameplayLight(Vector2 currentPosition, Vector2 nextPosition,
        out Vector2 sourcePosition)
    {
        sourcePosition = nextPosition;
        var lights = GameplayLight2D.ActiveLights;
        for (int i = 0; i < lights.Count; i++)
        {
            GameplayLight2D light = lights[i];
            if (light == null)
                continue;

            bool currentlyInside = IsInsideGameplayLight(light, currentPosition,
                out float currentStrength, out Vector2 currentSource);
            if (!IsInsideGameplayLight(light, nextPosition,
                    out float nextStrength, out Vector2 nextSource))
                continue;

            if (!currentlyInside || nextStrength > currentStrength + 0.0001f)
            {
                sourcePosition = nextSource;
                return true;
            }

            sourcePosition = currentSource;
        }

        return false;
    }

    public Vector2 GetSensorOffsetFromActor()
    {
        return sensorPoint != null
            ? (Vector2)sensorPoint.position - (Vector2)transform.position
            : sensorOffset;
    }

    public event Action<LightExposureState, LightExposureState> StateChanged;

    private void OnEnable()
    {
        candidateSince = Time.time;
        candidateState = CurrentState;
    }

    private void Update()
    {
        EvaluatedState = EvaluateState();
        if (EvaluatedState == CurrentState)
        {
            candidateState = EvaluatedState;
            candidateSince = Time.time;
            return;
        }

        if (EvaluatedState != candidateState)
        {
            candidateState = EvaluatedState;
            candidateSince = Time.time;
        }

        float delay = EvaluatedState == LightExposureState.Exposed
            ? exposedEnterDelay
            : stateExitDelay;
        if (Time.time - candidateSince < delay)
            return;

        LightExposureState previousState = CurrentState;
        CurrentState = EvaluatedState;
        StateChanged?.Invoke(previousState, CurrentState);
    }

    private LightExposureState EvaluateState()
    {
        Vector2 position = sensorPoint != null
            ? sensorPoint.position
            : (Vector2)transform.position + sensorOffset;

        bool hasBlockedRelevantLight = false;
        bool hasUnblockedRelevantLight = false;
        bool hasExposedLight = false;
        string strongestRelevantLightName = "없음";
        float strongestRelevantLight = 0f;
        Vector2 strongestRelevantSource = position;
        string strongestExposedLightName = "없음";
        float strongestExposedLight = 0f;
        Vector2 strongestExposedSource = position;

        var lights = GameplayLight2D.ActiveLights;
        for (int i = 0; i < lights.Count; i++)
        {
            GameplayLight2D light = lights[i];
            if (light == null || !light.TryGetStrength(position, out float strength, out Vector2 source))
                continue;
            if (strength < minimumRangeStrength)
                continue;

            bool blocked = LightOccluder2D.IsBlocked(source, position, transform, occluderLayers);
            if (strength > strongestRelevantLight)
            {
                strongestRelevantLight = strength;
                strongestRelevantLightName = light.gameObject.name;
                strongestRelevantSource = source;
            }

            if (blocked)
            {
                hasBlockedRelevantLight = true;
                continue;
            }

            hasUnblockedRelevantLight = true;
            if (strength >= minimumExposureStrength)
            {
                hasExposedLight = true;
                if (strength > strongestExposedLight)
                {
                    strongestExposedLight = strength;
                    strongestExposedLightName = light.gameObject.name;
                    strongestExposedSource = source;
                }
            }
        }

        if (hasExposedLight)
        {
            activeLightName = strongestExposedLightName;
            activeLightStrength = strongestExposedLight;
            ActiveLightSource = strongestExposedSource;
            return LightExposureState.Exposed;
        }

        activeLightName = strongestRelevantLightName;
        activeLightStrength = strongestRelevantLight;
        ActiveLightSource = strongestRelevantSource;

        return hasBlockedRelevantLight && !hasUnblockedRelevantLight
            ? LightExposureState.ShadowSafe
            : LightExposureState.Normal;
    }

    private bool IsInsideGameplayLight(GameplayLight2D light, Vector2 position,
        out float strength, out Vector2 source)
    {
        if (!light.TryGetStrength(position, out strength, out source) ||
            strength < minimumRangeStrength ||
            LightOccluder2D.IsBlocked(source, position, transform, occluderLayers))
        {
            strength = 0f;
            return false;
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 position = sensorPoint != null
            ? sensorPoint.position
            : transform.position + (Vector3)sensorOffset;
        Gizmos.color = CurrentState == LightExposureState.Exposed
            ? new Color(1f, 0.2f, 0.15f, 0.9f)
            : CurrentState == LightExposureState.ShadowSafe
                ? new Color(0.25f, 0.8f, 1f, 0.9f)
                : Color.white;
        Gizmos.DrawWireSphere(position, 0.13f);
    }
}
