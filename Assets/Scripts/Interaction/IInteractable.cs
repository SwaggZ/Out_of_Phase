namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Interface for objects that can be interacted with using E key.
    /// Basic interaction without requiring any tool.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Display name shown in interaction prompt</summary>
        string InteractionPrompt { get; }
        
        /// <summary>Whether this object can currently be interacted with</summary>
        bool CanInteract { get; }
        
        /// <summary>Called when player presses E on this object</summary>
        void Interact(InteractionContext context);
    }

    /// <summary>
    /// Context passed to interactables containing player/interaction info.
    /// </summary>
    public struct InteractionContext
    {
        /// <summary>The player's transform</summary>
        public UnityEngine.Transform PlayerTransform;
        
        /// <summary>The interactor component</summary>
        public Interactor Interactor;
        
        /// <summary>The inventory (for giving items, etc.)</summary>
        public Inventory.Inventory Inventory;
        
        /// <summary>The hotbar controller</summary>
        public Inventory.HotbarController Hotbar;
    }
}
