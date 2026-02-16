using UnityEngine;
using OutOfPhase.Items;
using OutOfPhase.Items.ToolActions;
using OutOfPhase.Inventory;

namespace OutOfPhase.Player
{
    /// <summary>
    /// Procedural first-person tool animations.
    /// Attach to the player — animates the heldItemAnchor transform
    /// with swing (pickaxe) or thrust (shovel) motions on use.
    /// Also adds idle bob when walking.
    /// </summary>
    public class HeldItemAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HotbarController hotbar;
        [SerializeField] private Transform heldItemAnchor;
        [SerializeField] private CharacterController characterController;

        [Header("Mining Swing (Pickaxe)")]
        [SerializeField] private float mineSwingDuration = 0.35f;
        [SerializeField] private float mineRaiseHeight = 0.15f;
        [SerializeField] private float mineDropHeight = 0.25f;
        [SerializeField] private float mineForwardThrust = 0.15f;
        [SerializeField] private float mineRotationAngle = 45f;

        [Header("Dig Thrust (Shovel)")]
        [SerializeField] private float digSwingDuration = 0.4f;
        [SerializeField] private float digRaiseHeight = 0.12f;
        [SerializeField] private float digDropHeight = 0.30f;
        [SerializeField] private float digForwardThrust = 0.20f;
        [SerializeField] private float digRotationAngle = 50f;

        [Header("Generic Swing")]
        [SerializeField] private float genericSwingDuration = 0.3f;
        [SerializeField] private float genericBobHeight = 0.15f;
        [SerializeField] private float genericRotationAngle = 25f;

        [Header("Idle Bob")]
        [SerializeField] private float bobSpeed = 8f;
        [SerializeField] private float bobAmountY = 0.01f;
        [SerializeField] private float bobAmountX = 0.005f;

        // Base transform (anchor position after offset applied once)
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private bool _baseCaptured;

        // Animation state
        private bool _isAnimating;
        private float _animTimer;
        private float _animDuration;
        private AnimationType _currentAnim;
        private float _bobTimer;

        private enum AnimationType
        {
            None,
            MineSwing,
            DigThrust,
            GenericSwing
        }

        private void Awake()
        {
            TryFindReferences();
        }

        private void Start()
        {
            TryFindReferences();
            CaptureBaseTransform();
        }

        private void TryFindReferences()
        {
            if (hotbar == null) hotbar = GetComponent<HotbarController>();
            if (hotbar == null) hotbar = GetComponentInParent<HotbarController>();
            if (hotbar == null) hotbar = FindFirstObjectByType<HotbarController>();

            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (characterController == null) characterController = GetComponentInParent<CharacterController>();
            if (characterController == null) characterController = FindFirstObjectByType<CharacterController>();

            if (heldItemAnchor == null && hotbar != null)
            {
                heldItemAnchor = hotbar.HeldItemAnchor;
            }
        }

        private void CaptureBaseTransform()
        {
            if (_baseCaptured || heldItemAnchor == null) return;
            _baseLocalPosition = heldItemAnchor.localPosition;
            _baseLocalRotation = heldItemAnchor.localRotation;
            _baseCaptured = true;
        }

        private void LateUpdate()
        {
            // Retry finding anchor if still null
            if (heldItemAnchor == null)
            {
                TryFindReferences();
                if (heldItemAnchor == null) return;
            }

            CaptureBaseTransform();

            if (_isAnimating)
            {
                UpdateSwingAnimation();
            }
            else
            {
                // Reset to base position when idle
                heldItemAnchor.localPosition = _baseLocalPosition;
                UpdateIdleBob();
            }
        }

        /// <summary>
        /// Triggers the appropriate animation based on tool action type.
        /// Call this from Interactor when a tool is used.
        /// </summary>
        public void PlayToolAnimation(ToolAction action)
        {
            if (action is PickaxeAction)
                StartAnimation(AnimationType.MineSwing, mineSwingDuration);
            else if (action is ShovelAction)
                StartAnimation(AnimationType.DigThrust, digSwingDuration);
            else
                StartAnimation(AnimationType.GenericSwing, genericSwingDuration);
        }

        /// <summary>
        /// Play a generic swing animation (e.g. clicking with no target).
        /// </summary>
        public void PlayGenericSwing()
        {
            var item = hotbar != null ? hotbar.SelectedItem : null;
            if (item == null) return;

            // Pick animation based on equipped tool type
            if (item.HasToolAction<PickaxeAction>())
                StartAnimation(AnimationType.MineSwing, mineSwingDuration);
            else if (item.HasToolAction<ShovelAction>())
                StartAnimation(AnimationType.DigThrust, digSwingDuration);
            else
                StartAnimation(AnimationType.GenericSwing, genericSwingDuration);
        }

        public bool IsAnimating => _isAnimating;

