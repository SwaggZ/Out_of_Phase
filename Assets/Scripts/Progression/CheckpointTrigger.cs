using UnityEngine;
using OutOfPhase.Dimension;

namespace OutOfPhase.Progression
{
    /// <summary>
    /// Place this trigger volume at checkpoint locations.
    /// When the player enters, the game auto-saves.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CheckpointTrigger : MonoBehaviour
    {
        [Header("Checkpoint")]
        [SerializeField] private string checkpointName = "Checkpoint";

        [Tooltip("Optional spawn point for respawning. If null, uses trigger position.")]
        [SerializeField] private Transform spawnPoint;

        [Header("Audio")]
        [SerializeField] private AudioClip saveSound;
        [SerializeField] private float saveVolume = 0.4f;

        [Header("Visual Feedback")]
        [Tooltip("Optional: flash or particle effect on save.")]
        [SerializeField] private GameObject saveVFX;

        private bool _triggered;

        private void Start()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;

            if (other.GetComponent<Player.PlayerMovement>() == null &&
                other.GetComponentInParent<Player.PlayerMovement>() == null)
                return;

            _triggered = true;

            // Move player to exact spawn point before saving
            // so that loading puts them at the clean position
            if (spawnPoint != null)
            {
                var player = other.GetComponent<Player.PlayerMovement>()
                    ?? other.GetComponentInParent<Player.PlayerMovement>();
                if (player != null)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    player.transform.position = spawnPoint.position;
                    player.transform.rotation = spawnPoint.rotation;
                    if (cc != null) cc.enabled = true;
                }
            }

            // Save
            if (CheckpointManager.Instance != null)
            {
                CheckpointManager.Instance.SaveCheckpoint(checkpointName);
            }

            // Feedback
            if (saveSound != null)
                SFXPlayer.PlayAtPoint(saveSound, transform.position, saveVolume);

            if (saveVFX != null)
            {
                var vfx = Instantiate(saveVFX, transform.position, Quaternion.identity);
                Destroy(vfx, 5f);
            }

            Debug.Log($"[Checkpoint] '{checkpointName}' saved.");
        }

        /// <summary>
        /// Allow re-triggering (e.g. if player revisits before section closes).
        /// </summary>
        public void ResetTrigger()
        {
            _triggered = false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.35f);

            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 0.75f);
            }

            // Draw spawn point
            if (spawnPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawLine(transform.position, spawnPoint.position);
                Gizmos.DrawWireSphere(spawnPoint.position, 0.3f);
            }
        }
    }
}
