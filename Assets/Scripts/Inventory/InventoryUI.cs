using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OutOfPhase.Items;

namespace OutOfPhase.Inventory
{
    /// <summary>
    /// Minimal hotbar UI displaying inventory slots and selection.
    /// Auto-creates UI if not manually set up.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Inventory inventory;
        [SerializeField] private HotbarController hotbarController;

        [Header("UI Elements")]
        [Tooltip("Parent transform for slot UI elements")]
        [SerializeField] private Transform slotsContainer;
        
        [Tooltip("Prefab for individual slot UI")]
        [SerializeField] private GameObject slotPrefab;

        [Header("Visuals")]
        [SerializeField] private Color normalSlotColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color selectedSlotColor = new Color(0.4f, 0.6f, 0.8f, 0.9f);
        [SerializeField] private Color emptySlotColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateUI = true;
        [SerializeField] private float slotSize = 60f;
        [SerializeField] private float slotSpacing = 8f;
        [SerializeField] private float bottomOffset = 40f;

        // Runtime
        private SlotUI[] _slotUIs;

        private void Awake()
        {
            // Auto-find references
            if (inventory == null)
                inventory = FindFirstObjectByType<Inventory>();
            if (hotbarController == null)
                hotbarController = FindFirstObjectByType<HotbarController>();

            // Auto-create UI if needed (guard against duplicates)
            if (autoCreateUI && slotsContainer == null)
            {
                // Check if another InventoryUI already created the hotbar
                var existing = GameObject.Find("Hotbar");
                if (existing != null)
                {
                    slotsContainer = existing.GetComponent<RectTransform>();
                }
                else
                {
                    CreateHotbarUI();
                }
            }
        }

        private void OnEnable()
        {
            if (inventory != null)
                inventory.OnSlotChanged += OnSlotChanged;
            if (hotbarController != null)
                hotbarController.OnSelectedSlotChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.OnSlotChanged -= OnSlotChanged;
            if (hotbarController != null)
                hotbarController.OnSelectedSlotChanged -= OnSelectionChanged;
        }

        private void Start()
        {
            CreateSlotUIs();
            RefreshAll();
        }

        private void CreateHotbarUI()
        {
            // Find or create canvas
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("UI Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 0;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Always ensure proper scaling
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Create hotbar container
            GameObject hotbarObj = new GameObject("Hotbar");
            hotbarObj.transform.SetParent(canvas.transform, false);

            RectTransform hotbarRect = hotbarObj.AddComponent<RectTransform>();
            hotbarRect.anchorMin = new Vector2(0.5f, 0f);
            hotbarRect.anchorMax = new Vector2(0.5f, 0f);
            hotbarRect.pivot = new Vector2(0.5f, 0f);
            hotbarRect.anchoredPosition = new Vector2(0, bottomOffset);

            // Add background
            Image bgImage = hotbarObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.4f);

            // Add horizontal layout
            HorizontalLayoutGroup layout = hotbarObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = slotSpacing;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Content size fitter to auto-size
            ContentSizeFitter fitter = hotbarObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            slotsContainer = hotbarRect;
        }

        private void CreateSlotUIs()
        {
            if (inventory == null || slotsContainer == null) return;

            // Clear existing
            foreach (Transform child in slotsContainer)
            {
                Destroy(child.gameObject);
            }

            _slotUIs = new SlotUI[inventory.SlotCount];

            for (int i = 0; i < inventory.SlotCount; i++)
            {
                GameObject slotObj;
                
                if (slotPrefab != null)
                {
                    slotObj = Instantiate(slotPrefab, slotsContainer);
                }
                else
                {
                    // Create basic slot UI if no prefab
                    slotObj = CreateDefaultSlotUI(i);
                }

                slotObj.name = $"Slot_{i}";
                _slotUIs[i] = new SlotUI(slotObj);
            }
        }

