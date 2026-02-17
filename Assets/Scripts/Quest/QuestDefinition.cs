using UnityEngine;
using OutOfPhase.Items;
using OutOfPhase.Dialogue;

namespace OutOfPhase.Quest
{
    /// <summary>
    /// The type of objective the player must complete.
    /// </summary>
    public enum QuestType
    {
        /// <summary>Talk to a specific NPC (matched by npcName).</summary>
        TalkTo,

        /// <summary>Collect / have a specific item in inventory.</summary>
        FindItem,

        /// <summary>Enter a trigger zone (QuestZone component).</summary>
        ReachArea,

        /// <summary>Completed via script calling QuestManager.CompleteQuest().</summary>
        Custom
    }

    /// <summary>
    /// Defines a single quest objective. Create via Assets menu.
    /// </summary>
    [CreateAssetMenu(fileName = "New Quest", menuName = "Out of Phase/Quest")]
    public class QuestDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique quest ID (auto-uses asset name if empty).")]
        public string questId;

        [Tooltip("Short title shown in the quest list.")]
        public string title = "New Quest";

        [Tooltip("Optional longer description (not currently shown in HUD).")]
        [TextArea(2, 4)]
        public string description;

        [Header("Objective")]
        public QuestType questType = QuestType.Custom;

        [Header("TalkTo Settings")]
        [Tooltip("The npcName field on the target NPCController.")]
        public string targetNPCName;

        [Tooltip("Optional: specific DialogueData that must be completed.")]
        public DialogueData targetDialogue;

        [Header("FindItem Settings")]
        [Tooltip("The item the player must have.")]
        public ItemDefinition targetItem;

        [Tooltip("Required quantity.")]
        public int targetItemCount = 1;

        [Header("Behaviour")]
        [Tooltip("If true, quest auto-completes as soon as objective is met. " +
                 "If false, something else must call CompleteQuest().")]
        public bool autoComplete = true;

        /// <summary>Returns a usable ID (falls back to asset name).</summary>
        public string Id => string.IsNullOrEmpty(questId) ? name : questId;
    }
}
