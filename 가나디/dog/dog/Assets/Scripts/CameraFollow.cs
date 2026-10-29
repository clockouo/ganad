using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Follow settings")]
    [Min(.01f)] public float smoothTime = 0.18f;
    [Tooltip("집 안 추적의 완충 시간 배율입니다. 1은 실내외에 같은 부드러움을 적용합니다.")]
    [Range(0.1f, 1f)] public float homeFollowResponseMultiplier = 1f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    [Min(0.1f)] public float homeOrthographicSize = 4.2f;

    [Header("Area framing")]
    public Vector3 exteriorOffset = new Vector3(0f, 0f, -10f);
    [Min(0.1f)] public float exteriorOrthographicSize = 4.2f;
    [Tooltip("외부에서 따라가는 지연 시간. 작을수록 빠르게 따라갑니다.")]
    [Min(0.01f)] public float exteriorSmoothTime = 0.18f;
    [Tooltip("켜면 외부 카메라 높이를 Exterior Locked Y에 고정합니다.")]
    public bool exteriorLockY = true;
    [Tooltip("외부에서도 집 안과 같은 강아지 크기로 보이게 합니다.")]
    public bool matchExteriorSizeToHome = true;
    public float exteriorLockedY = -1.52436f;
    public float exteriorMinY = -10f;

    [Header("Map bounds")]
    public bool constrainHorizontalPosition = true;
    public float mapMinX = -12.6f;
    public float mapMaxX = AfterTheGlowWorldLayout.ParkEndX;
    [Tooltip("집 안 카메라의 오른쪽 경계. 외부로 전환하면 Map Max X를 사용합니다.")]
    public float homeMapMaxX = 5.4f;
    [Tooltip("외부 카메라의 왼쪽 화면 경계. 집 내부가 보이지 않도록 집 끝보다 바깥에 둡니다.")]
    public float exteriorMapMinX = 6.6f;

    [Header("Vertical limits")]
    public bool lockY = false;
    public float lockedY = -1.52f;
    public float minY = 0f;

    private Vector3 velocity = Vector3.zero;
    private Camera attachedCamera;
    private bool usingExteriorFraming;
    private float resumeStarted = -10f;
    private float resumeSize;
    private float ActiveOrthographicSize => usingExteriorFraming && !matchExteriorSizeToHome
        ? exteriorOrthographicSize
        : homeOrthographicSize;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        if (target == null)
            target = FindAnyObjectByType<PlayerMovement>()?.transform;
        ApplyCameraFraming();
    }

    private void OnEnable() => velocity = Vector3.zero;

    private void ApplyCameraFraming()
    {
        if (attachedCamera == null) attachedCamera = GetComponent<Camera>();
        if (attachedCamera == null || !attachedCamera.orthographic) return;
        float size = ActiveOrthographicSize;
        float areaWidth = usingExteriorFraming ? mapMaxX - exteriorMapMinX : homeMapMaxX - mapMinX;
        // A viewport wider than the whole map cannot be contained by moving its
        // center alone. Fit only that exceptional aspect ratio inside the map.
        if (constrainHorizontalPosition && areaWidth > 0f)
            size = Mathf.Min(size, areaWidth / (2f * Mathf.Max(.01f, attachedCamera.aspect)));
        float blend = Mathf.SmoothStep(0f, 1f, (Time.time - resumeStarted) / .6f);
        attachedCamera.orthographicSize = Mathf.Max(.01f, Mathf.Lerp(resumeSize, size, blend));
    }

    public void ResumeFromCurrentPose()
    {
        if (attachedCamera == null) attachedCamera = GetComponent<Camera>();
        resumeSize = attachedCamera != null ? attachedCamera.orthographicSize : ActiveOrthographicSize;
        resumeStarted = Time.time;
        velocity = Vector3.zero;
        enabled = true;
    }

    public void SwitchToExteriorFraming()
    {
        usingExteriorFraming = true;

        if (attachedCamera == null)
            attachedCamera = GetComponent<Camera>();
        ApplyCameraFraming();

        velocity = Vector3.zero;
        if (target == null)
            return;

        Vector3 exteriorPosition = target.position + exteriorOffset;
        exteriorPosition.y = ResolveCameraY(exteriorPosition.y);
        exteriorPosition.x = ClampCameraX(exteriorPosition.x);
        transform.position = exteriorPosition;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        ApplyCameraFraming();
        Vector3 desiredPosition = target.position + (usingExteriorFraming ? exteriorOffset : offset);
        desiredPosition.y = ResolveCameraY(desiredPosition.y);
        desiredPosition.x = ClampCameraX(desiredPosition.x);

        Vector3 nextPosition = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref velocity,
            usingExteriorFraming ? exteriorSmoothTime :
                Mathf.Max(0.01f, smoothTime * homeFollowResponseMultiplier));
        float clampedX = ClampCameraX(nextPosition.x);
        if (!Mathf.Approximately(clampedX, nextPosition.x))
            velocity.x = 0f;
        nextPosition.x = clampedX;
        if (usingExteriorFraming ? exteriorLockY : lockY)
        {
            nextPosition.y = usingExteriorFraming ? exteriorLockedY : lockedY;
            velocity.y = 0f;
        }
        transform.position = nextPosition;
    }

    private float ResolveCameraY(float desiredY)
    {
        if (usingExteriorFraming)
            return exteriorLockY ? exteriorLockedY : Mathf.Max(desiredY, exteriorMinY);
        return lockY ? lockedY : Mathf.Max(desiredY, minY);
    }

    private float ClampCameraX(float cameraX)
    {
        float maximumX = usingExteriorFraming ? mapMaxX : homeMapMaxX;
        float minimumX = usingExteriorFraming ? exteriorMapMinX : mapMinX;
        if (!constrainHorizontalPosition || maximumX <= minimumX)
            return cameraX;

        if (attachedCamera == null)
            attachedCamera = GetComponent<Camera>();
        if (attachedCamera == null || !attachedCamera.orthographic)
            return cameraX;

        float halfWidth = attachedCamera.orthographicSize * attachedCamera.aspect;
        float mapWidth = maximumX - minimumX;
        if (mapWidth <= halfWidth * 2f)
            return (minimumX + maximumX) * 0.5f;

        return Mathf.Clamp(cameraX, minimumX + halfWidth, maximumX - halfWidth);
    }
}
