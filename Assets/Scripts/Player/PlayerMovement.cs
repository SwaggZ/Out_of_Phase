using UnityEngine;
using UnityEngine.InputSystem;

namespace OutOfPhase.Player
{
    /// <summary>
    /// Smooth first-person CharacterController movement with acceleration/deceleration,
    /// jumping, gravity, coyote time, jump buffer, and slope handling.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Walking speed in units/second")]
        [SerializeField] private float walkSpeed = 5f;
        
        [Tooltip("Sprinting speed in units/second")]
        [SerializeField] private float sprintSpeed = 8f;
        
        [Tooltip("How fast the player accelerates to target speed")]
        [SerializeField] private float acceleration = 10f;
        
        [Tooltip("How fast the player decelerates when no input")]
        [SerializeField] private float deceleration = 12f;
        
        [Tooltip("Multiplier for air control (0 = no air control, 1 = full)")]
        [Range(0f, 1f)]
        [SerializeField] private float airControlMultiplier = 0.4f;

        [Header("Jumping")]
        [Tooltip("Jump height in units")]
        [SerializeField] private float jumpHeight = 1.2f;
        
        [Tooltip("Gravity multiplier (Earth = 1)")]
        [SerializeField] private float gravityMultiplier = 2f;
        
        [Tooltip("Extra gravity multiplier when falling (makes falls feel snappier)")]
        [SerializeField] private float fallMultiplier = 2.5f;
        
        [Tooltip("Maximum fall speed")]
        [SerializeField] private float terminalVelocity = 50f;
        
        [Tooltip("Time after leaving ground where jump is still allowed")]
        [SerializeField] private float coyoteTime = 0.15f;
        
        [Tooltip("Time before landing where jump input is buffered")]
        [SerializeField] private float jumpBufferTime = 0.1f;

        [Header("Ground Check")]
        [Tooltip("Radius of ground check sphere")]
        [SerializeField] private float groundCheckRadius = 0.3f;
        
        [Tooltip("Offset below player center for ground check")]
        [SerializeField] private float groundCheckOffset = 0.1f;
        
        [Tooltip("Layers considered as ground")]
        [SerializeField] private LayerMask groundLayers = ~0;
        
        [Tooltip("Max slope angle the player can walk on")]
        [SerializeField] private float maxSlopeAngle = 45f;

        [Header("Step Handling")]
        [Tooltip("Max step height player can climb")]
        [SerializeField] private float stepHeight = 0.3f;

        // Components
        private CharacterController _controller;
        private PlayerInputActions _inputActions;

        // State
        private Vector3 _velocity;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private bool _isGrounded;
        private bool _wasGroundedLastFrame;
        private bool _hasJumped;
        private float _lastGroundedTime;
        private float _jumpTime;
        private float _lastJumpInputTime;
        private bool _isSprinting;
        private Vector2 _moveInput;
        private float _currentSlopeAngle;

        // Cached values
        private float _gravity;
        private float _jumpVelocity;

        // Public accessors for other systems
        public bool IsGrounded => _isGrounded;
        public bool IsSprinting => _isSprinting && _moveInput.magnitude > 0.1f;
        public Vector3 Velocity => _velocity;
        public float CurrentSpeed => _horizontalVelocity.magnitude;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _inputActions = new PlayerInputActions();
            
            // Set step offset
            _controller.stepOffset = stepHeight;
            
