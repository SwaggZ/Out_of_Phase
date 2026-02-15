namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Interface for objects that can be unlocked with keys.
    /// </summary>
    public interface IKeyTarget
    {
        /// <summary>The lock ID this target requires (empty = any key)</summary>
        string LockId { get; }
        
        /// <summary>Whether this target is currently locked</summary>
        bool IsLocked { get; }
        
        /// <summary>
        /// Attempts to unlock with the given key.
        /// Returns true if successfully unlocked.
        /// </summary>
        bool TryUnlock(Items.KeyItemDefinition key);
    }
}
