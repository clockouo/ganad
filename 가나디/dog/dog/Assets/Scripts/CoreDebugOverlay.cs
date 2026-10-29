using UnityEngine;
using UnityEngine.SceneManagement;
using System;

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
    [SerializeField] private KeyCode commandKey = KeyCode.F12;

    private bool visible;
    private bool commandConsoleVisible;
    private string commandLine = string.Empty;
    private string commandFeedback = "명령: skip, speed 2, speed 1";
    private float baseTimeScale = 1f;
    private float speedMultiplier = 1f;
    private float skipUntilUnscaledTime;
    private float multiplierBeforeSkip = 1f;
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
        baseTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
    }

    private void OnEnable()
    {
        GoalTrigger2D.GoalReached += HandleGoalReached;
    }

    private void OnDisable()
    {
        GoalTrigger2D.GoalReached -= HandleGoalReached;
        if (Time.timeScale != baseTimeScale)
            Time.timeScale = baseTimeScale;
    }

    private void Update()
    {
        bool commandChord = PlayerMovement.IsKeyHeld(KeyCode.LeftControl) &&
            PlayerMovement.IsKeyHeld(KeyCode.LeftShift);
        if (commandChord && PlayerMovement.WasKeyPressedThisFrame(commandKey))
            commandConsoleVisible = !commandConsoleVisible;

        if (PlayerMovement.WasKeyPressedThisFrame(toggleKey))
            visible = !visible;
        if (PlayerMovement.WasKeyPressedThisFrame(resetKey))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        if (skipUntilUnscaledTime > 0f && Time.unscaledTime >= skipUntilUnscaledTime)
        {
            speedMultiplier = multiplierBeforeSkip;
            skipUntilUnscaledTime = 0f;
            ApplyTimeScale();
            commandFeedback = "가속 종료 · 배속 x" + speedMultiplier.ToString("0.##");
        }
    }

    private void OnGUI()
    {
        if (!visible)
        {
            DrawCommandConsole();
            return;
        }

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

        DrawCommandConsole();
    }

    private void DrawCommandConsole()
    {
        if (!commandConsoleVisible)
            return;

        const float width = 390f;
        const float height = 178f;
        float x = Mathf.Max(12f, Screen.width - width - 12f);
        GUI.Box(new Rect(x, 12f, width, height), GUIContent.none);
        GUILayout.BeginArea(new Rect(x + 12f, 18f, width - 24f, height - 12f));
        GUILayout.Label("개발 커맨드 · Ctrl+Shift+F12", labelStyle ?? GUI.skin.label);
        GUILayout.Label(commandFeedback, labelStyle ?? GUI.skin.label);
        GUILayout.BeginHorizontal();
        commandLine = GUILayout.TextField(commandLine, 36);
        if (GUILayout.Button("실행", GUILayout.Width(58f)) ||
            (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
        {
            ExecuteCommand(commandLine);
            commandLine = string.Empty;
            GUI.FocusControl(string.Empty);
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Skip · 2초 가속")) ExecuteCommand("skip");
        if (GUILayout.Button("x2")) ExecuteCommand("speed 2");
        if (GUILayout.Button("x3")) ExecuteCommand("speed 3");
        if (GUILayout.Button("정상 속도")) ExecuteCommand("speed 1");
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void ExecuteCommand(string rawCommand)
    {
        if (string.IsNullOrWhiteSpace(rawCommand))
            return;

        string[] parts = rawCommand.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        string command = parts[0].TrimStart('/').ToLowerInvariant();
        if (command == "skip")
        {
            multiplierBeforeSkip = speedMultiplier;
            speedMultiplier = Mathf.Max(speedMultiplier, 8f);
            skipUntilUnscaledTime = Time.unscaledTime + 2f;
            ApplyTimeScale();
            commandFeedback = "현재 연출을 빠르게 진행 중 · 2초 후 복귀";
            return;
        }

        if (command == "speed" && parts.Length == 2 &&
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float requestedSpeed))
        {
            speedMultiplier = Mathf.Clamp(requestedSpeed, 0.25f, 4f);
            skipUntilUnscaledTime = 0f;
            ApplyTimeScale();
            commandFeedback = "배속 x" + speedMultiplier.ToString("0.##");
            return;
        }

        commandFeedback = "사용법: skip, speed 1, speed 2, speed 3";
    }

    private void ApplyTimeScale()
    {
        Time.timeScale = baseTimeScale * speedMultiplier;
    }

    private void OnDestroy()
    {
        if (Time.timeScale != baseTimeScale)
            Time.timeScale = baseTimeScale;
    }

    private void HandleGoalReached(GoalTrigger2D goal, PlayerMovement _)
    {
        completedGoalCount++;
        lastCompletedGoal = goal != null ? goal.gameObject.name : "Goal";
    }
}
