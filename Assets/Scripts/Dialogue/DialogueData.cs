using UnityEngine;
using OutOfPhase.Items;

namespace OutOfPhase.Dialogue
{
    /// <summary>
    /// A single dialogue conversation as a ScriptableObject.
    /// Contains an ordered list of dialogue nodes. Each node can have
    /// speaker text, optional player choices, and optional item rewards.
    /// </summary>
    [CreateAssetMenu(fileName = "New Dialogue", menuName = "Out of Phase/Dialogue/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        [Tooltip("Display name shown in the header when no speaker override is set")]
        public string defaultSpeakerName = "???";

        [Tooltip("Ordered list of dialogue nodes")]
        public DialogueNode[] nodes;

        /// <summary>Returns true if this dialogue has any nodes.</summary>
        public bool IsValid => nodes != null && nodes.Length > 0;
    }

    /// <summary>
    /// One step in a dialogue. Can be a line of NPC text,
    /// a set of player choices, or both.
    /// </summary>
    [System.Serializable]
    public class DialogueNode
    {
        [Tooltip("Override speaker name for this node (leave empty to use default)")]
        public string speakerNameOverride;

        [Tooltip("The dialogue text displayed with typewriter effect")]
        [TextArea(2, 5)]
        public string text;

        [Tooltip("Optional player choices. If empty, pressing E advances to nextNodeIndex")]
        public DialogueChoice[] choices;

        [Tooltip("Index of the next node when there are no choices (-1 = end dialogue)")]
        public int nextNodeIndex = -1;

        [Tooltip("Items given to the player when this node is reached")]
        public ItemReward[] itemRewards;

        /// <summary>True if this node presents choices to the player.</summary>
        public bool HasChoices => choices != null && choices.Length > 0;
    }

    /// <summary>
    /// A player dialogue choice that branches to a different node.
    /// </summary>
    [System.Serializable]
    public class DialogueChoice
    {
        [Tooltip("Text shown on the choice button")]
        public string choiceText;

        [Tooltip("Index of the node to jump to when this choice is selected (-1 = end dialogue)")]
        public int targetNodeIndex = -1;
    }

    /// <summary>
    /// An item reward given during dialogue. The item is dropped
    /// on the ground near the NPC for the player to pick up.
    /// </summary>
    [System.Serializable]
    public class ItemReward
    {
        [Tooltip("The item to give")]
        public ItemDefinition item;

        [Tooltip("How many to give")]
        public int quantity = 1;
    }
}
