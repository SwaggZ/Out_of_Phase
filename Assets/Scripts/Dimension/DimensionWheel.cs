using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using OutOfPhase.Player;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// GTA5-style radial wheel UI for selecting dimensions.
    /// Hold Tab to open, move mouse to select, release to confirm.
    /// </summary>
    public class DimensionWheel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform wheelContainer;
        [SerializeField] private RectTransform centerIndicator;
        [SerializeField] private TextMeshProUGUI dimensionNameText;
        [SerializeField] private Image backgroundImage;

        [Header("Wheel Settings")]
        [Tooltip("Radius of the wheel")]
        [SerializeField] private float wheelRadius = 200f;
        
        [Tooltip("Size of each segment icon")]
        [SerializeField] private float segmentSize = 80f;
        
        [Tooltip("Dead zone in center before selection starts")]
        [SerializeField] private float deadZoneRadius = 30f;
        
        [Tooltip("Time scale while wheel is open (0 = pause)")]
        [SerializeField] private float openTimeScale = 0.1f;

        [Header("Visuals")]
        [Tooltip("Background color/alpha")]
        [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);
        
        [Tooltip("Selected segment highlight color")]
        [SerializeField] private Color selectedColor = new Color(1, 1, 1, 1f);
        
        [Tooltip("Unselected segment color")]
        [SerializeField] private Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        
        [Tooltip("Locked dimension color")]
        [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        [Header("Auto Create")]
        [SerializeField] private bool autoCreateUI = true;

        // Input
        private PlayerInputActions _inputActions;
        
        // State
        private bool _isOpen;
        private int _hoveredSegment = -1;
        private int _previousSelection = -1;
        private float _previousTimeScale;
        private bool _cursorWasLocked;
        
        // Segment UI elements
        private RectTransform[] _segmentTransforms;
        private Image[] _segmentImages;
        private TextMeshProUGUI[] _segmentLabels;
        
        // Cached references
        private PlayerLook _playerLook;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            _inputActions = new PlayerInputActions();
            _playerLook = GetComponent<PlayerLook>();
            if (_playerLook == null)
                _playerLook = GetComponentInParent<PlayerLook>();
            
            if (autoCreateUI && canvas == null)
            {
                CreateWheelUI();
            }
            
            // Start hidden
            if (wheelContainer != null)
                wheelContainer.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            _inputActions.Player.DimensionWheel.started += _ => OpenWheel();
            _inputActions.Player.DimensionWheel.canceled += _ => CloseWheel();
        }

        private void OnDisable()
        {
            _inputActions.Player.DimensionWheel.started -= _ => OpenWheel();
            _inputActions.Player.DimensionWheel.canceled -= _ => CloseWheel();
            _inputActions.Player.Disable();
            
            // Ensure we restore state if disabled while open
            if (_isOpen)
            {
                RestoreGameState();
            }
        }

        private void Update()
        {
            if (!_isOpen) return;
            
            UpdateSelection();
        }

        private void OpenWheel()
        {
            if (DimensionManager.Instance == null) return;
            if (DimensionManager.Instance.IsSwitchingLocked) return;
            
            _isOpen = true;
            
            // Store current state
            _previousTimeScale = Time.timeScale;
            _cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            _previousSelection = DimensionManager.Instance.CurrentDimension;
            
            // Slow time
            Time.timeScale = openTimeScale;
            
            // Disable camera look
            if (_playerLook != null)
                _playerLook.SetLookEnabled(false);
            
            // Show cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Show UI
            if (wheelContainer != null)
            {
                wheelContainer.gameObject.SetActive(true);
                UpdateSegmentVisuals();
            }
            
            // Set hovered to current dimension initially
            _hoveredSegment = _previousSelection;
            UpdateDimensionNameDisplay();
        }

        private void CloseWheel()
        {
            if (!_isOpen) return;
            
            _isOpen = false;
            
            // Apply selection
            if (_hoveredSegment >= 0 && _hoveredSegment != _previousSelection)
            {
                DimensionManager.Instance.SwitchToDimension(_hoveredSegment);
            }
            
            RestoreGameState();
            
            // Hide UI
            if (wheelContainer != null)
                wheelContainer.gameObject.SetActive(false);
        }

        private void RestoreGameState()
        {
            // Restore time
            Time.timeScale = _previousTimeScale;
            
            // Re-enable camera look
            if (_playerLook != null)
                _playerLook.SetLookEnabled(true);
            
            // Restore cursor
            if (_cursorWasLocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void UpdateSelection()
        {
            if (DimensionManager.Instance == null) return;
            if (Mouse.current == null) return;
            
            // Get mouse position relative to wheel center
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector2 wheelCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 offset = mousePos - wheelCenter;
            
            float distance = offset.magnitude;
            
            // Dead zone check
            if (distance < deadZoneRadius)
            {
                // In dead zone - keep previous selection or current dimension
                if (_hoveredSegment < 0)
                    _hoveredSegment = DimensionManager.Instance.CurrentDimension;
                return;
            }
            
            // Calculate which segment based on angle
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            angle = (angle + 360f) % 360f; // Normalize to 0-360
            
            // Rotate so 0 is at the top
            angle = (angle + 90f) % 360f;
            
            int segmentCount = DimensionManager.Instance.DimensionCount;
            float segmentAngle = 360f / segmentCount;
            
            // Offset by half segment so segments are centered
            angle = (angle + segmentAngle / 2f) % 360f;
            
            int newHovered = Mathf.FloorToInt(angle / segmentAngle);
            newHovered = Mathf.Clamp(newHovered, 0, segmentCount - 1);
            
            if (newHovered != _hoveredSegment)
            {
                _hoveredSegment = newHovered;
                UpdateSegmentVisuals();
                UpdateDimensionNameDisplay();
            }
        }

        private void UpdateSegmentVisuals()
        {
            if (_segmentImages == null || DimensionManager.Instance == null) return;
            
            int currentDim = DimensionManager.Instance.CurrentDimension;
            
            for (int i = 0; i < _segmentImages.Length; i++)
            {
                if (_segmentImages[i] == null) continue;
                
                Color dimColor = DimensionManager.Instance.GetDimensionColor(i);
                
                if (i == _hoveredSegment)
                {
                    // Highlighted
                    _segmentImages[i].color = dimColor;
                    _segmentTransforms[i].localScale = Vector3.one * 1.2f;
                }
                else if (i == currentDim)
                {
                    // Current dimension
                    _segmentImages[i].color = Color.Lerp(dimColor, selectedColor, 0.3f);
                    _segmentTransforms[i].localScale = Vector3.one;
                }
                else
                {
                    // Other dimensions
                    _segmentImages[i].color = Color.Lerp(dimColor, unselectedColor, 0.5f);
                    _segmentTransforms[i].localScale = Vector3.one * 0.9f;
                }
            }
        }

        private void UpdateDimensionNameDisplay()
        {
            if (dimensionNameText == null || DimensionManager.Instance == null) return;
            
            if (_hoveredSegment >= 0)
            {
                dimensionNameText.text = DimensionManager.Instance.GetDimensionName(_hoveredSegment);
                dimensionNameText.color = DimensionManager.Instance.GetDimensionColor(_hoveredSegment);
            }
        }

        #region UI Creation

        private void CreateWheelUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("DimensionWheelCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Create wheel container
            GameObject containerObj = new GameObject("WheelContainer");
            containerObj.transform.SetParent(canvas.transform);
            wheelContainer = containerObj.AddComponent<RectTransform>();
            wheelContainer.anchorMin = new Vector2(0.5f, 0.5f);
            wheelContainer.anchorMax = new Vector2(0.5f, 0.5f);
            wheelContainer.anchoredPosition = Vector2.zero;
            wheelContainer.sizeDelta = new Vector2(wheelRadius * 2.5f, wheelRadius * 2.5f);
            
            // Create background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(wheelContainer);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            backgroundImage = bgObj.AddComponent<Image>();
            backgroundImage.color = backgroundColor;
            
            // Create center text
            GameObject textObj = new GameObject("DimensionName");
            textObj.transform.SetParent(wheelContainer);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(300, 50);
            dimensionNameText = textObj.AddComponent<TextMeshProUGUI>();
            dimensionNameText.alignment = TextAlignmentOptions.Center;
            dimensionNameText.fontSize = 32;
            dimensionNameText.fontStyle = FontStyles.Bold;
            
            // Create segments
            CreateSegments();
        }

        private void CreateSegments()
        {
            if (DimensionManager.Instance == null)
            {
                // Create with default 5 dimensions
                CreateSegmentsForCount(5);
            }
            else
            {
                CreateSegmentsForCount(DimensionManager.Instance.DimensionCount);
            }
        }

        private void CreateSegmentsForCount(int count)
        {
            _segmentTransforms = new RectTransform[count];
            _segmentImages = new Image[count];
            _segmentLabels = new TextMeshProUGUI[count];
            
            float angleStep = 360f / count;
            
            for (int i = 0; i < count; i++)
            {
                // Calculate position (start from top, go clockwise)
                float angle = -90f + (i * angleStep);
                float rad = angle * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * wheelRadius;
                
                // Create segment
                GameObject segObj = new GameObject($"Segment_{i}");
                segObj.transform.SetParent(wheelContainer);
                
                RectTransform segRect = segObj.AddComponent<RectTransform>();
                segRect.anchorMin = new Vector2(0.5f, 0.5f);
                segRect.anchorMax = new Vector2(0.5f, 0.5f);
                segRect.anchoredPosition = pos;
                segRect.sizeDelta = new Vector2(segmentSize, segmentSize);
                _segmentTransforms[i] = segRect;
                
                // Add image (circle)
                Image img = segObj.AddComponent<Image>();
                img.color = Color.white;
                _segmentImages[i] = img;
                
                // Add number label
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(segObj.transform);
                RectTransform labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                
                TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
                label.text = (i + 1).ToString();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 28;
                label.fontStyle = FontStyles.Bold;
                label.color = Color.black;
                _segmentLabels[i] = label;
            }
        }

        #endregion

        private void OnValidate()
        {
            wheelRadius = Mathf.Max(50f, wheelRadius);
            segmentSize = Mathf.Max(20f, segmentSize);
            deadZoneRadius = Mathf.Max(0f, deadZoneRadius);
            openTimeScale = Mathf.Clamp01(openTimeScale);
        }
    }
}
