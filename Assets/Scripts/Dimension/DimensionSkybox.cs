using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Swaps the skybox material when the active dimension changes.
    /// Assign one skybox material per dimension. Supports smooth
    /// blending between skybox colors (for procedural skyboxes)
    /// or instant swap (for cubemap skyboxes).
    /// </summary>
    public class DimensionSkybox : MonoBehaviour
    {
        [Header("Skybox per Dimension")]
        [Tooltip("One skybox material per dimension, indexed to match DimensionManager.")]
        [SerializeField] private DimensionSkyboxEntry[] skyboxEntries;

        [Header("Transition")]
        [Tooltip("Blend time between skyboxes (only for procedural skyboxes).")]
        [SerializeField] private float blendDuration = 1.5f;

        [Header("Fog per Dimension")]
        [SerializeField] private bool changeFog = true;

        // State
        private Material _currentSkybox;
        private Coroutine _blendCoroutine;

        private void OnEnable()
        {
            DimensionManager.OnManagerReady += OnManagerReady;

            if (DimensionManager.Instance != null)
                Subscribe();
        }

        private void OnDisable()
        {
            DimensionManager.OnManagerReady -= OnManagerReady;

            if (DimensionManager.Instance != null)
                DimensionManager.Instance.OnDimensionChanged -= OnDimensionChanged;
        }

        private void OnManagerReady() => Subscribe();

        private void Subscribe()
        {
            DimensionManager.Instance.OnDimensionChanged += OnDimensionChanged;

            // Apply starting skybox immediately
            ApplySkybox(DimensionManager.Instance.CurrentDimension, true);
        }

        private void OnDimensionChanged(int oldDim, int newDim)
        {
            ApplySkybox(newDim, false);
        }

        private void ApplySkybox(int dimension, bool instant)
        {
            if (skyboxEntries == null || dimension < 0 || dimension >= skyboxEntries.Length)
                return;

            var entry = skyboxEntries[dimension];
            if (entry.skyboxMaterial == null) return;

            // Swap skybox material
            RenderSettings.skybox = entry.skyboxMaterial;

            // Ambient light
            if (entry.useCustomAmbient)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = entry.ambientColor;
            }

            // Fog
            if (changeFog)
            {
                RenderSettings.fog = entry.enableFog;
                if (entry.enableFog)
                {
                    RenderSettings.fogColor = entry.fogColor;
                    RenderSettings.fogDensity = entry.fogDensity;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                }
            }

            // Sun / directional light tint
            if (entry.sunLight != null)
            {
                entry.sunLight.color = entry.sunColor;
                entry.sunLight.intensity = entry.sunIntensity;
            }

            // Force skybox reflection update
            DynamicGI.UpdateEnvironment();
        }
    }

    /// <summary>
    /// Per-dimension skybox configuration.
    /// </summary>
    [System.Serializable]
    public class DimensionSkyboxEntry
    {
        [Tooltip("Skybox material for this dimension.")]
        public Material skyboxMaterial;

        [Header("Ambient")]
        public bool useCustomAmbient = true;
        public Color ambientColor = new Color(0.2f, 0.2f, 0.25f, 1f);

        [Header("Fog")]
        public bool enableFog = true;
        public Color fogColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        [Range(0f, 0.1f)]
        public float fogDensity = 0.02f;

        [Header("Directional Light (Optional)")]
        [Tooltip("The scene's main directional light. Leave null to skip.")]
        public Light sunLight;
        public Color sunColor = Color.white;
        [Range(0f, 3f)]
        public float sunIntensity = 1f;
    }
}
