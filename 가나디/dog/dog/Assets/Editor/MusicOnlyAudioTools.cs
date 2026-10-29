using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Pipeline.Commands;

/// <summary>Archives temporarily removed sounds and checks the music-only policy.</summary>
public static class MusicOnlyAudioTools
{
    private const string BackgroundMusic = "AtmosphericEmotionalPiano";
    private const string BackgroundSessionKey = "AfterTheGlow.MusicOnly.PreviousRunInBackground";

    [CliCommand("music_only_audio", "Archive non-music audio or inspect the music-only runtime", MainThreadRequired = true)]
    public static object Command([CliArg("action", "status, archive, prepare, restore, probe")] string action = "status")
    {
        if (action == "archive") return Archive();
        if (action == "prepare")
        {
            SessionState.SetBool(BackgroundSessionKey, Application.runInBackground);
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            return new { runInBackground = Application.runInBackground };
        }
        if (action == "restore")
        {
            Application.runInBackground = SessionState.GetBool(BackgroundSessionKey, false);
            return new { runInBackground = Application.runInBackground };
        }
        if (action == "probe") Probe();
        var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var manager = GameAudioManager.Instance;
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        return new
        {
            playing = EditorApplication.isPlaying,
            frame = Time.frameCount,
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            sceneDirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,
            nonMusicEnabled = GameAudioManager.NonMusicAudioEnabled,
            audioAssets = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToArray(),
            sources = sources.Select(s => new { name = s.gameObject.name, clip = s.clip != null ? s.clip.name : null,
                playing = s.isPlaying, loop = s.loop, volume = s.volume, time = s.time }).ToArray(),
            loadedClips = Resources.FindObjectsOfTypeAll<AudioClip>().Where(c => !EditorUtility.IsPersistent(c) || AssetDatabase.GetAssetPath(c).StartsWith("Assets/"))
                .Select(c => c.name).Distinct().ToArray(),
            stage = story != null ? story.Stage.ToString() : null,
            effectsVolume = manager != null ? manager.GetBusVolume(GameAudioBus.Effects) : -1f
        };
    }

    private static object Archive()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before archiving audio.");
        string source = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources/Audio"));
        string destination = Path.GetFullPath(Path.Combine(Application.dataPath, "../AudioSourceBackup/2026-10-22-non-music"));
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")) + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(project, StringComparison.OrdinalIgnoreCase) || destination.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Archive must be inside the project and outside Assets.");
        var files = Directory.GetFiles(source).Where(p => new[] { ".mp3", ".wav", ".ogg", ".m4a", ".flac" }
            .Contains(Path.GetExtension(p).ToLowerInvariant()) && Path.GetFileNameWithoutExtension(p) != BackgroundMusic).ToArray();
        foreach (string file in files)
        {
            if (File.Exists(Path.Combine(destination, Path.GetFileName(file))) || File.Exists(Path.Combine(destination, Path.GetFileName(file) + ".meta")))
                throw new IOException("Archive already contains " + Path.GetFileName(file));
        }
        Directory.CreateDirectory(destination);
        string credits = Path.Combine(source, "SOUND_CREDITS.md");
        if (File.Exists(credits) && !File.Exists(Path.Combine(destination, "SOUND_CREDITS_before.md")))
            File.Copy(credits, Path.Combine(destination, "SOUND_CREDITS_before.md"));
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string file in files)
            {
                File.Move(file, Path.Combine(destination, Path.GetFileName(file)));
                if (File.Exists(file + ".meta"))
                    File.Move(file + ".meta", Path.Combine(destination, Path.GetFileName(file) + ".meta"));
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
        return new { archived = files.Select(Path.GetFileName).ToArray(), count = files.Length, destination };
    }

    private static void Probe()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode to verify audio.");
        var manager = GameAudioManager.EnsureInstance();
        int before = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        foreach (string key in new[] { "RoomTone", "StreetTraffic", "DieselIdle", "ParkBirds", "DogPaws", "BouncyBall", "Click", "BedBlanket", "ClothRubbing" })
        {
            if (manager.GetClip(key) != null) throw new InvalidOperationException("Non-music clip loaded: " + key);
            manager.PlaySfx(key);
            manager.PlaySfxIfIdle(key);
            manager.PlayLoopSfx(key);
            manager.PlayAmbience(key);
            manager.PlaySecondaryAmbience(key);
        }
        var temporaryClip = AudioClip.Create("MusicOnlyPolicyProbe", 100, 1, 44100, false);
        manager.PlayClip(temporaryClip, GameAudioBus.Effects);
        manager.PlayClip(temporaryClip, GameAudioBus.Background);
        UnityEngine.Object.DestroyImmediate(temporaryClip);
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story != null)
        {
            var doorField = typeof(AfterTheGlowStoryDirector).GetField("doorSlamClip", BindingFlags.Instance | BindingFlags.NonPublic);
            var previousDoorClip = doorField.GetValue(story) as AudioClip;
            typeof(AfterTheGlowStoryDirector).GetMethod("PlayDoorSlam", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(story, null);
            if ((doorField.GetValue(story) as AudioClip) != previousDoorClip)
                throw new InvalidOperationException("Door sound was synthesized.");
        }
        var ball = new GameObject("MusicOnlyBallProbe");
        var ballSound = ball.AddComponent<RollingBallSound>();
        bool ballSilent = !ballSound.enabled && ball.GetComponent<AudioSource>() == null;
        UnityEngine.Object.DestroyImmediate(ball);
        var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (!ballSilent || sources.Length != before || sources.Any(s => s.clip != null && s.clip.name != BackgroundMusic) ||
            manager.GetBusVolume(GameAudioBus.Effects) != 0f || manager.GetClip(BackgroundMusic) == null)
            throw new InvalidOperationException("Music-only audio policy failed: ballSilent=" + ballSilent +
                ", sources=" + before + "->" + sources.Length + ", musicLoaded=" + (manager.GetClip(BackgroundMusic) != null) +
                ", effectsVolume=" + manager.GetBusVolume(GameAudioBus.Effects));
    }
}
