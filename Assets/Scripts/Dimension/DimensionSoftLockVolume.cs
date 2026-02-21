using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Trigger volume that locks certain dimensions (greyed out on wheel),
    /// but does NOT force the player out if they're already in a locked dimension.
    /// Useful for preventing the player from switching TO certain dimensions
    /// while still allowing them to stay if they're already there.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DimensionSoftLockVolume : MonoBehaviour
    {
        [Header("Soft Lock Settings")]
        [Tooltip("Which dimensions to lock (grey out) while the player is inside this volume.")]
        [SerializeField] private bool[] lockedDimensions;

        [Tooltip("If true, also locks switching entirely while inside (like DimensionLockVolume).")]
        [SerializeField] private bool lockSwitching = false;

        [Header("Debug")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.3f);

        private int _playersInside = 0;
        private bool _locksApplied = false;

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

            // Don't apply zone effects during checkpoint load - let the checkpoint restore dimension state first
            if (OutOfPhase.Progression.CheckpointManager.IsCheckpointLoading)
            {
                return;
            }

            ApplyDimensionLocks();

            if (lockSwitching)
                DimensionManager.Instance.LockSwitching();

            // NOTE: Unlike DimensionLockVolume, we do NOT force the player out
            // if they're currently in a locked dimension. They can stay.
        }

        private void OnPlayerExit()
        {
            if (DimensionManager.Instance == null) return;

            RemoveDimensionLocks();

            if (lockSwitching)
                DimensionManager.Instance.UnlockSwitching();
        }

        private void ApplyDimensionLocks()
        {
            if (_locksApplied) return;
            if (DimensionManager.Instance == null) return;
            DimensionManager.Instance.AddDimensionLocks(lockedDimensions);
            _locksApplied = true;
        }

        private void RemoveDimensionLocks()
        {
            if (!_locksApplied) return;
            if (DimensionManager.Instance == null) return;
            DimensionManager.Instance.RemoveDimensionLocks(lockedDimensions);
            _locksApplied = false;
        }

        /// <summary>
        /// Manually reapply locks (used when checkpoint loads and player spawns already inside).
        /// </summary>
        public void ReapplyLocksIfPlayerInside()
        {
            OnPlayerEnter();
        }

        private void OnDisable()
        {
            if (_playersInside > 0 && lockSwitching && DimensionManager.Instance != null)
                DimensionManager.Instance.UnlockSwitching();

            if (_playersInside > 0 && DimensionManager.Instance != null)
                RemoveDimensionLocks();

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
            if (lockedDimensions == null)
            {
                lockedDimensions = new bool[5];
            }
        }
    }
}
