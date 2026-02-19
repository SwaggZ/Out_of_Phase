using System;
using UnityEngine;
using UnityEngine.InputSystem;
using OutOfPhase.Items;
using OutOfPhase.Player;
using OutOfPhase.Dimension;

namespace OutOfPhase.Inventory
{
    /// <summary>
    /// Handles hotbar slot selection via scroll wheel and number keys.
    /// Manages the currently selected/equipped item.
    /// </summary>
    public class HotbarController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the inventory component")]
        [SerializeField] private Inventory inventory;
        
        [Tooltip("Transform where held items are spawned")]
        [SerializeField] private Transform heldItemAnchor;

        [Header("Selection")]
        [Tooltip("Starting selected slot index")]
        [SerializeField] private int startingSlot = 0;
        
        [Tooltip("Invert scroll direction")]
        [SerializeField] private bool invertScroll = false;

        [Header("Audio")]
        [Tooltip("Sound played when switching hotbar slots")]
        [SerializeField] private AudioClip switchSound;
        [SerializeField] private float switchSoundVolume = 0.3f;

        // Input
        private PlayerInputActions _inputActions;

        // State
        private int _selectedSlot;
        private GameObject _currentHeldInstance;
        private ToolAction[] _activeToolActions;

        // Events
        /// <summary>Fired when selected slot changes (oldIndex, newIndex)</summary>
        public event Action<int, int> OnSelectedSlotChanged;
        
        /// <summary>Fired when equipped item changes (oldItem, newItem)</summary>
        public event Action<ItemDefinition, ItemDefinition> OnEquippedItemChanged;

        // Public accessors
        public int SelectedSlot => _selectedSlot;
        public InventorySlot SelectedInventorySlot => inventory?.GetSlot(_selectedSlot);
        public ItemDefinition SelectedItem => SelectedInventorySlot?.Item;
        public bool HasItemSelected => SelectedItem != null;
        public Inventory Inventory => inventory;
        public Transform HeldItemAnchor => heldItemAnchor;

        private void Awake()
        {
            _inputActions = new PlayerInputActions();
            
            // Auto-find inventory if not assigned
            if (inventory == null)
            {
                inventory = GetComponent<Inventory>();
                if (inventory == null)
                {
                    inventory = GetComponentInParent<Inventory>();
                }
            }
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            
            // Subscribe to input
            _inputActions.Player.HotbarScroll.performed += OnScrollInput;
            _inputActions.Player.HotbarSlot1.performed += _ => SelectSlot(0);
            _inputActions.Player.HotbarSlot2.performed += _ => SelectSlot(1);
            _inputActions.Player.HotbarSlot3.performed += _ => SelectSlot(2);
            _inputActions.Player.HotbarSlot4.performed += _ => SelectSlot(3);
            _inputActions.Player.HotbarSlot5.performed += _ => SelectSlot(4);
            
            // Subscribe to inventory changes
            if (inventory != null)
            {
                inventory.OnSlotChanged += OnInventorySlotChanged;
            }
        }

        private void OnDisable()
        {
            _inputActions.Player.HotbarScroll.performed -= OnScrollInput;
            _inputActions.Player.Disable();
            
            if (inventory != null)
            {
                inventory.OnSlotChanged -= OnInventorySlotChanged;
            }
        }

        private void Start()
        {
            _selectedSlot = Mathf.Clamp(startingSlot, 0, inventory.SlotCount - 1);
            UpdateHeldItem();
        }

        private void Update()
        {
            // Update active tool actions (for continuous effects like torch light)
            UpdateToolActions();
        }

        private void OnScrollInput(InputAction.CallbackContext context)
        {
            float scrollValue = context.ReadValue<float>();
            if (Mathf.Abs(scrollValue) < 0.1f) return;

            int direction = scrollValue > 0 ? -1 : 1;
            if (invertScroll) direction = -direction;

            int newSlot = _selectedSlot + direction;
            
            // Wrap around
            if (newSlot < 0) newSlot = inventory.SlotCount - 1;
            if (newSlot >= inventory.SlotCount) newSlot = 0;

            SelectSlot(newSlot);
        }

        /// <summary>
        /// Selects a specific hotbar slot.
        /// </summary>
        public void SelectSlot(int slotIndex)
        {
            if (inventory == null) return;
            if (slotIndex < 0 || slotIndex >= inventory.SlotCount) return;
            if (slotIndex == _selectedSlot) return;

            int oldSlot = _selectedSlot;
            var oldItem = SelectedItem;

            // Unequip old tool actions
            UnequipCurrentItem();

            _selectedSlot = slotIndex;

            // Update held item first so it exists when OnEquip is called
            UpdateHeldItem();
            EquipCurrentItem();

            var newItem = SelectedItem;

            // Play switch sound
            if (switchSound != null)
                SFXPlayer.PlayAtPoint(switchSound, transform.position, switchSoundVolume);

            OnSelectedSlotChanged?.Invoke(oldSlot, _selectedSlot);
            
            if (oldItem != newItem)
            {
                OnEquippedItemChanged?.Invoke(oldItem, newItem);
            }
        }

