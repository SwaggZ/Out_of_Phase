using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Plays random ambient one-shot sounds at configurable intervals.
    /// Can be placed per-dimension (controlled by DimensionManager objects)
    /// or as a standalone ambient layer.
    /// Sounds play at random positions around the listener for immersion.
    /// </summary>
    public class AmbientOneShots : MonoBehaviour
    {
        [Header("Clips")]
        [Tooltip("Pool of ambient clips — one is chosen randomly each time")]
        [SerializeField] private AudioClip[] clips;

        [Header("Timing")]
        [Tooltip("Minimum seconds between one-shots")]
        [SerializeField] private float minInterval = 8f;

        [Tooltip("Maximum seconds between one-shots")]
        [SerializeField] private float maxInterval = 25f;

        [Header("Audio")]
        [SerializeField] private float volume = 0.3f;

        [Tooltip("Min distance from listener to spawn sound")]
        [SerializeField] private float minRadius = 5f;

        [Tooltip("Max distance from listener to spawn sound")]
        [SerializeField] private float maxRadius = 25f;

        [Header("Pitch Variation")]
        [SerializeField] private float minPitch = 0.85f;
        [SerializeField] private float maxPitch = 1.15f;

        private float _nextPlayTime;
        private AudioSource _source;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 1f; // 3D
            _source.minDistance = 2f;
            _source.maxDistance = maxRadius + 10f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.volume = 1f;
        }

        private void OnEnable()
        {
            ScheduleNext();
        }

        private void Update()
        {
            if (clips == null || clips.Length == 0) return;
            if (Time.time < _nextPlayTime) return;

            PlayRandomAmbient();
            ScheduleNext();
        }

        private void PlayRandomAmbient()
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            // Find listener (camera or player)
            Transform listener = null;
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camObj = GameObject.Find("PlayerCamera");
                if (camObj != null) cam = camObj.GetComponent<Camera>();
            }
            if (cam != null) listener = cam.transform;
            else listener = transform;

            // Random position around listener
            Vector3 offset = Random.onUnitSphere * Random.Range(minRadius, maxRadius);
            offset.y = Mathf.Clamp(offset.y, -3f, 10f); // Keep mostly horizontal

            _source.transform.position = listener.position + offset;
            _source.pitch = Random.Range(minPitch, maxPitch);
            _source.PlayOneShot(clip, volume);
        }

        private void ScheduleNext()
        {
            _nextPlayTime = Time.time + Random.Range(minInterval, maxInterval);
        }
    }
}
