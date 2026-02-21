using UnityEngine;
using OutOfPhase.Dimension;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Pressure plate that the player places a PushableBox onto.
    /// Interact while carrying a box = place it. Interact when box is on plate = pick it up.
    /// When a box is placed, connected doors unlock and open.
    /// </summary>
    public class PressurePlate : MonoBehaviour, IInteractable
    {
        [Header("Connected Doors")]
        [Tooltip("Doors to unlock and open when a box is placed.")]
        [SerializeField] private DoorInteractable[] connectedDoors;

        [Header("Plate Animation")]
        [Tooltip("How far the plate sinks when pressed (local Y).")]
        [SerializeField] private float pressDepth = 0.05f;

        [Tooltip("How fast the plate animates.")]
        [SerializeField] private float pressSpeed = 8f;

        [Header("Box Placement")]
        [Tooltip("Where the box sits when placed (local offset from plate center). Auto-calculated if zero.")]
        [SerializeField] private Vector3 boxOffset = new Vector3(0f, 0.5f, 0f);

        [Header("Audio")]
        [SerializeField] private AudioClip pressSound;
        [SerializeField] private AudioClip releaseSound;
        [SerializeField] private float soundVolume = 0.5f;

        private bool _isPressed;
        private Vector3 _originalLocalPos;
        private Vector3 _pressedLocalPos;
        private PushableBox _placedBox;

        // IInteractable
        public string InteractionPrompt
        {
            get
            {
                if (_isPressed && _placedBox != null)
                    return "Pick up box";
                if (PushableBox.IsCarrying)
                    return "Place box";
                return "Needs a box";
            }
        }

        public bool CanInteract
        {
            get
            {
                // Can place if carrying a box and plate is empty
                if (PushableBox.IsCarrying && !_isPressed) return true;
                // Can pick up if plate has a box
                if (_isPressed && _placedBox != null) return true;
                return false;
            }
        }

        private void Awake()
        {
            _originalLocalPos = transform.localPosition;
            _pressedLocalPos = _originalLocalPos + Vector3.down * pressDepth;
        }

        private void Update()
        {
            // Animate plate position
            Vector3 target = _isPressed ? _pressedLocalPos : _originalLocalPos;
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, target, pressSpeed * Time.deltaTime);

            // Check if placed box was removed/destroyed
            if (_isPressed && _placedBox != null)
            {
                // Check if box was destroyed
                if (_placedBox == null || _placedBox.gameObject == null)
                {
                    ReleaseWithoutPickup();
                }
                // Check if box moved too far from plate
                else if (Vector3.Distance(transform.position + boxOffset, _placedBox.transform.position) > 1f)
                {
                    ReleaseWithoutPickup();
                }
            }
        }

        public void Interact(InteractionContext context)
        {
            if (PushableBox.IsCarrying && !_isPressed)
            {
                PlaceBox(PushableBox.CarriedBox);
            }
            else if (_isPressed && _placedBox != null)
            {
                RemoveBox();
            }
        }

        private void PlaceBox(PushableBox box)
        {
            _placedBox = box;
            Vector3 placePos = transform.position + boxOffset;
            box.PlaceAt(placePos);
            _isPressed = true;

            if (pressSound != null)
                SFXPlayer.PlayAtPoint(pressSound, transform.position, soundVolume);

            // Open connected doors
            foreach (var door in connectedDoors)
            {
                if (door != null)
                    door.UnlockAndOpen();
            }
        }

        private void RemoveBox()
        {
            _placedBox.PickUp();
            _placedBox = null;
            _isPressed = false;

            if (releaseSound != null)
                SFXPlayer.PlayAtPoint(releaseSound, transform.position, soundVolume);

            // Close connected doors
            foreach (var door in connectedDoors)
            {
                if (door != null)
                    door.LockAndClose();
            }
        }

        /// <summary>
        /// Release the plate when box is removed without player interaction
        /// (e.g., box destroyed, pushed off, or moved away).
        /// </summary>
        private void ReleaseWithoutPickup()
        {
            _placedBox = null;
            _isPressed = false;

            if (releaseSound != null)
                SFXPlayer.PlayAtPoint(releaseSound, transform.position, soundVolume);

            // Close connected doors
            foreach (var door in connectedDoors)
            {
                if (door != null)
                    door.LockAndClose();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _isPressed
                ? new Color(0.2f, 1f, 0.3f, 0.4f)
                : new Color(1f, 1f, 0.2f, 0.4f);

            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }

            // Box placement preview
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.3f);
            Gizmos.DrawWireCube(transform.position + boxOffset, Vector3.one * 0.5f);

            // Lines to connected doors
            if (connectedDoors == null) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            foreach (var door in connectedDoors)
            {
                if (door != null)
                    Gizmos.DrawLine(transform.position, door.transform.position);
            }
        }
    }
}
