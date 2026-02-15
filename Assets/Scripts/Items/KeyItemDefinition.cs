using UnityEngine;

namespace OutOfPhase.Items
{
    /// <summary>
    /// Specialized item definition for keys.
    /// Keys have an ID that must match lock IDs to unlock them.
    /// </summary>
    [CreateAssetMenu(fileName = "New Key", menuName = "Out of Phase/Items/Key Item")]
    public class KeyItemDefinition : ItemDefinition
    {
        [Header("Key Settings")]
        [Tooltip("Unique key identifier. Must match lock ID to unlock.")]
        [SerializeField] private string keyId;
        
        [Tooltip("If true, this key can unlock any lock (master key)")]
        [SerializeField] private bool isMasterKey = false;
        
        [Tooltip("If true, key is consumed when used")]
        [SerializeField] private bool consumeOnUse = false;
        
        [Tooltip("Description of what this key unlocks (for UI hints)")]
        [SerializeField] private string unlocksDescription;

        public string KeyId => keyId;
        public bool IsMasterKey => isMasterKey;
        public bool ConsumeOnUse => consumeOnUse;
        public string UnlocksDescription => unlocksDescription;

        /// <summary>
        /// Checks if this key can unlock a specific lock ID.
        /// </summary>
        public bool CanUnlock(string lockId)
        {
            if (isMasterKey) return true;
            if (string.IsNullOrEmpty(lockId)) return true; // No lock = any key works
            return keyId == lockId;
        }

        private void OnValidate()
        {
            // Auto-generate key ID from name if empty
            if (string.IsNullOrEmpty(keyId) && !string.IsNullOrEmpty(name))
            {
                keyId = "key_" + name.ToLower().Replace(" ", "_");
            }
        }
    }
}
