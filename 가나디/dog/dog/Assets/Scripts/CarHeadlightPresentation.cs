using UnityEngine;

/// <summary>Shows one car, switching its existing artwork with the gameplay light.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
public sealed class CarHeadlightPresentation : MonoBehaviour
{
    public GameObject lightRoot;
    public Sprite lightsOnSprite;
    public Sprite lightsOffSprite;
    public Vector2 worldSize = new Vector2(4.8f, 3.15f);
    public bool alignWheelsToGround = true;
    public float groundY = -3.86f;
    public Vector2 wheelBottomOffsets = new Vector2(-0.17f, -0.17f);
    [Header("Original illustrated background car")]
    public bool useBackdropCar;
    public SpriteRenderer[] backdropLampOffMasks;

    private void OnEnable() => Refresh();
    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        var renderer = GetComponent<SpriteRenderer>();
        if (useBackdropCar)
        {
            if (renderer != null) renderer.enabled = false;
            bool off = lightRoot == null || !lightRoot.activeInHierarchy;
            if (backdropLampOffMasks != null)
                foreach (var lamp in backdropLampOffMasks)
                    if (lamp != null) lamp.enabled = off;
            return;
        }
        Sprite desired = lightRoot != null && lightRoot.activeInHierarchy ? lightsOnSprite : lightsOffSprite;
        if (desired == null || renderer == null)
            return;
        renderer.enabled = true;
        if (renderer.sprite != desired)
            renderer.sprite = desired;
        Vector2 size = desired.bounds.size;
        if (size.x > 0f && size.y > 0f)
        {
            Vector3 desiredScale = new Vector3(worldSize.x / size.x, worldSize.y / size.y, 1f);
            if (transform.localScale != desiredScale)
                transform.localScale = desiredScale;
        }
        if (alignWheelsToGround)
        {
            float offset = desired == lightsOnSprite ? wheelBottomOffsets.x : wheelBottomOffsets.y;
            float positionY = groundY - offset * Mathf.Abs(transform.lossyScale.y);
            if (Mathf.Abs(transform.position.y - positionY) > 0.0001f)
            {
                Vector3 position = transform.position;
                position.y = positionY;
                transform.position = position;
            }
        }
    }
}
