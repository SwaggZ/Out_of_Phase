using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Simple UI for displaying interaction prompts.
    /// Auto-creates UI elements if not assigned.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Interactor interactor;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float fadeSpeed = 10f;
        [SerializeField] private Vector2 offset = new Vector2(0, -80f);
        [SerializeField] private int fontSize = 24;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.6f);

        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateUI = true;

        private float _targetAlpha;
        private Image _backgroundImage;

        private void Awake()
        {
            if (interactor == null)
                interactor = FindFirstObjectByType<Interactor>();

            if (autoCreateUI && promptText == null)
            {
                CreateUI();
            }
            
            if (canvasGroup == null && promptText != null)
                canvasGroup = promptText.GetComponentInParent<CanvasGroup>();
        }

        private void CreateUI()
        {
            // Find or create canvas
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("UI Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create prompt container
            GameObject promptContainer = new GameObject("InteractionPrompt");
            promptContainer.transform.SetParent(canvas.transform, false);
            
            RectTransform containerRect = promptContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = offset;
            containerRect.sizeDelta = new Vector2(400, 60);

            canvasGroup = promptContainer.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;

            // Background
            _backgroundImage = promptContainer.AddComponent<Image>();
            _backgroundImage.color = backgroundColor;

            // Add padding via content size fitter
            var layoutGroup = promptContainer.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.padding = new RectOffset(20, 20, 10, 10);
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;

            var fitter = promptContainer.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(promptContainer.transform, false);

            promptText = textObj.AddComponent<TextMeshProUGUI>();
            promptText.fontSize = fontSize;
            promptText.color = textColor;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.text = "";

            RectTransform textRect = promptText.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(360, 40);
        }

        private void Update()
        {
            if (interactor == null || promptText == null) return;

            string prompt = interactor.GetFullPrompt();
            
            if (string.IsNullOrEmpty(prompt))
            {
                _targetAlpha = 0f;
            }
            else
            {
                promptText.text = prompt;
                _targetAlpha = 1f;
            }

            // Fade
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);
            }
        }
    }
}
