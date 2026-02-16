using UnityEngine;
using OutOfPhase.Interaction;

namespace OutOfPhase.Items.ToolActions
{
    /// <summary>
    /// Pickaxe action for mining rocks and breaking objects.
    /// </summary>
    [CreateAssetMenu(fileName = "PickaxeAction", menuName = "Out of Phase/Tool Actions/Pickaxe")]
    public class PickaxeAction : ToolAction
    {
        [Header("Pickaxe Settings")]
        [Tooltip("Damage dealt per hit")]
        [SerializeField] private float damage = 25f;
        
        [Tooltip("Layer mask for mineable objects")]
        [SerializeField] private LayerMask mineableLayers = ~0;
        
        [Tooltip("VFX prefab spawned on hit")]
        [SerializeField] private GameObject hitVFXPrefab;

        public float Damage => damage;

        public override bool Use(ToolUseContext context)
        {
            // Raycast for mineable target
            if (context.TryRaycast(out RaycastHit hit, mineableLayers))
            {
                var target = hit.collider.GetComponent<IToolTarget>();
                
                if (target != null && target.AcceptsToolAction(typeof(PickaxeAction)))
                {
                    // Update context with hit info
                    context.Target = hit.collider.gameObject;
                    context.HitInfo = hit;
                    
                    bool success = target.ReceiveToolAction(this, context);
                    
                    if (success)
                    {
                        // Spawn hit VFX
                        if (hitVFXPrefab != null)
                        {
                            var vfx = Instantiate(hitVFXPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                            Destroy(vfx, 2f);
                        }
                    }
                    
                    return success;
                }
            }
            
            return false;
        }

        public override bool CanUseOn(GameObject target)
        {
            if (target == null) return false;
            var toolTarget = target.GetComponent<IToolTarget>();
            return toolTarget != null && toolTarget.AcceptsToolAction(typeof(PickaxeAction));
        }
    }
}
