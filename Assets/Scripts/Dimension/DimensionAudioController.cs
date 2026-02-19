using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Manages dimension-based audio: music and ambience crossfade, stingers, and mixer snapshots.
    /// Uses A/B source pairs for seamless crossfading.
    /// Attach to the same persistent GameObject as DimensionManager or the Player.
    /// </summary>
    public class DimensionAudioController : MonoBehaviour
    {
        [Header("Audio Profiles (one per dimension, indexed)")]
        [Tooltip("Assign one profile per dimension in order (0 = first dimension, etc.)")]
        [SerializeField] private DimensionAudioProfile[] dimensionProfiles;

        [Header("Crossfade")]
        [Tooltip("Duration of music/ambience crossfade in seconds")]
        [SerializeField] private float crossfadeDuration = 1.5f;

        [Header("Mixer (optional)")]
        [Tooltip("AudioMixer for volume control. Expose parameters: MusicVolume, AmbienceVolume, SFXVolume, MasterVolume")]
        [SerializeField] private AudioMixer audioMixer;

        // A/B audio source pairs for crossfading
        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _ambienceA;
        private AudioSource _ambienceB;
        private AudioSource _stingerSource;

        // Track which source is currently "active"
        private bool _musicAActive = true;
        private bool _ambienceAActive = true;

        // Coroutine refs
        private Coroutine _musicFade;
        private Coroutine _ambienceFade;

        // Current profile index
        private int _currentProfileIndex = -1;

        // Volume multipliers from settings
        private float _settingsMusicVolume = 0.8f;
        private float _settingsAmbienceVolume = 0.8f;
        private bool _subscribedToSettings;

        private void Awake()
        {
            CreateAudioSources();
        }

        private void OnEnable()
        {
            DimensionManager.OnManagerReady += OnManagerReady;

            if (DimensionManager.Instance != null)
                Subscribe();

            TrySubscribeToSettings();
        }

        private void OnDisable()
        {
            DimensionManager.OnManagerReady -= OnManagerReady;

            if (DimensionManager.Instance != null)
                Unsubscribe();

            if (_subscribedToSettings && UI.SettingsManager.Instance != null)
            {
                UI.SettingsManager.Instance.OnSettingsChanged -= OnSettingsChanged;
                _subscribedToSettings = false;
            }
        }

        private void OnManagerReady()
        {
            Subscribe();
            // Play initial dimension audio immediately (no crossfade)
            PlayDimensionAudio(DimensionManager.Instance.CurrentDimension, immediate: true);
        }

        private void Subscribe()
        {
            DimensionManager.Instance.OnDimensionChanged += OnDimensionChanged;
        }

        private void Unsubscribe()
        {
            DimensionManager.Instance.OnDimensionChanged -= OnDimensionChanged;
        }

        private void Start()
        {
            // Retry settings subscription (SettingsManager may not have existed in OnEnable)
            TrySubscribeToSettings();

            // If DimensionManager already exists, play initial audio
            if (DimensionManager.Instance != null && _currentProfileIndex < 0)
            {
                PlayDimensionAudio(DimensionManager.Instance.CurrentDimension, immediate: true);
            }
        }

        private void Update()
        {
            // Keep retrying until we successfully subscribe to settings
            if (!_subscribedToSettings)
                TrySubscribeToSettings();
        }

        /// <summary>
        /// Subscribe to SettingsManager if available and not already subscribed.
        /// Also re-applies current volumes to playing sources.
        /// </summary>
        private void TrySubscribeToSettings()
        {
            if (_subscribedToSettings) return;
            if (UI.SettingsManager.Instance == null) return;

            UI.SettingsManager.Instance.OnSettingsChanged += OnSettingsChanged;
            _subscribedToSettings = true;

            // Apply current settings immediately and update any playing sources
            ApplySettingsVolumes(UI.SettingsManager.Instance.Current);
        }

        private void OnDimensionChanged(int oldDimension, int newDimension)
        {
            PlayDimensionAudio(newDimension, immediate: false);
        }

        private void OnSettingsChanged(UI.SettingsData data)
        {
            ApplySettingsVolumes(data);
        }

        /// <summary>
        /// Public entry point for SettingsManager to directly push per-channel volumes.
        /// Called as a fallback in case the event subscription hasn't connected.
        /// </summary>
        public void SetChannelVolumes(float musicVolume, float ambienceVolume)
        {
            _settingsMusicVolume = musicVolume;
            _settingsAmbienceVolume = ambienceVolume;
            UpdateActiveVolumes();
        }

        private void ApplySettingsVolumes(UI.SettingsData data)
        {
            // Note: masterVolume is handled globally by SettingsManager via AudioListener.volume.
            // We only apply per-channel volumes here to avoid doubling master.
            _settingsMusicVolume = data.musicVolume;
            _settingsAmbienceVolume = data.ambienceVolume;

            // Apply to mixer if available
            if (audioMixer != null)
            {
                // Convert 0-1 linear to decibels (-80dB to 0dB)
                audioMixer.SetFloat("MasterVolume", LinearToDecibel(data.masterVolume));
                audioMixer.SetFloat("MusicVolume", LinearToDecibel(data.musicVolume));
                audioMixer.SetFloat("AmbienceVolume", LinearToDecibel(data.ambienceVolume));
                audioMixer.SetFloat("SFXVolume", LinearToDecibel(data.sfxVolume));
            }

            // Update active source volumes
            UpdateActiveVolumes();
        }

        #region Playback

        private void PlayDimensionAudio(int dimensionIndex, bool immediate)
        {
            if (dimensionProfiles == null || dimensionIndex < 0 || dimensionIndex >= dimensionProfiles.Length)
                return;

            DimensionAudioProfile profile = dimensionProfiles[dimensionIndex];
            if (profile == null) return;

            _currentProfileIndex = dimensionIndex;

            // Music crossfade
            CrossfadeMusic(profile, immediate);

            // Ambience crossfade
            CrossfadeAmbience(profile, immediate);

            // Stinger
            if (!immediate && profile.transitionStinger != null && _stingerSource != null)
            {
                _stingerSource.PlayOneShot(profile.transitionStinger, profile.stingerVolume * SFXPlayer.GetSFXVolume());
            }

            // Mixer snapshot
            if (profile.mixerSnapshot != null)
            {
                profile.mixerSnapshot.TransitionTo(immediate ? 0f : profile.snapshotTransitionTime);
            }
        }

        private void CrossfadeMusic(DimensionAudioProfile profile, bool immediate)
        {
            if (_musicFade != null) StopCoroutine(_musicFade);

            AudioSource incoming = _musicAActive ? _musicB : _musicA;
            AudioSource outgoing = _musicAActive ? _musicA : _musicB;
            _musicAActive = !_musicAActive;

            float targetVolume = profile.musicVolume * GetEffectiveMusicVolume();

            // Set up incoming
            incoming.clip = profile.musicLoop;
            incoming.loop = true;

            if (immediate)
            {
                outgoing.Stop();
                outgoing.volume = 0f;
                incoming.volume = targetVolume;
                if (profile.musicLoop != null) incoming.Play();
            }
            else
            {
                if (profile.musicLoop != null) incoming.Play();
                _musicFade = StartCoroutine(CrossfadeCoroutine(outgoing, incoming, targetVolume, crossfadeDuration));
            }
        }

        private void CrossfadeAmbience(DimensionAudioProfile profile, bool immediate)
        {
            if (_ambienceFade != null) StopCoroutine(_ambienceFade);

            AudioSource incoming = _ambienceAActive ? _ambienceB : _ambienceA;
            AudioSource outgoing = _ambienceAActive ? _ambienceA : _ambienceB;
            _ambienceAActive = !_ambienceAActive;

            float targetVolume = profile.ambienceVolume * GetEffectiveAmbienceVolume();

            incoming.clip = profile.ambienceLoop;
            incoming.loop = true;

            if (immediate)
            {
                outgoing.Stop();
                outgoing.volume = 0f;
                incoming.volume = targetVolume;
                if (profile.ambienceLoop != null) incoming.Play();
            }
            else
            {
                if (profile.ambienceLoop != null) incoming.Play();
                _ambienceFade = StartCoroutine(CrossfadeCoroutine(outgoing, incoming, targetVolume, crossfadeDuration));
            }
        }

        private IEnumerator CrossfadeCoroutine(AudioSource outgoing, AudioSource incoming, float targetVolume, float duration)
        {
            float elapsed = 0f;
            float startOutVol = outgoing.volume;
            float startInVol = 0f;
            incoming.volume = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth S-curve for natural crossfade
                float curve = t * t * (3f - 2f * t);

                outgoing.volume = Mathf.Lerp(startOutVol, 0f, curve);
                incoming.volume = Mathf.Lerp(startInVol, targetVolume, curve);

                yield return null;
            }

            outgoing.volume = 0f;
            outgoing.Stop();
            incoming.volume = targetVolume;
        }

        #endregion

        #region Volume Helpers

        private float GetEffectiveMusicVolume()
        {
            // Master is handled by AudioListener.volume; only apply channel volume here.
            if (audioMixer != null) return 1f;
            return _settingsMusicVolume;
        }

        private float GetEffectiveAmbienceVolume()
        {
            if (audioMixer != null) return 1f;
            return _settingsAmbienceVolume;
        }

        private void UpdateActiveVolumes()
        {
            if (audioMixer != null) return; // Mixer handles it
            if (_currentProfileIndex < 0 || dimensionProfiles == null) return;

            DimensionAudioProfile profile = dimensionProfiles[_currentProfileIndex];
            if (profile == null) return;

            float musicVol = profile.musicVolume * _settingsMusicVolume;
            float ambienceVol = profile.ambienceVolume * _settingsAmbienceVolume;

            AudioSource activeMusic = _musicAActive ? _musicA : _musicB;
            if (activeMusic.isPlaying) activeMusic.volume = musicVol;

            AudioSource activeAmbience = _ambienceAActive ? _ambienceA : _ambienceB;
            if (activeAmbience.isPlaying) activeAmbience.volume = ambienceVol;
        }

        private static float LinearToDecibel(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            return Mathf.Log10(linear) * 20f;
        }

        #endregion

        #region Audio Source Setup

        private void CreateAudioSources()
        {
            _musicA = CreateSource("MusicA");
            _musicB = CreateSource("MusicB");
            _ambienceA = CreateSource("AmbienceA");
            _ambienceB = CreateSource("AmbienceB");
            _stingerSource = CreateSource("Stinger");
            _stingerSource.loop = false;
            _stingerSource.volume = 1f; // PlayOneShot multiplies by source volume
        }

        private AudioSource CreateSource(string name)
        {
            GameObject obj = new GameObject($"Audio_{name}");
            obj.transform.SetParent(transform);
            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 2D
            source.volume = 0f;
            return source;
        }

        #endregion
    }
}
