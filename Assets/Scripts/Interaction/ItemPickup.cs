using UnityEngine;
using OutOfPhase.Inventory;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// World item pickup - grants an item when interacted with.
    /// Attach to a world object that the player can pick up.
    /// </summary>
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [Header("Item Settings")]
        [SerializeField] private Items.ItemDefinition item;
        [SerializeField] private int quantity = 1;
        
        [Header("Pickup Settings")]
        [Tooltip("Destroy object after pickup?")]
        [SerializeField] private bool destroyOnPickup = true;
        
        [Tooltip("Just disable instead of destroy?")]
        [SerializeField] private bool disableInsteadOfDestroy = false;
        
        [Tooltip("Sound to play on pickup")]
        [SerializeField] private AudioClip pickupSound;

        [Header("Respawn (if not destroyed)")]
        [SerializeField] private bool canRespawn = false;
        [SerializeField] private float respawnTime = 30f;

        private bool _isPickedUp;
        private float _respawnTimer;
        private MeshRenderer[] _renderers;
        private Collider _collider;

        // IInteractable
        public string InteractionPrompt => item != null ? $"Pick up {item.ItemName}" : "Pick up";
        public bool CanInteract => !_isPickedUp && item != null;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<MeshRenderer>();
            _collider = GetComponent<Collider>();
        }

        private void Update()
        {
            if (_isPickedUp && canRespawn)
            {
                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0)
                {
                    Respawn();
                }
            }
        }

        public void Interact(InteractionContext context)
        {
            if (_isPickedUp || item == null) return;
            if (context.Inventory == null) return;

            // Try to add to inventory
            int remaining = context.Inventory.TryAddItem(item, quantity);
            
            if (remaining < quantity) // At least some were added
            {
                int added = quantity - remaining;
                Debug.Log($"Picked up {added}x {item.ItemName}");
                
                // Play sound
                if (pickupSound != null)
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                }
                
                if (remaining > 0)
                {
                    // Partial pickup - update quantity
                    quantity = remaining;
                    Debug.Log($"Inventory full! {remaining}x {item.ItemName} left");
                }
                else
                {
                    // Full pickup
                    HandlePickedUp();
                }
            }
            else
            {
                Debug.Log("Inventory full!");
            }
        }

        private void HandlePickedUp()
        {
            _isPickedUp = true;

            if (destroyOnPickup)
            {
                if (disableInsteadOfDestroy)
                {
                    gameObject.SetActive(false);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
            else if (canRespawn)
            {
                // Hide but keep for respawn
                SetVisible(false);
                _respawnTimer = respawnTime;
            }
            else
            {
                SetVisible(false);
            }
        }

        private void Respawn()
        {
            _isPickedUp = false;
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (_renderers != null)
            {
                foreach (var r in _renderers)
                {
                    r.enabled = visible;
                }
            }
            if (_collider != null)
            {
                _collider.enabled = visible;
            }
        }

        /// <summary>
        /// Sets the item this pickup grants (for runtime spawning).
        /// </summary>
        public void SetItem(Items.ItemDefinition newItem, int newQuantity = 1)
        {
            item = newItem;
            quantity = newQuantity;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
