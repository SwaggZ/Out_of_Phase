using System;
using UnityEngine;
using UnityEngine.InputSystem;
using OutOfPhase.Player;
using OutOfPhase.Inventory;
using OutOfPhase.Items;
using OutOfPhase.Items.ToolActions;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Player component that handles interaction raycasting, highlighting,
    /// and dispatching interactions/tool usage.
    /// </summary>
    public class Interactor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Inventory.Inventory inventory;
        [SerializeField] private HotbarController hotbar;

        [Header("Raycast Settings")]
        [Tooltip("Maximum interaction distance")]
        [SerializeField] private float interactionDistance = 3f;
        
        [Tooltip("Layers that can be interacted with")]
        [SerializeField] private LayerMask interactionLayers = ~0;
        
        [Tooltip("How often to update the raycast (seconds)")]
        [SerializeField] private float raycastUpdateRate = 0.05f;

        [Header("Highlight Settings")]
        [Tooltip("Enable outline/highlight on interactable objects")]
        [SerializeField] private bool enableHighlight = true;
        
        [Tooltip("Highlight color for interactables")]
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.5f, 1f);

        // Input
        private PlayerInputActions _inputActions;

        // Player colliders to ignore
        private Collider[] _playerColliders;

        // Current target state
        private GameObject _currentTarget;
        private IInteractable _currentInteractable;
        private IToolTarget _currentToolTarget;
        private IKeyTarget _currentKeyTarget;
        private RaycastHit _currentHit;
        private float _lastRaycastTime;

        // Events
        /// <summary>Fired when hover target changes (oldTarget, newTarget)</summary>
        public event Action<GameObject, GameObject> OnTargetChanged;
        
        /// <summary>Fired when an interaction occurs (target, interactable)</summary>
        public event Action<GameObject, IInteractable> OnInteract;
        
        /// <summary>Fired when a tool is used (target, action)</summary>
        public event Action<GameObject, ToolAction> OnToolUsed;

        // Public accessors
        public GameObject CurrentTarget => _currentTarget;
        public IInteractable CurrentInteractable => _currentInteractable;
        public bool HasTarget => _currentTarget != null;
        public bool CanInteract => _currentInteractable != null && _currentInteractable.CanInteract;
        public string CurrentPrompt => _currentInteractable?.InteractionPrompt ?? "";
        public float InteractionDistance => interactionDistance;

        private void Awake()
        {
            _inputActions = new PlayerInputActions();

            // Auto-find references
            if (playerCamera == null)
                playerCamera = Camera.main;
            if (inventory == null)
                inventory = GetComponent<Inventory.Inventory>();
            if (hotbar == null)
                hotbar = GetComponent<HotbarController>();

            // Cache player colliders to ignore during raycast
            _playerColliders = GetComponentsInChildren<Collider>();
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            _inputActions.Player.Interact.performed += OnInteractInput;
            _inputActions.Player.UseItem.performed += OnUseItemInput;
        }

        private void OnDisable()
        {
            _inputActions.Player.Interact.performed -= OnInteractInput;
            _inputActions.Player.UseItem.performed -= OnUseItemInput;
            _inputActions.Player.Disable();
        }

        private void Update()
        {
            // Rate-limited raycast update
            if (Time.time - _lastRaycastTime >= raycastUpdateRate)
            {
                UpdateRaycast();
                _lastRaycastTime = Time.time;
            }
        }

        private void UpdateRaycast()
        {
            GameObject oldTarget = _currentTarget;
            
            // Clear current
            _currentTarget = null;
            _currentInteractable = null;
            _currentToolTarget = null;
            _currentKeyTarget = null;

            if (playerCamera == null) return;

            // Perform raycast, ignoring player colliders
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance, interactionLayers, QueryTriggerInteraction.Collide);
            
            // Sort by distance and find first non-player hit
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            
            foreach (var hit in hits)
            {
                // Skip player's own colliders
                if (IsPlayerCollider(hit.collider)) continue;
                
                _currentHit = hit;
                _currentTarget = hit.collider.gameObject;
                
                // Cache interface references
                _currentInteractable = _currentTarget.GetComponent<IInteractable>();
                _currentToolTarget = _currentTarget.GetComponent<IToolTarget>();
                _currentKeyTarget = _currentTarget.GetComponent<IKeyTarget>();
                
                // Check parent if not found on collider object
                if (_currentInteractable == null)
                    _currentInteractable = _currentTarget.GetComponentInParent<IInteractable>();
                if (_currentToolTarget == null)
                    _currentToolTarget = _currentTarget.GetComponentInParent<IToolTarget>();
                if (_currentKeyTarget == null)
                    _currentKeyTarget = _currentTarget.GetComponentInParent<IKeyTarget>();
                
                break; // Found valid target, stop searching
            }

            // Target changed?
            if (oldTarget != _currentTarget)
            {
                // Remove highlight from old
                if (enableHighlight && oldTarget != null)
                {
                    SetHighlight(oldTarget, false);
                }
                
                // Add highlight to new
                if (enableHighlight && _currentTarget != null && HasAnyInteraction())
                {
                    SetHighlight(_currentTarget, true);
                }
                
                OnTargetChanged?.Invoke(oldTarget, _currentTarget);
            }
        }

        private bool HasAnyInteraction()
        {
            return _currentInteractable != null || _currentToolTarget != null || _currentKeyTarget != null;
        }

        private bool IsPlayerCollider(Collider col)
        {
            if (_playerColliders == null) return false;
            foreach (var playerCol in _playerColliders)
            {
                if (playerCol == col) return true;
            }
            return false;
        }

        private void OnInteractInput(InputAction.CallbackContext context)
        {
            TryInteract();
        }

        private void OnUseItemInput(InputAction.CallbackContext context)
        {
            TryUseItem();
        }

        /// <summary>
        /// Attempts to interact with the current target (E key).
        /// </summary>
        public bool TryInteract()
        {
            if (_currentInteractable == null || !_currentInteractable.CanInteract)
                return false;

            var interactionContext = new InteractionContext
            {
                PlayerTransform = transform,
                Interactor = this,
                Inventory = inventory,
                Hotbar = hotbar
            };

            _currentInteractable.Interact(interactionContext);
            OnInteract?.Invoke(_currentTarget, _currentInteractable);
            
            return true;
        }

        /// <summary>
        /// Attempts to use the selected item on the current target (LMB).
        /// </summary>
        public bool TryUseItem()
        {
            if (hotbar == null) return false;
            
            var selectedItem = hotbar.SelectedItem;
            if (selectedItem == null) return false;

            // Create tool context
            var toolContext = new ToolUseContext
            {
                PlayerTransform = transform,
                CameraTransform = playerCamera.transform,
                Item = selectedItem,
                Slot = hotbar.SelectedInventorySlot,
                Target = _currentTarget,
                HitInfo = _currentTarget != null ? _currentHit : null,
                MaxDistance = interactionDistance
            };

            // Try each tool action on the item
            if (selectedItem.ToolActions != null)
            {
                foreach (var action in selectedItem.ToolActions)
                {
                    if (action == null) continue;

                    // Check if we have a valid target for this action
                    if (_currentToolTarget != null && _currentToolTarget.AcceptsToolAction(action.GetType()))
                    {
                        if (_currentToolTarget.ReceiveToolAction(action, toolContext))
                        {
                            // Apply durability if applicable
                            ApplyToolDurability(action);
                            
                            // Play sound
                            if (action.UseSound != null)
                            {
                                AudioSource.PlayClipAtPoint(action.UseSound, _currentHit.point);
                            }
                            
                            OnToolUsed?.Invoke(_currentTarget, action);
                            return true;
                        }
                    }
                    // Also try key actions
                    else if (action is KeyAction && _currentKeyTarget != null && _currentKeyTarget.IsLocked)
                    {
                        if (selectedItem is KeyItemDefinition keyItem)
                        {
                            bool unlocked = _currentKeyTarget.TryUnlock(keyItem);
                            if (unlocked)
                            {
                                // Consume key if needed
                                if (keyItem.ConsumeOnUse)
                                {
                                    inventory.RemoveFromSlot(hotbar.SelectedSlot, 1);
                                }
                                
                                OnToolUsed?.Invoke(_currentTarget, action);
                                return true;
                            }
                        }
                    }
                    // Generic action use (no specific target needed)
                    else if (action.Use(toolContext))
                    {
                        ApplyToolDurability(action);
                        OnToolUsed?.Invoke(_currentTarget, action);
                        return true;
                    }
                }
            }

            return false;
        }

        private void ApplyToolDurability(ToolAction action)
        {
            if (hotbar == null || inventory == null) return;
            
            var slot = hotbar.SelectedInventorySlot;
            if (slot != null && slot.HasDurability)
            {
                bool broke = inventory.ReduceDurability(hotbar.SelectedSlot, action.DurabilityCost);
                if (broke)
                {
                    Debug.Log("Tool broke!");
                }
            }
        }

        private void SetHighlight(GameObject target, bool enabled)
        {
            // Simple approach: adjust material emission
            // For a full game, use a shader-based outline or post-processing highlight
            var renderers = target.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.materials)
                {
                    if (enabled)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", highlightColor * 0.1f);
                    }
                    else
                    {
                        mat.SetColor("_EmissionColor", Color.black);
                    }
                }
            }
        }

        /// <summary>
        /// Forces a raycast update (useful after dimension change).
        /// </summary>
        public void ForceUpdateTarget()
        {
            UpdateRaycast();
        }

        /// <summary>
        /// Gets current look prompt including tool compatibility hints.
        /// </summary>
        public string GetFullPrompt()
        {
            if (_currentTarget == null) return "";

            string prompt = "";

            // Basic interact prompt
            if (_currentInteractable != null && _currentInteractable.CanInteract)
            {
                prompt = $"[E] {_currentInteractable.InteractionPrompt}";
            }

            // Tool hint
            if (hotbar != null && hotbar.HasItemSelected)
            {
                var item = hotbar.SelectedItem;
                if (item.ToolActions != null)
                {
                    foreach (var action in item.ToolActions)
                    {
                        if (action != null && _currentToolTarget != null && 
                            _currentToolTarget.AcceptsToolAction(action.GetType()))
                        {
                            if (!string.IsNullOrEmpty(prompt)) prompt += "\n";
                            prompt += $"[LMB] Use {action.ActionName}";
                            break;
                        }
                    }
                }
                
                // Key hint
                if (item is KeyItemDefinition && _currentKeyTarget != null && _currentKeyTarget.IsLocked)
                {
                    if (!string.IsNullOrEmpty(prompt)) prompt += "\n";
                    prompt += "[LMB] Unlock";
                }
            }

            return prompt;
        }
    }
}
