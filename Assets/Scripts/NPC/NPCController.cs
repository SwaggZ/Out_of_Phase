using UnityEngine;
using OutOfPhase.Interaction;
using OutOfPhase.Dialogue;
using OutOfPhase.Quest;
using OutOfPhase.Dimension;

namespace OutOfPhase.NPC
{
    /// <summary>
    /// Base NPC component. Implements IInteractable to trigger dialogue.
    /// Place on any GameObject with a collider to create a talkable NPC.
    /// Features: look-at-player when nearby, dialogue trigger, optional one-time dialogues.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NPCController : MonoBehaviour, IInteractable
    {
        [Header("NPC Identity")]
        [Tooltip("Display name of this NPC")]
        [SerializeField] private string npcName = "NPC";

        [Header("Dialogue")]
        [Tooltip("The dialogue played when the player interacts")]
        [SerializeField] private DialogueData dialogue;

        [Tooltip("If true, this NPC can only be talked to once")]
        [SerializeField] private bool oneTimeDialogue;

        [Tooltip("Optional dialogue to play after the first conversation")]
        [SerializeField] private DialogueData repeatDialogue;

        [Header("Look At Player")]
        [Tooltip("NPC rotates to face the player when within this distance")]
        [SerializeField] private float lookAtDistance = 5f;

        [Tooltip("How fast the NPC turns toward the player (degrees/sec)")]
        [SerializeField] private float lookAtSpeed = 3f;

        [Tooltip("Only rotate on the Y axis")]
        [SerializeField] private bool yAxisOnly = true;

        [Header("Audio")]
        [Tooltip("Sound played when starting dialogue")]
        [SerializeField] private AudioClip greetingSound;
        [SerializeField] private float greetingSoundVolume = 0.5f;

        // State
        private bool _hasSpoken;
        private bool _inDialogue;
        private Transform _playerTransform;
        private Quaternion _originalRotation;

        // IInteractable
        public string InteractionPrompt => $"Talk to {npcName}";
        public bool CanInteract => !_inDialogue && !(oneTimeDialogue && _hasSpoken && repeatDialogue == null);

        private void Start()
        {
            _originalRotation = transform.rotation;

            // Find player
            var player = FindFirstObjectByType<Player.PlayerMovement>();
            if (player != null)
                _playerTransform = player.transform;

            // Ensure DialogueManager exists
            if (DialogueManager.Instance == null)
            {
                var dmObj = new GameObject("DialogueManager");
                dmObj.AddComponent<DialogueManager>();
            }
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, _playerTransform.position);

            if (dist <= lookAtDistance)
            {
                LookAtPlayer();
            }
            else
            {
                // Smoothly return to original rotation
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, _originalRotation, Time.deltaTime * lookAtSpeed);
            }
        }

        public void Interact(InteractionContext context)
        {
            if (_inDialogue) return;

            // Pick which dialogue to play
            DialogueData dialogueToPlay = dialogue;
            if (_hasSpoken && repeatDialogue != null)
            {
                dialogueToPlay = repeatDialogue;
            }

            if (dialogueToPlay == null || !dialogueToPlay.IsValid)
            {
                Debug.LogWarning($"[NPC] {npcName} has no valid dialogue to play.");
                return;
            }

            _inDialogue = true;

            // Play greeting sound
            if (greetingSound != null)
            {
                SFXPlayer.PlayAtPoint(greetingSound, transform.position, greetingSoundVolume);
            }

            _lastPlayedDialogue = dialogueToPlay;
            DialogueManager.Instance.StartDialogue(dialogueToPlay, transform, OnDialogueEnded);
        }

        private DialogueData _lastPlayedDialogue;

        private void OnDialogueEnded()
        {
            _inDialogue = false;
            _hasSpoken = true;

            // Notify quest system
            if (QuestManager.Instance != null)
                QuestManager.Instance.NotifyTalkedTo(npcName, _lastPlayedDialogue);
        }

        private void LookAtPlayer()
        {
            Vector3 direction = _playerTransform.position - transform.position;

            if (yAxisOnly)
                direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f) return;

            Quaternion targetRot = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, Time.deltaTime * lookAtSpeed);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, lookAtDistance);
        }
    }
}
