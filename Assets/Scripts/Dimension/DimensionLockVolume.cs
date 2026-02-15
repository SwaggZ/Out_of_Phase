using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Trigger volume that locks dimension switching while the player is inside.
    /// Useful for puzzle areas, boss fights, or story sequences.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DimensionLockVolume : MonoBehaviour
    {
        [Header("Lock Settings")]
        [Tooltip("Lock dimension switching while player is inside")]
        [SerializeField] private bool lockSwitching = true;
        
        [Tooltip("Force player to a specific dimension when entering")]
        [SerializeField] private bool forceDimension = false;
        
        [Tooltip("The dimension to force (if forceDimension is true)")]
        [SerializeField] private int targetDimension = 0;

        [Header("Feedback")]
        [Tooltip("Show UI feedback when switching is blocked")]
        [SerializeField] private bool showBlockedFeedback = true;
        
        [Tooltip("Message to show when blocked")]
        [SerializeField] private string blockedMessage = "Dimension shifting blocked in this area";

        [Header("Debug")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0.5f, 0.3f);

        private int _playersInside = 0;

        private void Awake()
        {
            // Ensure trigger is set
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if it's the player
            if (!other.CompareTag("Player")) return;
            
            _playersInside++;
            
            if (_playersInside == 1)
            {
                OnPlayerEnter();
            }
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
            
            // Force dimension if configured
            if (forceDimension)
            {
                DimensionManager.Instance.SwitchToDimension(targetDimension);
            }
            
            // Lock switching
            if (lockSwitching)
            {
                DimensionManager.Instance.LockSwitching();
            }
        }

        private void OnPlayerExit()
        {
            if (DimensionManager.Instance == null) return;
            
            // Unlock switching
            if (lockSwitching)
            {
                DimensionManager.Instance.UnlockSwitching();
            }
        }

        private void OnDisable()
        {
            // Ensure we unlock if disabled while player is inside
            if (_playersInside > 0 && lockSwitching && DimensionManager.Instance != null)
            {
                DimensionManager.Instance.UnlockSwitching();
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
            targetDimension = Mathf.Max(0, targetDimension);
        }
    }
}
