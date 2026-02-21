using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.InputSystem;

namespace OutOfPhase.Progression
{
    /// <summary>
    /// Failsafe reload checkpoint system.
    /// Hold R to load the last checkpoint - displays a loading circle while held.
    /// Works everywhere: pause menu, dialogue, etc.
    /// </summary>
    public class CheckpointReloadHandler : MonoBehaviour
    {
        [Header("Reload Settings")]
        [SerializeField] private float reloadDuration = 2f; // Time to hold R
        [SerializeField] private Key reloadKey = Key.R;
        
        [Header("UI Settings")]
        [SerializeField] private float circleSize = 200f;
        [SerializeField] private Color circleColor = new Color(0f, 0.85f, 1f, 0.8f); // Cyan
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);

        private Keyboard _keyboard;
        private Canvas _canvas;
        private Image _loadingCircle;
        private Image _backgroundCircle;
        private TextMeshProUGUI _reloadText;
        private float _holdDuration = 0f;
        private bool _isActive = false;

        private void Awake()
        {
            Debug.Log("[CheckpointReloadHandler] Awake() called");
            
            // Make persistent
            if (gameObject.scene.name != "DontDestroyOnLoad")
            {
                if (transform.parent != null)
                {
                    transform.SetParent(null, false);
                    Debug.Log("[CheckpointReloadHandler] Detached from parent");
                }
                DontDestroyOnLoad(gameObject);
                Debug.Log("[CheckpointReloadHandler] Added to DontDestroyOnLoad");
            }
            
            _keyboard = Keyboard.current;
            CreateUI();
        }

        private void CreateUI()
        {
            Debug.Log("[CheckpointReloadHandler] CreateUI() called");
            
            // Create canvas
            GameObject canvasObj = new GameObject("CheckpointReloadCanvas");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200; // On top of everything
            canvasObj.AddComponent<GraphicRaycaster>();
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            // Background circle (gray)
            GameObject bgObj = new GameObject("BackgroundCircle");
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(circleSize, circleSize);
            
            _backgroundCircle = bgObj.AddComponent<Image>();
            _backgroundCircle.sprite = Resources.Load<Sprite>("UI/Circle"); // Fallback: will use built-in circle
            _backgroundCircle.color = backgroundColor;
            
            // Loading circle (cyan, radial fill)
            GameObject circleObj = new GameObject("LoadingCircle");
            circleObj.transform.SetParent(canvasObj.transform, false);
            RectTransform circleRect = circleObj.AddComponent<RectTransform>();
            circleRect.anchorMin = new Vector2(0.5f, 0.5f);
            circleRect.anchorMax = new Vector2(0.5f, 0.5f);
            circleRect.sizeDelta = new Vector2(circleSize, circleSize);
            
            _loadingCircle = circleObj.AddComponent<Image>();
            _loadingCircle.sprite = Resources.Load<Sprite>("UI/Circle");
            _loadingCircle.type = Image.Type.Filled;
            _loadingCircle.fillMethod = Image.FillMethod.Radial360;
            _loadingCircle.fillAmount = 0f;
            _loadingCircle.color = circleColor;
            
            // Text display
            GameObject textObj = new GameObject("ReloadText");
            textObj.transform.SetParent(canvasObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0, -circleSize / 2 - 40);
            textRect.sizeDelta = new Vector2(400, 60);
            
            _reloadText = textObj.AddComponent<TextMeshProUGUI>();
            _reloadText.text = "Hold R to Reload Checkpoint";
            _reloadText.alignment = TextAlignmentOptions.Center;
            _reloadText.fontSize = 20;
            _reloadText.color = circleColor;
            
            // Hide initially
            _canvas.gameObject.SetActive(false);
            
            Debug.Log("[CheckpointReloadHandler] CreateUI() completed");
        }

        private void Update()
        {
            if (_keyboard == null)
                return;

            // Check if canvas was destroyed and recreate if needed
            if (_canvas == null && _loadingCircle == null && _reloadText == null)
            {
                Debug.LogWarning("[CheckpointReloadHandler] UI was destroyed! Recreating...");
                CreateUI();
            }

            // If UI still doesn't exist, skip
            if (_canvas == null || _loadingCircle == null || _reloadText == null)
                return;
            
            // Check if R is pressed
            bool isHoldingR = _keyboard[reloadKey].isPressed;
            
            if (isHoldingR)
            {
                _holdDuration += Time.deltaTime;
                
                // Show UI
                if (!_isActive)
                {
                    _canvas.gameObject.SetActive(true);
                    _isActive = true;
                    Debug.Log("[CheckpointReloadHandler] Reload UI activated");
                }
                
                // Update loading circle
                float progress = Mathf.Clamp01(_holdDuration / reloadDuration);
                _loadingCircle.fillAmount = progress;
                
                // Update text
                float remainingTime = Mathf.Max(0, reloadDuration - _holdDuration);
                _reloadText.text = $"Hold R to Reload\n{remainingTime:F1}s";
                
                // Check if reload threshold reached
                if (_holdDuration >= reloadDuration)
                {
                    Debug.Log("[CheckpointReloadHandler] Reload checkpoint triggered!");
                    ExecuteReload();
                }
            }
            else
            {
                // R released
                if (_isActive)
                {
                    _canvas.gameObject.SetActive(false);
                    _isActive = false;
                    Debug.Log("[CheckpointReloadHandler] Reload UI deactivated");
                }
                
                _holdDuration = 0f;
            }
        }

        private void ExecuteReload()
        {
            Debug.Log("[CheckpointReloadHandler] ExecuteReload triggered!");
            
            // Ensure time is running
            Time.timeScale = 1f;
            
            // Close any active dialogue
            if (Dialogue.DialogueManager.Instance != null)
            {
                Debug.Log("[CheckpointReloadHandler] Closing active dialogue...");
                Dialogue.DialogueManager.Instance.EndDialogue();
            }
            
            // Reload scene while preserving checkpoint
            if (CheckpointManager.Instance != null && CheckpointManager.Instance.HasCheckpoint)
            {
                Debug.Log("[CheckpointReloadHandler] Reloading scene with checkpoint...");
                CheckpointManager.Instance.PrepareLoadOnSceneReady();
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            else
            {
                Debug.LogError("[CheckpointReloadHandler] CheckpointManager.Instance is null or no checkpoint saved!");
            }
            
            // Reset state
            _holdDuration = 0f;
            _canvas.gameObject.SetActive(false);
            _isActive = false;
        }

        private void OnGUI()
        {
            // Visual debug info
            if (_isActive)
            {
                GUI.Label(new Rect(10, 10, 300, 30), $"[Checkpoint Reload] {_holdDuration:F1}s / {reloadDuration:F1}s");
            }
        }
    }
}
