using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AfterTheGlowStoryDirector))]
public sealed class GateLeashScenePreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("목줄 크기", EditorStyles.boldLabel);
        var scale = serializedObject.FindProperty("gateMemoryLeashScale");
        var brokenPreview = serializedObject.FindProperty("gateMemoryLeashPreviewBroken");
        EditorGUILayout.PropertyField(scale, new GUIContent("크기 배율",
            "1.0이 현재 화면 크기입니다. 목줄의 모든 장면에 공통 적용됩니다."));
        EditorGUILayout.PropertyField(brokenPreview, new GUIContent("마지막 파손 미리보기",
            "켜면 Scene 뷰에서 마지막으로 끊어진 목줄 모습을 미리 봅니다."));
        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "m_Script", "gateMemoryLeashScale", "gateMemoryLeashPreviewBroken");
        if (serializedObject.ApplyModifiedProperties())
            SceneView.RepaintAll();
    }
}

[InitializeOnLoad]
public static class GateLeashScenePreviewHook
{
    private const float PreviewAspect = 16f / 9f;
    private static Texture2D cleanFence;
    private static Texture2D tornLeash;

    static GateLeashScenePreviewHook()
    {
        SceneView.duringSceneGui -= DrawScenePreview;
        SceneView.duringSceneGui += DrawScenePreview;
    }

    private static void DrawScenePreview(SceneView sceneView)
    {
        if (EditorApplication.isPlaying || sceneView == null)
            return;

        var director = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<AfterTheGlowStoryDirector>()
            : null;
        if (director == null)
            return;

        if (cleanFence == null)
            cleanFence = Resources.Load<Texture2D>("Puzzle03/LeashGateClean");
        if (tornLeash == null)
            tornLeash = Resources.Load<Texture2D>("Puzzle03/LeashTornHalves");
        if (cleanFence == null)
            return;

        float previewWidth = Mathf.Min(560f, sceneView.position.width - 24f);
        if (previewWidth < 160f)
            return;

        float previewHeight = previewWidth / PreviewAspect;
        Rect panel = new Rect(12f, 12f, previewWidth + 16f, previewHeight + 72f);
        Rect image = new Rect(panel.x + 8f, panel.y + 30f, previewWidth, previewHeight);

        Handles.BeginGUI();
        GUI.Box(panel, GUIContent.none, EditorStyles.helpBox);
        GUI.Label(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, 20f),
            director.GateMemoryLeashPreviewBroken
                ? "목줄 미리보기 · 마지막 파손 상태"
                : "목줄 미리보기 · 묶인 상태");

        GUI.BeginGroup(image);
        Rect localImage = new Rect(0f, 0f, image.width, image.height);
        GUI.DrawTexture(localImage, cleanFence, ScaleMode.ScaleToFit, false);

        if (director.GateMemoryLeashPreviewBroken && tornLeash != null)
        {
            Rect tornRect = localImage;
            tornRect.size *= director.GateMemoryLeashScale;
            tornRect.center = localImage.center;
            GUI.DrawTexture(tornRect, tornLeash, ScaleMode.ScaleToFit, true);
        }
        else
        {
            float leashWidth = image.width * .95f * director.GateMemoryLeashScale;
            float leashHeight = image.height * .38f * director.GateMemoryLeashScale;
            Rect leashRect = new Rect(localImage.center.x - leashWidth * .5f,
                localImage.center.y - leashHeight * .5f, leashWidth, leashHeight);
            JourneyArt.Draw(leashRect, JourneyArt.Frame("Puzzles/LeashStatesFixed__c2r3", 0));
        }

        GUI.EndGroup();

        float scale = director.GateMemoryLeashScale;
        GUI.Label(new Rect(panel.x + 10f, image.yMax + 7f, 78f, 20f), $"크기 {scale:F2}");
        Rect slider = new Rect(panel.x + 86f, image.yMax + 7f, previewWidth - 98f, 20f);
        if (slider.Contains(Event.current.mousePosition))
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        var serializedDirector = new SerializedObject(director);
        serializedDirector.Update();
        var scaleProperty = serializedDirector.FindProperty("gateMemoryLeashScale");
        EditorGUI.BeginChangeCheck();
        float newScale = GUI.HorizontalSlider(slider, scaleProperty.floatValue, .5f, 1.5f);
        if (EditorGUI.EndChangeCheck())
        {
            scaleProperty.floatValue = newScale;
            serializedDirector.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }

        Handles.EndGUI();
    }
}
