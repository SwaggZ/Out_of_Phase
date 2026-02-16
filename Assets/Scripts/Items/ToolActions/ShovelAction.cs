using UnityEngine;
using OutOfPhase.Interaction;

namespace OutOfPhase.Items.ToolActions
{
    /// <summary>
    /// Shovel action for digging spots and uncovering buried items.
    /// </summary>
    [CreateAssetMenu(fileName = "ShovelAction", menuName = "Out of Phase/Tool Actions/Shovel")]
    public class ShovelAction : ToolAction
    {
        [Header("Shovel Settings")]
        [Tooltip("How fast digging progresses (per use)")]
        [SerializeField] private float digPower = 1f;
        
        [Tooltip("Layer mask for diggable objects")]
        [SerializeField] private LayerMask diggableLayers = ~0;
        
        [Tooltip("VFX prefab for digging")]
        [SerializeField] private GameObject digVFXPrefab;

        public float DigPower => digPower;

        public override bool Use(ToolUseContext context)
        {
            // Raycast for diggable target
            if (context.TryRaycast(out RaycastHit hit, diggableLayers))
            {
                var target = hit.collider.GetComponent<IToolTarget>();
                
                if (target != null && target.AcceptsToolAction(typeof(ShovelAction)))
                {
                    context.Target = hit.collider.gameObject;
                    context.HitInfo = hit;
                    
                    bool success = target.ReceiveToolAction(this, context);
                    
                    if (success)
                    {
                        // Spawn dig VFX
                        if (digVFXPrefab != null)
                        {
                            var vfx = Instantiate(digVFXPrefab, hit.point, Quaternion.identity);
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
            return toolTarget != null && toolTarget.AcceptsToolAction(typeof(ShovelAction));
        }
    }
}
