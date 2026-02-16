using System;
using UnityEngine;

namespace OutOfPhase.Cinematic
{
    /// <summary>
    /// ScriptableObject defining a cinematic sequence.
    /// Each cinematic is a series of shots: camera position/rotation,
    /// optional movement, and dialogue-style text at the bottom.
    /// Text uses the same DialogueManager typewriter mechanic (no voice).
    /// </summary>
    [CreateAssetMenu(fileName = "New Cinematic", menuName = "Out of Phase/Cinematic/Cinematic Data")]
    public class CinematicData : ScriptableObject
    {
        [Tooltip("Display name (for debugging / editor).")]
        public string cinematicName = "Cinematic";

        [Tooltip("Ordered list of shots in this cinematic.")]
        public CinematicShot[] shots;

        [Tooltip("Black bars (letterbox) during the cinematic.")]
        public bool showLetterbox = true;

        [Tooltip("Fade-in time at start of the cinematic.")]
        public float fadeInDuration = 0.5f;

        [Tooltip("Fade-out time at end of the cinematic.")]
        public float fadeOutDuration = 0.5f;

        public bool IsValid => shots != null && shots.Length > 0;
    }

    /// <summary>
    /// One shot in a cinematic sequence.
    /// The camera moves from startPosition/Rotation to endPosition/Rotation
    /// over the shot's duration, while text is displayed.
    /// </summary>
    [Serializable]
    public class CinematicShot
    {
        [Header("Camera")]
        [Tooltip("World-space start position. If useRelativeToTrigger is true, this is offset from the trigger.")]
        public Vector3 startPosition;
        public Vector3 startRotation; // Euler angles

        [Tooltip("World-space end position. Camera lerps here over duration.")]
        public Vector3 endPosition;
        public Vector3 endRotation;

        [Tooltip("If true, positions are relative to the CinematicTrigger transform.")]
        public bool useRelativeToTrigger = true;

        [Tooltip("Duration of this shot in seconds.")]
        public float duration = 4f;

        [Tooltip("Easing curve for the camera movement.")]
        public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Text (Dialogue-style)")]
        [Tooltip("Speaker name (leave empty for no speaker label).")]
        public string speakerName;

        [Tooltip("Text displayed at the bottom during this shot. Leave empty for no text.")]
        [TextArea(2, 5)]
        public string text;

        [Tooltip("Characters per second for typewriter.")]
        public float charsPerSecond = 40f;

        [Tooltip("Wait for player to press E/click before advancing. If false, auto-advances after duration.")]
        public bool waitForInput = false;

        [Header("Transition")]
        [Tooltip("Pause (black screen) between this shot and the next.")]
        public float pauseAfter = 0f;

        public bool HasText => !string.IsNullOrEmpty(text);
    }
}
