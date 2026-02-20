using UnityEngine;
using UnityEngine.Events;
using OutOfPhase.Dialogue;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Bridges dialogue trigger IDs to ending triggers.
    /// Add this to the same object as WhiteHoleCoreInteractable.
    /// Set the trigger IDs to match those in your DialogueData nodes.
    /// </summary>
    public class DialogueEndingBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WhiteHoleCoreInteractable whiteHoleCore;

        [Header("Trigger IDs")]
        [Tooltip("The triggerID set on the dialogue node for 'Destroy Device' choice.")]
        [SerializeField] private string destroyDeviceTriggerID = "ending_destroy";

        [Tooltip("The triggerID set on the dialogue node for 'Keep Device' choice.")]
        [SerializeField] private string keepDeviceTriggerID = "ending_keep";

        [Header("Events")]
        [Tooltip("Called when 'Destroy Device' ending is triggered.")]
        public UnityEvent OnDestroyDeviceSelected;

        [Tooltip("Called when 'Keep Device' ending is triggered.")]
        public UnityEvent OnKeepDeviceSelected;

        private void Start()
        {
            if (whiteHoleCore == null)
                whiteHoleCore = GetComponent<WhiteHoleCoreInteractable>();

            // Subscribe to dialogue trigger events
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.OnNodeTrigger += HandleNodeTrigger;
            }
        }

        private void OnDestroy()
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.OnNodeTrigger -= HandleNodeTrigger;
            }
        }

        private void HandleNodeTrigger(string triggerID)
        {
            if (string.IsNullOrEmpty(triggerID)) return;

            if (triggerID == destroyDeviceTriggerID)
            {
                TriggerDestroyEnding();
            }
            else if (triggerID == keepDeviceTriggerID)
            {
                TriggerKeepEnding();
            }
        }

        /// <summary>
        /// Triggers the Destroy Device ending.
        /// </summary>
        public void TriggerDestroyEnding()
        {
            OnDestroyDeviceSelected?.Invoke();
            
            if (whiteHoleCore != null)
            {
                whiteHoleCore.TriggerDestroyDeviceEnding();
            }
        }

        /// <summary>
        /// Triggers the Keep Device ending.
        /// </summary>
        public void TriggerKeepEnding()
        {
            OnKeepDeviceSelected?.Invoke();
            
            if (whiteHoleCore != null)
            {
                whiteHoleCore.TriggerKeepDeviceEnding();
            }
        }
    }
}
