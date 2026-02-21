using UnityEngine;
using UnityEngine.UI;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Battery-style GUI in the top-left showing dimension shift cooldown.
    /// 4 segments fill up as cooldown recharges. Cyan colored to match the cyberpunk theme.
    /// </summary>
    public class DimensionCooldownUI : MonoBehaviour
    {
        [Header("Battery Settings")]
        [SerializeField] private int segmentCount = 4;
        [SerializeField] private Color chargedColor = new Color(0f, 0.85f, 1f, 1f); // Cyan
        [SerializeField] private Color emptyColor = new Color(0.15f, 0.15f, 0.2f, 0.6f);
        [SerializeField] private Color outlineColor = new Color(0f, 0.85f, 1f, 0.8f);

        [Header("Size")]
        [SerializeField] private float batteryWidth = 50f;
        [SerializeField] private float batteryHeight = 90f;
        [SerializeField] private float margin = 20f;
        [SerializeField] private float outlineThickness = 3f;
        [SerializeField] private float segmentGap = 4f;
        [SerializeField] private float segmentPadding = 5f;

        private Image[] _segments;
        private Canvas _canvas;
        private bool _uiCreated = false;

        private void Awake()
        {
            Debug.Log("[DimensionCooldownUI] Awake() called");
            Debug.Log($"[DimensionCooldownUI] Current parent: {(transform.parent != null ? transform.parent.name : "NONE (root level)")}, Scene: {gameObject.scene.name}");
            
            // CRITICAL: Ensure this GameObject is not a child of anything that can be destroyed
            // If it's parented, detach it FIRST before DontDestroyOnLoad
            if (transform.parent != null)
            {
                Debug.LogWarning($"[DimensionCooldownUI] WARNING: Component is parented to {transform.parent.name}! Detaching immediately...");
                transform.SetParent(null, false);
                Debug.Log("[DimensionCooldownUI] Detached from parent");
            }
            
            // Make the GAMEOBJECT persistent (not just this component)
            if (gameObject.scene.name != "DontDestroyOnLoad")
            {
                DontDestroyOnLoad(gameObject);
                Debug.Log("[DimensionCooldownUI] Added GameObject to DontDestroyOnLoad");
            }
            else
            {
                Debug.Log("[DimensionCooldownUI] GameObject already in DontDestroyOnLoad scene");
            }
        }

        private void Start()
        {
            Debug.Log("[DimensionCooldownUI] Start() called");
            
            if (!_uiCreated)
            {
                CreateUI();
                _uiCreated = true;
            }
        }

        private void OnEnable()
        {
            Debug.Log("[DimensionCooldownUI] OnEnable() called");
            
            // Create UI if Start never ran (e.g., component was destroyed and re-enabled)
            if (!_uiCreated)
            {
                CreateUI();
                _uiCreated = true;
            }
        }

        private void Update()
        {
            // Check if canvas was destroyed and recreate if needed
            if (_canvas == null && _uiCreated)
            {
                Debug.LogWarning("[DimensionCooldownUI] Canvas was destroyed! Recreating...");
                _uiCreated = false;
                _segments = null;
            }

            // If segments don't exist or were destroyed, stop here
            if (_segments == null || _segments.Length == 0)
                return;

            // Check if any segment Images were destroyed (e.g., scene reload)
            foreach (var seg in _segments)
            {
                if (seg == null || seg.GetComponentInParent<Canvas>() == null)
                {
                    Debug.LogWarning("[DimensionCooldownUI] Segments were destroyed during scene reload. Recreating UI...");
                    _uiCreated = false;
                    _segments = null;
                    OnEnable();
                    return;
                }
            }

            // Wait for DimensionManager if not available yet
            var dimMgr = DimensionManager.Instance;
            if (dimMgr == null) 
            {
                return;
            }

            float progress = dimMgr.CooldownProgress;

            for (int i = 0; i < _segments.Length; i++)
            {
                // Bottom segment = index 0, top = last
                float segMin = (float)i / segmentCount;
                float segMax = (float)(i + 1) / segmentCount;

                if (progress >= segMax)
                {
                    // Fully charged segment
                    _segments[i].color = chargedColor;
                }
                else if (progress > segMin)
                {
                    // Partially charged — lerp alpha
                    float t = (progress - segMin) / (segMax - segMin);
                    _segments[i].color = Color.Lerp(emptyColor, chargedColor, t);
                }
                else
                {
                    // Empty segment
                    _segments[i].color = emptyColor;
                }
            }
        }

        private void CreateUI()
        {
            Debug.Log("[DimensionCooldownUI] CreateUI() called");
            
            // Always create a dedicated canvas for dimension cooldown UI
            Canvas canvas = null;
            
            // Check if we already have a dimension UI canvas
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.gameObject.name == "DimensionUICanvas")
                {
                    canvas = c;
                    break;
                }
            }
            
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("DimensionUICanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 15; // Above inventory
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Ensure proper scaling
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvas = canvas;

            // Battery root — top-left anchor
            GameObject batteryRoot = new GameObject("BatteryCooldown");
            batteryRoot.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = batteryRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(margin, -margin);
            rootRect.sizeDelta = new Vector2(batteryWidth, batteryHeight);

            // Battery cap (top nub)
            float capWidth = batteryWidth * 0.4f;
            float capHeight = 8f;
            GameObject cap = new GameObject("Cap");
            cap.transform.SetParent(batteryRoot.transform, false);
            Image capImage = cap.AddComponent<Image>();
            capImage.color = outlineColor;
            RectTransform capRect = cap.GetComponent<RectTransform>();
            capRect.anchorMin = new Vector2(0.5f, 1f);
            capRect.anchorMax = new Vector2(0.5f, 1f);
            capRect.pivot = new Vector2(0.5f, 0f);
            capRect.anchoredPosition = Vector2.zero;
            capRect.sizeDelta = new Vector2(capWidth, capHeight);

            // Battery outline (body border)
            GameObject outline = new GameObject("Outline");
            outline.transform.SetParent(batteryRoot.transform, false);
            Image outlineImage = outline.AddComponent<Image>();
            outlineImage.color = outlineColor;
            RectTransform outlineRect = outline.GetComponent<RectTransform>();
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = Vector2.zero;
            outlineRect.offsetMax = Vector2.zero;

            // Inner background (dark area inside outline)
            GameObject inner = new GameObject("InnerBG");
            inner.transform.SetParent(outline.transform, false);
            Image innerImage = inner.AddComponent<Image>();
            innerImage.color = Color.clear;
            RectTransform innerRect = inner.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(outlineThickness, outlineThickness);
            innerRect.offsetMax = new Vector2(-outlineThickness, -outlineThickness);

            // Segments — bottom to top
            _segments = new Image[segmentCount];
            float innerHeight = batteryHeight - (outlineThickness * 2) - (segmentPadding * 2);
            float totalGaps = (segmentCount - 1) * segmentGap;
            float segHeight = (innerHeight - totalGaps) / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                GameObject seg = new GameObject($"Segment_{i}");
                seg.transform.SetParent(inner.transform, false);

                Image segImage = seg.AddComponent<Image>();
                segImage.color = chargedColor;

                RectTransform segRect = seg.GetComponent<RectTransform>();
                segRect.anchorMin = new Vector2(0f, 0f);
                segRect.anchorMax = new Vector2(1f, 0f);
                segRect.pivot = new Vector2(0.5f, 0f);

                float yPos = segmentPadding + i * (segHeight + segmentGap);
                segRect.anchoredPosition = new Vector2(0f, yPos);
                segRect.sizeDelta = new Vector2(-(segmentPadding * 2), segHeight);

                _segments[i] = segImage;
            }
            
            Debug.Log("[DimensionCooldownUI] CreateUI completed successfully");
        }
    }
}
