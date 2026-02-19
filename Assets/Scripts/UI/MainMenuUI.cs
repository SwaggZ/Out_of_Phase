using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace OutOfPhase.UI
{
    /// <summary>
    /// Main menu with New Game, Settings, and Quit.
    /// Place on a GameObject in a dedicated "MainMenu" scene.
    /// Auto-creates all UI elements.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("Scene name or build index to load for New Game")]
        [SerializeField] private string gameSceneName = "SampleScene";

        [Header("Title")]
        [SerializeField] private string gameTitle = "OUT OF PHASE";
        [SerializeField] private string gameSubtitle = "Strange Places";

        [Header("Audio Preload")]
        [Tooltip("Dimension audio profiles to preload during loading screen")]
        [SerializeField] private Dimension.DimensionAudioProfile[] audioProfilesToPreload;

        [Header("Colors")]
        [SerializeField] private Color bgColor = new Color(0.02f, 0.02f, 0.06f, 1f);
        [SerializeField] private Color accentColor = new Color(0f, 0.9f, 1f, 1f); // Cyan
        [SerializeField] private Color buttonColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        [SerializeField] private Color buttonHoverColor = new Color(0.15f, 0.15f, 0.25f, 1f);
        [SerializeField] private Color textColor = Color.white;

        private Canvas _canvas;
        private GameObject _mainPanel;
        private GameObject _settingsPanel;
        private SettingsUI _settingsUI;

        private void Start()
        {
            // Ensure mouse is visible and game is running
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            // Ensure SettingsManager exists
            if (SettingsManager.Instance == null)
            {
                GameObject go = new GameObject("SettingsManager");
                go.AddComponent<SettingsManager>();
            }

            // Ensure EventSystem exists (required for button clicks)
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<InputSystemUIInputModule>();
            }

            CreateUI();
        }

        private void CreateUI()
        {
            // --- Canvas ---
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // --- Main Panel ---
            _mainPanel = CreatePanel(canvasObj.transform, "MainPanel");
            Image mainBg = _mainPanel.GetComponent<Image>();
            mainBg.color = Color.clear;
            SetFullScreen(_mainPanel.GetComponent<RectTransform>());

            // --- Full screen background (lowest sibling so it renders behind everything) ---
            GameObject bgObj = CreatePanel(canvasObj.transform, "Background");
            Image bgImage = bgObj.GetComponent<Image>();
            bgImage.color = bgColor;
            SetFullScreen(bgObj.GetComponent<RectTransform>());
            bgObj.transform.SetAsFirstSibling();

            // Title
            CreateTitle(_mainPanel.transform);

            // Button container
            GameObject buttonContainer = new GameObject("Buttons");
            buttonContainer.transform.SetParent(_mainPanel.transform, false);
            RectTransform btnContRect = buttonContainer.AddComponent<RectTransform>();
            btnContRect.anchorMin = new Vector2(0.5f, 0.35f);
            btnContRect.anchorMax = new Vector2(0.5f, 0.55f);
            btnContRect.sizeDelta = new Vector2(320, 400);
            btnContRect.anchoredPosition = Vector2.zero;

            var layout = buttonContainer.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 80;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // Buttons
            CreateMenuButton(buttonContainer.transform, "NEW GAME", OnNewGame);
            CreateMenuButton(buttonContainer.transform, "SETTINGS", OnSettings);
            CreateMenuButton(buttonContainer.transform, "QUIT", OnQuit);

            // --- Settings Panel (hidden) ---
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(canvasObj.transform, false);
            SetFullScreen(_settingsPanel.AddComponent<RectTransform>());

            _settingsUI = _settingsPanel.AddComponent<SettingsUI>();
            _settingsUI.Initialize(accentColor, buttonColor, buttonHoverColor, textColor, bgColor, OnSettingsBack);
            _settingsPanel.SetActive(false);

            // Version text
            CreateVersionText(canvasObj.transform);
        }

        #region UI Creation Helpers

        private void CreateTitle(Transform parent)
        {
            // Main title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.7f);
            titleRect.anchorMax = new Vector2(0.5f, 0.85f);
            titleRect.sizeDelta = new Vector2(800, 120);
            titleRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = gameTitle;
            titleText.fontSize = 72;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = accentColor;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;

            // Subtitle
            GameObject subObj = new GameObject("Subtitle");
            subObj.transform.SetParent(parent, false);
            RectTransform subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.63f);
            subRect.anchorMax = new Vector2(0.5f, 0.7f);
            subRect.sizeDelta = new Vector2(600, 40);
            subRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI subText = subObj.AddComponent<TextMeshProUGUI>();
            subText.text = gameSubtitle;
            subText.fontSize = 24;
            subText.fontStyle = FontStyles.Italic;
            subText.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f);
            subText.alignment = TextAlignmentOptions.Center;
        }

        private void CreateMenuButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(label + "Button");
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(300, 50);

            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 300;
            le.preferredHeight = 50;

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = buttonColor;
            btnImage.raycastTarget = true;

            Button btn = btnObj.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = buttonHoverColor;
            colors.pressedColor = accentColor;
            colors.selectedColor = buttonHoverColor;
            btn.colors = colors;
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 28;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = textColor;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.raycastTarget = false;
        }

        private void CreateVersionText(Transform parent)
        {
            GameObject obj = new GameObject("Version");
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-20, 10);
            rect.sizeDelta = new Vector2(300, 30);

            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            text.text = $"v{Application.version}";
            text.fontSize = 16;
            text.color = new Color(1, 1, 1, 0.3f);
            text.alignment = TextAlignmentOptions.BottomRight;
        }

        private GameObject CreatePanel(Transform parent, string name)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            obj.AddComponent<Image>();
            return obj;
        }

        private void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion

        #region Button Handlers

        private void OnNewGame()
        {
            Time.timeScale = 1f;
            GameLoader.Begin(gameSceneName, audioProfilesToPreload, bgColor, accentColor);
        }

        private void OnSettings()
        {
            _mainPanel.SetActive(false);
            _settingsPanel.SetActive(true);
        }

        private void OnSettingsBack()
        {
            _settingsPanel.SetActive(false);
            _mainPanel.SetActive(true);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion
    }
}
