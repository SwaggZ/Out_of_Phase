using UnityEngine;

namespace OutOfPhase.Quest
{
    /// <summary>
    /// Trigger zone that completes a ReachArea quest when the player enters.
    /// Assign the matching QuestDefinition in the Inspector.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class QuestZone : MonoBehaviour
    {
        [Tooltip("The ReachArea quest this zone completes.")]
        [SerializeField] private QuestDefinition quest;

        [Tooltip("Destroy this zone after the quest is completed.")]
        [SerializeField] private bool destroyOnComplete = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (quest == null) return;
            if (QuestManager.Instance == null) return;

            if (!QuestManager.Instance.IsQuestActive(quest.Id)) return;

            QuestManager.Instance.NotifyReachedArea(quest.Id);

            if (destroyOnComplete)
                Destroy(gameObject);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.2f);

            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
                return;
            }

            var sphere = GetComponent<SphereCollider>();
            if (sphere != null)
            {
                Gizmos.DrawSphere(transform.TransformPoint(sphere.center),
                    sphere.radius * Mathf.Max(transform.lossyScale.x,
                        transform.lossyScale.y, transform.lossyScale.z));
            }
        }
    }
}
