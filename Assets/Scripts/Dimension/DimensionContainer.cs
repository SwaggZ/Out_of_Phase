using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Container that manages dimension groups.
    /// Assign GameObjects to the dimensionRoots list - each index = dimension number.
    /// Only the current dimension's GameObject is active.
    /// </summary>
    public class DimensionContainer : MonoBehaviour
    {
        [Header("Dimension Roots")]
        [Tooltip("Assign one GameObject per dimension. Index 0 = Dimension 0, etc.")]
        [SerializeField] private GameObject[] dimensionRoots;

        private void Awake()
        {
            // Subscribe to manager ready event
            DimensionManager.OnManagerReady += OnManagerReady;
            
            // If manager already exists, initialize now
            if (DimensionManager.Instance != null)
            {
                Initialize();
            }
        }

        private void OnDestroy()
        {
            DimensionManager.OnManagerReady -= OnManagerReady;
            
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= OnDimensionChanged;
            }
        }

        private void OnManagerReady()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (DimensionManager.Instance == null) return;
            
            // Subscribe to dimension changes
            DimensionManager.Instance.OnDimensionChanged += OnDimensionChanged;
            
            // Set initial state
            UpdateDimensionVisibility(DimensionManager.Instance.CurrentDimension);
        }

        private void OnDimensionChanged(int oldDimension, int newDimension)
        {
            UpdateDimensionVisibility(newDimension);
        }

        private void UpdateDimensionVisibility(int activeDimension)
        {
            if (dimensionRoots == null) return;
            
            for (int i = 0; i < dimensionRoots.Length; i++)
            {
                if (dimensionRoots[i] != null)
                {
                    dimensionRoots[i].SetActive(i == activeDimension);
                }
            }
        }

        /// <summary>
        /// Gets the root GameObject for a specific dimension.
        /// </summary>
        public GameObject GetDimensionRoot(int dimension)
        {
            if (dimensionRoots == null || dimension < 0 || dimension >= dimensionRoots.Length)
                return null;
            return dimensionRoots[dimension];
        }
    }
}
