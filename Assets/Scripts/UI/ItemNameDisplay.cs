using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OutOfPhase.Items;
using OutOfPhase.Inventory;

namespace OutOfPhase.UI
{
    /// <summary>
    /// Displays the held item's name above the hotbar, then fades out.
    /// Minecraft-style: appears on item switch, holds briefly, then fades away.
    /// </summary>
    public class ItemNameDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HotbarController hotbar;

        [Header("Timing")]
        [Tooltip("How long the name stays fully visible")]
        [SerializeField] private float displayDuration = 2f;

        [Tooltip("How long the fade-out takes")]
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("Appearance")]
        [SerializeField] private int fontSize = 28;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Vector2 position = new Vector2(0f, 560f);

        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateUI = true;

        private TextMeshProUGUI _nameText;
        private CanvasGroup _canvasGroup;
        private float _displayTimer;
        private bool _showing;

        private void Awake()
        {
            if (hotbar == null) hotbar = GetComponent<HotbarController>();
            if (hotbar == null) hotbar = GetComponentInParent<HotbarController>();
            if (hotbar == null) hotbar = FindFirstObjectByType<HotbarController>();

            if (autoCreateUI && _nameText == null)
            {
                CreateUI();
            }
        }

        private void OnEnable()
        {
            // Defer subscription to ensure hotbar is found
            if (hotbar == null)
            {
                hotbar = GetComponent<HotbarController>();
                if (hotbar == null) hotbar = GetComponentInParent<HotbarController>();
                if (hotbar == null) hotbar = FindFirstObjectByType<HotbarController>();
            }

            if (hotbar != null)
            {
                hotbar.OnEquippedItemChanged += OnItemChanged;
                hotbar.OnSelectedSlotChanged += OnSlotChanged;
            }
        }

        private void Start()
        {
            // Show name for whatever item is already equipped at start
            if (hotbar != null && hotbar.SelectedItem != null)
            {
                ShowName(hotbar.SelectedItem.ItemName);
            }
        }

        private void OnDisable()
        {
            if (hotbar != null)
            {
                hotbar.OnEquippedItemChanged -= OnItemChanged;
                hotbar.OnSelectedSlotChanged -= OnSlotChanged;
            }
        }

        private void Update()
        {
            if (!_showing || _canvasGroup == null) return;

            _displayTimer -= Time.deltaTime;

            if (_displayTimer <= 0f)
            {
                // Fading out
                float fadeT = Mathf.Clamp01(-_displayTimer / fadeDuration);
                _canvasGroup.alpha = 1f - fadeT;

                if (fadeT >= 1f)
                {
                    _showing = false;
                    _canvasGroup.alpha = 0f;
                }
            }
        }

        private void OnItemChanged(ItemDefinition oldItem, ItemDefinition newItem)
        {
            if (newItem != null)
                ShowName(newItem.ItemName);
            else
                HideName();
        }

        private void OnSlotChanged(int oldSlot, int newSlot)
        {
            // Also trigger on slot change even if item is the same type
            // (matches Minecraft behavior of showing name on any slot switch)
            var item = hotbar.SelectedItem;
            if (item != null)
                ShowName(item.ItemName);
            else
                HideName();
        }

        private void ShowName(string itemName)
        {
            if (_nameText == null) return;

            _nameText.text = itemName;
            _canvasGroup.alpha = 1f;
            _displayTimer = displayDuration;
            _showing = true;
        }

        private void HideName()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
            _showing = false;
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
                canvas.sortingOrder = 100;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Always ensure proper scaling
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Container with CanvasGroup for fading
            GameObject container = new GameObject("ItemNameDisplay");
            container.transform.SetParent(canvas.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0f);
            containerRect.anchorMax = new Vector2(0.5f, 0f);
            containerRect.pivot = new Vector2(0.5f, 0f);
            containerRect.anchoredPosition = position;
            containerRect.sizeDelta = new Vector2(400f, 50f);

            _canvasGroup = container.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // Text
            GameObject textObj = new GameObject("ItemNameText");
            textObj.transform.SetParent(container.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _nameText = textObj.AddComponent<TextMeshProUGUI>();
            _nameText.text = "";
            _nameText.fontSize = fontSize;
            _nameText.color = textColor;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.textWrappingMode = TextWrappingModes.NoWrap;
            _nameText.overflowMode = TextOverflowModes.Overflow;

            // Drop shadow for readability
            _nameText.fontMaterial.EnableKeyword("UNDERLAY_ON");
            _nameText.fontMaterial.SetFloat("_UnderlayOffsetX", 1f);
            _nameText.fontMaterial.SetFloat("_UnderlayOffsetY", -1f);
            _nameText.fontMaterial.SetFloat("_UnderlayDilate", 0.2f);
            _nameText.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.8f));
        }
    }
}
