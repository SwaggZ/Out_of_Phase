using UnityEngine;
using OutOfPhase.Dimension;

namespace OutOfPhase.Progression
{
    /// <summary>
    /// A trigger volume placed at the boundary between two sections.
    /// When the player enters, it tells <see cref="SectionManager"/>
    /// to advance to the next section and locks the door behind them.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SectionGate : MonoBehaviour
    {
        [Header("Gate Settings")]
        [Tooltip("The section index the player is entering (0-based).")]
        [SerializeField] private int targetSectionIndex = 1;

        [Tooltip("Optional: block the player can't go back through (e.g. a door that closes).")]
        [SerializeField] private GameObject blockingBarrier;

        [Tooltip("Optional: trigger a cinematic when crossing.")]
        [SerializeField] private Cinematic.CinematicData gateInCinematic;

        [Header("Audio")]
        [SerializeField] private AudioClip gateCrossSound;
        [SerializeField] private float gateSoundVolume = 0.6f;

        private bool _triggered;

        private void Start()
        {
            // Make sure the collider is a trigger
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            // Hide barrier until triggered
            if (blockingBarrier != null)
                blockingBarrier.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;

            // Only react to the player
            if (other.GetComponent<Player.PlayerMovement>() == null &&
                other.GetComponentInParent<Player.PlayerMovement>() == null)
                return;

            _triggered = true;

            // Activate blocking barrier so player can't go back
            if (blockingBarrier != null)
                blockingBarrier.SetActive(true);

            // Play sound
            if (gateCrossSound != null)
                SFXPlayer.PlayAtPoint(gateCrossSound, transform.position, gateSoundVolume);

            // Play cinematic if assigned
            if (gateInCinematic != null && Cinematic.CinematicManager.Instance != null)
            {
                Cinematic.CinematicManager.Instance.PlayCinematic(gateInCinematic, () =>
                {
                    AdvanceSection();
                });
            }
            else
            {
                AdvanceSection();
            }
        }

        private void AdvanceSection()
        {
            if (SectionManager.Instance != null)
            {
                SectionManager.Instance.AdvanceToSection(targetSectionIndex);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            var col = GetComponent<BoxCollider>();
            if (col != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(col.center, col.size);
                Gizmos.DrawWireCube(col.center, col.size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 1f);
            }
        }
    }
}
