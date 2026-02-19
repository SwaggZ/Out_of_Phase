using UnityEngine;
using OutOfPhase.Dimension;

namespace OutOfPhase.Cinematic
{
    /// <summary>
    /// Trigger volume that plays a cinematic when the player enters.
    /// Positions in CinematicShots marked "useRelativeToTrigger" will be
    /// offset from this trigger's transform.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CinematicTrigger : MonoBehaviour
    {
        [Header("Cinematic")]
        [SerializeField] private CinematicData cinematic;

        [Header("Settings")]
        [Tooltip("If true, plays only once. If false, replays each time the player enters.")]
        [SerializeField] private bool oneShot = true;

        [Tooltip("Delay before the cinematic starts after entering the trigger.")]
        [SerializeField] private float startDelay = 0f;

        [Header("Audio")]
        [SerializeField] private AudioClip triggerSound;
        [SerializeField] private float triggerSoundVolume = 0.5f;

        private bool _hasPlayed;

        private void Start()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (oneShot && _hasPlayed) return;
            if (cinematic == null) return;

            // Only react to player
            if (other.GetComponent<Player.PlayerMovement>() == null &&
                other.GetComponentInParent<Player.PlayerMovement>() == null)
                return;

            // Don't interrupt another cinematic
            if (CinematicManager.Instance != null && CinematicManager.Instance.IsPlaying)
                return;

            _hasPlayed = true;

            if (triggerSound != null)
                SFXPlayer.PlayAtPoint(triggerSound, transform.position, triggerSoundVolume);

            if (startDelay > 0f)
                StartCoroutine(DelayedPlay());
            else
                Play();
        }

        private System.Collections.IEnumerator DelayedPlay()
        {
            yield return new WaitForSeconds(startDelay);
            Play();
        }

        private void Play()
        {
            if (CinematicManager.Instance == null)
            {
                var managerObj = new GameObject("CinematicManager");
                managerObj.AddComponent<CinematicManager>();
            }

            CinematicManager.Instance.PlayCinematic(cinematic, null, transform);
        }

        /// <summary> Allow replaying (e.g. after checkpoint reset). </summary>
        public void ResetTrigger()
        {
            _hasPlayed = false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);

            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 1f);
            }

            // Draw camera positions for first shot
            if (cinematic != null && cinematic.shots != null && cinematic.shots.Length > 0)
            {
                Gizmos.matrix = Matrix4x4.identity;
                foreach (var shot in cinematic.shots)
                {
                    Vector3 start = shot.useRelativeToTrigger
                        ? transform.TransformPoint(shot.startPosition) : shot.startPosition;
                    Vector3 end = shot.useRelativeToTrigger
                        ? transform.TransformPoint(shot.endPosition) : shot.endPosition;

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(start, 0.2f);
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(end, 0.2f);
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(start, end);
                }
            }
        }
    }
}
