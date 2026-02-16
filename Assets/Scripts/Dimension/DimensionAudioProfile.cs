using UnityEngine;
using UnityEngine.Audio;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Audio profile for a single dimension.
    /// Assign music loop, ambience loop, optional transition stinger, and mixer snapshot.
    /// Create one per dimension via Assets > Create > OutOfPhase > Dimension Audio Profile.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDimensionAudio", menuName = "OutOfPhase/Dimension Audio Profile")]
    public class DimensionAudioProfile : ScriptableObject
    {
        [Header("Music")]
        [Tooltip("Looping music track for this dimension")]
        public AudioClip musicLoop;

        [Tooltip("Music volume multiplier (0-1)")]
        [Range(0f, 1f)]
        public float musicVolume = 1f;

        [Header("Ambience")]
        [Tooltip("Looping ambient sound for this dimension")]
        public AudioClip ambienceLoop;

        [Tooltip("Ambience volume multiplier (0-1)")]
        [Range(0f, 1f)]
        public float ambienceVolume = 1f;

        [Header("Transition")]
        [Tooltip("Optional one-shot stinger played when entering this dimension")]
        public AudioClip transitionStinger;

        [Tooltip("Stinger volume")]
        [Range(0f, 1f)]
        public float stingerVolume = 0.8f;

        [Header("Mixer")]
        [Tooltip("AudioMixer snapshot to transition to in this dimension (optional)")]
        public AudioMixerSnapshot mixerSnapshot;

        [Tooltip("Time to transition to the mixer snapshot")]
        public float snapshotTransitionTime = 0.5f;
    }
}
