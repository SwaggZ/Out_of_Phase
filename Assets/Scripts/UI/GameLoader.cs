using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace OutOfPhase.UI
{
    /// <summary>
    /// Loading screen that async-loads a scene while preloading all dimension audio clips.
    /// Call GameLoader.Load(sceneName, audioProfiles) to start.
    /// </summary>
    public class GameLoader : MonoBehaviour
    {
        [Header("Audio Preload")]
        [Tooltip("All dimension audio profiles to preload before gameplay starts")]
        [SerializeField] private Dimension.DimensionAudioProfile[] audioProfiles;

        [Header("Colors")]
        [SerializeField] private Color bgColor = new Color(0.02f, 0.02f, 0.06f, 1f);
        [SerializeField] private Color accentColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color barBgColor = new Color(0.1f, 0.1f, 0.15f, 1f);

        // UI refs
        private Canvas _canvas;
        private Image _fillBar;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _percentText;

        /// <summary>
        /// Start loading a scene with audio preloading.
        /// </summary>
        public void StartLoading(string sceneName)
        {
            CreateUI();
            StartCoroutine(LoadSequence(sceneName));
        }

        /// <summary>
        /// Convenience: create a GameLoader, assign profiles, and begin loading.
        /// </summary>
        public static GameLoader Begin(string sceneName, Dimension.DimensionAudioProfile[] profiles,
            Color? bg = null, Color? accent = null)
        {
            GameObject obj = new GameObject("GameLoader");
            DontDestroyOnLoad(obj);
            GameLoader loader = obj.AddComponent<GameLoader>();
            loader.audioProfiles = profiles;
            if (bg.HasValue) loader.bgColor = bg.Value;
            if (accent.HasValue) loader.accentColor = accent.Value;
            loader.StartLoading(sceneName);
            return loader;
        }

        private IEnumerator LoadSequence(string sceneName)
        {
            float totalSteps = 0f;
            float completedSteps = 0f;

            // Count total work: non-streaming audio clips + scene load
            // Streaming clips don't need preloading (they read from disk on demand)
            int clipCount = 0;
            if (audioProfiles != null)
            {
                foreach (var profile in audioProfiles)
                {
                    if (profile == null) continue;
                    if (NeedsPreload(profile.musicLoop)) clipCount++;
                    if (NeedsPreload(profile.ambienceLoop)) clipCount++;
                    if (NeedsPreload(profile.transitionStinger)) clipCount++;
                }
            }
            totalSteps = clipCount + 1; // +1 for scene load

            // ---- Phase 1: Preload audio clips ----
            SetStatus("Preloading audio...");

            if (audioProfiles != null)
            {
                int dimIndex = 1;
                foreach (var profile in audioProfiles)
                {
                    if (profile == null) { dimIndex++; continue; }
                    string dimLabel = $"Dimension {dimIndex}";

                    if (NeedsPreload(profile.musicLoop))
                    {
                        SetStatus($"Loading {dimLabel} audio...");
                        yield return PreloadClip(profile.musicLoop);
                        completedSteps++;
                        SetProgress(completedSteps / totalSteps);
                    }

                    if (NeedsPreload(profile.ambienceLoop))
                    {
                        SetStatus($"Loading {dimLabel} ambience...");
                        yield return PreloadClip(profile.ambienceLoop);
                        completedSteps++;
                        SetProgress(completedSteps / totalSteps);
                    }

                    if (NeedsPreload(profile.transitionStinger))
                    {
                        SetStatus($"Loading {dimLabel} stinger...");
                        yield return PreloadClip(profile.transitionStinger);
                        completedSteps++;
                        SetProgress(completedSteps / totalSteps);
                    }

                    dimIndex++;
                }
            }

            // ---- Phase 2: Load scene async ----
            SetStatus("Loading world...");

            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(sceneName);
            sceneLoad.allowSceneActivation = false;

            while (sceneLoad.progress < 0.9f)
            {
                // Scene loading goes 0-0.9, then waits for activation
                float sceneProgress = sceneLoad.progress / 0.9f;
                SetProgress((completedSteps + sceneProgress) / totalSteps);
                yield return null;
            }

            completedSteps++;
            SetProgress(1f);
            SetStatus("Ready!");

            // Brief pause so player sees 100%
            yield return new WaitForSecondsRealtime(0.3f);

            // Activate scene
            sceneLoad.allowSceneActivation = true;

            // Wait for scene to finish activating
            while (!sceneLoad.isDone)
                yield return null;

            // Clean up loader
            Destroy(gameObject);
        }

        /// <summary>
        /// Returns true if the clip exists, is not streaming, and is not already loaded.
        /// Streaming clips read from disk on demand and never need preloading.
        /// </summary>
        private bool NeedsPreload(AudioClip clip)
        {
            if (clip == null) return false;
            if (clip.loadType == AudioClipLoadType.Streaming) return false;
            if (clip.loadState == AudioDataLoadState.Loaded) return false;
            return true;
        }

        private IEnumerator PreloadClip(AudioClip clip)
        {
            clip.LoadAudioData();

            // Wait for audio data to finish loading
            while (clip.loadState == AudioDataLoadState.Loading)
                yield return null;
        }

        #region UI

        private void SetProgress(float progress)
        {
            if (_fillBar != null)
                _fillBar.fillAmount = progress;
            if (_percentText != null)
                _percentText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }

        private void SetStatus(string text)
        {
            if (_statusText != null)
                _statusText.text = text;
        }

        private void CreateUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("LoadingCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000; // Above everything

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Background
            GameObject bg = new GameObject("BG");
            bg.transform.SetParent(canvasObj.transform, false);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = bgColor;
            SetFullScreen(bg.GetComponent<RectTransform>());

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(canvasObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.6f);
            titleRect.anchorMax = new Vector2(0.5f, 0.7f);
            titleRect.sizeDelta = new Vector2(600, 60);
            titleRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "LOADING";
            titleTmp.fontSize = 48;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = accentColor;
            titleTmp.alignment = TextAlignmentOptions.Center;

            // Progress bar background
            GameObject barBg = new GameObject("BarBG");
            barBg.transform.SetParent(canvasObj.transform, false);
            Image barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = barBgColor;
            RectTransform barBgRect = barBg.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.2f, 0.45f);
            barBgRect.anchorMax = new Vector2(0.8f, 0.48f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;

            // Progress bar fill
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(barBg.transform, false);
            _fillBar = fill.AddComponent<Image>();
            _fillBar.color = accentColor;
            _fillBar.type = Image.Type.Filled;
            _fillBar.fillMethod = Image.FillMethod.Horizontal;
            _fillBar.fillOrigin = 0;
            _fillBar.fillAmount = 0f;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            SetFullScreen(fillRect);

            // Percent text
            GameObject pctObj = new GameObject("Percent");
            pctObj.transform.SetParent(canvasObj.transform, false);
            RectTransform pctRect = pctObj.AddComponent<RectTransform>();
            pctRect.anchorMin = new Vector2(0.5f, 0.48f);
            pctRect.anchorMax = new Vector2(0.5f, 0.55f);
            pctRect.sizeDelta = new Vector2(200, 40);
            pctRect.anchoredPosition = Vector2.zero;

            _percentText = pctObj.AddComponent<TextMeshProUGUI>();
            _percentText.text = "0%";
            _percentText.fontSize = 28;
            _percentText.color = Color.white;
            _percentText.alignment = TextAlignmentOptions.Center;

            // Status text
            GameObject statusObj = new GameObject("Status");
            statusObj.transform.SetParent(canvasObj.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.35f);
            statusRect.anchorMax = new Vector2(0.5f, 0.42f);
            statusRect.sizeDelta = new Vector2(600, 40);
            statusRect.anchoredPosition = Vector2.zero;

            _statusText = statusObj.AddComponent<TextMeshProUGUI>();
            _statusText.text = "";
            _statusText.fontSize = 20;
            _statusText.fontStyle = FontStyles.Italic;
            _statusText.color = new Color(1, 1, 1, 0.5f);
            _statusText.alignment = TextAlignmentOptions.Center;
        }

        private void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
