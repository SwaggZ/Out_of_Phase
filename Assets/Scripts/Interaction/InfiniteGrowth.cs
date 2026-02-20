using UnityEngine;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Continuously grows a target GameObject's scale over time.
    /// Useful for making particle effects like SpaceTear expand infinitely.
    /// </summary>
    public class InfiniteGrowth : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The GameObject to scale. If empty, uses this GameObject.")]
        [SerializeField] private Transform target;

        [Header("Growth Settings")]
        [Tooltip("How fast the object grows per second (additive scale).")]
        [SerializeField] private float growthRate = 0.5f;

        [Tooltip("If true, growth rate increases over time (exponential growth).")]
        [SerializeField] private bool exponentialGrowth = false;

        [Tooltip("Multiplier for exponential growth acceleration.")]
        [SerializeField] private float exponentialFactor = 1.1f;

        [Header("Starting Scale")]
        [Tooltip("Starting scale multiplier (1 = current scale).")]
        [SerializeField] private float startingScale = 1f;

        [Header("Optional Limits")]
        [Tooltip("Maximum scale (0 = no limit).")]
        [SerializeField] private float maxScale = 0f;

        [Tooltip("Delay before growth starts (seconds).")]
        [SerializeField] private float startDelay = 0f;

        private Vector3 _initialScale;
        private float _currentGrowthRate;
        private float _elapsedTime;
        private bool _isGrowing;

        private void Start()
        {
            if (target == null)
                target = transform;

            _initialScale = target.localScale;
            target.localScale = _initialScale * startingScale;
            _currentGrowthRate = growthRate;

            if (startDelay <= 0f)
                _isGrowing = true;
            else
                Invoke(nameof(StartGrowth), startDelay);
        }

        private void StartGrowth()
        {
            _isGrowing = true;
        }

        private void Update()
        {
            if (!_isGrowing || target == null) return;

            _elapsedTime += Time.deltaTime;

            // Calculate growth
            float growth = _currentGrowthRate * Time.deltaTime;
            target.localScale += Vector3.one * growth;

            // Apply exponential acceleration if enabled
            if (exponentialGrowth)
            {
                _currentGrowthRate *= Mathf.Pow(exponentialFactor, Time.deltaTime);
            }

            // Check max scale limit
            if (maxScale > 0f && target.localScale.x >= maxScale)
            {
                target.localScale = Vector3.one * maxScale;
                _isGrowing = false;
            }
        }

        /// <summary>
        /// Start or resume growth.
        /// </summary>
        public void StartGrowing()
        {
            _isGrowing = true;
        }

        /// <summary>
        /// Pause growth.
        /// </summary>
        public void StopGrowing()
        {
            _isGrowing = false;
        }

        /// <summary>
        /// Reset to initial scale and restart growth.
        /// </summary>
        public void ResetGrowth()
        {
            if (target != null)
                target.localScale = _initialScale * startingScale;

            _currentGrowthRate = growthRate;
            _elapsedTime = 0f;
            _isGrowing = true;
        }

        /// <summary>
        /// Set a new growth rate at runtime.
        /// </summary>
        public void SetGrowthRate(float rate)
        {
            _currentGrowthRate = rate;
            growthRate = rate;
        }
    }
}
