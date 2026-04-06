using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Echo Mortalis/Audio Cue Library", fileName = "AudioCueLibrary")]
public class AudioCueLibrary : ScriptableObject
{
    [SerializeField] private AudioCueEntry[] cues = Array.Empty<AudioCueEntry>();

    public AudioCueEntry[] Cues => cues;

    [Serializable]
    public struct AudioCueEntry
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float defaultVolume;
        public Vector2 pitchRange;
        public bool loop;

        public float GetDefaultPitch()
        {
            float minPitch = pitchRange.x <= 0f ? 1f : pitchRange.x;
            float maxPitch = pitchRange.y <= 0f ? minPitch : pitchRange.y;

            if (Mathf.Approximately(minPitch, maxPitch))
                return minPitch;

            if (maxPitch < minPitch)
            {
                float swap = minPitch;
                minPitch = maxPitch;
                maxPitch = swap;
            }

            return UnityEngine.Random.Range(minPitch, maxPitch);
        }
    }
}
