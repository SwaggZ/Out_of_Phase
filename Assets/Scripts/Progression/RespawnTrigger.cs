using UnityEngine;
using OutOfPhase.Dimension;

namespace OutOfPhase.Progression
{
    /// <summary>
    /// Trigger zone that teleports the player back to their last checkpoint.
    /// Use for kill zones, fall detection, hazards, etc.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RespawnTrigger : MonoBehaviour
    {
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

        private void Start()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check cooldown
            if (Time.time - _lastTriggerTime < cooldown) return;

            // Check if it's the player
            var player = other.GetComponent<Player.PlayerMovement>()
                ?? other.GetComponentInParent<Player.PlayerMovement>();
            if (player == null) return;

            _lastTriggerTime = Time.time;

            // Spawn death VFX at player position
            if (deathVFX != null)
            {
                var vfx = Instantiate(deathVFX, player.transform.position, Quaternion.identity);
                Destroy(vfx, 5f);
            }

            // Play sound
            if (respawnSound != null)
                SFXPlayer.PlayAtPoint(respawnSound, player.transform.position, respawnVolume);

            // Teleport to last checkpoint
            if (CheckpointManager.Instance != null && CheckpointManager.Instance.HasCheckpoint)
            {
                CheckpointManager.Instance.LoadCheckpoint();
                Debug.Log("[RespawnTrigger] Player teleported to last checkpoint.");
            }
            else
            {
                Debug.LogWarning("[RespawnTrigger] No checkpoint saved. Cannot respawn player.");
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }
}
