using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OutOfPhase.UI
{
    /// <summary>
    /// Singleton that loads, saves, and applies game settings via PlayerPrefs.
    /// Attach to a persistent GameObject (e.g., alongside DimensionManager).
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        private const string PREFS_KEY = "OutOfPhase_Settings";

        [Header("References (auto-found if null)")]
        [Tooltip("URP Volume for brightness control")]
        [SerializeField] private Volume postProcessVolume;

        /// <summary>Current live settings.</summary>
        public SettingsData Current { get; private set; }

        /// <summary>Fires after any setting is applied.</summary>
        public event Action<SettingsData> OnSettingsChanged;

        /// <summary>Check this from any effect script to skip flashing/glitch.</summary>
        public static bool EpilepsyMode => Instance != null && Instance.Current.epilepsyMode;

        private ColorAdjustments _colorAdjustments;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Current = Load();
            FindVolume();
            ApplyAll();
        }

        /// <summary>
        /// Apply a full SettingsData, save it, and broadcast the change.
        /// </summary>
        public void Apply(SettingsData data)
        {
            Current = data.Clone();
            ApplyAll();
            Save();
            OnSettingsChanged?.Invoke(Current);
        }

        /// <summary>
        /// Apply all current settings to game systems.
        /// </summary>
        public void ApplyAll()
        {
            ApplyBrightness();
            ApplyFOV();
            ApplySensitivity();
            ApplyAudio();
        }

        #region Individual Apply

        private void ApplyBrightness()
        {
            if (_colorAdjustments == null) return;

            // Map brightness 0.5–2.0 → post-exposure -2 to +2
            float exposure = Mathf.Lerp(-2f, 2f, Mathf.InverseLerp(0.5f, 2f, Current.brightness));
            _colorAdjustments.postExposure.Override(exposure);
        }

        private void ApplyFOV()
        {
            var playerLook = FindFirstObjectByType<Player.PlayerLook>();
            if (playerLook != null)
            {
                playerLook.SetFOV(Current.fov);
            }
        }

        private void ApplySensitivity()
        {
            var playerLook = FindFirstObjectByType<Player.PlayerLook>();
            if (playerLook != null)
            {
                playerLook.SetSensitivity(Current.mouseSensitivity);
            }
        }

        private void ApplyAudio()
        {
            // Set global volume via AudioListener (0-1 linear)
            AudioListener.volume = Current.masterVolume;

            // Directly push per-channel volumes to DimensionAudioController
            // as a safety net in case the event subscription hasn't connected yet
            var audioController = FindFirstObjectByType<Dimension.DimensionAudioController>();
            if (audioController != null)
            {
                audioController.SetChannelVolumes(Current.musicVolume, Current.ambienceVolume);
            }
        }

        #endregion

        #region Volume Discovery

        private void FindVolume()
        {
            if (postProcessVolume == null)
            {
                postProcessVolume = FindFirstObjectByType<Volume>();
            }

            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                if (!postProcessVolume.profile.TryGet(out _colorAdjustments))
                {
                    _colorAdjustments = postProcessVolume.profile.Add<ColorAdjustments>(true);
                }
                _colorAdjustments.postExposure.overrideState = true;
            }
        }

        #endregion

        #region Persistence

        private void Save()
        {
            string json = JsonUtility.ToJson(Current);
            PlayerPrefs.SetString(PREFS_KEY, json);
            PlayerPrefs.Save();
        }

        private SettingsData Load()
        {
            if (PlayerPrefs.HasKey(PREFS_KEY))
            {
                string json = PlayerPrefs.GetString(PREFS_KEY);
                try
                {
                    return JsonUtility.FromJson<SettingsData>(json);
                }
                catch
                {
                    Debug.LogWarning("SettingsManager: Corrupt settings, using defaults.");
                }
            }
            return new SettingsData();
        }

        /// <summary>
        /// Reset all settings to defaults.
        /// </summary>
        public void ResetToDefaults()
        {
            Apply(new SettingsData());
        }

        #endregion
    }
}
