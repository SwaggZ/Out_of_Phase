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
        [Range(50f, 120f)]
        public float fov = 90f;

        public bool epilepsyMode = false;

        [Header("Controls")]
        [Range(0.1f, 10f)]
        public float mouseSensitivity = 1f;

        [Header("Audio")]
        [Range(0f, 1f)]
        public float masterVolume = 0.03f;

        [Range(0f, 1f)]
        public float musicVolume = 0.24f;

        [Range(0f, 1f)]
        public float ambienceVolume = 0.42f;

        [Range(0f, 1f)]
        public float sfxVolume = 1f;

        /// <summary>
        /// Returns a deep copy.
        /// </summary>
        public SettingsData Clone()
        {
            return new SettingsData
            {
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
