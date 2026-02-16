using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace OutOfPhase.UI
{
    /// <summary>
    /// In-game pause menu (Escape key). Pauses game, shows Resume/Settings/Main Menu/Quit.
    /// Attach to the Player alongside other player scripts.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string mainMenuScene = "MainMenu";

        [Header("Colors")]
        [SerializeField] private Color bgColor = new Color(0.02f, 0.02f, 0.06f, 0.85f);
        [SerializeField] private Color accentColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color buttonColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        [SerializeField] private Color buttonHoverColor = new Color(0.15f, 0.15f, 0.25f, 1f);
        [SerializeField] private Color textColor = Color.white;

        private Canvas _canvas;
        private GameObject _root;
        private GameObject _mainPanel;
        private GameObject _settingsPanel;
        private SettingsUI _settingsUI;

        private bool _isPaused;
        private Player.PlayerInputActions _inputActions;
        private Player.PlayerLook _playerLook;

        private void Awake()
        {
            _inputActions = new Player.PlayerInputActions();
            _playerLook = GetComponent<Player.PlayerLook>();

            // Ensure SettingsManager exists early so other systems can subscribe
            if (SettingsManager.Instance == null)
            {
                GameObject go = new GameObject("SettingsManager");
                go.AddComponent<SettingsManager>();
            }
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Player.Disable();
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_isPaused)
                    Resume();
                else
                    Pause();
            }
        }

        private void Pause()
        {
            _isPaused = true;
            Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_playerLook != null)
                _playerLook.SetLookEnabled(false);

            if (_root == null)
                CreateUI();

            _root.SetActive(true);
            _mainPanel.SetActive(true);
            _settingsPanel.SetActive(false);
        }

        private void Resume()
        {
            _isPaused = false;
            Time.timeScale = 1f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_playerLook != null)
                _playerLook.SetLookEnabled(true);

            if (_root != null)
                _root.SetActive(false);
        }

        private void CreateUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("PauseMenuCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 900;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            _root = canvasObj;

            // Dim background
            GameObject bg = new GameObject("DimBG");
            bg.transform.SetParent(canvasObj.transform, false);
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = bgColor;
            SetFullScreen(bg.GetComponent<RectTransform>());

            // --- Main Panel ---
            _mainPanel = new GameObject("MainPanel");
            _mainPanel.transform.SetParent(canvasObj.transform, false);
            SetFullScreen(_mainPanel.AddComponent<RectTransform>());

            // Title
            GameObject titleObj = new GameObject("PauseTitle");
            titleObj.transform.SetParent(_mainPanel.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.7f);
            titleRect.anchorMax = new Vector2(0.5f, 0.8f);
            titleRect.sizeDelta = new Vector2(400, 60);
            titleRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "PAUSED";
            titleText.fontSize = 52;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = accentColor;
            titleText.alignment = TextAlignmentOptions.Center;

            // Buttons
            GameObject btnContainer = new GameObject("Buttons");
            btnContainer.transform.SetParent(_mainPanel.transform, false);
            RectTransform btnRect = btnContainer.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.3f);
            btnRect.anchorMax = new Vector2(0.5f, 0.6f);
            btnRect.sizeDelta = new Vector2(300, 500);
            btnRect.anchoredPosition = Vector2.zero;

            var layout = btnContainer.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 80;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            CreateButton(btnContainer.transform, "RESUME", Resume);
            CreateButton(btnContainer.transform, "SETTINGS", () =>
            {
                _mainPanel.SetActive(false);
                _settingsPanel.SetActive(true);
            });
            CreateButton(btnContainer.transform, "MAIN MENU", () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(mainMenuScene);
            });
            CreateButton(btnContainer.transform, "QUIT", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });

            // --- Settings Panel ---
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(canvasObj.transform, false);
            SetFullScreen(_settingsPanel.AddComponent<RectTransform>());

            _settingsUI = _settingsPanel.AddComponent<SettingsUI>();
            _settingsUI.Initialize(accentColor, buttonColor, buttonHoverColor, textColor, bgColor, () =>
            {
                _settingsPanel.SetActive(false);
                _mainPanel.SetActive(true);
            });
            _settingsPanel.SetActive(false);
        }

        private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(label + "Button");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280, 50);

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = buttonColor;

            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = buttonHoverColor;
            colors.pressedColor = accentColor;
            colors.selectedColor = buttonHoverColor;
            btn.colors = colors;
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            SetFullScreen(labelRect);

            TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 26;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        private void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public bool IsPaused => _isPaused;
    }
}
