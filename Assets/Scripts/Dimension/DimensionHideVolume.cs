using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Trigger volume that completely hides certain dimensions from the dimension wheel.
    /// Hidden dimensions are removed from the wheel UI entirely (not greyed out like locked ones).
    /// If the player is currently in a hidden dimension when entering the volume,
    /// they are forced to switch to a visible one.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DimensionHideVolume : MonoBehaviour
    {
        [Header("Hide Settings")]
        [Tooltip("Which dimensions to hide from the wheel while the player is inside this volume.")]
        [SerializeField] private bool[] hiddenDimensions;

        [Tooltip("If true, also locks switching entirely while inside (like DimensionLockVolume).")]
        [SerializeField] private bool lockSwitching = false;

        [Tooltip("Dimension to force the player into if their current dimension becomes hidden. " +
                 "Set to -1 to auto-pick the first visible dimension.")]
        [SerializeField] private int fallbackDimension = -1;

        [Header("Debug")]
        [SerializeField] private Color gizmoColor = new Color(0f, 0.5f, 1f, 0.3f);

        private int _playersInside = 0;
        private bool _hidesApplied = false;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playersInside++;
            if (_playersInside == 1)
                OnPlayerEnter();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playersInside--;
            if (_playersInside <= 0)
            {
                _playersInside = 0;
                OnPlayerExit();
            }
        }

        private void OnPlayerEnter()
        {
            if (DimensionManager.Instance == null) return;

            ApplyHides();

            if (lockSwitching)
                DimensionManager.Instance.LockSwitching();

            // If the player's current dimension is now hidden, force them out
            int current = DimensionManager.Instance.CurrentDimension;
            if (DimensionManager.Instance.IsDimensionHidden(current))
            {
                int target = ResolveVisibleDimension();
                if (target >= 0 && target != current)
                    DimensionManager.Instance.ForceSwitchToDimension(target);
            }
        }

        private void OnPlayerExit()
        {
            if (DimensionManager.Instance == null) return;

            RemoveHides();

            if (lockSwitching)
                DimensionManager.Instance.UnlockSwitching();
        }

        /// <summary>
        /// Find a dimension to switch the player to when their current one is hidden.
        /// </summary>
        private int ResolveVisibleDimension()
        {
            if (DimensionManager.Instance == null) return 0;

            // Explicit fallback
            if (fallbackDimension >= 0
                && fallbackDimension < DimensionManager.Instance.DimensionCount
                && !DimensionManager.Instance.IsDimensionHidden(fallbackDimension)
                && !DimensionManager.Instance.IsDimensionLocked(fallbackDimension))
            {
                return fallbackDimension;
            }

            // Auto-pick first visible & unlocked dimension
            for (int i = 0; i < DimensionManager.Instance.DimensionCount; i++)
            {
                if (!DimensionManager.Instance.IsDimensionHidden(i)
                    && !DimensionManager.Instance.IsDimensionLocked(i))
                {
                    return i;
                }
            }

            // Last resort: first non-hidden (even if locked)
            for (int i = 0; i < DimensionManager.Instance.DimensionCount; i++)
            {
                if (!DimensionManager.Instance.IsDimensionHidden(i))
                    return i;
            }

            return 0;
        }

        private void ApplyHides()
        {
            if (_hidesApplied) return;
            if (DimensionManager.Instance == null) return;
            DimensionManager.Instance.AddDimensionHides(hiddenDimensions);
            _hidesApplied = true;
        }

        private void RemoveHides()
        {
            if (!_hidesApplied) return;
            if (DimensionManager.Instance == null) return;
            DimensionManager.Instance.RemoveDimensionHides(hiddenDimensions);
            _hidesApplied = false;
        }

        private void OnDisable()
        {
            if (_playersInside > 0)
            {
                if (lockSwitching && DimensionManager.Instance != null)
                    DimensionManager.Instance.UnlockSwitching();

                if (DimensionManager.Instance != null)
                    RemoveHides();
            }
            _playersInside = 0;
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;
            Gizmos.color = gizmoColor;
            if (col is BoxCollider box)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = oldMatrix;
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
                Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
            }
            else
            {
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
        }

        private void OnValidate()
        {
            fallbackDimension = Mathf.Max(-1, fallbackDimension);

            if (hiddenDimensions == null)
            {
                hiddenDimensions = new bool[5];
            }
        }
    }
}
