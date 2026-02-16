using UnityEngine;
using OutOfPhase.Interaction;

namespace OutOfPhase.Items.ToolActions
{
    /// <summary>
    /// Key action for unlocking doors and locked objects.
    /// </summary>
    [CreateAssetMenu(fileName = "KeyAction", menuName = "Out of Phase/Tool Actions/Key")]
    public class KeyAction : ToolAction
    {
        [Header("Key Settings")]
        [Tooltip("Layer mask for lockable objects")]
        [SerializeField] private LayerMask lockableLayers = ~0;
        
        [Tooltip("Sound played when unlocking")]
        [SerializeField] private AudioClip unlockSound;
        
        [Tooltip("Sound played when lock doesn't match")]
        [SerializeField] private AudioClip failSound;

        public override bool Use(ToolUseContext context)
        {
            // The key item definition contains the key ID
            var keyItem = context.Item as KeyItemDefinition;
            if (keyItem == null) return false;

            // Raycast for lockable target
            if (context.TryRaycast(out RaycastHit hit, lockableLayers))
            {
                var keyTarget = hit.collider.GetComponent<IKeyTarget>();
                
                if (keyTarget != null && keyTarget.IsLocked)
                {
                    context.Target = hit.collider.gameObject;
                    context.HitInfo = hit;
                    
                    bool success = keyTarget.TryUnlock(keyItem);
                    
                    if (success)
                    {
                        // Play unlock sound
                        var sound = unlockSound != null ? unlockSound : GetRandomClip(useSounds);
                        if (sound != null)
                        {
                            AudioSource.PlayClipAtPoint(sound, hit.point);
                        }
                        
                        Debug.Log($"Unlocked with key: {keyItem.KeyId}");
                    }
                    else
                    {
                        // Wrong key
                        if (failSound != null)
                        {
                            AudioSource.PlayClipAtPoint(failSound, hit.point);
                        }
                        Debug.Log($"Key {keyItem.KeyId} doesn't fit lock {keyTarget.LockId}");
                    }
                    
                    return success;
                }
            }
            
            return false;
        }

        public override bool CanUseOn(GameObject target)
        {
            if (target == null) return false;
            var keyTarget = target.GetComponent<IKeyTarget>();
            return keyTarget != null && keyTarget.IsLocked;
        }
    }
}
