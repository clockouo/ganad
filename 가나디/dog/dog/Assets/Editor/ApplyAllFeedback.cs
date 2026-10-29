using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Reapplies the accepted feedback to existing scene objects and assets.</summary>
public static class ApplyAllFeedback
{
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before saving feedback.");
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story == null) throw new MissingReferenceException("Open SampleScene first.");
        var scene = story.gameObject.scene;
        string folder = "tmp/feedback-applied-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(folder);
        EditorSceneManager.SaveScene(scene, folder + "/before-active.unity", true);
        foreach (string path in Directory.GetFiles("Assets/Art/PixelSnapper/Animations/"))
            if (Path.GetFileName(path).StartsWith("PixelSnapperBorderCollie"))
                File.Copy(path, folder + "/" + Path.GetFileName(path));

        Type setup = typeof(AfterTheGlowStorySetup);
        void Run(string name, params object[] args)
        {
            var method = setup.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(name);
            method.Invoke(null, args);
        }
        Run("RefreshPptDogArt");
        Run("ConfigureHouseFlow", story.transform);
        Run("EnsureVisibleHeadlightBeam", story.gameObject);
        Run("ConfigureSingleCarPresentation", story.transform);
        ReferenceStreetLayout.Apply(story.transform);

        story.minimumOwnerPreparationSeconds = 4f;
        story.headlightOffDelay = 2.5f;
        var follow = story.cameraFollow;
        follow.target = story.player.transform;
        follow.offset = follow.exteriorOffset = new Vector3(0f, 0f, -10f);
        follow.smoothTime = follow.exteriorSmoothTime = .18f;
        follow.homeFollowResponseMultiplier = 1f;
        follow.homeOrthographicSize = follow.exteriorOrthographicSize = 3f;
        follow.matchExteriorSizeToHome = true;
        follow.constrainHorizontalPosition = true;
        follow.mapMinX = -11.5f;
        follow.homeMapMaxX = 6f;
        follow.exteriorMapMinX = AfterTheGlowWorldLayout.ExteriorLeftX;
        follow.mapMaxX = AfterTheGlowWorldLayout.ParkEndX;
        follow.lockY = follow.exteriorLockY = true;
        var camera = follow.GetComponent<Camera>();
        camera.orthographicSize = Mathf.Min(3f, (follow.homeMapMaxX - follow.mapMinX) /
            (2f * Mathf.Max(.01f, camera.aspect)));
        float half = camera.orthographicSize * camera.aspect;
        follow.transform.position = new Vector3(Mathf.Clamp(story.player.transform.position.x,
            follow.mapMinX + half, follow.homeMapMaxX - half), follow.lockedY, -10f);

        var wake = new SerializedObject(story.bedWakePuzzleUI);
        wake.FindProperty("approachDuration").floatValue = 1.1f;
        wake.FindProperty("timingWindow").floatValue = .16f;
        wake.FindProperty("retryDelay").floatValue = .32f;
        wake.ApplyModifiedPropertiesWithoutUndo();
        var fountain = story.GetComponent<ParkFountainExploration>();
        fountain.fountainWorldX = AfterTheGlowWorldLayout.FountainX;
        fountain.entryWorldX = AfterTheGlowWorldLayout.FountainEntryX;
        fountain.exitWorldX = AfterTheGlowWorldLayout.FountainExitX;
        fountain.approachDistance = 1f;
        fountain.memoryFadeInSeconds = .45f;
        fountain.memoryHoldSeconds = 1.6f;
        fountain.memoryFadeOutSeconds = .7f;
        foreach (UnityEngine.Object changed in new UnityEngine.Object[] { story, follow, fountain, story.bedWakePuzzleUI, story.player })
            EditorUtility.SetDirty(changed);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/PixelSnapper/Animations/PixelSnapperBorderCollie_Idle.anim");
        var idleBinding = AnimationUtility.GetObjectReferenceCurveBindings(idle).Single();
        var frames = AnimationUtility.GetObjectReferenceCurve(idle, idleBinding);
        File.WriteAllLines(folder + "/applied.txt", new[] {
            "All accepted feedback applied to " + scene.path,
            "Camera X offsets=0; smoothing=.18; horizontal map limits enabled.",
            "Wake rhythm: 1.1 seconds +/- .16; unlimited retries after .32; success count preserved.",
            "Dog: five-row 22-pose action sheet and separate six-frame idle strip.",
            "Idle: " + frames.Length + " keys; loop=" + AnimationUtility.GetAnimationClipSettings(idle).loopTime + "; seconds=" + idle.length,
            "Exterior: each source width=18; connected backdrop; bounded sky; light length=2.05/end half width=1.90/outward=.40.",
            "House door RIGHT, garden gate LEFT; contact house exit; repeated light retreat and no petting heart are in current runtime code.",
            "Owner preparation=4; headlight off=2.5 after first retreat; fountain left entry/right exit and automatic fading memories.",
            "Scene and assets saved. This apply operation does not run a playtest."
        });
        return "Applied and saved all accepted feedback. Record: " + folder + "/applied.txt";
    }
}
