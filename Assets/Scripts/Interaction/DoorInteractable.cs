using UnityEngine;
using OutOfPhase.Dimension;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Simple open/close door that can be locked by key.
    /// </summary>
    public class DoorInteractable : MonoBehaviour, IInteractable, IKeyTarget
    {
        [Header("Door Settings")]
        [SerializeField] private string doorName = "Door";
        [SerializeField] private Transform doorRoot;
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 6f;
        [SerializeField] private bool startOpen = false;
        [SerializeField] private bool canClose = true;
        [SerializeField] private bool openAwayFromPlayer = true;

        [Header("Lock Settings")]
        [SerializeField] private bool startLocked = false;
        [SerializeField] private string lockId = "";

        [Header("Audio")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip lockedSound;
        [SerializeField] private AudioClip unlockSound;

        private bool _isOpen;
        private bool _isLocked;
        private Quaternion _closedRotation;
        private Quaternion _targetRotation;
        private bool _isAnimating;

        public string InteractionPrompt
        {
            get
            {
                if (_isLocked) return "Locked";
                string action = _isOpen ? "Close" : "Open";
                return string.IsNullOrEmpty(doorName) ? action : $"{action} {doorName}";
            }
        }

        public bool CanInteract
        {
            get
            {
                if (_isOpen && !canClose) return false;
                return true;
            }
        }

        public string LockId => lockId;
        public bool IsLocked => _isLocked;

        private void Awake()
        {
            if (doorRoot == null)
                doorRoot = transform;

            _closedRotation = doorRoot.localRotation;
            _isOpen = startOpen;
            _isLocked = startLocked;

            if (_isOpen)
            {
                _targetRotation = GetOpenRotation(null);
                doorRoot.localRotation = _targetRotation;
            }
        }

        private void Update()
        {
            if (!_isAnimating) return;

            doorRoot.localRotation = Quaternion.Slerp(
                doorRoot.localRotation,
                _targetRotation,
                openSpeed * Time.deltaTime
            );

            if (Quaternion.Angle(doorRoot.localRotation, _targetRotation) <= 0.5f)
            {
                doorRoot.localRotation = _targetRotation;
                _isAnimating = false;
            }
        }

        public void Interact(InteractionContext context)
        {
            if (_isLocked)
            {
                if (lockedSound != null)
                    SFXPlayer.PlayAtPoint(lockedSound, transform.position);
                return;
            }

            if (_isOpen && !canClose) return;

            ToggleDoor(context.PlayerTransform);
        }

        public bool TryUnlock(Items.KeyItemDefinition key)
        {
            if (!_isLocked) return true;
            if (key == null) return false;
            if (!key.CanUnlock(lockId)) return false;

            _isLocked = false;

            if (unlockSound != null)
                SFXPlayer.PlayAtPoint(unlockSound, transform.position);

            return true;
        }

        /// <summary>
        /// Unlock the door and open it (used by PressurePlate).
        /// </summary>
        public void UnlockAndOpen()
        {
            _isLocked = false;

            if (!_isOpen)
            {
                _isOpen = true;
                _targetRotation = GetOpenRotation(null);
                _isAnimating = true;

                if (unlockSound != null)
                    SFXPlayer.PlayAtPoint(unlockSound, transform.position);
                else if (openSound != null)
                    SFXPlayer.PlayAtPoint(openSound, transform.position);
            }
        }

        /// <summary>
        /// Close and re-lock the door (used by PressurePlate on release).
        /// </summary>
        public void LockAndClose()
        {
            if (_isOpen)
            {
                _isOpen = false;
                _targetRotation = _closedRotation;
                _isAnimating = true;

                if (closeSound != null)
                    SFXPlayer.PlayAtPoint(closeSound, transform.position);
            }

            _isLocked = startLocked; // restore original lock state
        }

        private void ToggleDoor(Transform player)
        {
            _isOpen = !_isOpen;

            _targetRotation = _isOpen ? GetOpenRotation(player) : _closedRotation;
            _isAnimating = true;

            var clip = _isOpen ? openSound : closeSound;
            if (clip != null)
                SFXPlayer.PlayAtPoint(clip, transform.position);
        }

        private Quaternion GetOpenRotation(Transform player)
        {
            float angle = openAngle;

            if (openAwayFromPlayer && player != null)
            {
                Vector3 toPlayer = player.position - doorRoot.position;
                float dot = Vector3.Dot(doorRoot.right, toPlayer.normalized);
                angle = dot >= 0f ? openAngle : -openAngle;
            }

            return _closedRotation * Quaternion.Euler(0f, angle, 0f);
        }
    }
}
