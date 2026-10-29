using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Minimal development-only status display for the phase-one testbed.
/// F1 toggles the overlay; this is not a production HUD.
/// </summary>
[DisallowMultipleComponent]
public sealed class CoreDebugOverlay : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [SerializeField] private LightExposureSensor exposureSensor;
    [SerializeField] private PlayerInteraction interaction;
    [SerializeField] private AfterTheGlowStoryDirector storyDirector;
    [SerializeField] private bool visibleAtStart = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    private bool visible;
    private int completedGoalCount;
    private string lastCompletedGoal = "없음";
    private GUIStyle labelStyle;

    private void Awake()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerMovement>();
        if (exposureSensor == null && player != null)
            exposureSensor = player.GetComponent<LightExposureSensor>();
        if (interaction == null && player != null)
            interaction = player.GetComponent<PlayerInteraction>();
        if (storyDirector == null)
            storyDirector = FindAnyObjectByType<AfterTheGlowStoryDirector>();
        visible = visibleAtStart;
    }

    private void OnEnable()
    {
        GoalTrigger2D.GoalReached += HandleGoalReached;
    }

    private void OnDisable()
    {
        GoalTrigger2D.GoalReached -= HandleGoalReached;
    }

    private void Update()
    {
        if (PlayerMovement.WasKeyPressedThisFrame(toggleKey))
            visible = !visible;
        if (PlayerMovement.WasKeyPressedThisFrame(resetKey))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnGUI()
    {
        if (!visible)
            return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white }
            };
        }

        const float width = 440f;
        const float height = 390f;
        GUI.Box(new Rect(12f, 12f, width, height), GUIContent.none);
        GUILayout.BeginArea(new Rect(24f, 18f, width - 24f, height - 12f));
        GUILayout.Label("After the Glow · Phase 1 Debug (F1)", labelStyle);
        GUILayout.Label(
            "빛 상태: " + (exposureSensor != null
                ? exposureSensor.CurrentState.ToString()
                : LightExposureState.Normal.ToString()),
            labelStyle);
        GUILayout.Label(
            "판정 광원: " + (exposureSensor != null ? exposureSensor.ActiveLightName : "없음"),
            labelStyle);
        GUILayout.Label(
            "상호작용 대상: " + (interaction != null ? interaction.CurrentTargetName : "없음"),
            labelStyle);
        GUILayout.Label(
            "제어: " + (player != null ? player.ControlMode.ToString() : "없음") +
            " / 자세: " + (player != null && player.IsCrouching ? "엎드림" : "서 있음"),
            labelStyle);
        if (interaction != null && interaction.ActiveTarget != null && interaction.ActiveTarget.NeedsBiteToRelease)
        {
            GUILayout.Label(
                "결박 당김: " + interaction.ActiveTarget.PullProgress.ToString("F2") + " / 필요 거리",
                labelStyle);
        }
        GUILayout.Label(
            "완료 이벤트: " + completedGoalCount + "회 (최근: " + lastCompletedGoal + ")",
            labelStyle);
        GUILayout.Label(
            "바닥 판정: " + (player != null && player.IsGrounded ? "접지" : "공중"),
            labelStyle);
        GUILayout.Label(
            "점프 입력/수락: " + (player != null
                ? player.JumpInputCount + "/" + player.JumpAcceptedCount + "회"
                : "-"),
            labelStyle);
        GUILayout.Label(
            "수직 속도: " + (player != null && player.Body != null
                ? player.Body.linearVelocity.y.ToString("F2")
                : "-"),
            labelStyle);
        if (storyDirector != null)
        {
            GUILayout.Label("스토리 단계: " + storyDirector.Stage, labelStyle);
            GUILayout.Label("진행 상태: " + storyDirector.StatusMessage, labelStyle);
            GUILayout.Label(
                "기상 / 쓰다듬기 / 퇴장 / 결박 해제 / 문 열기: " +
                storyDirector.BedWakeCount + " / " + storyDirector.PetCount + " / " +
                storyDirector.OwnerDepartureCount + " / " + storyDirector.GateReleaseCount + " / " +
                storyDirector.GateOpenCount,
                labelStyle);
        }
        GUILayout.EndArea();
    }

    private void HandleGoalReached(GoalTrigger2D goal, PlayerMovement _)
    {
        completedGoalCount++;
        lastCompletedGoal = goal != null ? goal.gameObject.name : "Goal";
    }
}
