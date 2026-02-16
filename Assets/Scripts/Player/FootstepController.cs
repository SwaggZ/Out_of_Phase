using UnityEngine;

namespace OutOfPhase.Player
{
    /// <summary>
    /// Plays footstep sounds based on movement speed and ground state.
    /// Attach to the same GameObject as PlayerMovement.
    /// Assign footstep clips in the Inspector; supports multiple clips for variety.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class FootstepController : MonoBehaviour
    {
        [Header("Footstep Clips")]
        [Tooltip("Array of footstep clips — a random one is chosen each step")]
        [SerializeField] private AudioClip[] footstepClips;

        [Tooltip("Played when the player jumps")]
        [SerializeField] private AudioClip jumpClip;

        [Tooltip("Played when the player lands")]
        [SerializeField] private AudioClip landClip;

        [Header("Volume")]
        [SerializeField] private float walkVolume = 0.4f;
        [SerializeField] private float sprintVolume = 0.6f;
        [SerializeField] private float jumpVolume = 0.5f;
        [SerializeField] private float landVolume = 0.6f;

        [Header("Timing")]
        [Tooltip("Seconds between footsteps when walking")]
        [SerializeField] private float walkStepInterval = 0.5f;

        [Tooltip("Seconds between footsteps when sprinting")]
        [SerializeField] private float sprintStepInterval = 0.33f;

        [Tooltip("Minimum speed to trigger footsteps")]
        [SerializeField] private float minSpeedThreshold = 0.5f;

        [Header("Pitch Variation")]
        [SerializeField] private float minPitch = 0.9f;
        [SerializeField] private float maxPitch = 1.1f;

        private PlayerMovement _movement;
        private AudioSource _source;
        private float _stepTimer;
        private bool _wasGrounded;
        private float _lastTimeScale;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();

            // Dedicated AudioSource for footsteps — 3D spatial
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 1f;
            _source.minDistance = 1f;
            _source.maxDistance = 20f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.volume = 1f;
            _lastTimeScale = Time.timeScale;
        }

        private void Update()
        {
            // If the game was paused last frame and just resumed, sync grounded state
            // without playing the landing SFX (the player didn't actually fall).
            bool justResumed = _lastTimeScale == 0f && Time.timeScale > 0f;
            _lastTimeScale = Time.timeScale;

            if (justResumed)
            {
                _wasGrounded = _movement.IsGrounded;
                return;
            }

            HandleLanding();
            HandleFootsteps();
            _wasGrounded = _movement.IsGrounded;
        }

        private void HandleFootsteps()
        {
            if (!_movement.IsGrounded) return;
            if (_movement.CurrentSpeed < minSpeedThreshold) return;

            float interval = _movement.IsSprinting ? sprintStepInterval : walkStepInterval;

            _stepTimer += Time.deltaTime;
            if (_stepTimer >= interval)
            {
                _stepTimer = 0f;
                PlayFootstep();
            }
        }

        private void HandleLanding()
        {
            // Detect transition from airborne → grounded
            if (_movement.IsGrounded && !_wasGrounded)
            {
                PlayLand();
                _stepTimer = 0f; // Reset step timer so we don't double-tap
            }
        }

        /// <summary>
        /// Call this from PlayerMovement or externally when the player jumps.
        /// </summary>
        public void PlayJump()
        {
            if (jumpClip == null) return;
            _source.pitch = Random.Range(minPitch, maxPitch);
            _source.PlayOneShot(jumpClip, jumpVolume);
        }

        private void PlayFootstep()
        {
            if (footstepClips == null || footstepClips.Length == 0) return;

            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            float vol = _movement.IsSprinting ? sprintVolume : walkVolume;
            _source.pitch = Random.Range(minPitch, maxPitch);
            _source.PlayOneShot(clip, vol);
        }

        private void PlayLand()
        {
            if (landClip == null) return;
            _source.pitch = Random.Range(minPitch, maxPitch);
            _source.PlayOneShot(landClip, landVolume);
        }
    }
}
