using System;
using UnityEngine;

namespace OutOfPhase.UI
{
    /// <summary>
    /// Serializable settings data model. Saved/loaded via PlayerPrefs JSON.
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        [Header("Display")]
        [Range(0.5f, 2f)]
        public float brightness = 1f;

        [Range(50f, 120f)]
        public float fov = 75f;

        public bool epilepsyMode = false;

        [Header("Controls")]
        [Range(0.1f, 10f)]
        public float mouseSensitivity = 2f;

        [Header("Audio")]
        [Range(0f, 1f)]
        public float masterVolume = 1f;

        [Range(0f, 1f)]
        public float musicVolume = 0.8f;

        [Range(0f, 1f)]
        public float ambienceVolume = 0.8f;

        [Range(0f, 1f)]
        public float sfxVolume = 1f;

        /// <summary>
        /// Returns a deep copy.
        /// </summary>
        public SettingsData Clone()
        {
            return new SettingsData
            {
                brightness = brightness,
                fov = fov,
                epilepsyMode = epilepsyMode,
                mouseSensitivity = mouseSensitivity,
                masterVolume = masterVolume,
                musicVolume = musicVolume,
                ambienceVolume = ambienceVolume,
                sfxVolume = sfxVolume
            };
        }
    }
}
