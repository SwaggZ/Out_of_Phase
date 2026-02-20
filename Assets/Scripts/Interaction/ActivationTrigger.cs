using UnityEngine;
using OutOfPhase.Quest;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Activates target GameObjects and/or quests when the player enters the trigger collider.
    /// Targets should be disabled in the scene until the player reaches this trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ActivationTrigger : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("GameObjects to activate when the player enters the trigger.")]
        [SerializeField] private GameObject[] targetObjects;

        [Header("Quest Activation")]
        [Tooltip("Quests to activate when the player enters the trigger.")]
        [SerializeField] private QuestDefinition[] questsToActivate;

        [Header("Options")]
        [Tooltip("If true, the trigger disables itself after activating (one-time use).")]
        [SerializeField] private bool onlyOnce = true;

        [Tooltip("If true, deactivate targets when the player exits the trigger.")]
        [SerializeField] private bool deactivateOnExit = false;

        [Tooltip("Optional delay before activation (in seconds).")]
        [SerializeField] private float activationDelay = 0f;

        private bool _hasTriggered;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (onlyOnce && _hasTriggered) return;

            _hasTriggered = true;

            if (activationDelay > 0f)
                Invoke(nameof(ActivateTargets), activationDelay);
            else
                ActivateTargets();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!deactivateOnExit) return;
            if (!other.CompareTag("Player")) return;

            DeactivateTargets();
        }

        private void ActivateTargets()
        {
            // Activate GameObjects
            if (targetObjects != null)
            {
                foreach (var target in targetObjects)
                {
                    if (target != null)
                    {
                        target.SetActive(true);
                        Debug.Log($"[ActivationTrigger] Activated: {target.name}");
                    }
                }
            }

            // Activate Quests
            if (questsToActivate != null && questsToActivate.Length > 0 && QuestManager.Instance != null)
            {
                QuestManager.Instance.ActivateQuests(questsToActivate);
                Debug.Log($"[ActivationTrigger] Activated {questsToActivate.Length} quest(s)");
            }
        }

        private void DeactivateTargets()
        {
            if (targetObjects != null)
            {
                foreach (var target in targetObjects)
                {
                    if (target != null)
                        target.SetActive(false);
                }
            }

            // Optionally deactivate quests too
            if (deactivateOnExit && questsToActivate != null && questsToActivate.Length > 0 && QuestManager.Instance != null)
            {
                QuestManager.Instance.DeactivateQuests(questsToActivate);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.3f);
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

            // Draw lines to targets
            if (targetObjects != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var target in targetObjects)
                {
                    if (target != null)
                        Gizmos.DrawLine(transform.position, target.transform.position);
                }
            }

            // Draw quest indicator
            if (questsToActivate != null && questsToActivate.Length > 0)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.2f);
            }
        }
    }
}