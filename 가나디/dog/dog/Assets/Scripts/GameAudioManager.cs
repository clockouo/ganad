using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameAudioBus
{
    Effects,
    Background
}

/// <summary>Central volume control and loudness matching for the story slice.</summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class GameAudioManager : MonoBehaviour
{
    private const string MasterPref = "AfterTheGlow.Audio.Master";
    private const string BackgroundPref = "AfterTheGlow.Audio.Background";
    private const string EffectsPref = "AfterTheGlow.Audio.Effects";
    private static GameAudioManager instance;

    public static bool NonMusicAudioEnabled => true;

    private sealed class Voice
    {
        public AudioSource Source;
        public GameAudioBus Bus;
        public float MatchedGain = 1f;
        public float Trim = 1f;
        public float Fade = 1f;
        public bool OneShot;
        public Coroutine FadeRoutine;
    }

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<AudioClip, float> matchedGains = new Dictionary<AudioClip, float>();
    private readonly Dictionary<string, Voice> loopSfx = new Dictionary<string, Voice>();
    private readonly Dictionary<string, Voice> gatedOneShotSfx = new Dictionary<string, Voice>();
    private readonly List<Voice> voices = new List<Voice>();
    private Voice mainAmbience;
    private Voice secondaryAmbience;
    private Voice musicVoice;
    private GameAudioCueCatalog cueCatalog;
    private float masterVolume = .8f;
    private float backgroundVolume = .6f;
    private float effectsVolume = .8f;
    private bool preferencesDirty;
    private float savePreferencesAt;

    public static GameAudioManager Instance => instance;

    public static GameAudioManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<GameAudioManager>();
        if (instance != null)
            return instance;

        var managerObject = new GameObject("After the Glow Audio Manager");
        return managerObject.AddComponent<GameAudioManager>();
    }

    public float MasterVolume => masterVolume;
    public float BackgroundVolume => backgroundVolume;
    public float EffectsVolume => effectsVolume;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterPref, .8f));
        backgroundVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BackgroundPref, .6f));
        effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsPref, .8f));
        cueCatalog = Resources.Load<GameAudioCueCatalog>("Audio/GameAudioCatalog");

        // Any AudioClip placed in Resources/Audio is available by its file name.
        // This lets sound replacements be added without editing this script.
        foreach (AudioClip clip in Resources.LoadAll<AudioClip>("Audio"))
        {
            if (clip == null)
                continue;

            if (clips.ContainsKey(clip.name))
            {
                Debug.LogWarning("Duplicate game audio clip name; keep one file named " + clip.name, this);
                continue;
            }

            clip.LoadAudioData();
            clips[clip.name] = clip;
        }

        if (NonMusicAudioEnabled)
        {
            mainAmbience = CreateLoopVoice("Main Ambience");
            secondaryAmbience = CreateLoopVoice("Secondary Ambience");
        }
        musicVoice = CreateLoopVoice("Background Music");
        SettingsMenuController.EnsureInstance();
    }

    private void Update()
    {
        for (int i = voices.Count - 1; i >= 0; i--)
        {
            Voice voice = voices[i];
            if (voice.Source == null)
            {
                voices.RemoveAt(i);
                continue;
            }

            if (voice.OneShot && !voice.Source.isPlaying)
            {
                GameObject voiceObject = voice.Source.gameObject;
                voices.RemoveAt(i);
                Destroy(voiceObject);
                continue;
            }

            RefreshVoiceVolume(voice);
        }

        if (preferencesDirty && Time.unscaledTime >= savePreferencesAt)
        {
            PlayerPrefs.Save();
            preferencesDirty = false;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnApplicationQuit() => PlayerPrefs.Save();

    private Voice CreateLoopVoice(string voiceName)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.priority = 64;

        var voice = new Voice
        {
            Source = source,
            Bus = GameAudioBus.Background,
            OneShot = false,
            Fade = 0f,
            Trim = 1f
        };
        source.name = voiceName;
        voices.Add(voice);
        return voice;
    }

    public AudioClip GetClip(string key)
    {
        if (cueCatalog != null && cueCatalog.TryGetClip(key, out AudioClip assigned) && assigned != null)
            return assigned;
        return !string.IsNullOrEmpty(key) && clips.TryGetValue(key, out AudioClip clip) ? clip : null;
    }

    public void SetVolumes(float master, float background, float effects)
    {
        masterVolume = Mathf.Clamp01(master);
        backgroundVolume = Mathf.Clamp01(background);
        effectsVolume = Mathf.Clamp01(effects);
        PlayerPrefs.SetFloat(MasterPref, masterVolume);
        PlayerPrefs.SetFloat(BackgroundPref, backgroundVolume);
        PlayerPrefs.SetFloat(EffectsPref, effectsVolume);
        preferencesDirty = true;
        savePreferencesAt = Time.unscaledTime + .65f;

        for (int i = 0; i < voices.Count; i++)
            RefreshVoiceVolume(voices[i]);
    }

    public float GetBusVolume(GameAudioBus bus)
    {
        if (bus == GameAudioBus.Effects && !NonMusicAudioEnabled)
            return 0f;
        return masterVolume * (bus == GameAudioBus.Background ? backgroundVolume : effectsVolume);
    }

    public float GetMatchedGain(string key)
    {
        AudioClip clip = GetClip(key);
        return clip != null ? GetMatchedGain(clip, GetBusForKey(key)) : .45f;
    }

    public void PlaySfx(string key, float trim = 1f, float pitch = 1f)
    {
        if (!NonMusicAudioEnabled)
            return;
        AudioClip clip = GetClip(key);
        if (clip == null)
            return;
        PlayClip(clip, GameAudioBus.Effects, trim, pitch);
    }

    public void PlaySfxIfIdle(string key, float trim = 1f, float pitch = 1f)
    {
        if (!NonMusicAudioEnabled)
            return;
        if (string.IsNullOrEmpty(key))
            return;

        if (gatedOneShotSfx.TryGetValue(key, out Voice active))
        {
            if (active.Source != null && active.Source.isPlaying)
                return;
            gatedOneShotSfx.Remove(key);
        }

        AudioClip clip = GetClip(key);
        if (clip == null)
            return;

        var voiceObject = new GameObject("SFX - " + clip.name);
        voiceObject.transform.SetParent(transform, false);
        AudioSource source = voiceObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.priority = 64;
        source.pitch = Mathf.Clamp(pitch, .5f, 1.5f);

        var voice = new Voice
        {
            Source = source,
            Bus = GameAudioBus.Effects,
            MatchedGain = GetMatchedGain(clip, GameAudioBus.Effects),
            Trim = Mathf.Clamp01(trim),
            Fade = 1f,
            OneShot = true
        };
        gatedOneShotSfx[key] = voice;
        voices.Add(voice);
        RefreshVoiceVolume(voice);
        source.Play();
    }

    public void StopGatedSfx(string key)
    {
        if (string.IsNullOrEmpty(key) || !gatedOneShotSfx.TryGetValue(key, out Voice voice))
            return;

        gatedOneShotSfx.Remove(key);
        voices.Remove(voice);
        if (voice.Source != null)
        {
            voice.Source.Stop();
            Destroy(voice.Source.gameObject);
        }
    }

    public void PlayClip(AudioClip clip, GameAudioBus bus, float trim = 1f, float pitch = 1f)
    {
        if (!NonMusicAudioEnabled)
            return;
        if (clip == null)
            return;

        var voiceObject = new GameObject("SFX - " + clip.name);
        voiceObject.transform.SetParent(transform, false);
        AudioSource source = voiceObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.priority = 64;
        source.pitch = Mathf.Clamp(pitch, .5f, 1.5f);

        var voice = new Voice
        {
            Source = source,
            Bus = bus,
            MatchedGain = GetMatchedGain(clip, bus),
            Trim = Mathf.Clamp01(trim),
            Fade = 1f,
            OneShot = true
        };
        voices.Add(voice);
        RefreshVoiceVolume(voice);
        source.Play();
    }

    public void PlayLoopSfx(string key, float trim = 1f, float pitch = 1f)
    {
        if (!NonMusicAudioEnabled)
            return;
        AudioClip clip = GetClip(key);
        if (clip == null)
            return;

        if (loopSfx.TryGetValue(key, out Voice existing))
        {
            if (existing.Source != null && existing.Source.clip == clip)
            {
                if (!existing.Source.isPlaying)
                    existing.Source.Play();
                return;
            }

            StopLoopSfx(key);
        }

        var voiceObject = new GameObject("Loop SFX - " + clip.name);
        voiceObject.transform.SetParent(transform, false);
        AudioSource source = voiceObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.priority = 64;
        source.pitch = Mathf.Clamp(pitch, .5f, 1.5f);

        var voice = new Voice
        {
            Source = source,
            Bus = GameAudioBus.Effects,
            MatchedGain = GetMatchedGain(clip, GameAudioBus.Effects),
            Trim = Mathf.Clamp01(trim),
            Fade = 1f,
            OneShot = false
        };
        loopSfx[key] = voice;
        voices.Add(voice);
        RefreshVoiceVolume(voice);
        source.Play();
    }

    public void StopLoopSfx(string key)
    {
        if (string.IsNullOrEmpty(key) || !loopSfx.TryGetValue(key, out Voice voice))
            return;

        loopSfx.Remove(key);
        voices.Remove(voice);
        if (voice.Source != null)
        {
            voice.Source.Stop();
            Destroy(voice.Source.gameObject);
        }
    }

    public void PlayAmbience(string key, float trim = 1f)
    {
        if (NonMusicAudioEnabled)
            SwitchLoop(mainAmbience, key, trim);
    }

    public void PlaySecondaryAmbience(string key, float trim = 1f)
    {
        if (NonMusicAudioEnabled)
            SwitchLoop(secondaryAmbience, key, trim);
    }

    public void PlayMusic(string key, float trim = 1f) => SwitchLoop(musicVoice, key, trim);

    private void SwitchLoop(Voice voice, string key, float trim)
    {
        if (voice == null)
            return;

        AudioClip clip = string.IsNullOrEmpty(key) ? null : GetClip(key);
        if (clip != null && voice.Source.clip == clip && voice.Source.isPlaying)
            return;

        if (voice.FadeRoutine != null)
            StopCoroutine(voice.FadeRoutine);
        voice.FadeRoutine = StartCoroutine(SwitchLoopRoutine(voice, key, clip, trim));
    }

    private IEnumerator SwitchLoopRoutine(Voice voice, string key, AudioClip clip, float trim)
    {
        const float fadeSeconds = .42f;
        float fadeStart = voice.Fade;
        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            voice.Fade = Mathf.Lerp(fadeStart, 0f, Mathf.Clamp01(elapsed / fadeSeconds));
            RefreshVoiceVolume(voice);
            yield return null;
        }

        voice.Source.Stop();
        voice.Source.clip = clip;
        voice.Source.loop = true;
        voice.Source.pitch = 1f;
        voice.Trim = Mathf.Clamp01(trim);
        voice.MatchedGain = clip != null ? GetMatchedGain(clip, GameAudioBus.Background) : 1f;
        voice.Fade = 0f;
        if (clip != null)
            voice.Source.Play();

        if (clip != null)
        {
            elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                voice.Fade = Mathf.Clamp01(elapsed / fadeSeconds);
                RefreshVoiceVolume(voice);
                yield return null;
            }
            voice.Fade = 1f;
        }

        voice.FadeRoutine = null;
        RefreshVoiceVolume(voice);
    }

    private void RefreshVoiceVolume(Voice voice)
    {
        if (voice == null || voice.Source == null)
            return;
        float busVolume = GetBusVolume(voice.Bus);
        voice.Source.volume = Mathf.Clamp01(voice.MatchedGain * voice.Trim * voice.Fade * busVolume);
    }

    private float GetMatchedGain(AudioClip clip, GameAudioBus bus)
    {
        if (clip == null)
            return .45f;
        if (matchedGains.TryGetValue(clip, out float cached))
            return cached;

        if (clip.loadState != AudioDataLoadState.Loaded)
            clip.LoadAudioData();

        int count = clip.samples * clip.channels;
        if (count <= 0)
            return 1f;

        float[] samples = new float[count];
        if (!clip.GetData(samples, 0))
        {
            Debug.LogWarning("Couldn't read audio samples for loudness matching: " + clip.name, this);
            matchedGains[clip] = 1f;
            return 1f;
        }

        double squareTotal = 0d;
        int activeSamples = 0;
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float sample = samples[i];
            float magnitude = Mathf.Abs(sample);
            if (magnitude > peak)
                peak = magnitude;
            if (magnitude < .006f)
                continue;
            squareTotal += sample * sample;
            activeSamples++;
        }

        if (activeSamples == 0 || peak <= .0001f)
        {
            matchedGains[clip] = 1f;
            return 1f;
        }

        float measuredRms = Mathf.Sqrt((float)(squareTotal / activeSamples));
        float targetRms = bus == GameAudioBus.Background ? .034f : .092f;
        float gain = targetRms / Mathf.Max(.004f, measuredRms);
        gain = Mathf.Min(gain, .88f / peak);
        gain = Mathf.Clamp(gain, .08f, 4f);
        matchedGains[clip] = gain;
        return gain;
    }

    private static GameAudioBus GetBusForKey(string key)
    {
        return key == "RoomTone" || key == "StreetTraffic" || key == "DieselIdle" || key == "ParkBirds" ||
               key == "AtmosphericEmotionalPiano"
            ? GameAudioBus.Background
            : GameAudioBus.Effects;
    }
}
