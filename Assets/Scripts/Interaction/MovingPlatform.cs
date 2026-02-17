using UnityEngine;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Platform that moves back and forth between two points.
    /// Carries the player (and pushable boxes) along with it.
    /// Set start/end positions via the Inspector or by using the A/B gizmo handles.
    /// </summary>
    public class MovingPlatform : MonoBehaviour
    {
        [Header("Positions")]
        [Tooltip("Start position (world space). Defaults to the object's initial position.")]
        [SerializeField] private Vector3 pointA;

        [Tooltip("End position (world space).")]
        [SerializeField] private Vector3 pointB;

        [Tooltip("Use current transform position as Point A on Awake.")]
        [SerializeField] private bool useCurrentPosAsA = true;

        [Header("Movement")]
        [Tooltip("Travel speed in units per second.")]
        [SerializeField] private float speed = 2f;

        [Tooltip("Pause time at each end point.")]
        [SerializeField] private float waitTime = 0.5f;

        [Header("Easing")]
        [Tooltip("Smooth acceleration/deceleration at endpoints.")]
        [SerializeField] private bool useEasing = true;

        [Header("Audio")]
        [SerializeField] private AudioClip moveLoopSound;
        [SerializeField] private float moveVolume = 0.2f;

        // State
        private float _t; // 0 = pointA, 1 = pointB
        private bool _movingToB = true;
        private float _waitTimer;
        private AudioSource _audioSource;

        private void Awake()
        {
            if (useCurrentPosAsA)
                pointA = transform.position;

            transform.position = pointA;

            if (moveLoopSound != null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.clip = moveLoopSound;
                _audioSource.loop = true;
                _audioSource.volume = moveVolume;
                _audioSource.spatialBlend = 1f;
                _audioSource.playOnAwake = false;
            }
        }

        private void Update()
        {
            if (_waitTimer > 0f)
            {
                _waitTimer -= Time.deltaTime;

                if (_audioSource != null && _audioSource.isPlaying)
                    _audioSource.Stop();

                return;
            }

            float distance = Vector3.Distance(pointA, pointB);
            if (distance < 0.001f) return;

            float step = (speed / distance) * Time.deltaTime;

            if (_movingToB)
                _t += step;
            else
                _t -= step;

            // Clamp and check endpoints
            if (_t >= 1f)
            {
                _t = 1f;
                _movingToB = false;
                _waitTimer = waitTime;
            }
            else if (_t <= 0f)
            {
                _t = 0f;
                _movingToB = true;
                _waitTimer = waitTime;
            }

            // Apply position
            float eval = useEasing ? SmoothStep(_t) : _t;
            transform.position = Vector3.Lerp(pointA, pointB, eval);

            // Audio
            if (_audioSource != null && !_audioSource.isPlaying && _waitTimer <= 0f)
                _audioSource.Play();
        }

        private float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        // ── Carry passengers (player / boxes) ──────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.CompareTag("PushableBox"))
            {
                other.transform.SetParent(transform);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || other.CompareTag("PushableBox"))
            {
                other.transform.SetParent(null);
            }
        }

        // ── Gizmos ─────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Vector3 a = useCurrentPosAsA && !Application.isPlaying ? transform.position : pointA;
            Vector3 b = pointB;

            // Points
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawSphere(a, 0.15f);
            Gizmos.DrawSphere(b, 0.15f);

            // Path line
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawLine(a, b);

            // Platform ghost at B
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.matrix = Matrix4x4.TRS(b, transform.rotation, transform.lossyScale);
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 a = useCurrentPosAsA && !Application.isPlaying ? transform.position : pointA;
            Vector3 b = pointB;

            // Labels
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(a, 0.2f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(b, 0.2f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(a, b);
        }
    }
}
