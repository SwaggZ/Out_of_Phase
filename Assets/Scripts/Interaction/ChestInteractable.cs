using UnityEngine;
using OutOfPhase.Items;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// A loot chest — works like MineableRock/DigSpot but no tool required.
    /// Press E to open: items go to inventory (overflow spawns as pickups), then the chest is destroyed.
    /// </summary>
    public class ChestInteractable : MonoBehaviour, IInteractable
    {
        [System.Serializable]
        public struct LootEntry
        {
            public ItemDefinition Item;
            public int Quantity;
        }

        [Header("Chest Settings")]
        [SerializeField] private string chestName = "Chest";
        [SerializeField] private bool destroyOnOpen = true;

        [Header("Loot")]
        [SerializeField] private LootEntry[] drops;

        [Header("VFX/SFX")]
        [SerializeField] private GameObject openVFXPrefab;
        [SerializeField] private AudioClip openSound;

        private bool _opened;

        // IInteractable
        public string InteractionPrompt => _opened ? "Empty" : $"Open {chestName}";
        public bool CanInteract => !_opened;

        public void Interact(InteractionContext context)
        {
            if (_opened) return;
            _opened = true;

            if (openSound != null)
                AudioSource.PlayClipAtPoint(openSound, transform.position);

            if (openVFXPrefab != null)
            {
                var vfx = Instantiate(openVFXPrefab, transform.position, Quaternion.identity);
                Destroy(vfx, 2f);
            }

            // Spawn all items as world pickups, spread out in a circle
            if (drops != null)
            {
                float angleStep = drops.Length > 1 ? 360f / drops.Length : 0f;
                for (int i = 0; i < drops.Length; i++)
                {
                    var drop = drops[i];
                    if (drop.Item == null || drop.Quantity <= 0) continue;

                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.6f;
                    SpawnPickup(drop.Item, drop.Quantity, offset);
                }
            }

            if (destroyOnOpen)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }

        private void SpawnPickup(ItemDefinition item, int quantity, Vector3 offset)
        {
            if (item.WorldPrefab == null) return;

            var instance = Instantiate(item.WorldPrefab,
                transform.position + Vector3.up * 0.5f + offset,
                Quaternion.identity);

            var pickup = instance.GetComponent<ItemPickup>();
            if (pickup == null)
                pickup = instance.AddComponent<ItemPickup>();
            pickup.SetItem(item, quantity);

            // Gentle upward pop, no horizontal force
            var rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.mass = 0.5f;
            rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
    }
}

