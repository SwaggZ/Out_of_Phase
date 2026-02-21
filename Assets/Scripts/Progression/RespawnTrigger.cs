using UnityEngine;
using OutOfPhase.Dimension;

namespace OutOfPhase.Progression
{
    /// <summary>
    /// Y-level trigger that teleports the player back to their last checkpoint
    /// when they fall below a specified Y threshold.
    /// Use for kill zones, fall detection, hazards, etc.
    /// </summary>
    public class RespawnTrigger : MonoBehaviour
    {
        [Header("Y Level Settings")]
        [Tooltip("Player will respawn when their Y position falls below this value.")]
        [SerializeField] private float triggerYLevel = -10f;
        
        [Tooltip("Size of the gizmo plane for visualization in the editor.")]
        [SerializeField] private float gizmoPlaneSize = 50f;

        [Header("Audio")]
        [SerializeField] private AudioClip respawnSound;
        [SerializeField] private float respawnVolume = 0.5f;

        [Header("Visual Feedback")]
        [Tooltip("Optional VFX to spawn at player position before teleporting.")]
        [SerializeField] private GameObject deathVFX;

        [Header("Settings")]
        [Tooltip("Cooldown in seconds before the trigger can activate again.")]
        [SerializeField] private float cooldown = 1f;

        private float _lastTriggerTime = -999f;
        private Transform _playerTransform;
        private Player.PlayerMovement _playerMovement;

        private void Start()
        {
            // Find the player at start
            var player = FindFirstObjectByType<Player.PlayerMovement>();
            if (player != null)
            {
                _playerMovement = player;
                _playerTransform = player.transform;
            }
        }

        private void Update()
        {
            // Try to find player if not cached
            if (_playerTransform == null)
            {
                var player = FindFirstObjectByType<Player.PlayerMovement>();
                if (player != null)
                {
                    _playerMovement = player;
                    _playerTransform = player.transform;
                }
                return;
            }

            // Check if player is below the Y threshold
            if (_playerTransform.position.y < triggerYLevel)
            {
                TriggerRespawn();
            }
        }

        private void TriggerRespawn()
        {
            // Check cooldown
            if (Time.time - _lastTriggerTime < cooldown) return;

            // Get checkpoint position from SectionManager
            if (SectionManager.Instance == null)
            {
                Debug.LogWarning("[RespawnTrigger] No SectionManager found. Cannot teleport player.");
                return;
            }

            int sectionIndex = SectionManager.Instance.CurrentSectionIndex;
            Vector3 checkpointPos = SectionManager.Instance.GetCheckpointPosition(sectionIndex);
            Quaternion checkpointRot = SectionManager.Instance.GetCheckpointRotation(sectionIndex);

            if (checkpointPos == Vector3.zero)
            {
                Debug.LogWarning("[RespawnTrigger] No checkpoint position found for current section.");
                return;
            }

            _lastTriggerTime = Time.time;

            // Spawn death VFX at player position
            if (deathVFX != null && _playerTransform != null)
            {
                var vfx = Instantiate(deathVFX, _playerTransform.position, Quaternion.identity);
                Destroy(vfx, 5f);
            }

            // Play sound
            if (respawnSound != null && _playerTransform != null)
                SFXPlayer.PlayAtPoint(respawnSound, _playerTransform.position, respawnVolume);

            // Teleport the player directly
            TeleportPlayer(checkpointPos, checkpointRot);
            Debug.Log("[RespawnTrigger] Player fell below Y=" + triggerYLevel + ". Teleported to checkpoint.");
        }

        private void TeleportPlayer(Vector3 position, Quaternion rotation)
        {
            if (_playerMovement == null) return;

            // Disable CharacterController to allow position change
            var cc = _playerMovement.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            _playerTransform.position = position;
            _playerTransform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);

            if (cc != null) cc.enabled = true;

            // Snap camera look direction
            var look = _playerMovement.GetComponent<Player.PlayerLook>()
                ?? _playerMovement.GetComponentInChildren<Player.PlayerLook>();
            if (look != null)
                look.SnapToRotation(rotation.eulerAngles.y, rotation.eulerAngles.x);
        }

        private void OnDrawGizmos()
        {
            // Draw a translucent red plane at the trigger Y level
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            
            Vector3 center = new Vector3(transform.position.x, triggerYLevel, transform.position.z);
            Vector3 size = new Vector3(gizmoPlaneSize, 0.1f, gizmoPlaneSize);
            
            Gizmos.DrawCube(center, size);
            
            // Draw wireframe for better visibility
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