            // Ensure player is on its own layer so ground check doesn't self-detect
            // If groundLayers includes everything, exclude the player's layer
            if (groundLayers == ~0)
            {
                groundLayers = ~(1 << gameObject.layer);
            }
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            _inputActions.Player.Jump.performed += OnJumpInput;
            _inputActions.Player.Sprint.started += OnSprintStarted;
            _inputActions.Player.Sprint.canceled += OnSprintCanceled;
        }

        private void OnDisable()
        {
            _inputActions.Player.Jump.performed -= OnJumpInput;
            _inputActions.Player.Sprint.started -= OnSprintStarted;
            _inputActions.Player.Sprint.canceled -= OnSprintCanceled;
            _inputActions.Player.Disable();
        }

        private void Start()
        {
            // Cache gravity and jump velocity
            CalculatePhysicsValues();
            
            // Lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            ReadInput();
            UpdateGroundCheck();
            UpdateTimers();
            HandleMovement();
            HandleJump();
            ApplyGravity();
            ApplyMovement();
        }

        /// <summary>
        /// Recalculates jump velocity based on jump height and gravity.
        /// Call this if you change jump height or gravity at runtime.
        /// </summary>
        public void CalculatePhysicsValues()
        {
            _gravity = Physics.gravity.y * gravityMultiplier;
            // v = sqrt(2 * g * h) - derived from kinematic equations
            _jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(_gravity) * jumpHeight);
        }

        private void ReadInput()
        {
            _moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
        }

        private void UpdateGroundCheck()
        {
            _wasGroundedLastFrame = _isGrounded;
            
            // After jumping, ignore ground checks briefly so we actually leave the ground
            if (_hasJumped && Time.time - _jumpTime < 0.15f)
            {
                _isGrounded = false;
                return;
            }
            
            // Sphere cast for ground check
            Vector3 spherePosition = transform.position + Vector3.down * groundCheckOffset;
            _isGrounded = Physics.CheckSphere(spherePosition, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);
            
            // Also check CharacterController's built-in ground check
            _isGrounded = _isGrounded || _controller.isGrounded;
            
            // Check slope angle
            if (_isGrounded)
            {
                CheckSlopeAngle();
            }
            
            // Update grounded time for coyote time
            if (_isGrounded)
            {
                _lastGroundedTime = Time.time;
                _hasJumped = false;
            }
        }

        private void CheckSlopeAngle()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, _controller.height / 2f + 0.5f, groundLayers))
            {
                _currentSlopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                
                // If slope is too steep, treat as not grounded for movement purposes
                if (_currentSlopeAngle > maxSlopeAngle)
                {
                    _isGrounded = false;
                }
            }
        }

        private void UpdateTimers()
        {
            // Nothing special here for now, timers update naturally with Time.time comparisons
        }

        private void HandleMovement()
        {
            // Get input direction relative to player facing
            Vector3 inputDirection = new Vector3(_moveInput.x, 0f, _moveInput.y);
            Vector3 worldDirection = transform.TransformDirection(inputDirection);
            
            // Determine target speed
            float targetSpeed = _isSprinting ? sprintSpeed : walkSpeed;
            Vector3 targetVelocity = worldDirection * targetSpeed;
            
            // Calculate acceleration/deceleration rate
            float accelRate;
            if (_isGrounded)
            {
                accelRate = inputDirection.magnitude > 0.1f ? acceleration : deceleration;
            }
            else
            {
                // Reduced control in air
                accelRate = (inputDirection.magnitude > 0.1f ? acceleration : deceleration) * airControlMultiplier;
            }
            
            // Smoothly interpolate horizontal velocity
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity,
                targetVelocity,
                accelRate * Time.deltaTime
            );
        }

        private void HandleJump()
        {
            // Check for buffered jump
            bool hasBufferedJump = Time.time - _lastJumpInputTime <= jumpBufferTime;
            
            // Only allow jumping if truly grounded right now (not via coyote after a jump)
            bool canJump = _isGrounded && !_hasJumped;
            
            // Also allow coyote jump if we walked off an edge (not from jumping)
            if (!canJump && !_hasJumped)
            {
                bool canCoyoteJump = Time.time - _lastGroundedTime <= coyoteTime;
                canJump = canCoyoteJump;
            }
            
            if (hasBufferedJump && canJump)
            {
                _verticalVelocity = _jumpVelocity;
                _lastJumpInputTime = -1f; // Clear buffer
                _lastGroundedTime = -1f; // Prevent coyote after jump
                _hasJumped = true;
                _jumpTime = Time.time;

                // Trigger jump sound on FootstepController if present
                var footsteps = GetComponent<FootstepController>();
                if (footsteps != null) footsteps.PlayJump();
            }
        }

        private void ApplyGravity()
        {
            if (_isGrounded && _verticalVelocity < 0f)
            {
                // Small downward force to keep grounded
                _verticalVelocity = -2f;
            }
            else
            {
                // Apply stronger gravity when falling for snappy, realistic feel
                float gravityThisFrame = _gravity;
                if (_verticalVelocity < 0f)
                {
                    gravityThisFrame *= fallMultiplier;
                }
                
                _verticalVelocity += gravityThisFrame * Time.deltaTime;
                
                // Clamp to terminal velocity
                if (_verticalVelocity < -terminalVelocity)
                {
                    _verticalVelocity = -terminalVelocity;
                }
            }
        }

        private void ApplyMovement()
        {
            // Combine horizontal and vertical velocities
            _velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            
            // Move the character
            _controller.Move(_velocity * Time.deltaTime);
        }

        #region Input Callbacks

        private void OnJumpInput(InputAction.CallbackContext context)
        {
            _lastJumpInputTime = Time.time;
        }

        private void OnSprintStarted(InputAction.CallbackContext context)
        {
            _isSprinting = true;
        }

        private void OnSprintCanceled(InputAction.CallbackContext context)
        {
            _isSprinting = false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets movement enabled/disabled (for menus, dialogue, etc.)
        /// </summary>
        public void SetMovementEnabled(bool enabled)
        {
            if (enabled)
                _inputActions.Player.Enable();
            else
                _inputActions.Player.Disable();
        }

        /// <summary>
        /// Applies an external force to the player (knockback, launch pads, etc.)
        /// </summary>
        public void AddForce(Vector3 force)
        {
            _horizontalVelocity += new Vector3(force.x, 0f, force.z);
            _verticalVelocity += force.y;
        }

        /// <summary>
        /// Updates walk speed at runtime (for settings)
        /// </summary>
        public void SetWalkSpeed(float speed)
        {
            walkSpeed = speed;
        }

        /// <summary>
        /// Updates sprint speed at runtime
        /// </summary>
        public void SetSprintSpeed(float speed)
        {
            sprintSpeed = speed;
        }

        #endregion

        #region Editor Visualization

        private void OnDrawGizmosSelected()
        {
            // Draw ground check sphere
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Vector3 spherePosition = transform.position + Vector3.down * groundCheckOffset;
            Gizmos.DrawWireSphere(spherePosition, groundCheckRadius);
        }

        #endregion
    }
}