        private GameObject CreateDefaultSlotUI(int index)
        {
            // Create slot container
            GameObject slot = new GameObject($"Slot_{index}");
            slot.transform.SetParent(slotsContainer, false);
            
            // Add RectTransform
            var rectTransform = slot.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(slotSize, slotSize);
            
            // Add background image
            var bgImage = slot.AddComponent<Image>();
            bgImage.color = normalSlotColor;
            
            // Create icon child
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slot.transform, false);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4, 4);
            iconRect.offsetMax = new Vector2(-4, -4);
            var iconImage = iconObj.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.enabled = false;
            
            // Create quantity text
            GameObject quantityObj = new GameObject("Quantity");
            quantityObj.transform.SetParent(slot.transform, false);
            var quantityRect = quantityObj.AddComponent<RectTransform>();
            quantityRect.anchorMin = new Vector2(1, 0);
            quantityRect.anchorMax = new Vector2(1, 0);
            quantityRect.pivot = new Vector2(1, 0);
            quantityRect.anchoredPosition = new Vector2(-2, 2);
            quantityRect.sizeDelta = new Vector2(30, 20);
            var quantityText = quantityObj.AddComponent<TextMeshProUGUI>();
            quantityText.fontSize = 14;
            quantityText.alignment = TextAlignmentOptions.BottomRight;
            quantityText.text = "";
            
            // Create key hint text
            GameObject keyHintObj = new GameObject("KeyHint");
            keyHintObj.transform.SetParent(slot.transform, false);
            var keyRect = keyHintObj.AddComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0, 1);
            keyRect.anchorMax = new Vector2(0, 1);
            keyRect.pivot = new Vector2(0, 1);
            keyRect.anchoredPosition = new Vector2(4, -2);
            keyRect.sizeDelta = new Vector2(20, 16);
            var keyText = keyHintObj.AddComponent<TextMeshProUGUI>();
            keyText.fontSize = 12;
            keyText.alignment = TextAlignmentOptions.TopLeft;
            keyText.text = (index + 1).ToString();
            keyText.color = new Color(1, 1, 1, 0.6f);

            return slot;
        }

        private void OnSlotChanged(int index, InventorySlot oldSlot, InventorySlot newSlot)
        {
            if (_slotUIs == null || index < 0 || index >= _slotUIs.Length) return;
            RefreshSlot(index);
        }

        private void OnSelectionChanged(int oldIndex, int newIndex)
        {
            if (_slotUIs == null) return;
            
            if (oldIndex >= 0 && oldIndex < _slotUIs.Length)
                UpdateSlotSelection(oldIndex, false);
            if (newIndex >= 0 && newIndex < _slotUIs.Length)
                UpdateSlotSelection(newIndex, true);
        }

        private void RefreshAll()
        {
            if (_slotUIs == null || inventory == null) return;

            for (int i = 0; i < _slotUIs.Length; i++)
            {
                RefreshSlot(i);
            }
            
            // Update selection
            if (hotbarController != null)
            {
                UpdateSlotSelection(hotbarController.SelectedSlot, true);
            }
        }

        private void RefreshSlot(int index)
        {
            if (_slotUIs == null || index < 0 || index >= _slotUIs.Length) return;
            
            var slot = inventory.GetSlot(index);
            var slotUI = _slotUIs[index];
            
            if (slot == null || slot.IsEmpty)
            {
                slotUI.SetEmpty(emptySlotColor);
            }
            else
            {
                slotUI.SetItem(slot.Item, slot.Quantity, normalSlotColor);
            }
            
            // Update selection state
            bool isSelected = hotbarController != null && hotbarController.SelectedSlot == index;
            UpdateSlotSelection(index, isSelected);
        }

        private void UpdateSlotSelection(int index, bool selected)
        {
            if (_slotUIs == null || index < 0 || index >= _slotUIs.Length) return;
            
            var slot = inventory.GetSlot(index);
            bool isEmpty = slot == null || slot.IsEmpty;
            
            Color targetColor = selected ? selectedSlotColor : (isEmpty ? emptySlotColor : normalSlotColor);
            _slotUIs[index].SetBackgroundColor(targetColor);
        }

        /// <summary>
        /// Helper class to manage individual slot UI elements.
        /// </summary>
        private class SlotUI
        {
            public GameObject Root;
            public Image Background;
            public Image Icon;
            public TextMeshProUGUI QuantityText;

            public SlotUI(GameObject root)
            {
                Root = root;
                Background = root.GetComponent<Image>();
                
                var iconTransform = root.transform.Find("Icon");
                if (iconTransform != null)
                    Icon = iconTransform.GetComponent<Image>();
                
                var quantityTransform = root.transform.Find("Quantity");
                if (quantityTransform != null)
                    QuantityText = quantityTransform.GetComponent<TextMeshProUGUI>();
            }

            public void SetEmpty(Color bgColor)
            {
                if (Icon != null)
                {
                    Icon.enabled = false;
                    Icon.sprite = null;
                }
                if (QuantityText != null)
                    QuantityText.text = "";
                SetBackgroundColor(bgColor);
            }

            public void SetItem(ItemDefinition item, int quantity, Color bgColor)
            {
                if (Icon != null)
                {
                    Icon.sprite = item.Icon;
                    Icon.enabled = item.Icon != null;
                }
                if (QuantityText != null)
                {
                    QuantityText.text = quantity > 1 ? quantity.ToString() : "";
                }
                SetBackgroundColor(bgColor);
            }

            public void SetBackgroundColor(Color color)
            {
                if (Background != null)
                    Background.color = color;
            }
        }
    }
}
