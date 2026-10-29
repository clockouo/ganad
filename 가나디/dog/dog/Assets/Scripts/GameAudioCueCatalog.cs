using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Audio clips assigned to the story situations that already emit audio cues.</summary>
[CreateAssetMenu(menuName = "After the Glow/Audio Cue Catalog", fileName = "GameAudioCatalog")]
public sealed class GameAudioCueCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Cue
    {
        public string key;
        public string situation;
        public GameAudioBus bus;
        public AudioClip clip;
    }

    [SerializeField] private List<Cue> cues = new List<Cue>();
    public List<Cue> Cues => cues;

    public bool TryGetClip(string key, out AudioClip clip)
    {
        for (int i = 0; i < cues.Count; i++)
        {
            Cue cue = cues[i];
            if (cue != null && cue.key == key)
            {
                clip = cue.clip;
                return true;
            }
        }

        clip = null;
        return false;
    }
}
