using UnityEngine;
using UnityEngine.InputSystem;
using OutOfPhase.Items;
using OutOfPhase.Inventory;
using OutOfPhase.Player;
using OutOfPhase.Dimension;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Handles dropping/throwing the currently held item onto the ground.
    /// Press Q to drop 1 of the selected item in front of the player.
    /// Requires a WorldPrefab on the ItemDefinition to spawn the pickup.
    /// </summary>
    public class ItemDropper : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HotbarController hotbar;

        [Header("Drop Settings")]
        [Tooltip("How far in front of the player the item spawns")]
        [SerializeField] private float dropDistance = 1.5f;

        [Tooltip("Height offset from player feet for the drop")]
        [SerializeField] private float dropHeight = 0.5f;

        [Tooltip("Forward toss force applied to dropped item")]
        [SerializeField] private float tossForce = 3f;

        [Tooltip("Upward toss force applied to dropped item")]
        [SerializeField] private float tossUpForce = 2f;

        [Tooltip("Cooldown between drops (seconds)")]
        [SerializeField] private float dropCooldown = 0.3f;

        [Header("Audio")]
        [Tooltip("Sound played when dropping an item")]
        [SerializeField] private AudioClip dropSound;
        [SerializeField] private float dropSoundVolume = 0.5f;

        private PlayerInputActions _inputActions;
        private float _lastDropTime;

        private void Awake()
        {
            _inputActions = new PlayerInputActions();

            if (hotbar == null) hotbar = GetComponent<HotbarController>();
            if (hotbar == null) hotbar = GetComponentInParent<HotbarController>();
            if (hotbar == null) hotbar = FindFirstObjectByType<HotbarController>();
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            _inputActions.Player.DropItem.performed += OnDropInput;
        }

        private void OnDisable()
        {
            _inputActions.Player.DropItem.performed -= OnDropInput;
            _inputActions.Player.Disable();
        }

        private void OnDropInput(InputAction.CallbackContext context)
        {
            Debug.Log("[ItemDropper] Q pressed — DropItem input received");
            if (Time.time - _lastDropTime < dropCooldown)
            {
                Debug.Log("[ItemDropper] On cooldown, ignoring");
                return;
            }
            DropSelectedItem();
        }

        /// <summary>
        /// Drops one of the currently selected item onto the ground.
        /// </summary>
        public void DropSelectedItem()
        {
            if (hotbar == null || hotbar.Inventory == null)
            {
                Debug.LogWarning("[ItemDropper] hotbar or inventory is null");
                return;
            }

            var slot = hotbar.SelectedInventorySlot;
            if (slot == null || slot.IsEmpty)
            {
                Debug.Log("[ItemDropper] Selected slot is empty, nothing to drop");
                return;
            }

            var item = slot.Item;

            // Need a world prefab to spawn
            if (item.WorldPrefab == null)
            {
                Debug.LogWarning($"Cannot drop {item.ItemName} — no WorldPrefab assigned.");
                return;
            }

            // Calculate drop position: in front of camera
            var cam = Camera.main;
            if (cam == null) cam = GameObject.Find("PlayerCamera")?.GetComponent<Camera>();
            if (cam == null)
            {
                Debug.LogWarning("[ItemDropper] No camera found!");
                return;
            }

            Debug.Log($"[ItemDropper] Dropping {item.ItemName} in front of player");

            Vector3 dropPos = cam.transform.position
                + cam.transform.forward * dropDistance
                + Vector3.up * dropHeight;

            // Raycast down to find ground level
            if (Physics.Raycast(dropPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
            {
                dropPos.y = hit.point.y + 0.1f; // Slightly above ground
            }

            // Spawn the world prefab
            GameObject dropped = Instantiate(item.WorldPrefab, dropPos, Quaternion.identity);

            // Add or configure ItemPickup component so it can be picked back up
            var pickup = dropped.GetComponent<ItemPickup>();
            if (pickup == null)
            {
                pickup = dropped.AddComponent<ItemPickup>();
            }
            pickup.SetItem(item, 1);

            // Ensure it has a collider for interaction
            if (dropped.GetComponent<Collider>() == null)
            {
                var box = dropped.AddComponent<BoxCollider>();
                // Auto-size from renderers
                var renderer = dropped.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    box.center = dropped.transform.InverseTransformPoint(renderer.bounds.center);
                    box.size = dropped.transform.InverseTransformVector(renderer.bounds.size);
                }
            }

            // Ensure rigidbody is active for physics toss
            var rb = dropped.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = dropped.AddComponent<Rigidbody>();
            }
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.mass = 0.5f;
            rb.linearDamping = 1f;
            rb.angularDamping = 2f;

            // Toss it forward and slightly up
            Vector3 tossDir = cam.transform.forward * tossForce + Vector3.up * tossUpForce;
            rb.AddForce(tossDir, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);

            // Play drop sound
            if (dropSound != null)
                SFXPlayer.PlayAtPoint(dropSound, dropPos, dropSoundVolume);

            // Remove one from inventory
            hotbar.Inventory.RemoveFromSlot(hotbar.SelectedSlot, 1);

            _lastDropTime = Time.time;
        }
    }
}
