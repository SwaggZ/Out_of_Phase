using UnityEngine;

namespace OutOfPhase.Items.ToolActions
{
    /// <summary>
    /// Torch action providing light when equipped.
    /// Can also be used to light/ignite things.
    /// </summary>
    [CreateAssetMenu(fileName = "TorchAction", menuName = "Out of Phase/Tool Actions/Torch")]
    public class TorchAction : ToolAction
    {
        [Header("Torch Light Settings")]
        [Tooltip("Light color when equipped")]
        [SerializeField] private Color lightColor = new Color(1f, 0.8f, 0.4f);
        
        [Tooltip("Light intensity")]
        [SerializeField] private float lightIntensity = 1.5f;
        
        [Tooltip("Light range")]
        [SerializeField] private float lightRange = 10f;
        
        [Tooltip("Enable flickering effect")]
        [SerializeField] private bool enableFlicker = true;
        
        [Tooltip("Flicker speed")]
        [SerializeField] private float flickerSpeed = 15f;
        
        [Tooltip("Flicker intensity variation")]
        [SerializeField] private float flickerAmount = 0.2f;

        [Header("Ignite Settings")]
        [Tooltip("Can this torch ignite things?")]
        [SerializeField] private bool canIgnite = true;
        
        [Tooltip("Layer mask for ignitable objects")]
        [SerializeField] private LayerMask ignitableLayers = ~0;

        // Runtime references (created when equipped)
        private Light _activeLight;
        private float _baseIntensity;

        public Color LightColor => lightColor;
        public float LightIntensity => lightIntensity;
        public float LightRange => lightRange;
        public bool CanIgnite => canIgnite;

        public override bool Use(ToolUseContext context)
        {
            if (!canIgnite) return false;

            // Try to ignite something
            if (context.TryRaycast(out RaycastHit hit, ignitableLayers))
            {
                var ignitable = hit.collider.GetComponent<Interaction.IToolTarget>();
                
                if (ignitable != null && ignitable.AcceptsToolAction(typeof(TorchAction)))
                {
                    context.Target = hit.collider.gameObject;
                    context.HitInfo = hit;
                    
                    bool success = ignitable.ReceiveToolAction(this, context);
                    
                    if (success)
                    {
                        var clip = GetRandomClip(useSounds);
                        if (clip != null)
                            AudioSource.PlayClipAtPoint(clip, hit.point);
                    }
                    
                    return success;
                }
            }
            
            return false;
        }

        public override void OnEquip(ToolUseContext context)
        {
            if (context.CameraTransform == null || _activeLight != null) return;

            // Play equip sound
            var eqClip = GetRandomClip(equipSounds);
            if (eqClip != null && context.PlayerTransform != null)
            {
                AudioSource.PlayClipAtPoint(eqClip, context.PlayerTransform.position, 0.5f);
            }

            Transform lightParent = context.CameraTransform;
            Vector3 lightLocalPos = new Vector3(0.3f, -0.2f, 0.5f); // Default offset

            // Look for LightPoint in the held model instance (already created by HotbarController)
            if (context.HeldModelInstance != null)
            {
                Transform lightPoint = context.HeldModelInstance.transform.Find("LightPoint");
                if (lightPoint != null)
                {
                    lightParent = lightPoint;
                    lightLocalPos = Vector3.zero; // Light will be exactly at the LightPoint
                }
                else
                {
                    // No LightPoint found, use the model root
                    lightParent = context.HeldModelInstance.transform;
                }
            }

            // Create the light
            GameObject lightObj = new GameObject("TorchLight");
            lightObj.transform.SetParent(lightParent);
            lightObj.transform.localPosition = lightLocalPos;
            lightObj.transform.localRotation = Quaternion.identity;
            
            _activeLight = lightObj.AddComponent<Light>();
            _activeLight.type = LightType.Point;
            _activeLight.color = lightColor;
            _activeLight.intensity = lightIntensity;
            _activeLight.range = lightRange;
            _activeLight.shadows = LightShadows.Soft;
            
            _baseIntensity = lightIntensity;
        }

        public override void OnUnequip(ToolUseContext context)
        {
            // Destroy the light (held model is managed by HotbarController)
            if (_activeLight != null)
            {
                Object.Destroy(_activeLight.gameObject);
                _activeLight = null;
            }
        }

        public override void OnEquippedUpdate(ToolUseContext context)
        {
            // Flicker effect
            if (_activeLight != null && enableFlicker)
            {
                float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
                float flicker = 1f + (noise - 0.5f) * 2f * flickerAmount;
                _activeLight.intensity = _baseIntensity * flicker;
            }
        }

        public override bool CanUseOn(GameObject target)
        {
            if (!canIgnite || target == null) return false;
            var toolTarget = target.GetComponent<Interaction.IToolTarget>();
            return toolTarget != null && toolTarget.AcceptsToolAction(typeof(TorchAction));
        }
    }
}
