using UnityEngine;
using OutOfPhase.Dimension;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Teleports the player to a specified location and dimension when interacted with.
    /// </summary>
    public class TeleportInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [Tooltip("Prompt shown when player can interact.")]
        [SerializeField] private string interactionPrompt = "Teleport";

        [Tooltip("If true, can only be used once.")]
        [SerializeField] private bool oneTimeUse = false;

        [Header("Destination")]
        [Tooltip("Target position to teleport to. If set, uses this transform's position.")]
        [SerializeField] private Transform destinationTransform;

        [Tooltip("Target position if no destinationTransform is set.")]
        [SerializeField] private Vector3 destinationPosition;

        [Tooltip("If true, also set player rotation to match destination.")]
        [SerializeField] private bool matchRotation = true;

        [Header("Dimension")]
        [Tooltip("Target dimension index to switch to (-1 = don't change dimension).")]
        [SerializeField] private int targetDimension = -1;

        [Header("Effects")]
        [Tooltip("Optional sound to play on teleport.")]
        [SerializeField] private AudioClip teleportSound;

        [Tooltip("Volume of the teleport sound.")]
        [SerializeField] private float soundVolume = 1f;

        [Tooltip("Delay before teleporting (for effects).")]
        [SerializeField] private float teleportDelay = 0f;

        private bool _hasBeenUsed;

        // IInteractable
        public string InteractionPrompt => interactionPrompt;
        public bool CanInteract => !_hasBeenUsed || !oneTimeUse;

        public void Interact(InteractionContext context)
        {
            if (!CanInteract) return;

            if (oneTimeUse)
                _hasBeenUsed = true;

            if (teleportDelay > 0f)
            {
                StartCoroutine(TeleportAfterDelay(context.PlayerTransform));
            }
            else
            {
                DoTeleport(context.PlayerTransform);
            }
        }

        private System.Collections.IEnumerator TeleportAfterDelay(Transform player)
        {
            yield return new WaitForSeconds(teleportDelay);
            DoTeleport(player);
        }

        private void DoTeleport(Transform player)
        {
            if (player == null) return;

            // Get destination
            Vector3 targetPos = destinationTransform != null 
                ? destinationTransform.position 
                : destinationPosition;

            Quaternion targetRot = destinationTransform != null
                ? destinationTransform.rotation
                : Quaternion.identity;

            // Teleport player
            var characterController = player.GetComponent<CharacterController>();
            if (characterController != null)
            {
                // Disable CharacterController to allow position change
                characterController.enabled = false;
                player.position = targetPos;
                if (matchRotation && destinationTransform != null)
                    player.rotation = targetRot;
                characterController.enabled = true;
            }
            else
            {
                player.position = targetPos;
                if (matchRotation && destinationTransform != null)
                    player.rotation = targetRot;
            }

            // Play sound
            if (teleportSound != null)
            {
                SFXPlayer.PlayAtPoint(teleportSound, targetPos, soundVolume);
            }

            // Switch dimension after physics processes the new position
            // This ensures trigger volumes at the destination have fired first
            if (targetDimension >= 0 && DimensionManager.Instance != null)
            {
                StartCoroutine(SwitchDimensionAfterPhysics(targetDimension));
            }

            Debug.Log($"[Teleport] Player teleported to {targetPos}, dimension {targetDimension}");
        }

        private System.Collections.IEnumerator SwitchDimensionAfterPhysics(int dimension)
        {
            // Wait for the destination to fully load and all triggers to process
            // Multiple fixed updates ensure physics has settled
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            
            // Wait an additional short delay for any async loading at destination
            yield return new WaitForSeconds(0.1f);
            
            // Final frame wait to ensure everything is ready
            yield return null;
            
            if (DimensionManager.Instance != null)
            {
                // Use AbsoluteForce to bypass all locks - teleporters guarantee the switch
                bool success = DimensionManager.Instance.AbsoluteForceSwitchToDimension(dimension);
                Debug.Log($"[Teleport] Dimension switch to {dimension} - success: {success}");
            }
        }

        /// <summary>
        /// Reset the one-time use flag (e.g., on game reload).
        /// </summary>
        public void ResetUsage()
        {
            _hasBeenUsed = false;
        }

        private void OnDrawGizmos()
        {
            // Draw teleport source
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw destination
            Vector3 dest = destinationTransform != null 
                ? destinationTransform.position 
                : destinationPosition;

            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.5f);
            Gizmos.DrawSphere(dest, 0.3f);
            Gizmos.DrawWireSphere(dest, 0.5f);

            // Draw line between them
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, dest);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw destination direction arrow
            if (destinationTransform != null && matchRotation)
            {
                Gizmos.color = Color.green;
                Vector3 forward = destinationTransform.forward * 1f;
                Gizmos.DrawRay(destinationTransform.position, forward);
            }
        }
    }
}