        /// <summary>
        /// Cycles to the next slot.
        /// </summary>
        public void NextSlot()
        {
            int next = (_selectedSlot + 1) % inventory.SlotCount;
            SelectSlot(next);
        }

        /// <summary>
        /// Cycles to the previous slot.
        /// </summary>
        public void PreviousSlot()
        {
            int prev = _selectedSlot - 1;
            if (prev < 0) prev = inventory.SlotCount - 1;
            SelectSlot(prev);
        }

        private void OnInventorySlotChanged(int index, InventorySlot oldSlot, InventorySlot newSlot)
        {
            // If the changed slot is our selected slot, update held item
            if (index == _selectedSlot)
            {
                var oldItem = oldSlot?.Item;
                var newItem = newSlot?.Item;
                
                if (oldItem != newItem)
                {
                    UnequipCurrentItem();
                    UpdateHeldItem();
                    EquipCurrentItem();
                    OnEquippedItemChanged?.Invoke(oldItem, newItem);
                }
            }
        }

        private void EquipCurrentItem()
        {
            var item = SelectedItem;
            if (item == null || item.ToolActions == null) return;

            _activeToolActions = item.ToolActions;
            
            var context = CreateToolContext();
            foreach (var action in _activeToolActions)
            {
                if (action != null)
                {
                    action.OnEquip(context);
                }
            }
        }

        private void UnequipCurrentItem()
        {
            if (_activeToolActions == null) return;

            var context = CreateToolContext();
            foreach (var action in _activeToolActions)
            {
                if (action != null)
                {
                    action.OnUnequip(context);
                }
            }
            
            _activeToolActions = null;
        }

        private void UpdateToolActions()
        {
            if (_activeToolActions == null) return;

            var context = CreateToolContext();
            foreach (var action in _activeToolActions)
            {
                if (action != null)
                {
                    action.OnEquippedUpdate(context);
                }
            }
        }

        private void UpdateHeldItem()
        {
            // Destroy current held item
            if (_currentHeldInstance != null)
            {
                Destroy(_currentHeldInstance);
                _currentHeldInstance = null;
            }

            // Spawn new held item if we have one
            var item = SelectedItem;
            if (item != null && item.HeldPrefab != null && heldItemAnchor != null)
            {
                _currentHeldInstance = Instantiate(
                    item.HeldPrefab,
                    heldItemAnchor
                );
                _currentHeldInstance.transform.localPosition = Vector3.zero;
                _currentHeldInstance.transform.localRotation = Quaternion.identity;

                // Disable physics on held items so they don't fall
                foreach (var rb in _currentHeldInstance.GetComponentsInChildren<Rigidbody>())
                {
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                }
                foreach (var col in _currentHeldInstance.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
            }
        }

        /// <summary>
        /// Creates a tool use context for the current state.
        /// </summary>
        public ToolUseContext CreateToolContext()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) mainCam = GameObject.Find("PlayerCamera")?.GetComponent<Camera>();
            var cameraTransform = mainCam != null ? mainCam.transform : transform;
            
            return new ToolUseContext
            {
                PlayerTransform = transform,
                CameraTransform = cameraTransform,
                Item = SelectedItem,
                Slot = SelectedInventorySlot,
                Target = null,
                HitInfo = null,
                MaxDistance = 3f, // Default interaction distance
                HeldModelInstance = _currentHeldInstance
            };
        }

        /// <summary>
        /// Uses the currently selected item's tool action.
        /// Called by the Interactor when player clicks.
        /// </summary>
        public bool UseSelectedItem(GameObject target = null, RaycastHit? hitInfo = null)
        {
            if (_activeToolActions == null || _activeToolActions.Length == 0)
                return false;

            var context = CreateToolContext();
            context.Target = target;
            context.HitInfo = hitInfo;

            bool anyUsed = false;
            foreach (var action in _activeToolActions)
            {
                if (action != null && action.Use(context))
                {
                    anyUsed = true;
                    
                    // Apply durability cost
                    if (SelectedInventorySlot != null && SelectedInventorySlot.HasDurability)
                    {
                        bool broke = inventory.ReduceDurability(_selectedSlot, action.DurabilityCost);
                        if (broke)
                        {
                            Debug.Log($"Tool broke: {context.Item.ItemName}");
                        }
                    }
                    
                    break; // Only use first successful action
                }
            }

            return anyUsed;
        }

        /// <summary>
        /// Gets a specific tool action from the currently selected item.
        /// </summary>
        public T GetSelectedToolAction<T>() where T : ToolAction
        {
            return SelectedItem?.GetToolAction<T>();
        }

        /// <summary>
        /// Checks if the selected item has a specific tool action.
        /// </summary>
        public bool SelectedItemHasAction<T>() where T : ToolAction
        {
            return SelectedItem?.HasToolAction<T>() ?? false;
        }
    }
}
