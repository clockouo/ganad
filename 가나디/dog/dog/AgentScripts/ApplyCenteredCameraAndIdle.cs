using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ApplyCenteredCameraAndIdle
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before saving the camera and idle assets.");
        const string folder = "tmp/feedback-v2.38/";
        Directory.CreateDirectory(folder);
        var scene = EditorSceneManager.GetActiveScene();
        if (!File.Exists(folder + "before-active.unity"))
            EditorSceneManager.SaveScene(scene, folder + "before-active.unity", true);
        foreach (string path in Directory.GetFiles("Assets/Art/PixelSnapper/Animations/"))
            if (Path.GetFileName(path).StartsWith("PixelSnapperBorderCollie") && !File.Exists(folder + Path.GetFileName(path)))
                File.Copy(path, folder + Path.GetFileName(path));
        const string idlePath = "Assets/Art/AfterTheGlow/Characters/Dog_User_Idle_6Frames.png";
        AssetDatabase.ImportAsset(idlePath, ImportAssetOptions.ForceSynchronousImport);
        var setup = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AfterTheGlowStorySetup")).First(t => t != null);
        setup.GetMethod("RefreshPptDogArt", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        var follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        Undo.RecordObject(follow, "Center smooth player camera");
        follow.target = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform;
        follow.offset = new Vector3(0f, 0f, -10f);
        follow.exteriorOffset = new Vector3(0f, 0f, -10f);
        follow.smoothTime = follow.exteriorSmoothTime = .18f;
        follow.homeFollowResponseMultiplier = 1f;
        follow.constrainHorizontalPosition = true;
        var camera = follow.GetComponent<Camera>();
        camera.orthographicSize = Mathf.Min(follow.homeOrthographicSize,
            (follow.homeMapMaxX - follow.mapMinX) / (2f * Mathf.Max(.01f, camera.aspect)));
        float half = camera.orthographicSize * camera.aspect;
        float x = Mathf.Clamp(follow.target.position.x, follow.mapMinX + half, follow.homeMapMaxX - half);
        follow.transform.position = new Vector3(x, follow.lockedY, -10f);
        EditorUtility.SetDirty(follow);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/PixelSnapper/Animations/PixelSnapperBorderCollie_Idle.anim");
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(idle).Single();
        File.WriteAllLines(folder + "assets.txt", new[] {
            "Camera: home and exterior offset X=0; SmoothDamp=.18s; home multiplier=1; clamp both desired and smoothed positions.",
            "Camera limits: home=" + follow.mapMinX + ".." + follow.homeMapMaxX + "; exterior=" + follow.exteriorMapMinX + ".." + follow.mapMaxX,
            "Idle: loop=" + AnimationUtility.GetAnimationClipSettings(idle).loopTime + "; duration=" + idle.length
        }.Concat(AnimationUtility.GetObjectReferenceCurve(idle, binding).Select(k => k.time + " " + AssetDatabase.GetAssetPath(k.value) + ":" + k.value.name)));
        return "Centered camera and six-frame idle saved. Heart overlay removed in StoryDirector source.";
    }
}
