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

        // Visible (non-hidden) dimension indices, rebuilt when visibility changes
        private int[] _visibleDimensions;
        
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
        }

        private void Start()
        {
            if (autoCreateUI && canvas == null)
            {
                CreateWheelUI();
                Debug.Log("[DimensionWheel] Created wheel UI");
            }
            else if (canvas != null)
            {
                Debug.Log($"[DimensionWheel] Canvas already assigned: {canvas.gameObject.name}");
            }
            
            // Check if DimensionManager is available
            var dimMgr = GetDimensionManager();
            if (dimMgr != null)
                Debug.Log($"[DimensionWheel] DimensionManager is ready, current dimension: {dimMgr.CurrentDimension}");
            else
                Debug.LogWarning("[DimensionWheel] DimensionManager not found during Start()");
            
            // Start hidden
            if (wheelContainer != null)
            {
                wheelContainer.gameObject.SetActive(false);
                Debug.Log($"[DimensionWheel] Wheel container hidden, active: {wheelContainer.gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.LogWarning("[DimensionWheel] wheelContainer is null!");
            }
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            _inputActions.Player.DimensionWheel.started += OnWheelStarted;
            _inputActions.Player.DimensionWheel.canceled += OnWheelCanceled;

            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionLocksChanged += OnLocksChanged;
                DimensionManager.Instance.OnDimensionVisibilityChanged += OnVisibilityChanged;
            }
        }

        private void OnDisable()
        {
            _inputActions.Player.DimensionWheel.started -= OnWheelStarted;
            _inputActions.Player.DimensionWheel.canceled -= OnWheelCanceled;
            _inputActions.Player.Disable();

            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionLocksChanged -= OnLocksChanged;
                DimensionManager.Instance.OnDimensionVisibilityChanged -= OnVisibilityChanged;
            }
            
            // Ensure we restore state if disabled while open
            if (_isOpen)
            {
                RestoreGameState();
            }
        }

        private void OnWheelStarted(InputAction.CallbackContext ctx) => OpenWheel();
        private void OnWheelCanceled(InputAction.CallbackContext ctx) => CloseWheel();

        private void Update()
        {
            if (!_isOpen) return;
            
            UpdateSelection();
        }

        private DimensionManager GetDimensionManager()
        {
            if (DimensionManager.Instance != null)
                return DimensionManager.Instance;
            
            // Try to find it in scene if Instance is somehow null
            var manager = FindFirstObjectByType<DimensionManager>();
            if (manager != null)
            {
                Debug.LogWarning("[DimensionWheel] DimensionManager.Instance was null but found instance in scene, resetting Instance");
                return manager;
            }
            
            Debug.LogError("[DimensionWheel] No DimensionManager found in scene! Available DimensionManager instances: " 
                + FindObjectsByType<DimensionManager>(FindObjectsSortMode.None).Length);
            return null;
        }

        private void OpenWheel()
        {
            Debug.Log("[DimensionWheel] OpenWheel called");
            
            var dimensionManager = GetDimensionManager();
            if (dimensionManager == null) 
            {
                Debug.LogWarning("[DimensionWheel] Could not get DimensionManager!");
                return;
            }
            if (dimensionManager.IsSwitchingLocked) 
            {
                Debug.Log("[DimensionWheel] Dimension switching is locked");
                return;
            }
            
            _isOpen = true;
            Debug.Log("[DimensionWheel] Wheel opened, _isOpen = true");
            
            // Store current state
            _previousTimeScale = Time.timeScale;
            _cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            _previousSelection = dimensionManager.CurrentDimension;
            
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
                Debug.Log("[DimensionWheel] Showing wheel container");
                RebuildVisibleDimensions();
                RebuildWheelLayout();
                wheelContainer.gameObject.SetActive(true);
                Debug.Log($"[DimensionWheel] Wheel container active: {wheelContainer.gameObject.activeInHierarchy}");
                UpdateSegmentVisuals();
            }
            else
            {
                Debug.LogError("[DimensionWheel] wheelContainer is null in OpenWheel!");
            }
            
            // Set hovered to current dimension initially
            _hoveredSegment = _previousSelection;
            UpdateDimensionNameDisplay();
        }

        private void CloseWheel()
        {
            if (!_isOpen) return;
            
            _isOpen = false;
            
            // Apply selection (hoveredSegment is an actual dimension index)
            if (_hoveredSegment >= 0 && _hoveredSegment != _previousSelection)
            {
                if (!DimensionManager.Instance.IsDimensionLocked(_hoveredSegment)
                    && !DimensionManager.Instance.IsDimensionHidden(_hoveredSegment))
                {
                    DimensionManager.Instance.SwitchToDimension(_hoveredSegment);
                }
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
            
            int segmentCount = _visibleDimensions != null ? _visibleDimensions.Length : 0;
            if (segmentCount == 0) return;
            float segmentAngle = 360f / segmentCount;
            
            // Offset by half segment so segments are centered
            angle = (angle + segmentAngle / 2f) % 360f;
            
            int newHovered = Mathf.FloorToInt(angle / segmentAngle);
            newHovered = Mathf.Clamp(newHovered, 0, segmentCount - 1);

            // Map visual segment index back to actual dimension index
            int actualDimension = _visibleDimensions[newHovered];
            
            if (actualDimension != _hoveredSegment)
            {
                _hoveredSegment = actualDimension;
                UpdateSegmentVisuals();
                UpdateDimensionNameDisplay();
            }
        }

        private void UpdateSegmentVisuals()
        {
            if (_segmentImages == null || DimensionManager.Instance == null || _visibleDimensions == null) return;
            
            int currentDim = DimensionManager.Instance.CurrentDimension;
            
            for (int vi = 0; vi < _segmentImages.Length; vi++)
            {
                if (_segmentImages[vi] == null) continue;

                int dimIndex = _visibleDimensions[vi];
                bool isLocked = DimensionManager.Instance.IsDimensionLocked(dimIndex);
                Color dimColor = DimensionManager.Instance.GetDimensionColor(dimIndex);
                
                if (isLocked)
                {
                    _segmentImages[vi].color = lockedColor;
                    _segmentTransforms[vi].localScale = Vector3.one * 0.85f;
                    if (_segmentLabels != null && vi < _segmentLabels.Length && _segmentLabels[vi] != null)
                        _segmentLabels[vi].color = Color.black;
                }
                else if (dimIndex == _hoveredSegment)
                {
                    _segmentImages[vi].color = dimColor;
                    _segmentTransforms[vi].localScale = Vector3.one * 1.2f;
                }
                else if (dimIndex == currentDim)
                {
                    _segmentImages[vi].color = Color.Lerp(dimColor, selectedColor, 0.3f);
                    _segmentTransforms[vi].localScale = Vector3.one;
                }
                else
                {
                    _segmentImages[vi].color = Color.Lerp(dimColor, unselectedColor, 0.5f);
                    _segmentTransforms[vi].localScale = Vector3.one * 0.9f;
                }

                if (!isLocked && _segmentLabels != null && vi < _segmentLabels.Length && _segmentLabels[vi] != null)
                    _segmentLabels[vi].color = Color.black;
            }
        }

        private void UpdateDimensionNameDisplay()
        {
            if (dimensionNameText == null || DimensionManager.Instance == null) return;
            
            if (_hoveredSegment >= 0)
            {
                bool isLocked = DimensionManager.Instance.IsDimensionLocked(_hoveredSegment);
                string name = DimensionManager.Instance.GetDimensionName(_hoveredSegment);
                dimensionNameText.text = isLocked ? $"{name} (Locked)" : name;
                dimensionNameText.color = isLocked ? lockedColor : DimensionManager.Instance.GetDimensionColor(_hoveredSegment);
            }
        }

        private void OnLocksChanged()
        {
            if (!_isOpen) return;
            UpdateSegmentVisuals();
            UpdateDimensionNameDisplay();
        }

        private void OnVisibilityChanged()
        {
            if (!_isOpen) return;
            RebuildVisibleDimensions();
            RebuildWheelLayout();
            UpdateSegmentVisuals();
            UpdateDimensionNameDisplay();
        }

        /// <summary>
        /// Rebuilds the list of non-hidden dimension indices.
        /// </summary>
        private void RebuildVisibleDimensions()
        {
            if (DimensionManager.Instance == null)
            {
                _visibleDimensions = new int[] { 0, 1, 2, 3, 4 };
                return;
            }

            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < DimensionManager.Instance.DimensionCount; i++)
            {
                if (!DimensionManager.Instance.IsDimensionHidden(i))
                    list.Add(i);
            }
            _visibleDimensions = list.ToArray();
        }

        /// <summary>
        /// Destroys existing segments and recreates them for visible dimensions only.
        /// </summary>
        private void RebuildWheelLayout()
        {
            // Destroy old segments
            if (_segmentTransforms != null)
            {
                foreach (var rt in _segmentTransforms)
                {
                    if (rt != null) Destroy(rt.gameObject);
                }
            }

            int count = _visibleDimensions != null ? _visibleDimensions.Length : 0;
            _segmentTransforms = new RectTransform[count];
            _segmentImages = new Image[count];
            _segmentLabels = new TextMeshProUGUI[count];

            float angleStep = count > 0 ? 360f / count : 360f;

            for (int vi = 0; vi < count; vi++)
            {
                int dimIndex = _visibleDimensions[vi];

                float angle = -90f + (vi * angleStep);
                float rad = angle * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * wheelRadius;

                GameObject segObj = new GameObject($"Segment_{dimIndex}");
                segObj.transform.SetParent(wheelContainer);

                RectTransform segRect = segObj.AddComponent<RectTransform>();
                segRect.anchorMin = new Vector2(0.5f, 0.5f);
                segRect.anchorMax = new Vector2(0.5f, 0.5f);
                segRect.anchoredPosition = pos;
                segRect.sizeDelta = new Vector2(segmentSize, segmentSize);
                _segmentTransforms[vi] = segRect;

                Image img = segObj.AddComponent<Image>();
                img.color = Color.white;
                _segmentImages[vi] = img;

                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(segObj.transform);
                RectTransform labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
                // Show dimension name instead of just number
                if (DimensionManager.Instance != null)
                {
                    label.text = DimensionManager.Instance.GetDimensionName(dimIndex);
                }
                else
                {
                    label.text = (dimIndex + 1).ToString();
                }
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 28;
                label.fontStyle = FontStyles.Bold;
                label.color = Color.black;
                _segmentLabels[vi] = label;
            }
        }

        #region UI Creation

        private void CreateWheelUI()
        {
            Debug.Log("[DimensionWheel] CreateWheelUI - Starting wheel UI creation");
            
            // Create Canvas
            GameObject canvasObj = new GameObject("DimensionWheelCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            Debug.Log("[DimensionWheel] Created canvas with sorting order 100");
            
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Create wheel container
            GameObject containerObj = new GameObject("WheelContainer");
            containerObj.transform.SetParent(canvas.transform);
            wheelContainer = containerObj.AddComponent<RectTransform>();
            wheelContainer.anchorMin = new Vector2(0.5f, 0.5f);
            wheelContainer.anchorMax = new Vector2(0.5f, 0.5f);
            wheelContainer.anchoredPosition = Vector2.zero;
            wheelContainer.sizeDelta = new Vector2(wheelRadius * 2.5f, wheelRadius * 2.5f);
            Debug.Log($"[DimensionWheel] Created wheel container: {containerObj.name}");
            
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
            Debug.Log("[DimensionWheel] CreateWheelUI complete");
        }

        private void CreateSegments()
        {
            // Use visibility-aware rebuild so hidden dimensions are excluded from the start
            RebuildVisibleDimensions();
            RebuildWheelLayout();
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
