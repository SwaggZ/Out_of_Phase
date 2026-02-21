using System;
using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Singleton manager for the dimension system.
    /// Tracks current dimension and broadcasts dimension changes.
    /// </summary>
    public class DimensionManager : MonoBehaviour
    {
        public static DimensionManager Instance { get; private set; }

        [Header("Dimension Settings")]
        [Tooltip("Total number of dimensions (default 5)")]
        [SerializeField] private int dimensionCount = 5;
        
        [Tooltip("Starting dimension (0-indexed)")]
        [SerializeField] private int startingDimension = 0;
        
        [Tooltip("Names for each dimension (for UI display)")]
        [SerializeField] private string[] dimensionNames = new string[]
        {
            "Reality",
            "Ethereal",
            "Void",
            "Primal",
            "Ascended"
        };
        
        [Tooltip("Colors for each dimension (for UI/effects)")]
        [SerializeField] private Color[] dimensionColors = new Color[]
        {
            new Color(0.8f, 0.8f, 0.8f),  // Reality - white/gray
            new Color(0.5f, 0.8f, 1.0f),  // Ethereal - light blue
            new Color(0.2f, 0.0f, 0.3f),  // Void - dark purple
            new Color(0.2f, 0.8f, 0.2f),  // Primal - green
            new Color(1.0f, 0.8f, 0.3f)   // Ascended - gold
        };

        [Header("Transition")]
        [Tooltip("Duration of dimension transition effect")]
        [SerializeField] private float transitionDuration = 0.3f;

        [Tooltip("Cooldown after a dimension switch before another is allowed")]
        [SerializeField] private float switchCooldown = 0.5f;

        // Current state
        private int _currentDimension;
        private int _transitionTargetDimension = -1; // Track target during transition
        private bool _isTransitioning;
        private bool _switchingLocked;
        private int[] _dimensionLockCounts;
        private int[] _dimensionHideCounts;
        private float _cooldownUntil;

        // Events
        /// <summary>Fired when dimension changes. Args: (oldDimension, newDimension)</summary>
        public event Action<int, int> OnDimensionChanged;
        
        /// <summary>Fired when transition starts</summary>
        public event Action<int> OnTransitionStart;
        
        /// <summary>Fired when transition completes</summary>
        public event Action<int> OnTransitionComplete;
        
        /// <summary>Fired when dimension switch is attempted while locked</summary>
        public event Action OnSwitchBlocked;

        /// <summary>Fired when per-dimension locks change</summary>
        public event Action OnDimensionLocksChanged;

        /// <summary>Fired when per-dimension visibility (hidden) changes</summary>
        public event Action OnDimensionVisibilityChanged;

        /// <summary>Fired when DimensionManager is initialized and ready</summary>
        public static event Action OnManagerReady;
        
        /// <summary>True if DimensionManager has been initialized</summary>
        public static bool IsManagerReady { get; private set; }

        // Properties
        public int CurrentDimension => _currentDimension;
        public int DimensionCount => dimensionCount;
        public bool IsTransitioning => _isTransitioning;
        public bool IsSwitchingLocked => _switchingLocked;
        public float SwitchCooldown => switchCooldown;
        
        /// <summary>0 = on cooldown, 1 = fully charged.</summary>
        public float CooldownProgress
        {
            get
            {
                if (_isTransitioning) return 0f;
                float remaining = _cooldownUntil - Time.time;
                if (remaining <= 0f) return 1f;
                return 1f - (remaining / switchCooldown);
            }
        }
        
        public bool IsOnCooldown => _isTransitioning || Time.time < _cooldownUntil;
        
        public string CurrentDimensionName => GetDimensionName(_currentDimension);
        public Color CurrentDimensionColor => GetDimensionColor(_currentDimension);

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Debug.Log($"[DimensionManager] Another instance exists ({Instance.gameObject.name}), destroying this one ({gameObject.name})");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[DimensionManager] Initialized and set to DontDestroyOnLoad");
            
            // Initialize
            _currentDimension = Mathf.Clamp(startingDimension, 0, dimensionCount - 1);
            
            // Ensure arrays are properly sized
            ValidateArraySizes();
            EnsureLockArraySize();
            
            // Notify all listeners that manager is ready
            IsManagerReady = true;
            Debug.Log("[DimensionManager] Invoking OnManagerReady event");
            OnManagerReady?.Invoke();
            Debug.Log("[DimensionManager] OnManagerReady event completed");
        }

        private void ValidateArraySizes()
        {
            if (dimensionNames == null || dimensionNames.Length < dimensionCount)
            {
                var newNames = new string[dimensionCount];
                for (int i = 0; i < dimensionCount; i++)
                {
                    newNames[i] = (dimensionNames != null && i < dimensionNames.Length) 
                        ? dimensionNames[i] 
                        : $"Dimension {i + 1}";
                }
                dimensionNames = newNames;
            }
            
            if (dimensionColors == null || dimensionColors.Length < dimensionCount)
            {
                var newColors = new Color[dimensionCount];
                for (int i = 0; i < dimensionCount; i++)
                {
                    newColors[i] = (dimensionColors != null && i < dimensionColors.Length)
                        ? dimensionColors[i]
                        : Color.HSVToRGB((float)i / dimensionCount, 0.7f, 0.9f);
                }
                dimensionColors = newColors;
            }
        }

        private void EnsureLockArraySize()
        {
            if (_dimensionLockCounts == null || _dimensionLockCounts.Length != dimensionCount)
            {
                var newCounts = new int[dimensionCount];
                if (_dimensionLockCounts != null)
                {
                    int copyCount = Mathf.Min(_dimensionLockCounts.Length, dimensionCount);
                    for (int i = 0; i < copyCount; i++)
                    {
                        newCounts[i] = _dimensionLockCounts[i];
                    }
                }
                _dimensionLockCounts = newCounts;
            }

            if (_dimensionHideCounts == null || _dimensionHideCounts.Length != dimensionCount)
            {
                var newCounts = new int[dimensionCount];
                if (_dimensionHideCounts != null)
                {
                    int copyCount = Mathf.Min(_dimensionHideCounts.Length, dimensionCount);
                    for (int i = 0; i < copyCount; i++)
                    {
                        newCounts[i] = _dimensionHideCounts[i];
                    }
                }
                _dimensionHideCounts = newCounts;
            }
        }

        /// <summary>
        /// Switches to the specified dimension.
        /// </summary>
        /// <param name="targetDimension">The dimension to switch to (0-indexed)</param>
        /// <returns>True if switch was successful</returns>
        public bool SwitchToDimension(int targetDimension)
        {
            // Validate
            if (targetDimension < 0 || targetDimension >= dimensionCount)
            {
                Debug.LogWarning($"[DimensionManager] Invalid dimension: {targetDimension}");
                return false;
            }
            
            // Already in this dimension
            if (targetDimension == _currentDimension)
                return true;
            
            // Check if switching is locked
            if (_switchingLocked)
            {
                OnSwitchBlocked?.Invoke();
                return false;
            }

            // Check if target dimension is locked
            if (IsDimensionLocked(targetDimension))
            {
                OnSwitchBlocked?.Invoke();
                return false;
            }
            
            // Check if already transitioning or on cooldown
            if (_isTransitioning || Time.time < _cooldownUntil)
                return false;
            
            // Perform the switch
            StartCoroutine(TransitionToDimension(targetDimension));
            return true;
        }

        /// <summary>
        /// Forces a switch to a dimension, ignoring global switching lock.
        /// Still respects per-dimension locks.
        /// </summary>
        public bool ForceSwitchToDimension(int targetDimension)
        {
            if (targetDimension < 0 || targetDimension >= dimensionCount)
                return false;

            if (IsDimensionLocked(targetDimension))
                return false;

            if (_isTransitioning)
                return false;

            if (targetDimension == _currentDimension)
                return true;

            StartCoroutine(TransitionToDimension(targetDimension));
            return true;
        }

        /// <summary>
        /// Absolutely forces a switch to a dimension, ignoring ALL locks and restrictions.
        /// Use for teleporters that need to guarantee the dimension changes.
        /// </summary>
        public bool AbsoluteForceSwitchToDimension(int targetDimension)
        {
            if (targetDimension < 0 || targetDimension >= dimensionCount)
            {
                Debug.LogWarning($"[DimensionManager] AbsoluteForce failed: dimension {targetDimension} out of range (0-{dimensionCount - 1})");
                return false;
            }

            if (targetDimension == _currentDimension)
            {
                Debug.Log($"[DimensionManager] AbsoluteForce: already in dimension {targetDimension}");
                return true;
            }

            // If transitioning, stop the current transition
            if (_isTransitioning)
            {
                StopAllCoroutines();
                _isTransitioning = false;
            }

            Debug.Log($"[DimensionManager] AbsoluteForce switching from {_currentDimension} to {targetDimension}");
            StartCoroutine(TransitionToDimension(targetDimension));
            return true;
        }

        private System.Collections.IEnumerator TransitionToDimension(int targetDimension)
        {
            int oldDimension = _currentDimension;
            _isTransitioning = true;
            _transitionTargetDimension = targetDimension;
            
            OnTransitionStart?.Invoke(targetDimension);
            
            // Wait for transition duration (visual effects can hook into this)
            if (transitionDuration > 0)
            {
                yield return new WaitForSeconds(transitionDuration);
            }
            
            // Check if target dimension became blocked (hidden or locked) during transition
            // OR if switching got locked during transition
            if (IsDimensionHidden(targetDimension) || IsDimensionLocked(targetDimension) || _switchingLocked)
            {
                Debug.Log($"[DimensionManager] Transition to dimension {targetDimension} failed - dimension became blocked or switching locked!");
                _isTransitioning = false;
                _transitionTargetDimension = -1;
                _cooldownUntil = Time.time + switchCooldown;
                OnSwitchBlocked?.Invoke();
                yield break; // Cancel transition
            }
            
            // Check if player would be inside a wall in target dimension
            if (!IsDestinationSafe(targetDimension))
            {
                Debug.Log($"[DimensionManager] Transition to dimension {targetDimension} failed - destination is inside a solid object!");
                _isTransitioning = false;
                _transitionTargetDimension = -1;
                _cooldownUntil = Time.time + switchCooldown;
                OnSwitchBlocked?.Invoke();
                yield break; // Cancel transition
            }
            
            // Actually change the dimension
            _currentDimension = targetDimension;
            
            OnDimensionChanged?.Invoke(oldDimension, _currentDimension);
            
            _isTransitioning = false;
            _transitionTargetDimension = -1;
            _cooldownUntil = Time.time + switchCooldown;
            
            OnTransitionComplete?.Invoke(_currentDimension);
        }

        /// <summary>
        /// Checks if the player's current position would be safe in the target dimension.
        /// Returns false if the player would be inside a solid object.
        /// </summary>
        private bool IsDestinationSafe(int targetDimension)
        {
            // Find player
            var player = FindFirstObjectByType<UnityEngine.CharacterController>();
            if (player == null)
            {
                // Can't find player, assume safe (fallback)
                return true;
            }

            // Check for colliders at player position
            float checkRadius = 0.5f; // Player capsule radius
            Collider[] hitColliders = Physics.OverlapSphere(player.transform.position, checkRadius);

            foreach (var collider in hitColliders)
            {
                // Skip the player's own collider
                if (collider.GetComponent<UnityEngine.CharacterController>() != null)
                    continue;

                // Skip triggers
                if (collider.isTrigger)
                    continue;

                // Check if this object is dimension-specific
                var dimObj = collider.GetComponentInParent<DimensionObject>();
                if (dimObj != null)
                {
                    // Check if this object would be visible in target dimension
                    if (IsVisibleInCurrentDimension(dimObj.GetVisibleDimensions(), targetDimension))
                    {
                        // This solid object exists in target dimension at player position - unsafe!
                        return false;
                    }
                }
                else
                {
                    // Object has no DimensionObject component, so it exists in all dimensions
                    // This is a solid object at player position - unsafe!
                    return false;
                }
            }

            // No solid objects at player position in target dimension
            return true;
        }

        /// <summary>
        /// Helper to check if an object would be visible in a specific dimension.
        /// </summary>
        private bool IsVisibleInCurrentDimension(int[] visibleDimensions, int dimension)
        {
            if (visibleDimensions == null || visibleDimensions.Length == 0)
                return true; // Default to visible

            foreach (int dim in visibleDimensions)
            {
                if (dim == dimension)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Cycles to the next dimension.
        /// </summary>
        public bool NextDimension()
        {
            int next = (_currentDimension + 1) % dimensionCount;
            return SwitchToDimension(next);
        }

        /// <summary>
        /// Cycles to the previous dimension.
        /// </summary>
        public bool PreviousDimension()
        {
            int prev = _currentDimension - 1;
            if (prev < 0) prev = dimensionCount - 1;
            return SwitchToDimension(prev);
        }

        /// <summary>
        /// Locks dimension switching (e.g., during cutscenes or in lock zones).
        /// </summary>
        public void LockSwitching()
        {
            _switchingLocked = true;
        }

        /// <summary>
        /// Unlocks dimension switching.
        /// </summary>
        public void UnlockSwitching()
        {
            _switchingLocked = false;
        }

        /// <summary>
        /// Adds per-dimension locks (increments lock counts).
        /// </summary>
        public void AddDimensionLocks(bool[] lockedDimensions)
        {
            EnsureLockArraySize();
            if (lockedDimensions == null) return;

            int count = Mathf.Min(lockedDimensions.Length, dimensionCount);
            bool anyChanged = false;
            for (int i = 0; i < count; i++)
            {
                if (!lockedDimensions[i]) continue;
                _dimensionLockCounts[i]++;
                anyChanged = true;
            }

            if (anyChanged)
                OnDimensionLocksChanged?.Invoke();
        }

        /// <summary>
        /// Removes per-dimension locks (decrements lock counts).
        /// </summary>
        public void RemoveDimensionLocks(bool[] lockedDimensions)
        {
            EnsureLockArraySize();
            if (lockedDimensions == null) return;

            int count = Mathf.Min(lockedDimensions.Length, dimensionCount);
            bool anyChanged = false;
            for (int i = 0; i < count; i++)
            {
                if (!lockedDimensions[i]) continue;
                _dimensionLockCounts[i] = Mathf.Max(0, _dimensionLockCounts[i] - 1);
                anyChanged = true;
            }

            if (anyChanged)
                OnDimensionLocksChanged?.Invoke();
        }

        /// <summary>
        /// Checks if a dimension is currently locked by any volume.
        /// </summary>
        public bool IsDimensionLocked(int dimension)
        {
            if (_dimensionLockCounts == null) return false;
            if (dimension < 0 || dimension >= _dimensionLockCounts.Length) return false;
            return _dimensionLockCounts[dimension] > 0;
        }

        /// <summary>
        /// Adds per-dimension hides (increments hide counts).
        /// Hidden dimensions are completely removed from the dimension wheel.
        /// </summary>
        public void AddDimensionHides(bool[] hiddenDimensions)
        {
            EnsureLockArraySize();
            if (hiddenDimensions == null) return;

            int count = Mathf.Min(hiddenDimensions.Length, dimensionCount);
            bool anyChanged = false;
            for (int i = 0; i < count; i++)
            {
                if (!hiddenDimensions[i]) continue;
                _dimensionHideCounts[i]++;
                anyChanged = true;
            }

            if (anyChanged)
                OnDimensionVisibilityChanged?.Invoke();
        }

        /// <summary>
        /// Removes per-dimension hides (decrements hide counts).
        /// </summary>
        public void RemoveDimensionHides(bool[] hiddenDimensions)
        {
            EnsureLockArraySize();
            if (hiddenDimensions == null) return;

            int count = Mathf.Min(hiddenDimensions.Length, dimensionCount);
            bool anyChanged = false;
            for (int i = 0; i < count; i++)
            {
                if (!hiddenDimensions[i]) continue;
                _dimensionHideCounts[i] = Mathf.Max(0, _dimensionHideCounts[i] - 1);
                anyChanged = true;
            }

            if (anyChanged)
                OnDimensionVisibilityChanged?.Invoke();
        }

        /// <summary>
        /// Checks if a dimension is currently hidden (not shown in wheel).
        /// </summary>
        public bool IsDimensionHidden(int dimension)
        {
            if (_dimensionHideCounts == null) return false;
            if (dimension < 0 || dimension >= _dimensionHideCounts.Length) return false;
            return _dimensionHideCounts[dimension] > 0;
        }

        /// <summary>
        /// Finds the next unlocked dimension, starting after the given index.
        /// Returns the current dimension if all are locked.
        /// </summary>
        public int FindNextUnlockedDimension(int fromDimension)
        {
            if (dimensionCount <= 0) return fromDimension;

            for (int offset = 1; offset <= dimensionCount; offset++)
            {
                int index = (fromDimension + offset) % dimensionCount;
                if (!IsDimensionLocked(index))
                    return index;
            }

            return fromDimension;
        }

        /// <summary>
        /// Gets the name of a dimension.
        /// </summary>
        public string GetDimensionName(int dimension)
        {
            if (dimension < 0 || dimension >= dimensionCount)
                return "Unknown";
            return dimensionNames[dimension];
        }

        /// <summary>
        /// Gets the color of a dimension.
        /// </summary>
        public Color GetDimensionColor(int dimension)
        {
            if (dimension < 0 || dimension >= dimensionCount)
                return Color.white;
            return dimensionColors[dimension];
        }

        /// <summary>
        /// Checks if an object should be visible in the current dimension.
        /// </summary>
        public bool IsVisibleInCurrentDimension(int[] visibleDimensions)
        {
            if (visibleDimensions == null || visibleDimensions.Length == 0)
                return true; // Default to visible
            
            foreach (int dim in visibleDimensions)
            {
                if (dim == _currentDimension)
                    return true;
            }
            return false;
        }

        private void OnValidate()
        {
            dimensionCount = Mathf.Max(1, dimensionCount);
            startingDimension = Mathf.Clamp(startingDimension, 0, dimensionCount - 1);
            transitionDuration = Mathf.Max(0, transitionDuration);
            EnsureLockArraySize();
        }
    }
}
