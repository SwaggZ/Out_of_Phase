using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace OutOfPhase.Cinematic
{
    /// <summary>
    /// Singleton that plays cinematic sequences.
    /// Locks player input, moves a dedicated cinematic camera,
    /// shows letterbox bars and dialogue-style text (typewriter).
    /// Text uses the same visual style as DialogueManager — no voice audio.
    /// </summary>
    public class CinematicManager : MonoBehaviour
    {
        public static CinematicManager Instance { get; private set; }

        [Header("Typewriter")]
        [SerializeField] private float defaultCharsPerSecond = 40f;

        [Header("Colors")]
        [SerializeField] private Color bgColor = new Color(0.02f, 0.02f, 0.06f, 0.92f);
        [SerializeField] private Color textColor = new Color(0.9f, 0.95f, 1f, 1f);
        [SerializeField] private Color speakerColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color letterboxColor = new Color(0f, 0f, 0f, 1f);

        [Header("Letterbox")]
        [SerializeField] private float letterboxHeight = 80f;
        [SerializeField] private float letterboxAnimDuration = 0.4f;

        // ── UI (auto-created) ──
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private Image _fadeImage;
        private RectTransform _topBar;
        private RectTransform _bottomBar;
        private GameObject _textPanel;
        private TextMeshProUGUI _speakerText;
        private TextMeshProUGUI _bodyText;
        private TextMeshProUGUI _continueHint;

        // ── Camera ──
        private Camera _cinematicCamera;
        private Camera _playerCamera;

        // ── State ──
        private CinematicData _currentCinematic;
        private Transform _triggerTransform;
        private Action _onComplete;
        private Coroutine _playCoroutine;
        private bool _isPlaying;
        private bool _skipRequested;
        private bool _advanceRequested;

        // ── Input ──
        private Player.PlayerInputActions _inputActions;

        /// <summary>True while a cinematic is playing.</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>Fired when any cinematic starts.</summary>
        public event Action OnCinematicStarted;

        /// <summary>Fired when any cinematic ends.</summary>
        public event Action OnCinematicEnded;

        // ═══════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═══════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _inputActions = new Player.PlayerInputActions();
            CreateCinematicCamera();
            CreateUI();
            HideAll();
        }

        private void OnEnable() => _inputActions.Player.Enable();
        private void OnDisable() => _inputActions.Player.Disable();

        private void Update()
        {
            if (!_isPlaying) return;

            bool ePressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

            if (ePressed || clicked || spacePressed)
            {
                _skipRequested = true;
                _advanceRequested = true;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  PUBLIC API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Play a cinematic. Locks player, enables cinematic camera.
        /// </summary>
        /// <param name="data">The cinematic to play.</param>
        /// <param name="onComplete">Callback when finished.</param>
        /// <param name="triggerTransform">Transform for relative positioning (optional).</param>
        public void PlayCinematic(CinematicData data, Action onComplete = null, Transform triggerTransform = null)
        {
            if (data == null || !data.IsValid)
            {
                Debug.LogWarning("[Cinematic] Invalid cinematic data.");
                onComplete?.Invoke();
                return;
            }

            if (_isPlaying)
            {
                Debug.LogWarning("[Cinematic] Already playing a cinematic.");
                return;
            }

            _currentCinematic = data;
            _triggerTransform = triggerTransform;
            _onComplete = onComplete;

            _playCoroutine = StartCoroutine(PlaySequence());
        }

        /// <summary> Force-stop the current cinematic. </summary>
        public void StopCinematic()
        {
            if (!_isPlaying) return;

            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);

            EndCinematic();
        }

        // ═══════════════════════════════════════════════════════
        //  PLAYBACK COROUTINE
        // ═══════════════════════════════════════════════════════

        private IEnumerator PlaySequence()
        {
            _isPlaying = true;
            OnCinematicStarted?.Invoke();
            LockPlayer(true);

            // Find and disable player camera
            _playerCamera = Camera.main;
            if (_playerCamera == null)
            {
                var camObj = GameObject.Find("PlayerCamera");
                if (camObj != null) _playerCamera = camObj.GetComponent<Camera>();
            }
            if (_playerCamera != null)
                _playerCamera.gameObject.SetActive(false);

            _cinematicCamera.gameObject.SetActive(true);

            // Show UI
            _canvas.gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;

            // Fade in
            yield return FadeScreen(1f, 0f, _currentCinematic.fadeInDuration);

            // Letterbox in
            if (_currentCinematic.showLetterbox)
                yield return AnimateLetterbox(true);

            // Play each shot
            for (int i = 0; i < _currentCinematic.shots.Length; i++)
            {
                yield return PlayShot(_currentCinematic.shots[i]);

                // Pause between shots
                if (_currentCinematic.shots[i].pauseAfter > 0f)
                {
                    ClearText();
                    yield return new WaitForSecondsRealtime(_currentCinematic.shots[i].pauseAfter);
                }
            }

            // Letterbox out
            if (_currentCinematic.showLetterbox)
                yield return AnimateLetterbox(false);

            // Fade out
            yield return FadeScreen(0f, 1f, _currentCinematic.fadeOutDuration);

            // Brief hold on black
            yield return new WaitForSecondsRealtime(0.1f);

            // Fade back in
            yield return FadeScreen(1f, 0f, 0.3f);

            EndCinematic();
        }

        private IEnumerator PlayShot(CinematicShot shot)
        {
            // Resolve positions
            Vector3 startPos = shot.startPosition;
            Vector3 endPos = shot.endPosition;
            Quaternion startRot = Quaternion.Euler(shot.startRotation);
            Quaternion endRot = Quaternion.Euler(shot.endRotation);

            if (shot.useRelativeToTrigger && _triggerTransform != null)
            {
                startPos = _triggerTransform.TransformPoint(shot.startPosition);
                endPos = _triggerTransform.TransformPoint(shot.endPosition);

                // Rotate rotations relative to trigger
                startRot = _triggerTransform.rotation * startRot;
                endRot = _triggerTransform.rotation * endRot;
            }

            // Set initial camera
            _cinematicCamera.transform.position = startPos;
            _cinematicCamera.transform.rotation = startRot;

            // Start typewriter if there's text
            if (shot.HasText)
            {
                ShowTextPanel(true);
                _speakerText.text = shot.speakerName ?? "";
                StartCoroutine(Typewriter(shot.text, shot.charsPerSecond > 0 ? shot.charsPerSecond : defaultCharsPerSecond));
            }
            else
            {
                ShowTextPanel(false);
            }

            // Camera movement
            _skipRequested = false;
            _advanceRequested = false;
            float elapsed = 0f;

            while (elapsed < shot.duration)
            {
                float t = elapsed / shot.duration;
                float curved = shot.movementCurve != null ? shot.movementCurve.Evaluate(t) : t;

                _cinematicCamera.transform.position = Vector3.Lerp(startPos, endPos, curved);
                _cinematicCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, curved);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Snap to final
            _cinematicCamera.transform.position = endPos;
            _cinematicCamera.transform.rotation = endRot;

            // Wait for input if required
            if (shot.waitForInput)
            {
                _continueHint.gameObject.SetActive(true);
                _continueHint.text = "[E] Continue";
                _advanceRequested = false;

                while (!_advanceRequested)
                    yield return null;

                _continueHint.gameObject.SetActive(false);
            }

            ClearText();
        }

        private IEnumerator Typewriter(string fullText, float cps)
        {
            _bodyText.text = fullText;
            _bodyText.ForceMeshUpdate();
            int total = _bodyText.textInfo.characterCount;
            _bodyText.maxVisibleCharacters = 0;

            float interval = 1f / Mathf.Max(1f, cps);
            _skipRequested = false;

            for (int i = 0; i <= total; i++)
            {
                if (_skipRequested)
                {
                    _bodyText.maxVisibleCharacters = total;
                    _skipRequested = false;
                    yield break;
                }

                _bodyText.maxVisibleCharacters = i;
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        // ═══════════════════════════════════════════════════════
        //  UI HELPERS
        // ═══════════════════════════════════════════════════════

        private IEnumerator FadeScreen(float fromAlpha, float toAlpha, float duration)
        {
            if (duration <= 0f)
            {
                _fadeImage.color = new Color(0, 0, 0, toAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float a = Mathf.Lerp(fromAlpha, toAlpha, t);
                _fadeImage.color = new Color(0, 0, 0, a);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _fadeImage.color = new Color(0, 0, 0, toAlpha);
        }

        private IEnumerator AnimateLetterbox(bool show)
        {
            float from = show ? 0f : letterboxHeight;
            float to = show ? letterboxHeight : 0f;
            float elapsed = 0f;

            while (elapsed < letterboxAnimDuration)
            {
                float t = elapsed / letterboxAnimDuration;
                float h = Mathf.Lerp(from, to, t);
                _topBar.sizeDelta = new Vector2(0f, h);
                _bottomBar.sizeDelta = new Vector2(0f, h);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _topBar.sizeDelta = new Vector2(0f, to);
            _bottomBar.sizeDelta = new Vector2(0f, to);
        }

        private void ShowTextPanel(bool show)
        {
            if (_textPanel != null)
                _textPanel.SetActive(show);
        }

        private void ClearText()
        {
            if (_bodyText != null)
            {
                _bodyText.text = "";
                _bodyText.maxVisibleCharacters = 99999;
            }
            if (_speakerText != null)
                _speakerText.text = "";
            if (_continueHint != null)
                _continueHint.gameObject.SetActive(false);
        }

        private void HideAll()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_cinematicCamera != null) _cinematicCamera.gameObject.SetActive(false);
        }

        // ═══════════════════════════════════════════════════════
        //  END
        // ═══════════════════════════════════════════════════════

        private void EndCinematic()
        {
            _isPlaying = false;

            // Restore player camera
            if (_playerCamera != null)
                _playerCamera.gameObject.SetActive(true);

            _cinematicCamera.gameObject.SetActive(false);

            ClearText();
            HideAll();
            LockPlayer(false);

            _onComplete?.Invoke();
            _onComplete = null;
            OnCinematicEnded?.Invoke();
        }

        // ═══════════════════════════════════════════════════════
        //  PLAYER LOCK
        // ═══════════════════════════════════════════════════════

        private void LockPlayer(bool locked)
        {
            var movement = FindFirstObjectByType<Player.PlayerMovement>();
            if (movement != null) movement.enabled = !locked;

            var look = FindFirstObjectByType<Player.PlayerLook>();
            if (look != null) look.enabled = !locked;

            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ═══════════════════════════════════════════════════════
        //  CAMERA CREATION
        // ═══════════════════════════════════════════════════════

        private void CreateCinematicCamera()
        {
            var camObj = new GameObject("CinematicCamera");
            camObj.transform.SetParent(transform);
            _cinematicCamera = camObj.AddComponent<Camera>();
            _cinematicCamera.clearFlags = CameraClearFlags.Skybox;
            _cinematicCamera.nearClipPlane = 0.1f;
            _cinematicCamera.farClipPlane = 1000f;
            _cinematicCamera.fieldOfView = 60f;
            _cinematicCamera.depth = 100; // render on top of player camera
            camObj.AddComponent<AudioListener>();
            camObj.SetActive(false);
        }

        // ═══════════════════════════════════════════════════════
        //  UI CREATION
        // ═══════════════════════════════════════════════════════

        private void CreateUI()
        {
            // Canvas
            var canvasObj = new GameObject("CinematicCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200; // above dialogue (100)

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            _canvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // Full-screen fade image
            var fadeObj = new GameObject("FadeOverlay");
            fadeObj.transform.SetParent(canvasObj.transform);
            _fadeImage = fadeObj.AddComponent<Image>();
            _fadeImage.color = new Color(0, 0, 0, 0);
            SetFullScreen(fadeObj.GetComponent<RectTransform>());
            _fadeImage.raycastTarget = false;

            // Top letterbox bar
            var topBarObj = new GameObject("TopBar");
            topBarObj.transform.SetParent(canvasObj.transform);
            var topImg = topBarObj.AddComponent<Image>();
            topImg.color = letterboxColor;
            topImg.raycastTarget = false;
            _topBar = topBarObj.GetComponent<RectTransform>();
            _topBar.anchorMin = new Vector2(0f, 1f);
            _topBar.anchorMax = new Vector2(1f, 1f);
            _topBar.pivot = new Vector2(0.5f, 1f);
            _topBar.anchoredPosition = Vector2.zero;
            _topBar.sizeDelta = new Vector2(0f, 0f);

            // Bottom letterbox bar
            var bottomBarObj = new GameObject("BottomBar");
            bottomBarObj.transform.SetParent(canvasObj.transform);
            var bottomImg = bottomBarObj.AddComponent<Image>();
            bottomImg.color = letterboxColor;
            bottomImg.raycastTarget = false;
            _bottomBar = bottomBarObj.GetComponent<RectTransform>();
            _bottomBar.anchorMin = new Vector2(0f, 0f);
            _bottomBar.anchorMax = new Vector2(1f, 0f);
            _bottomBar.pivot = new Vector2(0.5f, 0f);
            _bottomBar.anchoredPosition = Vector2.zero;
            _bottomBar.sizeDelta = new Vector2(0f, 0f);

            // Text panel — same style as DialogueManager
            _textPanel = CreatePanel(canvasObj.transform, "CinematicTextPanel",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 220f),
                bgColor);

            var panelRect = _textPanel.GetComponent<RectTransform>();
            panelRect.anchoredPosition = new Vector2(0f, 90f); // above bottom letterbox

            // Content with padding
            var content = CreatePanel(_textPanel.transform, "Content",
                Vector2.zero, Vector2.one,
                new Vector2(60f, 15f), new Vector2(-60f, -15f),
                Color.clear);

            // Speaker name
            var speakerObj = new GameObject("SpeakerName");
            speakerObj.transform.SetParent(content.transform);
            var speakerRect = speakerObj.AddComponent<RectTransform>();
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0f, 1f);
            speakerRect.anchoredPosition = Vector2.zero;
            speakerRect.sizeDelta = new Vector2(0f, 30f);
            _speakerText = speakerObj.AddComponent<TextMeshProUGUI>();
            _speakerText.fontSize = 20;
            _speakerText.fontStyle = FontStyles.Bold;
            _speakerText.color = speakerColor;
            _speakerText.alignment = TextAlignmentOptions.TopLeft;
            _speakerText.enableWordWrapping = false;

            // Body text
            var bodyObj = new GameObject("BodyText");
            bodyObj.transform.SetParent(content.transform);
            var bodyRect = bodyObj.AddComponent<RectTransform>();
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(0f, 30f);
            bodyRect.offsetMax = new Vector2(0f, -32f);
            _bodyText = bodyObj.AddComponent<TextMeshProUGUI>();
            _bodyText.fontSize = 18;
            _bodyText.color = textColor;
            _bodyText.alignment = TextAlignmentOptions.TopLeft;
            _bodyText.enableWordWrapping = true;

            // Continue hint
            var hintObj = new GameObject("ContinueHint");
            hintObj.transform.SetParent(content.transform);
            var hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.anchoredPosition = Vector2.zero;
            hintRect.sizeDelta = new Vector2(200f, 25f);
            _continueHint = hintObj.AddComponent<TextMeshProUGUI>();
            _continueHint.fontSize = 14;
            _continueHint.color = new Color(speakerColor.r, speakerColor.g, speakerColor.b, 0.6f);
            _continueHint.alignment = TextAlignmentOptions.BottomRight;
            _continueHint.fontStyle = FontStyles.Italic;
            _continueHint.text = "[E] Continue";
            _continueHint.gameObject.SetActive(false);

            _textPanel.SetActive(false);
        }

        private GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            if (color.a > 0f)
            {
                var img = obj.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
            }
            return obj;
        }

        private void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
