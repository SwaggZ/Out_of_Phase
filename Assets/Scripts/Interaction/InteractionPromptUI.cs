using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Displays [E] interaction prompts for IInteractable objects.
    /// Always creates its own dedicated canvas to avoid CanvasGroup conflicts.
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
        private bool _firstUpdateLogged = false;

        private void Awake()
        {
            Debug.Log("[InteractionPromptUI] Awake() called");
            Debug.Log($"[InteractionPromptUI] Current parent: {(transform.parent != null ? transform.parent.name : "NONE (root level)")}, Scene: {gameObject.scene.name}");
            
            // CRITICAL: Ensure this GameObject is not a child of anything that can be destroyed
            // If it's parented, detach it FIRST before DontDestroyOnLoad
            if (transform.parent != null)
            {
                Debug.LogWarning($"[InteractionPromptUI] WARNING: Component is parented to {transform.parent.name}! Detaching immediately...");
                transform.SetParent(null, false);
                Debug.Log("[InteractionPromptUI] Detached from parent");
            }
            
            // Make the GAMEOBJECT persistent (not just this component)
            if (gameObject.scene.name != "DontDestroyOnLoad")
            {
                DontDestroyOnLoad(gameObject);
                Debug.Log("[InteractionPromptUI] Added GameObject to DontDestroyOnLoad");
            }
            else
            {
                Debug.Log("[InteractionPromptUI] GameObject already in DontDestroyOnLoad scene");
            }
            
            if (interactor == null)
                interactor = FindFirstObjectByType<Interactor>();

            if (autoCreateUI && promptText == null)
                CreateUI();

            if (canvasGroup == null && promptText != null)
                canvasGroup = promptText.GetComponentInParent<CanvasGroup>();
        }

        private void CreateUI()
        {
            Debug.Log("[InteractionPromptUI] CreateUI() called");
            
            // Always create a DEDICATED canvas so other CanvasGroups
            // (e.g. DialogueManager, DimensionTransitionEffect) cannot hide us.
            GameObject canvasObj = new GameObject("InteractionPromptCanvas");
            // DO NOT parent to transform - keep canvas independent
            
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // above game UI, below dialogue (100)
            canvasObj.AddComponent<GraphicRaycaster>();
            
            Debug.Log("[InteractionPromptUI] Created independent canvas");

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Prompt container
            GameObject promptContainer = new GameObject("InteractionPrompt");
            promptContainer.transform.SetParent(canvasObj.transform, false);

            RectTransform containerRect = promptContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = offset;
            containerRect.sizeDelta = new Vector2(400, 60);

            canvasGroup = promptContainer.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;

            _backgroundImage = promptContainer.AddComponent<Image>();
            _backgroundImage.color = backgroundColor;

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
            
            Debug.Log("[InteractionPromptUI] CreateUI() completed successfully");
        }

        private void Update()
        {
            if (!_firstUpdateLogged)
            {
                Debug.Log($"[InteractionPromptUI] First Update: interactor={interactor}, promptText={promptText}, canvasGroup={canvasGroup}");
                _firstUpdateLogged = true;
            }
            
            // Safety check - component might have been destroyed
            if (this == null || gameObject == null)
            {
                Debug.LogWarning("[InteractionPromptUI] Component/GameObject destroyed!");
                return;
            }
            
            // Retry finding Interactor every frame until found
            if (interactor == null)
                interactor = FindFirstObjectByType<Interactor>();

            if (interactor == null || promptText == null) return;

            // Hide prompt while dialogue is playing
            if (Dialogue.DialogueManager.Instance != null && Dialogue.DialogueManager.Instance.IsDialogueActive)
            {
                _targetAlpha = 0f;
            }
            else
            {
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
            }

            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);
        }
    }
}