        private void StartAnimation(AnimationType type, float duration)
        {
            _currentAnim = type;
            _animDuration = duration;
            _animTimer = 0f;
            _isAnimating = true;
        }

        private void UpdateSwingAnimation()
        {
            _animTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_animTimer / _animDuration);

            // Swing curve: fast forward (0→0.4), hold briefly (0.4→0.5), return (0.5→1.0)
            float swingT;
            if (t < 0.4f)
                swingT = t / 0.4f; // Wind up to peak
            else if (t < 0.5f)
                swingT = 1f; // Hold at peak
            else
                swingT = 1f - ((t - 0.5f) / 0.5f); // Return

            // Ease in-out
            swingT = swingT * swingT * (3f - 2f * swingT);

            switch (_currentAnim)
            {
                case AnimationType.MineSwing:
                    ApplyMineSwing(swingT);
                    break;
                case AnimationType.DigThrust:
                    ApplyDigThrust(swingT);
                    break;
                case AnimationType.GenericSwing:
                    ApplyGenericSwing(swingT);
                    break;
            }

            if (t >= 1f)
            {
                _isAnimating = false;
                heldItemAnchor.localPosition = _baseLocalPosition;
                heldItemAnchor.localRotation = _baseLocalRotation;
            }
        }

        private void ApplyMineSwing(float t)
        {
            // Raise up + tilt back, then swing down + thrust forward
            float y, z, rot;
            if (t < 0.3f)
            {
                float p = t / 0.3f;
                y = Mathf.Lerp(0f, mineRaiseHeight, p);              // Lift up
                z = 0f;
                rot = Mathf.Lerp(0f, -mineRotationAngle * 0.4f, p);  // Tilt back
            }
            else
            {
                float p = (t - 0.3f) / 0.7f;
                y = Mathf.Lerp(mineRaiseHeight, -mineDropHeight, p); // Swing down
                z = Mathf.Lerp(0f, mineForwardThrust, p);            // Thrust forward
                rot = Mathf.Lerp(-mineRotationAngle * 0.4f, mineRotationAngle, p); // Rotate forward
            }

            heldItemAnchor.localPosition = _baseLocalPosition + new Vector3(0f, y, z);
            heldItemAnchor.localRotation = _baseLocalRotation * Quaternion.Euler(rot, 0f, 0f);
        }

        private void ApplyDigThrust(float t)
        {
            // Raise up + tilt back, then thrust down and forward
            float y, z, rot;
            if (t < 0.25f)
            {
                float p = t / 0.25f;
                y = Mathf.Lerp(0f, digRaiseHeight, p);               // Lift up
                z = Mathf.Lerp(0f, -digForwardThrust * 0.3f, p);     // Pull back slightly
                rot = Mathf.Lerp(0f, -digRotationAngle * 0.3f, p);   // Tilt back
            }
            else
            {
                float p = (t - 0.25f) / 0.75f;
                y = Mathf.Lerp(digRaiseHeight, -digDropHeight, p);   // Thrust down
                z = Mathf.Lerp(-digForwardThrust * 0.3f, digForwardThrust, p); // Thrust forward
                rot = Mathf.Lerp(-digRotationAngle * 0.3f, digRotationAngle, p); // Rotate forward
            }

            heldItemAnchor.localPosition = _baseLocalPosition + new Vector3(0f, y, z);
            heldItemAnchor.localRotation = _baseLocalRotation * Quaternion.Euler(rot, 0f, 0f);
        }

        private void ApplyGenericSwing(float t)
        {
            // Arc motion: up-down with rotation
            float y = Mathf.Sin(t * Mathf.PI) * genericBobHeight;
            float rot = Mathf.Sin(t * Mathf.PI) * genericRotationAngle;

            heldItemAnchor.localPosition = _baseLocalPosition + new Vector3(0f, y, 0f);
            heldItemAnchor.localRotation = _baseLocalRotation * Quaternion.Euler(rot, 0f, 0f);
        }

        private void UpdateIdleBob()
        {
            if (characterController == null) return;

            float speed = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z).magnitude;
            if (speed < 0.1f)
            {
                // Smoothly return rotation to base when stationary
                heldItemAnchor.localRotation = Quaternion.Slerp(
                    heldItemAnchor.localRotation, _baseLocalRotation, Time.deltaTime * 6f);
                return;
            }

            _bobTimer += Time.deltaTime * bobSpeed * Mathf.Clamp01(speed / 4f);

            // Bob via small rotation tilts
            float tiltX = Mathf.Sin(_bobTimer * 2f) * bobAmountY * 100f;
            float tiltZ = Mathf.Sin(_bobTimer) * bobAmountX * 100f;

            Quaternion bobRot = Quaternion.Euler(tiltX, 0f, tiltZ);
            heldItemAnchor.localRotation = _baseLocalRotation * bobRot;
        }
    }
}
