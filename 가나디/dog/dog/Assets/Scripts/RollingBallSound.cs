using UnityEngine;

/// <summary>Plays one short, level-matched bounce when the ball starts moving.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class RollingBallSound : MonoBehaviour
{
    private const float RollingThreshold = .12f;
    private const float BaseTrim = .34f;
    private Rigidbody2D body;
    private AudioSource source;
    private GameAudioManager audioManager;
    private bool wasRolling;

    private void Awake()
    {
        if (!GameAudioManager.NonMusicAudioEnabled)
        {
            AudioSource existingSource = GetComponent<AudioSource>();
            if (existingSource != null)
            {
                existingSource.Stop();
                existingSource.clip = null;
                existingSource.playOnAwake = false;
            }
            enabled = false;
            return;
        }
        body = GetComponent<Rigidbody2D>();
        audioManager = GameAudioManager.EnsureInstance();
        source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.pitch = .84f;
        source.priority = 72;
        source.clip = audioManager.GetClip("BouncyBall");
        RefreshVolume();
    }

    private void Update()
    {
        if (audioManager == null)
            audioManager = GameAudioManager.EnsureInstance();
        if (source == null)
            return;
        if (source.clip == null && audioManager != null)
            source.clip = audioManager.GetClip("BouncyBall");

        bool rolling = source.clip != null && body != null && Time.timeScale > 0f &&
                       body.linearVelocity.magnitude >= RollingThreshold;
        if (rolling && !wasRolling && !source.isPlaying)
            source.Play();
        wasRolling = rolling;

        RefreshVolume();
    }

    private void RefreshVolume()
    {
        if (source == null || audioManager == null)
            return;
        source.volume = Mathf.Clamp01(audioManager.GetMatchedGain("BouncyBall") * BaseTrim *
                                      audioManager.GetBusVolume(GameAudioBus.Effects));
    }
}
