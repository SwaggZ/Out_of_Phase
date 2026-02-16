using UnityEngine;

namespace OutOfPhase.Items
{
    /// <summary>
    /// Base class for tool actions. Subclass this to create specific tool behaviors
    /// like Pickaxe, Shovel, Torch, etc.
    /// 
    /// Tool actions are ScriptableObjects that can be assigned to ItemDefinitions,
    /// allowing items to have multiple actions and easy extension of new tool types.
    /// </summary>
    public abstract class ToolAction : ScriptableObject
    {
        [Header("Base Tool Settings")]
        [Tooltip("Display name for this action")]
        [SerializeField] protected string actionName;
        
        [Tooltip("Durability cost per use")]
        [SerializeField] protected float durabilityCost = 1f;
        
        [Tooltip("Cooldown between uses in seconds")]
        [SerializeField] protected float cooldown = 0.5f;
        
        [Tooltip("Sounds to play when this tool hits (random pick)")]
        [SerializeField] protected AudioClip[] useSounds;

        [Tooltip("Sounds to play on every swing/use attempt, even on miss (random pick)")]
        [SerializeField] protected AudioClip[] swingSounds;

        [Tooltip("Sounds to play when this tool is equipped (random pick)")]
        [SerializeField] protected AudioClip[] equipSounds;

        // Public accessors
        public string ActionName => actionName;
        public float DurabilityCost => durabilityCost;
        public float Cooldown => cooldown;
        public AudioClip UseSound => GetRandomClip(useSounds);
        public AudioClip SwingSound => GetRandomClip(swingSounds);
        public AudioClip EquipSound => GetRandomClip(equipSounds);

        /// <summary>
        /// Returns a random clip from an array, or null if the array is empty/null.
        /// </summary>
        protected static AudioClip GetRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }

        /// <summary>
        /// Called when the player uses this tool action (left click).
        /// Override in subclasses to implement specific behavior.
        /// </summary>
        /// <param name="context">The interaction context containing player/tool info</param>
        /// <returns>True if the action was successfully performed</returns>
        public abstract bool Use(ToolUseContext context);

        /// <summary>
        /// Called to check if this tool can be used on a specific target.
        /// Override for target-specific tools like Pickaxe on MineableRock.
        /// </summary>
        public virtual bool CanUseOn(GameObject target)
        {
            return true;
        }

        /// <summary>
        /// Called every frame while the tool is equipped.
        /// Override for continuous effects like Torch light.
        /// </summary>
        public virtual void OnEquippedUpdate(ToolUseContext context)
        {
            // Default: no continuous effect
        }

        /// <summary>
        /// Called when the tool is equipped (selected in hotbar).
        /// </summary>
        public virtual void OnEquip(ToolUseContext context)
        {
            // Default: nothing
        }

        /// <summary>
        /// Called when the tool is unequipped (deselected from hotbar).
        /// </summary>
        public virtual void OnUnequip(ToolUseContext context)
        {
            // Default: nothing
        }
    }

    /// <summary>
    /// Context passed to tool actions containing all relevant information
    /// for performing the action.
    /// </summary>
    public struct ToolUseContext
    {
        /// <summary>The player's transform</summary>
        public Transform PlayerTransform;
        
        /// <summary>The camera transform (for raycasting)</summary>
        public Transform CameraTransform;
        
        /// <summary>The item being used</summary>
        public ItemDefinition Item;
        
        /// <summary>The inventory slot containing the item</summary>
        public Inventory.InventorySlot Slot;
        
        /// <summary>The target hit by raycast (if any)</summary>
        public GameObject Target;
        
        /// <summary>The raycast hit info (if any)</summary>
        public RaycastHit? HitInfo;
        
        /// <summary>Maximum interaction distance</summary>
        public float MaxDistance;
        
        /// <summary>The instantiated held model (if any)</summary>
        public GameObject HeldModelInstance;

        /// <summary>
        /// Performs a raycast from the camera forward.
        /// </summary>
        public bool TryRaycast(out RaycastHit hit, LayerMask layers)
        {
            if (CameraTransform == null)
            {
                hit = default;
                return false;
            }
            return Physics.Raycast(
                CameraTransform.position,
                CameraTransform.forward,
                out hit,
                MaxDistance,
                layers
            );
        }
    }
}
