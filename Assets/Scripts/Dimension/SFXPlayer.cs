using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Lightweight singleton for playing one-shot 2D/3D sound effects.
    /// Uses a small pool of AudioSources to avoid the garbage from AudioSource.PlayClipAtPoint.
    /// Lives across scenes via DontDestroyOnLoad.
    /// </summary>
    public class SFXPlayer : MonoBehaviour
    {
        public static SFXPlayer Instance { get; private set; }

        [Header("Pool")]
        [Tooltip("Number of pooled AudioSources for 3D sounds")]
        [SerializeField] private int poolSize = 8;

        private AudioSource _source2D;
        private AudioSource[] _pool3D;
        private int _nextPool;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 2D source for UI / non-positional sounds
            _source2D = CreateSource("SFX_2D", spatialBlend: 0f);

            // 3D pool
            _pool3D = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                _pool3D[i] = CreateSource($"SFX_3D_{i}", spatialBlend: 1f);
            }
        }

        /// <summary>
        /// Play a 2D (non-positional) sound effect. Good for UI, pickups, hotbar clicks.
        /// </summary>
        public void Play2D(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            _source2D.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Play a 3D (positional) sound at a world position. Good for impacts, breaks, ambient pings.
        /// </summary>
        public void Play3D(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource src = _pool3D[_nextPool];
            src.transform.position = position;
            src.PlayOneShot(clip, volume);
            _nextPool = (_nextPool + 1) % _pool3D.Length;
        }

        /// <summary>
        /// Play a random clip from an array at a 3D position.
        /// </summary>
        public void PlayRandom3D(AudioClip[] clips, Vector3 position, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return;
            Play3D(clips[Random.Range(0, clips.Length)], position, volume);
        }

        /// <summary>
        /// Play a random clip from an array as a 2D sound.
        /// </summary>
        public void PlayRandom2D(AudioClip[] clips, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return;
            Play2D(clips[Random.Range(0, clips.Length)], volume);
        }

        private AudioSource CreateSource(string name, float spatialBlend)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform);
            AudioSource src = obj.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = spatialBlend;
            src.volume = 1f;
            src.minDistance = 1f;
            src.maxDistance = 30f;
            src.rolloffMode = AudioRolloffMode.Linear;
            return src;
        }
    }
}
