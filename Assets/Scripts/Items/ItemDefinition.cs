using UnityEngine;

namespace OutOfPhase.Items
{
    /// <summary>
    /// ScriptableObject defining an item's properties.
    /// This is the base class for all items in the game.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "Out of Phase/Items/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this item")]
        [SerializeField] private string itemId;
        
        [Tooltip("Display name shown in UI")]
        [SerializeField] private string itemName;
        
        [Tooltip("Description shown in tooltips")]
        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Header("Visuals")]
        [Tooltip("Icon for inventory/hotbar UI")]
        [SerializeField] private Sprite icon;
        
        [Tooltip("3D model prefab for world/held representation")]
        [SerializeField] private GameObject worldPrefab;
        
        [Tooltip("Prefab for how item appears when held by player")]
        [SerializeField] private GameObject heldPrefab;

        [Header("Stacking")]
        [Tooltip("Can this item stack with others of same type?")]
        [SerializeField] private bool isStackable = false;
        
        [Tooltip("Maximum stack size (only if stackable)")]
        [SerializeField] private int maxStackSize = 1;

        [Header("Durability")]
        [Tooltip("Does this item have durability (for tools)?")]
        [SerializeField] private bool hasDurability = false;
        
        [Tooltip("Maximum durability value")]
        [SerializeField] private float maxDurability = 100f;

        [Header("Tool Actions")]
        [Tooltip("Tool actions this item can perform (pickaxe, shovel, etc.)")]
        [SerializeField] private ToolAction[] toolActions;

        [Header("Item Type")]
        [Tooltip("Category of this item for filtering/UI purposes")]
        [SerializeField] private ItemCategory category = ItemCategory.Misc;

        // Public accessors
        public string ItemId => itemId;
        public string ItemName => itemName;
        public string Description => description;
        public Sprite Icon => icon;
        public GameObject WorldPrefab => worldPrefab;
        public GameObject HeldPrefab => heldPrefab;
        public bool IsStackable => isStackable;
        public int MaxStackSize => isStackable ? maxStackSize : 1;
        public bool HasDurability => hasDurability;
        public float MaxDurability => maxDurability;
        public ToolAction[] ToolActions => toolActions;
        public ItemCategory Category => category;

        /// <summary>
        /// Checks if this item has a specific tool action type.
        /// </summary>
        public bool HasToolAction<T>() where T : ToolAction
        {
            if (toolActions == null) return false;
            foreach (var action in toolActions)
            {
                if (action is T) return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a specific tool action type from this item.
        /// </summary>
        public T GetToolAction<T>() where T : ToolAction
        {
            if (toolActions == null) return null;
            foreach (var action in toolActions)
            {
                if (action is T typed) return typed;
            }
            return null;
        }

        /// <summary>
        /// Checks if this item has any tool action that matches the given type.
        /// </summary>
        public bool HasToolActionOfType(System.Type actionType)
        {
            if (toolActions == null) return false;
            foreach (var action in toolActions)
            {
                if (action != null && actionType.IsAssignableFrom(action.GetType()))
                    return true;
            }
            return false;
        }

        private void OnValidate()
        {
            // Auto-generate ID from name if empty
            if (string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(name))
            {
                itemId = name.ToLower().Replace(" ", "_");
            }
            
            // Ensure max stack is at least 1
            if (maxStackSize < 1) maxStackSize = 1;
            if (!isStackable) maxStackSize = 1;
            
            // Ensure durability is positive
            if (maxDurability < 0) maxDurability = 0;
        }
    }

    /// <summary>
    /// Categories for items (for UI filtering, etc.)
    /// </summary>
    public enum ItemCategory
    {
        Misc,
        Tool,
        Key,
        Resource,
        Quest
    }
}
