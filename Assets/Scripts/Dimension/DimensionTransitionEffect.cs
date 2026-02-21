using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Futuristic glitchy transition effect when switching dimensions.
    /// Creates scanlines, digital noise, chromatic aberration simulation, and glitch displacement.
    /// Respects epilepsy mode: skips flashing/glitch, does a gentle color fade instead.
    /// </summary>
    public class DimensionTransitionEffect : MonoBehaviour
    {
        [Header("Effect Settings")]
        [Tooltip("Duration of the transition effect")]
        [SerializeField] private float transitionDuration = 2.5f;
        
        [Tooltip("Number of glitch frames during transition")]
        [SerializeField] private int glitchFrameCount = 8;
        
        [Tooltip("Intensity of the glitch effect (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float glitchIntensity = 0.8f;

        [Header("Colors")]
        [SerializeField] private Color scanlineColor = new Color(0, 1, 1, 0.1f); // Cyan
        [SerializeField] private Color glitchColor1 = new Color(1, 0, 0.5f, 0.3f); // Magenta
        [SerializeField] private Color glitchColor2 = new Color(0, 1, 1, 0.3f); // Cyan
        [SerializeField] private Color flashColor = new Color(1, 1, 1, 0.8f);

        [Header("UI References (Auto-created if null)")]
        [SerializeField] private Canvas effectCanvas;
        [SerializeField] private RawImage mainOverlay;
        [SerializeField] private RawImage scanlineOverlay;
        [SerializeField] private RawImage glitchOverlay;
        [SerializeField] private TextMeshProUGUI dimensionText;

        // Generated textures
        private Texture2D _noiseTexture;
        private Texture2D _scanlineTexture;
        private Texture2D _glitchTexture;
        
        // State
        private Coroutine _transitionCoroutine;

        private void Awake()
        {
            Debug.Log("[DimensionTransitionEffect] Awake() called");
            Debug.Log($"[DimensionTransitionEffect] Current parent: {(transform.parent != null ? transform.parent.name : "NONE (root level)")}, Scene: {gameObject.scene.name}");
            
            // CRITICAL: Ensure this GameObject is not a child of anything that can be destroyed
            // If it's parented, detach it FIRST before DontDestroyOnLoad
            if (transform.parent != null)
            {
                Debug.LogWarning($"[DimensionTransitionEffect] WARNING: Component is parented to {transform.parent.name}! Detaching immediately...");
                transform.SetParent(null, false);
                Debug.Log("[DimensionTransitionEffect] Detached from parent");
            }
            
            // Make the GAMEOBJECT persistent (not just this component)
            if (gameObject.scene.name != "DontDestroyOnLoad")
            {
                DontDestroyOnLoad(gameObject);
                Debug.Log("[DimensionTransitionEffect] Added GameObject to DontDestroyOnLoad");
            }
            else
            {
                Debug.Log("[DimensionTransitionEffect] GameObject already in DontDestroyOnLoad scene");
            }
            
            // Subscribe to manager ready event NOW, so it survives disable/enable cycles
            DimensionManager.OnManagerReady += OnManagerReady;
            Debug.Log("[DimensionTransitionEffect] Registered for OnManagerReady event");
            
            CreateTextures();
            
            if (effectCanvas == null)
            {
                CreateEffectUI();
            }
            
            // Hide initially
            SetOverlayActive(false);
        }

        private void OnEnable()
        {
            Debug.Log("[DimensionTransitionEffect] OnEnable() called, component enabled: " + enabled);
            
            // If manager is already ready, subscribe to dimension changes immediately
            if (DimensionManager.IsManagerReady)
            {
                Debug.Log("[DimensionTransitionEffect] DimensionManager is already ready in OnEnable");
                SubscribeToManager();
            }
            else if (DimensionManager.Instance != null)
            {
                Debug.Log("[DimensionTransitionEffect] DimensionManager.Instance is available but not marked ready yet");
                SubscribeToManager();
            }
            else
            {
                Debug.LogWarning("[DimensionTransitionEffect] DimensionManager not available in OnEnable, will wait for ready event");
            }
        }

        private void OnDisable()
        {
            Debug.Log("[DimensionTransitionEffect] OnDisable() called");
            // Note: We do NOT unregister from OnManagerReady here - that subscription persists
            
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnTransitionStart -= OnTransitionStart;
            }
        }

        private void Start()
        {
            Debug.Log("[DimensionTransitionEffect] Start() called, component active: " + gameObject.activeInHierarchy + ", enabled: " + enabled);
        }

        private void OnManagerReady()
        {
            Debug.Log("[DimensionTransitionEffect] OnManagerReady event received");
            
            // Safety check in case component was destroyed
            if (this == null || gameObject == null)
            {
                Debug.LogError("[DimensionTransitionEffect] Component/GameObject destroyed before OnManagerReady could process!");
                return;
            }
            
            SubscribeToManager();
        }

        private void SubscribeToManager()
        {
            // Another safety check
            if (this == null || gameObject == null)
            {
                Debug.LogError("[DimensionTransitionEffect] Component/GameObject destroyed during SubscribeToManager!");
                return;
            }
            
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnTransitionStart += OnTransitionStart;
                Debug.Log("[DimensionTransitionEffect] Successfully subscribed to DimensionManager.OnTransitionStart");
            }
            else
            {
                Debug.LogWarning("[DimensionTransitionEffect] Could not subscribe - DimensionManager.Instance is still null");
            }
        }

        private void OnTransitionStart(int targetDimension)
        {
            Debug.Log($"[DimensionTransitionEffect] OnTransitionStart triggered for dimension {targetDimension}");
            
            // Safety check: if this component was destroyed, we can't run the effect
            if (this == null || gameObject == null)
            {
                Debug.LogWarning("[DimensionTransitionEffect] Component is destroyed, cannot play transition effect");
                return;
            }
            
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }
            _transitionCoroutine = StartCoroutine(PlayTransitionEffect(targetDimension));
        }

        private IEnumerator PlayTransitionEffect(int targetDimension)
        {
            Debug.Log($"[DimensionTransitionEffect] PlayTransitionEffect started for dimension {targetDimension}");
            // Epilepsy mode: simple color fade, no glitch/flash
            if (UI.SettingsManager.EpilepsyMode)
            {
                yield return PlaySafeTransition(targetDimension);
                yield break;
            }

            SetOverlayActive(true);
            
            string targetName = DimensionManager.Instance.GetDimensionName(targetDimension);
            Color targetColor = DimensionManager.Instance.GetDimensionColor(targetDimension);
            
            float elapsed = 0f;
            int glitchFrame = 0;
            float glitchInterval = transitionDuration / glitchFrameCount;
            float nextGlitchTime = 0f;
            
            // Initial flash
            mainOverlay.color = flashColor;
            yield return new WaitForSecondsRealtime(0.05f);
            
            while (elapsed < transitionDuration)
            {
                float t = elapsed / transitionDuration;
                
                // Update glitch texture periodically
                if (elapsed >= nextGlitchTime)
                {
                    UpdateGlitchTexture(glitchIntensity * (1f - t * 0.5f));
                    glitchFrame++;
                    nextGlitchTime = glitchFrame * glitchInterval;
                    
                    // Random displacement of glitch overlay
                    if (glitchOverlay != null)
                    {
                        float dispX = Random.Range(-20f, 20f) * glitchIntensity;
                        float dispY = Random.Range(-5f, 5f) * glitchIntensity;
                        glitchOverlay.rectTransform.anchoredPosition = new Vector2(dispX, dispY);
                    }
                }
                
                // Fade main overlay
                float fadeAlpha = Mathf.Sin(t * Mathf.PI); // Peaks in middle
                Color overlayColor = Color.Lerp(Color.black, targetColor, t);
                overlayColor.a = fadeAlpha * 0.7f;
                mainOverlay.color = overlayColor;
                
                // Chromatic aberration simulation - offset glitch colors
                if (glitchOverlay != null)
                {
                    Color glitchCol = Color.Lerp(glitchColor1, glitchColor2, Mathf.PingPong(elapsed * 10f, 1f));
                    glitchCol.a = fadeAlpha * glitchIntensity * 0.5f;
                    glitchOverlay.color = glitchCol;
                }
                
                // Scanline intensity
                if (scanlineOverlay != null)
                {
                    Color scanCol = scanlineColor;
                    scanCol.a = 0.1f + fadeAlpha * 0.2f;
                    scanlineOverlay.color = scanCol;
                }
                
                // Update dimension text
                if (dimensionText != null)
                {
                    // Glitchy text effect
                    if (Random.value < 0.3f * glitchIntensity)
                    {
                        dimensionText.text = GenerateGlitchText(targetName);
                    }
                    else
                    {
                        dimensionText.text = $"SHIFTING TO:\n<size=48>{targetName.ToUpper()}</size>";
                    }
                    
                    Color textCol = targetColor;
                    textCol.a = fadeAlpha;
                    dimensionText.color = textCol;
                }
                
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            
            // Final flash
            mainOverlay.color = new Color(targetColor.r, targetColor.g, targetColor.b, 0.5f);
            yield return new WaitForSecondsRealtime(0.05f);
            
            // Fade out
            float fadeOutDuration = 0.15f;
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                float t = elapsed / fadeOutDuration;
                Color col = mainOverlay.color;
                col.a = Mathf.Lerp(0.5f, 0f, t);
                mainOverlay.color = col;
                
                if (glitchOverlay != null)
                {
                    Color gCol = glitchOverlay.color;
                    gCol.a = Mathf.Lerp(gCol.a, 0f, t);
                    glitchOverlay.color = gCol;
                }
                
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            
            SetOverlayActive(false);

            _transitionCoroutine = null;
        }

        private string GenerateGlitchText(string original)
        {
            char[] glitchChars = "█▓▒░╔╗╚╝║═╬╣╠╩╦".ToCharArray();
            char[] result = original.ToUpper().ToCharArray();
            
            int glitchCount = Random.Range(1, Mathf.Max(2, result.Length / 2));
            for (int i = 0; i < glitchCount; i++)
            {
                int idx = Random.Range(0, result.Length);
                result[idx] = glitchChars[Random.Range(0, glitchChars.Length)];
            }
            
            return $"SHIFTING TO:\n<size=48>{new string(result)}</size>";
        }

        private void SetOverlayActive(bool active)
        {
            Debug.Log($"[DimensionTransitionEffect] SetOverlayActive({active}), effectCanvas: {(effectCanvas != null ? "exists" : "null")}");
            if (effectCanvas != null)
            {
                effectCanvas.gameObject.SetActive(active);
                Debug.Log($"[DimensionTransitionEffect] Canvas set to active: {active}");
            }
            else
            {
                Debug.LogWarning("[DimensionTransitionEffect] effectCanvas is null!");
            }
        }

        /// <summary>
        /// Epilepsy-safe transition: gentle fade to dimension color and back, no flashing.
        /// </summary>
        private IEnumerator PlaySafeTransition(int targetDimension)
        {
            SetOverlayActive(true);

            Color targetColor = DimensionManager.Instance.GetDimensionColor(targetDimension);
            string targetName = DimensionManager.Instance.GetDimensionName(targetDimension);

            // Hide scanlines and glitch
            if (scanlineOverlay != null) scanlineOverlay.color = Color.clear;
            if (glitchOverlay != null) glitchOverlay.color = Color.clear;

            // Show dimension name (static text, no corruption)
            if (dimensionText != null)
            {
                dimensionText.text = $"SHIFTING TO:\n<size=48>{targetName.ToUpper()}</size>";
                dimensionText.color = targetColor;
            }

            // Fade in
            float half = transitionDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < half)
            {
                float t = elapsed / half;
                Color c = targetColor;
                c.a = Mathf.Lerp(0f, 0.5f, t);
                mainOverlay.color = c;
                if (dimensionText != null)
                {
                    Color tc = dimensionText.color;
                    tc.a = t;
                    dimensionText.color = tc;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Fade out
            elapsed = 0f;
            while (elapsed < half)
            {
                float t = elapsed / half;
                Color c = targetColor;
                c.a = Mathf.Lerp(0.5f, 0f, t);
                mainOverlay.color = c;
                if (dimensionText != null)
                {
                    Color tc = dimensionText.color;
                    tc.a = 1f - t;
                    dimensionText.color = tc;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetOverlayActive(false);
            _transitionCoroutine = null;
        }

        #region Texture Generation

        private void CreateTextures()
        {
            // Noise texture
            _noiseTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            _noiseTexture.filterMode = FilterMode.Point;
            UpdateNoiseTexture();
            
            // Scanline texture
            _scanlineTexture = new Texture2D(1, 4, TextureFormat.RGBA32, false);
            _scanlineTexture.filterMode = FilterMode.Point;
            _scanlineTexture.wrapMode = TextureWrapMode.Repeat;
            Color[] scanPixels = new Color[]
            {
                new Color(0, 0, 0, 0.3f),
                new Color(0, 0, 0, 0f),
                new Color(0, 0, 0, 0f),
                new Color(0, 0, 0, 0f)
            };
            _scanlineTexture.SetPixels(scanPixels);
            _scanlineTexture.Apply();
            
            // Glitch texture
            _glitchTexture = new Texture2D(64, 256, TextureFormat.RGBA32, false);
            _glitchTexture.filterMode = FilterMode.Point;
            UpdateGlitchTexture(1f);
        }

        private void UpdateNoiseTexture()
        {
            Color[] pixels = new Color[256 * 256];
            for (int i = 0; i < pixels.Length; i++)
            {
                float v = Random.value;
                pixels[i] = new Color(v, v, v, Random.value * 0.5f);
            }
            _noiseTexture.SetPixels(pixels);
            _noiseTexture.Apply();
        }

        private void UpdateGlitchTexture(float intensity)
        {
            Color[] pixels = new Color[64 * 256];
            
            // Create horizontal glitch bars
            int y = 0;
            while (y < 256)
            {
                bool isGlitch = Random.value < 0.15f * intensity;
                int barHeight = isGlitch ? Random.Range(1, 8) : Random.Range(5, 30);
                
                Color barColor = isGlitch 
                    ? new Color(Random.value, Random.value, Random.value, Random.Range(0.2f, 0.6f) * intensity)
                    : new Color(0, 0, 0, 0);
                
                float offset = isGlitch ? Random.Range(-0.3f, 0.3f) : 0f;
                
                for (int by = 0; by < barHeight && y + by < 256; by++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        int idx = (y + by) * 64 + x;
                        pixels[idx] = barColor;
                    }
                }
                
                y += barHeight;
            }
            
            _glitchTexture.SetPixels(pixels);
            _glitchTexture.Apply();
        }

        #endregion

        #region UI Creation

        private void CreateEffectUI()
        {
            Debug.Log("[DimensionTransitionEffect] CreateEffectUI() called");
            
            // Create a top-level canvas GameObject (not as child of this component)
            GameObject canvasObj = new GameObject("DimensionTransitionEffectCanvas");
            effectCanvas = canvasObj.AddComponent<Canvas>();
            effectCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            effectCanvas.sortingOrder = 999; // On top of everything
            
            Debug.Log($"[DimensionTransitionEffect] Created canvas with sortingOrder 999");
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            // Main overlay (color fade)
            GameObject mainObj = new GameObject("MainOverlay");
            mainObj.transform.SetParent(canvasObj.transform);
            mainOverlay = mainObj.AddComponent<RawImage>();
            mainOverlay.color = Color.clear;
            SetFullScreen(mainOverlay.rectTransform);
            
            // Scanline overlay
            GameObject scanObj = new GameObject("ScanlineOverlay");
            scanObj.transform.SetParent(canvasObj.transform);
            scanlineOverlay = scanObj.AddComponent<RawImage>();
            scanlineOverlay.texture = _scanlineTexture;
            scanlineOverlay.uvRect = new Rect(0, 0, 1, 270); // Tile vertically
            scanlineOverlay.color = scanlineColor;
            SetFullScreen(scanlineOverlay.rectTransform);
            
            // Glitch overlay
            GameObject glitchObj = new GameObject("GlitchOverlay");
            glitchObj.transform.SetParent(canvasObj.transform);
            glitchOverlay = glitchObj.AddComponent<RawImage>();
            glitchOverlay.texture = _glitchTexture;
            glitchOverlay.uvRect = new Rect(0, 0, 30, 1); // Tile horizontally
            glitchOverlay.color = glitchColor1;
            SetFullScreen(glitchOverlay.rectTransform);
            
            // Dimension text
            GameObject textObj = new GameObject("DimensionText");
            textObj.transform.SetParent(canvasObj.transform);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(600, 200);
            
            dimensionText = textObj.AddComponent<TextMeshProUGUI>();
            dimensionText.alignment = TextAlignmentOptions.Center;
            dimensionText.fontSize = 24;
            dimensionText.fontStyle = FontStyles.Bold;
            dimensionText.color = Color.cyan;
            
            Debug.Log("[DimensionTransitionEffect] CreateEffectUI() completed successfully");
        }

        private void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion

        private void OnDestroy()
        {
            if (_noiseTexture != null) Destroy(_noiseTexture);
            if (_scanlineTexture != null) Destroy(_scanlineTexture);
            if (_glitchTexture != null) Destroy(_glitchTexture);
        }
    }
}
