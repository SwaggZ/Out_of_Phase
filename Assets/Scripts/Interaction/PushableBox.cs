using UnityEngine;
using OutOfPhase.Dimension;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// A box the player can pick up and place on a PressurePlate.
    /// Implements IInteractable so the player presses E to grab it.
    /// </summary>
    public class PushableBox : MonoBehaviour, IInteractable
    {
        [Header("Identity")]
        [Tooltip("Display name shown in interaction prompt.")]
        [SerializeField] private string boxName = "Box";

        [Header("Dimension")]
        [Tooltip("If true, the box switches to the player's current dimension when placed.")]
        [SerializeField] private bool syncDimensionOnPlace = true;

        [Tooltip("If true, re-parent the box under the active DimensionContainer root when placed.")]
        [SerializeField] private bool reparentToDimensionRoot = true;

        [Header("Audio")]
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private float soundVolume = 0.5f;

        // ── Static carry state (only one box at a time) ─────────
        private static PushableBox _carriedBox;

        /// <summary>True if the player is currently carrying any box.</summary>
        public static bool IsCarrying => _carriedBox != null;

        /// <summary>The box the player is currently carrying (null if none).</summary>
        public static PushableBox CarriedBox => _carriedBox;

        /// <summary>The prefab/GameObject of this box (used to re-spawn on plate).</summary>
        public GameObject BoxObject => gameObject;

        // IInteractable
        public string InteractionPrompt => $"Pick up {boxName}";
        public bool CanInteract => !IsCarrying;

        public void Interact(InteractionContext context)
        {
            PickUp();
        }

        /// <summary>Pick up this box (hides it and marks as carried).</summary>
        public void PickUp()
        {
            _carriedBox = this;
            gameObject.SetActive(false);

            if (pickupSound != null)
                SFXPlayer.PlayAtPoint(pickupSound, transform.position, soundVolume);
        }

        /// <summary>Place this box at a world position (shows it again).</summary>
        public void PlaceAt(Vector3 position)
        {
            transform.position = position;
            SyncToCurrentDimension();
            gameObject.SetActive(true);
            _carriedBox = null;

            if (pickupSound != null)
                SFXPlayer.PlayAtPoint(pickupSound, position, soundVolume);

            // Kill any physics velocity
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>Clear the carried reference (e.g. on scene unload).</summary>
        public static void ClearCarried()
        {
            _carriedBox = null;
        }

        private void SyncToCurrentDimension()
        {
            if (!syncDimensionOnPlace) return;
            if (DimensionManager.Instance == null) return;

            int currentDimension = DimensionManager.Instance.CurrentDimension;

            var dimensionObject = GetComponent<DimensionObject>();
            if (dimensionObject == null)
            {
                dimensionObject = GetComponentInChildren<DimensionObject>();
            }

            if (dimensionObject != null)
            {
                dimensionObject.SetVisibleDimensions(currentDimension);
            }

            if (!reparentToDimensionRoot) return;

            var container = FindFirstObjectByType<DimensionContainer>();
            if (container == null) return;

            var root = container.GetDimensionRoot(currentDimension);
            if (root != null && transform.parent != root.transform)
            {
                transform.SetParent(root.transform, true);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.3f);
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}
