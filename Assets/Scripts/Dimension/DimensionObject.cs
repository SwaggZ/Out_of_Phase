using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Makes an object visible/interactable only in specific dimensions.
    /// Attach to any GameObject that should appear/disappear based on dimension.
    /// </summary>
    public class DimensionObject : MonoBehaviour
    {
        [Header("Dimension Visibility")]
        [Tooltip("Which dimensions this object is visible in")]
        [SerializeField] private bool[] visibleInDimensions = new bool[] { true, false, false, false, false };
        
        [Header("Behavior")]
        [Tooltip("How to hide the object when not in visible dimension")]
        [SerializeField] private HideMode hideMode = HideMode.DisableGameObject;
        
        [Tooltip("Fade duration when using renderer fade mode")]
        [SerializeField] private float fadeDuration = 0.3f;

        [Header("Collision")]
        [Tooltip("Disable colliders when hidden (for DisableRenderers mode)")]
        [SerializeField] private bool disableCollidersWhenHidden = true;

        // Cached components
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Material[] _originalMaterials;
        private bool _isVisible = true;
        private float _fadeProgress = 1f;
        private bool _isFading;

        public bool IsVisible => _isVisible;

        private void Awake()
        {
            // Cache components
            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
            
            // Store original materials for fade mode
            if (hideMode == HideMode.FadeRenderers)
            {
                CacheOriginalMaterials();
            }
        }

        private void OnEnable()
        {
            // Subscribe to manager ready event (in case manager isn't ready yet)
            DimensionManager.OnManagerReady += OnManagerReady;
            
            // Subscribe to dimension changes if manager already exists
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += OnDimensionChanged;
                
                // Initial visibility check
                UpdateVisibility(DimensionManager.Instance.CurrentDimension, true);
            }
        }

        private void OnDisable()
        {
            DimensionManager.OnManagerReady -= OnManagerReady;
            
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= OnDimensionChanged;
            }
        }
        
        private void OnManagerReady()
        {
            // Manager just became ready, subscribe now
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += OnDimensionChanged;
                UpdateVisibility(DimensionManager.Instance.CurrentDimension, true);
            }
        }

        private void Start()
        {
            // Re-check visibility in case DimensionManager wasn't ready in OnEnable
            if (DimensionManager.Instance != null)
            {
                UpdateVisibility(DimensionManager.Instance.CurrentDimension, true);
            }
        }

        private void Update()
        {
            // Handle fading
            if (_isFading && hideMode == HideMode.FadeRenderers)
            {
                UpdateFade();
            }
        }

        private void OnDimensionChanged(int oldDimension, int newDimension)
        {
            UpdateVisibility(newDimension, false);
        }

        private void UpdateVisibility(int dimension, bool instant)
        {
            bool shouldBeVisible = IsVisibleInDimension(dimension);
            
            if (shouldBeVisible == _isVisible && !instant)
                return;
            
            _isVisible = shouldBeVisible;
            
            switch (hideMode)
            {
                case HideMode.DisableGameObject:
                    gameObject.SetActive(_isVisible);
                    break;
                    
                case HideMode.DisableRenderers:
                    SetRenderersEnabled(_isVisible);
                    if (disableCollidersWhenHidden)
                        SetCollidersEnabled(_isVisible);
                    break;
                    
                case HideMode.FadeRenderers:
                    if (instant)
                    {
                        _fadeProgress = _isVisible ? 1f : 0f;
                        ApplyFade(_fadeProgress);
                        if (disableCollidersWhenHidden)
                            SetCollidersEnabled(_isVisible);
                    }
                    else
                    {
                        StartFade(_isVisible);
                    }
                    break;
            }
        }

        private bool IsVisibleInDimension(int dimension)
        {
            if (visibleInDimensions == null || dimension < 0)
                return true;
            
            if (dimension >= visibleInDimensions.Length)
                return false;
            
            return visibleInDimensions[dimension];
        }

        private void SetRenderersEnabled(bool enabled)
        {
            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                    renderer.enabled = enabled;
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (var collider in _colliders)
            {
                if (collider != null)
                    collider.enabled = enabled;
            }
        }

        #region Fade Mode

        private void CacheOriginalMaterials()
        {
            int count = 0;
            foreach (var r in _renderers)
            {
                if (r != null) count += r.materials.Length;
            }
            
            _originalMaterials = new Material[count];
            int index = 0;
            foreach (var r in _renderers)
            {
                if (r != null)
                {
                    foreach (var mat in r.materials)
                    {
                        _originalMaterials[index++] = mat;
                    }
                }
            }
        }

        private void StartFade(bool fadeIn)
        {
            _isFading = true;
            
            // Enable colliders at start of fade-in
            if (fadeIn && disableCollidersWhenHidden)
            {
                SetCollidersEnabled(true);
            }
        }

        private void UpdateFade()
        {
            float targetAlpha = _isVisible ? 1f : 0f;
            float fadeSpeed = 1f / Mathf.Max(fadeDuration, 0.01f);
            
            _fadeProgress = Mathf.MoveTowards(_fadeProgress, targetAlpha, fadeSpeed * Time.deltaTime);
            ApplyFade(_fadeProgress);
            
            // Check if fade complete
            if (Mathf.Approximately(_fadeProgress, targetAlpha))
            {
                _isFading = false;
                
                // Disable colliders after fade-out
                if (!_isVisible && disableCollidersWhenHidden)
                {
                    SetCollidersEnabled(false);
                }
            }
        }

        private void ApplyFade(float alpha)
        {
            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;
                
                foreach (var mat in renderer.materials)
                {
                    if (mat.HasProperty("_Color"))
                    {
                        Color c = mat.color;
                        c.a = alpha;
                        mat.color = c;
                    }
                    
                    // Handle URP/HDRP surface type for transparency
                    if (alpha < 1f)
                    {
                        mat.SetFloat("_Surface", 1); // Transparent
                        mat.SetFloat("_Blend", 0); // Alpha
                        mat.renderQueue = 3000;
                    }
                    else
                    {
                        mat.SetFloat("_Surface", 0); // Opaque
                        mat.renderQueue = 2000;
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Sets which dimensions this object is visible in.
        /// </summary>
        public void SetVisibleDimensions(params int[] dimensions)
        {
            int count = DimensionManager.Instance != null 
                ? DimensionManager.Instance.DimensionCount 
                : 5;
            
            visibleInDimensions = new bool[count];
            
            foreach (int dim in dimensions)
            {
                if (dim >= 0 && dim < count)
                {
                    visibleInDimensions[dim] = true;
                }
            }
            
            // Update visibility immediately
            if (DimensionManager.Instance != null)
            {
                UpdateVisibility(DimensionManager.Instance.CurrentDimension, true);
            }
        }

        private void OnValidate()
        {
            // Ensure array has correct size
            int count = 5; // Default
            if (visibleInDimensions == null || visibleInDimensions.Length != count)
            {
                bool[] newArray = new bool[count];
                if (visibleInDimensions != null)
                {
                    for (int i = 0; i < Mathf.Min(visibleInDimensions.Length, count); i++)
                    {
                        newArray[i] = visibleInDimensions[i];
                    }
                }
                else
                {
                    newArray[0] = true; // Default visible in first dimension
                }
                visibleInDimensions = newArray;
            }
        }
    }

    /// <summary>
    /// How a DimensionObject hides itself when not in a visible dimension.
    /// </summary>
    public enum HideMode
    {
        /// <summary>Completely disables the GameObject</summary>
        DisableGameObject,
        
        /// <summary>Only disables renderers (keeps scripts running)</summary>
        DisableRenderers,
        
        /// <summary>Fades renderers in/out (requires transparent materials)</summary>
        FadeRenderers
    }
}
