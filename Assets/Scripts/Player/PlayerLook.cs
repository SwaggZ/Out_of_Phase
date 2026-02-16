using UnityEngine;
using UnityEngine.InputSystem;

namespace OutOfPhase.Player
{
    /// <summary>
    /// First-person camera controller with smoothing, sensitivity, and vertical clamping.
    /// Attach to the Player object; assign the Camera as cameraTransform.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The camera transform to rotate vertically")]
        [SerializeField] private Transform cameraTransform;

        [Header("Sensitivity")]
        [Tooltip("Mouse sensitivity multiplier")]
        [SerializeField] private float sensitivity = 2f;
        
        [Tooltip("Separate sensitivity multiplier for vertical look")]
        [SerializeField] private float verticalSensitivityMultiplier = 1f;

        [Header("Smoothing")]
        [Tooltip("Enable smooth camera movement")]
        [SerializeField] private bool enableSmoothing = true;
        
        [Tooltip("Smoothing factor (lower = smoother, higher = more responsive)")]
        [Range(5f, 30f)]
        [SerializeField] private float smoothingSpeed = 15f;

        [Header("Vertical Clamp")]
        [Tooltip("Minimum vertical angle (looking up)")]
        [SerializeField] private float minVerticalAngle = -89f;
        
        [Tooltip("Maximum vertical angle (looking down)")]
        [SerializeField] private float maxVerticalAngle = 89f;

        [Header("FOV")]
        [Tooltip("Camera field of view")]
        [SerializeField] private float fieldOfView = 75f;
        
        [Tooltip("Enable smooth FOV transitions")]
        [SerializeField] private bool smoothFOVTransition = true;
        
        [Tooltip("Speed of FOV transitions")]
        [SerializeField] private float fovTransitionSpeed = 10f;

        // Input
        private PlayerInputActions _inputActions;
        private Vector2 _lookInput;

        // State
        private float _targetYaw;
        private float _targetPitch;
        private float _currentYaw;
        private float _currentPitch;
        private float _targetFOV;
        private bool _isLookEnabled = true;

        // Camera component (cached)
        private Camera _camera;

        // Public accessors
        public float Sensitivity
        {
            get => sensitivity;
            set => sensitivity = value;
        }

        public float FieldOfView
        {
            get => fieldOfView;
            set
            {
                fieldOfView = value;
                _targetFOV = value;
            }
        }

        public bool IsLookEnabled => _isLookEnabled;

        private void Awake()
        {
            _inputActions = new PlayerInputActions();
            
            // Auto-find camera if not assigned
            if (cameraTransform == null)
            {
                _camera = GetComponentInChildren<Camera>();
                if (_camera != null)
                {
                    cameraTransform = _camera.transform;
                }
                else
                {
                    Debug.LogError("PlayerLook: No camera transform assigned and no Camera found in children!");
                }
            }
            else
            {
                _camera = cameraTransform.GetComponent<Camera>();
            }
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Player.Disable();
        }

        private void Start()
        {
            // Initialize rotation from current transform
            _currentYaw = transform.eulerAngles.y;
            _targetYaw = _currentYaw;
            
            if (cameraTransform != null)
            {
                _currentPitch = cameraTransform.localEulerAngles.x;
                // Convert from 0-360 to -180-180 range
                if (_currentPitch > 180f)
                    _currentPitch -= 360f;
                _targetPitch = _currentPitch;
            }

            // Initialize FOV
            _targetFOV = fieldOfView;
            ApplyFOV(fieldOfView);

            // Apply saved settings if SettingsManager is available
            if (UI.SettingsManager.Instance != null)
            {
                var s = UI.SettingsManager.Instance.Current;
                SetSensitivity(s.mouseSensitivity);
                SetFOV(s.fov);
            }
        }

        private void Update()
        {
            if (!_isLookEnabled) return;

            ReadInput();
            CalculateRotation();
            ApplyRotation();
            UpdateFOV();
        }

        private void ReadInput()
        {
            _lookInput = _inputActions.Player.Look.ReadValue<Vector2>();
        }

        private void CalculateRotation()
        {
            // Apply sensitivity
            float horizontalInput = _lookInput.x * sensitivity;
            float verticalInput = _lookInput.y * sensitivity * verticalSensitivityMultiplier;

            // Update target rotation
            _targetYaw += horizontalInput;
            _targetPitch -= verticalInput; // Inverted for natural feel

            // Clamp vertical rotation
            _targetPitch = Mathf.Clamp(_targetPitch, minVerticalAngle, maxVerticalAngle);
        }

        private void ApplyRotation()
        {
            if (enableSmoothing)
            {
                // Smooth interpolation
                _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, smoothingSpeed * Time.deltaTime);
                _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, smoothingSpeed * Time.deltaTime);
            }
            else
            {
                // Instant
                _currentYaw = _targetYaw;
                _currentPitch = _targetPitch;
            }

            // Apply horizontal rotation to player body
            transform.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);

            // Apply vertical rotation to camera
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(_currentPitch, 0f, 0f);
            }
        }

        private void UpdateFOV()
        {
            if (_camera == null) return;

            if (smoothFOVTransition)
            {
                float currentFOV = _camera.fieldOfView;
                if (!Mathf.Approximately(currentFOV, _targetFOV))
                {
                    _camera.fieldOfView = Mathf.Lerp(currentFOV, _targetFOV, fovTransitionSpeed * Time.deltaTime);
                }
            }
            else
            {
                _camera.fieldOfView = _targetFOV;
            }
        }

        private void ApplyFOV(float fov)
        {
            if (_camera != null)
            {
                _camera.fieldOfView = fov;
            }
        }

        #region Public Methods

        /// <summary>
        /// Enable or disable camera look (for menus, dialogue, dimension wheel, etc.)
        /// </summary>
        public void SetLookEnabled(bool enabled)
        {
            _isLookEnabled = enabled;
        }

        /// <summary>
        /// Updates sensitivity at runtime (for settings)
        /// </summary>
        public void SetSensitivity(float newSensitivity)
        {
            sensitivity = newSensitivity;
        }

        /// <summary>
        /// Updates FOV at runtime (for settings)
        /// </summary>
        public void SetFOV(float newFOV)
        {
            fieldOfView = newFOV;
            _targetFOV = newFOV;
        }

        /// <summary>
        /// Temporarily change FOV (e.g., sprint FOV boost)
        /// </summary>
        public void SetTemporaryFOV(float tempFOV)
        {
            _targetFOV = tempFOV;
        }

        /// <summary>
        /// Reset FOV to base value
        /// </summary>
        public void ResetFOV()
        {
            _targetFOV = fieldOfView;
        }

        /// <summary>
        /// Instantly snap to a rotation (for teleporting, respawning)
        /// </summary>
        public void SnapToRotation(float yaw, float pitch)
        {
            _targetYaw = yaw;
            _targetPitch = pitch;
            _currentYaw = yaw;
            _currentPitch = pitch;
            ApplyRotation();
        }

        /// <summary>
        /// Add rotation offset (for recoil, screen shake, etc.)
        /// </summary>
        public void AddRotationOffset(float yawOffset, float pitchOffset)
        {
            _targetYaw += yawOffset;
            _targetPitch += pitchOffset;
            _targetPitch = Mathf.Clamp(_targetPitch, minVerticalAngle, maxVerticalAngle);
        }

        /// <summary>
        /// Enable or disable smoothing
        /// </summary>
        public void SetSmoothingEnabled(bool enabled)
        {
            enableSmoothing = enabled;
        }

        #endregion
    }
}
