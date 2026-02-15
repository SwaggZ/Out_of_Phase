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

        // Current state
        private int _currentDimension;
        private bool _isTransitioning;
        private bool _switchingLocked;

        // Events
        /// <summary>Fired when dimension changes. Args: (oldDimension, newDimension)</summary>
        public event Action<int, int> OnDimensionChanged;
        
        /// <summary>Fired when transition starts</summary>
        public event Action<int> OnTransitionStart;
        
        /// <summary>Fired when transition completes</summary>
        public event Action<int> OnTransitionComplete;
        
        /// <summary>Fired when dimension switch is attempted while locked</summary>
        public event Action OnSwitchBlocked;
        
        /// <summary>Fired when DimensionManager is initialized and ready</summary>
        public static event Action OnManagerReady;

        // Properties
        public int CurrentDimension => _currentDimension;
        public int DimensionCount => dimensionCount;
        public bool IsTransitioning => _isTransitioning;
        public bool IsSwitchingLocked => _switchingLocked;
        
        public string CurrentDimensionName => GetDimensionName(_currentDimension);
        public Color CurrentDimensionColor => GetDimensionColor(_currentDimension);

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Initialize
            _currentDimension = Mathf.Clamp(startingDimension, 0, dimensionCount - 1);
            
            // Ensure arrays are properly sized
            ValidateArraySizes();
            
            // Notify all listeners that manager is ready
            OnManagerReady?.Invoke();
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
            
            // Check if already transitioning
            if (_isTransitioning)
                return false;
            
            // Perform the switch
            StartCoroutine(TransitionToDimension(targetDimension));
            return true;
        }

        private System.Collections.IEnumerator TransitionToDimension(int targetDimension)
        {
            int oldDimension = _currentDimension;
            _isTransitioning = true;
            
            OnTransitionStart?.Invoke(targetDimension);
            
            // Wait for transition duration (visual effects can hook into this)
            if (transitionDuration > 0)
            {
                yield return new WaitForSeconds(transitionDuration);
            }
            
            // Actually change the dimension
            _currentDimension = targetDimension;
            
            OnDimensionChanged?.Invoke(oldDimension, _currentDimension);
            
            _isTransitioning = false;
            
            OnTransitionComplete?.Invoke(_currentDimension);
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
        }
    }
}
