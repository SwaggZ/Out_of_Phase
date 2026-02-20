using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OutOfPhase.Interaction;
using OutOfPhase.Dialogue;
using OutOfPhase.Player;
using OutOfPhase.Progression;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Represents a single phase point with a position and target dimension.
    /// </summary>
    [System.Serializable]
    public class PhasePoint
    {
        [Tooltip("The transform position/rotation to teleport the player to.")]
        public Transform position;

        [Tooltip("The dimension to switch to at this phase point.")]
        public int dimensionIndex;
    }

    /// <summary>
    /// The White Hole Core interactable that triggers the game's ending sequence.
    /// Handles monologues, dimensional phasing sequence, and the final choice.
    /// </summary>
    public class WhiteHoleCoreInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [SerializeField] private string interactionPrompt = "Examine Core";
        [SerializeField] private bool hasBeenTriggered = false;

        [Header("Monologues")]
        [Tooltip("First monologue that plays when player interacts with the core.")]
        [SerializeField] private DialogueData firstMonologue;

        [Tooltip("Second monologue that plays after the phasing sequence, with the final choice.")]
        [SerializeField] private DialogueData secondMonologue;

        [Header("Phasing Sequence")]
        [Tooltip("Phase points with positions and dimensions for the phasing sequence.")]
        [SerializeField] private PhasePoint[] phasePoints;

        [Tooltip("Time between each phase jump (seconds).")]
        [SerializeField] private float timeBetweenPhases = 0.4f;

        [Tooltip("Number of rapid phase cycles through all positions.")]
        [SerializeField] private int phaseCycles = 3;

        [Tooltip("Position to return to after phasing sequence (near the core).")]
        [SerializeField] private Transform returnPosition;

        [Tooltip("Objects to disable during the phasing sequence.")]
        [SerializeField] private GameObject[] disableDuringPhasing;

        [Tooltip("Enable all game sections during phasing (for teleporting across the map).")]
        [SerializeField] private bool enableAllSectionsDuringPhasing = true;

        [Header("Visual Effects")]
        [Tooltip("VFX to spawn during phasing.")]
        [SerializeField] private GameObject phasingVFX;

        [Tooltip("Screen overlay color during phasing.")]
        [SerializeField] private Color phasingFlashColor = new Color(1f, 1f, 1f, 0.8f);

        [Tooltip("Duration of screen flash effect.")]
        [SerializeField] private float flashDuration = 0.15f;

        [Header("Audio")]
        [SerializeField] private AudioClip interactSound;
        [SerializeField] private AudioClip phasingSound;
        [SerializeField] private AudioClip returnSound;
        [SerializeField] private float soundVolume = 0.7f;

        [Header("Ending - Destroy Device (Save World)")]
        [Tooltip("Camera to enable for the 'destroy device' ending cutscene.")]
        [SerializeField] private Camera endingCamera;

        [Tooltip("Objects to animate/destroy during collapse.")]
        [SerializeField] private GameObject[] collapseTargets;

        [Tooltip("Duration of the collapse sequence.")]
        [SerializeField] private float collapseDuration = 8f;

        [Tooltip("VFX for the collapse effect.")]
        [SerializeField] private GameObject collapseVFX;

        [Tooltip("Audio for collapse sequence.")]
        [SerializeField] private AudioClip collapseSound;

        [Tooltip("Final fade to black duration.")]
        [SerializeField] private float fadeToBlackDuration = 3f;

        [Header("Ending - Keep Device (Abstract Expands)")]
        [Tooltip("VFX for the abstract expansion effect.")]
        [SerializeField] private GameObject expansionVFX;

        [Tooltip("Audio for expansion sequence.")]
        [SerializeField] private AudioClip expansionSound;

        [Tooltip("Duration of expansion sequence.")]
        [SerializeField] private float expansionDuration = 6f;

        // === Events (C# events, not shown in inspector) ===
        /// <summary>Called when the first monologue ends (before phasing).</summary>
        public event Action OnFirstMonologueComplete;

        /// <summary>Called when the phasing sequence ends.</summary>
        public event Action OnPhasingComplete;

        /// <summary>Called when player chooses 'Destroy Device' ending.</summary>
        public event Action OnDestroyDeviceChosen;

        /// <summary>Called when player chooses 'Keep Device' ending.</summary>
        public event Action OnKeepDeviceChosen;

        /// <summary>Called when any ending sequence completes.</summary>
        public event Action OnEndingComplete;

        // State
        private enum EndingState
        {
            Idle,
            FirstMonologue,
            Phasing,
            SecondMonologue,
            EndingCutscene
        }
        private EndingState _currentState = EndingState.Idle;
        private Camera _playerCamera;
        private PlayerMovement _playerMovement;
        private PlayerLook _playerLook;
        private CanvasGroup _flashOverlay;
        private CanvasGroup _fadeOverlay;

        // IInteractable
        public string InteractionPrompt => interactionPrompt;
        public bool CanInteract => !hasBeenTriggered && _currentState == EndingState.Idle;

        private void Start()
        {
            // Cache player references
            _playerMovement = FindFirstObjectByType<PlayerMovement>();
            _playerLook = FindFirstObjectByType<PlayerLook>();
            if (_playerMovement != null)
                _playerCamera = _playerMovement.GetComponentInChildren<Camera>();

            // Ensure ending camera is initially disabled
            if (endingCamera != null)
                endingCamera.enabled = false;

            // Create screen overlay for flash effects
            CreateScreenOverlays();
        }

        private void OnDisable()
        {
            Debug.LogWarning($"[WhiteHoleCore] OnDisable called! State: {_currentState}, Stack: {System.Environment.StackTrace}");
        }

        private void OnDestroy()
        {
            Debug.LogWarning($"[WhiteHoleCore] OnDestroy called! State: {_currentState}");
        }

        public void Interact(InteractionContext context)
        {
            if (!CanInteract) return;

            hasBeenTriggered = true;
            StartCoroutine(EndingSequence());
        }

        private IEnumerator EndingSequence()
        {
            // Play interact sound
            if (interactSound != null)
                SFXPlayer.PlayAtPoint(interactSound, transform.position, soundVolume);

            // === FIRST MONOLOGUE ===
            _currentState = EndingState.FirstMonologue;

            if (firstMonologue != null && DialogueManager.Instance != null)
            {
                bool dialogueComplete = false;
                DialogueManager.Instance.OnDialogueEnded += () => dialogueComplete = true;
                DialogueManager.Instance.StartDialogue(firstMonologue, transform, null);

                // Wait for dialogue to complete
                while (!dialogueComplete)
                    yield return null;
            }

            OnFirstMonologueComplete?.Invoke();
            yield return new WaitForSeconds(0.5f);

            // === PHASING SEQUENCE ===
            _currentState = EndingState.Phasing;

            if (phasePoints != null && phasePoints.Length > 0)
            {
                yield return StartCoroutine(PhasingSequence());
            }

            OnPhasingComplete?.Invoke();

            // Return to position near core
            if (returnPosition != null)
            {
                yield return StartCoroutine(TeleportPlayer(returnPosition.position, returnPosition.rotation));
                if (returnSound != null)
                    SFXPlayer.PlayAtPoint(returnSound, returnPosition.position, soundVolume);
            }

            // Ensure we're in the Abstract dimension (assuming it's dimension 4 or configurable)
            if (DimensionManager.Instance != null)
            {
                int abstractDimension = DimensionManager.Instance.DimensionCount - 1; // Last dimension = Abstract
                DimensionManager.Instance.AbsoluteForceSwitchToDimension(abstractDimension);
            }

            yield return new WaitForSeconds(0.5f);

            // === SECOND MONOLOGUE WITH CHOICE ===
            _currentState = EndingState.SecondMonologue;

            if (secondMonologue != null && DialogueManager.Instance != null)
            {
                bool dialogueComplete = false;
                int choiceIndex = -1;

                // Subscribe to dialogue end to capture which choice was made
                void OnEnded()
                {
                    dialogueComplete = true;
                }

                DialogueManager.Instance.OnDialogueEnded += OnEnded;
                DialogueManager.Instance.StartDialogue(secondMonologue, transform, null);

                // Wait for dialogue (with choice) to complete
                while (!dialogueComplete)
                    yield return null;

                DialogueManager.Instance.OnDialogueEnded -= OnEnded;
            }

            // Note: The choice handling is done via the dialogue system's branching.
            // The dialogue data should have choices that lead to different end nodes.
            // We'll provide public methods to trigger each ending from dialogue events.
        }

        /// <summary>
        /// Call this from a dialogue event or choice to trigger the "Destroy Device" ending.
        /// The player sacrifices themselves to save all dimensions.
        /// </summary>
        [ContextMenu("Trigger Destroy Device Ending")]
        public void TriggerDestroyDeviceEnding()
        {
            StartCoroutine(DestroyDeviceEndingSequence());
        }

        /// <summary>
        /// Call this from a dialogue event or choice to trigger the "Keep Device" ending.
        /// The Abstract dimension expands and consumes all others.
        /// </summary>
        [ContextMenu("Trigger Keep Device Ending")]
        public void TriggerKeepDeviceEnding()
        {
            StartCoroutine(KeepDeviceEndingSequence());
        }

        private IEnumerator DestroyDeviceEndingSequence()
        {
            _currentState = EndingState.EndingCutscene;
            OnDestroyDeviceChosen?.Invoke();

            // Lock player input
            SetPlayerControlsEnabled(false);

            // Switch cameras
            if (_playerCamera != null)
                _playerCamera.enabled = false;
            if (endingCamera != null)
                endingCamera.enabled = true;

            // Play collapse sound
            if (collapseSound != null)
                SFXPlayer.PlayAtPoint(collapseSound, transform.position, soundVolume);

            // Spawn collapse VFX
            if (collapseVFX != null)
            {
                var vfx = Instantiate(collapseVFX, transform.position, Quaternion.identity);
                Destroy(vfx, collapseDuration + 5f);
            }

            // Animate collapse - scale down and destroy targets
            float elapsed = 0f;
            Vector3[] originalScales = new Vector3[collapseTargets?.Length ?? 0];
            Vector3[] originalPositions = new Vector3[collapseTargets?.Length ?? 0];

            if (collapseTargets != null)
            {
                for (int i = 0; i < collapseTargets.Length; i++)
                {
                    if (collapseTargets[i] != null)
                    {
                        originalScales[i] = collapseTargets[i].transform.localScale;
                        originalPositions[i] = collapseTargets[i].transform.position;
                    }
                }
            }

            // Collapse animation
            while (elapsed < collapseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / collapseDuration;
                float collapseT = Mathf.Pow(t, 0.5f); // Ease in

                // Scale down all collapse targets toward center
                if (collapseTargets != null)
                {
                    for (int i = 0; i < collapseTargets.Length; i++)
                    {
                        if (collapseTargets[i] != null)
                        {
                            collapseTargets[i].transform.localScale = Vector3.Lerp(originalScales[i], Vector3.zero, collapseT);
                            collapseTargets[i].transform.position = Vector3.Lerp(originalPositions[i], transform.position, collapseT * 0.8f);
                        }
                    }
                }

                // Occasional flash
                if (UnityEngine.Random.value < 0.05f * t)
                {
                    yield return StartCoroutine(ScreenFlash(Color.white, 0.1f));
                }

                yield return null;
            }

            // Final flash
            yield return StartCoroutine(ScreenFlash(Color.white, 0.5f));

            // Destroy collapse targets
            if (collapseTargets != null)
            {
                foreach (var target in collapseTargets)
                {
                    if (target != null)
                        Destroy(target);
                }
            }

            // Fade to black
            yield return StartCoroutine(FadeToBlack(fadeToBlackDuration));

            OnEndingComplete?.Invoke();
            Debug.Log("[WhiteHoleCore] Destroy Device ending complete. Player saved all dimensions.");
        }

        private IEnumerator KeepDeviceEndingSequence()
        {
            _currentState = EndingState.EndingCutscene;
            OnKeepDeviceChosen?.Invoke();

            // Play expansion sound
            if (expansionSound != null)
                SFXPlayer.PlayAtPoint(expansionSound, transform.position, soundVolume);

            // Spawn expansion VFX
            if (expansionVFX != null)
            {
                var vfx = Instantiate(expansionVFX, transform.position, Quaternion.identity);
                Destroy(vfx, expansionDuration + 5f);
            }

            // Animate expansion - screen pulses and color shifts
            float elapsed = 0f;
            Color abstractColor = new Color(0.3f, 0.1f, 0.5f, 0.6f); // Purple/abstract tint

            while (elapsed < expansionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / expansionDuration;

                // Pulsing abstract overlay
                float pulse = Mathf.Sin(t * Mathf.PI * 8f) * 0.5f + 0.5f;
                SetFadeAlpha(abstractColor.a * pulse * t);

                // Occasional dimension flicker effect
                if (UnityEngine.Random.value < 0.03f)
                {
                    yield return StartCoroutine(ScreenFlash(abstractColor, 0.15f));
                }

                yield return null;
            }

            // Final pulse
            yield return StartCoroutine(ScreenFlash(abstractColor, 1f));

            // Fade to abstract color
            yield return StartCoroutine(FadeToColor(abstractColor, 2f));

            OnEndingComplete?.Invoke();
            Debug.Log("[WhiteHoleCore] Keep Device ending complete. Abstract dimension has expanded.");
        }

        private IEnumerator PhasingSequence()
        {
            if (phasePoints == null || phasePoints.Length == 0)
                yield break;

            // Lock player controls during phasing
            SetPlayerControlsEnabled(false);

            // Disable specified objects during phasing
            if (disableDuringPhasing != null)
            {
                foreach (var obj in disableDuringPhasing)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }

            // Enable all sections so we can teleport anywhere
            if (enableAllSectionsDuringPhasing && SectionManager.Instance != null)
            {
                SectionManager.Instance.EnableAllSections();
            }

            Debug.Log($"[WhiteHoleCore] Starting phasing: {phasePoints.Length} points, {phaseCycles} cycles");

            for (int cycle = 0; cycle < phaseCycles; cycle++)
            {
                Debug.Log($"[WhiteHoleCore] Starting cycle {cycle + 1}/{phaseCycles}");
                
                for (int i = 0; i < phasePoints.Length; i++)
                {
                    var phasePoint = phasePoints[i];
                    if (phasePoint == null || phasePoint.position == null)
                    {
                        Debug.LogWarning($"[WhiteHoleCore] Phase point {i} is null, skipping");
                        continue;
                    }

                    var pos = phasePoint.position;
                    Debug.Log($"[WhiteHoleCore] Phase {i + 1}/{phasePoints.Length}: pos={pos.position}, dim={phasePoint.dimensionIndex}");

                    // Flash effect
                    yield return StartCoroutine(ScreenFlash(phasingFlashColor, flashDuration));

                    // Play phasing sound
                    if (phasingSound != null)
                        SFXPlayer.PlayAtPoint(phasingSound, pos.position, soundVolume * 0.6f);

                    // Spawn VFX
                    if (phasingVFX != null)
                    {
                        var vfx = Instantiate(phasingVFX, pos.position, Quaternion.identity);
                        Destroy(vfx, 3f);
                    }

                    // Teleport player
                    yield return StartCoroutine(TeleportPlayer(pos.position, pos.rotation));
                    Debug.Log($"[WhiteHoleCore] Teleported to {pos.position}");

                    // Switch to the dimension specified for this phase point
                    if (DimensionManager.Instance != null)
                    {
                        DimensionManager.Instance.AbsoluteForceSwitchToDimension(phasePoint.dimensionIndex);
                        Debug.Log($"[WhiteHoleCore] Switched to dimension {phasePoint.dimensionIndex}");
                    }

                    // Brief pause between phases - gets faster each cycle
                    float adjustedTime = timeBetweenPhases * (1f - (cycle * 0.2f));
                    adjustedTime = Mathf.Max(adjustedTime, 0.1f);
                    Debug.Log($"[WhiteHoleCore] Waiting {adjustedTime}s before next phase");
                    yield return new WaitForSeconds(adjustedTime);
                    Debug.Log($"[WhiteHoleCore] Wait complete, continuing to next phase");
                }
                Debug.Log($"[WhiteHoleCore] Completed cycle {cycle + 1}/{phaseCycles}");
            }

            Debug.Log("[WhiteHoleCore] Phasing sequence complete!");

            // Re-enable disabled objects
            if (disableDuringPhasing != null)
            {
                foreach (var obj in disableDuringPhasing)
                {
                    if (obj != null) obj.SetActive(true);
                }
            }

            // Re-enable controls (will be disabled again for ending if needed)
            SetPlayerControlsEnabled(true);
        }

        private IEnumerator TeleportPlayer(Vector3 position, Quaternion rotation)
        {
            if (_playerMovement == null)
                _playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (_playerMovement == null)
                yield break;

            var cc = _playerMovement.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            _playerMovement.transform.position = position;
            _playerMovement.transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);

            if (cc != null) cc.enabled = true;

            // Snap camera look direction
            if (_playerLook == null)
                _playerLook = FindFirstObjectByType<PlayerLook>();
            if (_playerLook != null)
                _playerLook.SnapToRotation(rotation.eulerAngles.y, rotation.eulerAngles.x);

            yield return null;
        }

        private void SetPlayerControlsEnabled(bool enabled)
        {
            if (_playerMovement == null)
                _playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (_playerLook == null)
                _playerLook = FindFirstObjectByType<PlayerLook>();

            if (_playerMovement != null)
                _playerMovement.enabled = enabled;
            if (_playerLook != null)
                _playerLook.enabled = enabled;

            // Cursor state
            if (!enabled)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        #region Screen Effects

        private void CreateScreenOverlays()
        {
            // Create canvas for screen effects
            var canvasObj = new GameObject("WhiteHoleCoreScreenEffects");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            // Flash overlay
            var flashObj = new GameObject("FlashOverlay");
            flashObj.transform.SetParent(canvasObj.transform);
            var flashImage = flashObj.AddComponent<UnityEngine.UI.Image>();
            flashImage.color = Color.white;
            flashImage.raycastTarget = false;
            var flashRect = flashObj.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            _flashOverlay = flashObj.AddComponent<CanvasGroup>();
            _flashOverlay.alpha = 0f;
            _flashOverlay.blocksRaycasts = false;
            _flashOverlay.interactable = false;

            // Fade overlay (for endings)
            var fadeObj = new GameObject("FadeOverlay");
            fadeObj.transform.SetParent(canvasObj.transform);
            var fadeImage = fadeObj.AddComponent<UnityEngine.UI.Image>();
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = false;
            var fadeRect = fadeObj.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
            _fadeOverlay = fadeObj.AddComponent<CanvasGroup>();
            _fadeOverlay.alpha = 0f;
            _fadeOverlay.blocksRaycasts = false;
            _fadeOverlay.interactable = false;
        }

        private IEnumerator ScreenFlash(Color color, float duration)
        {
            if (_flashOverlay == null) yield break;

            var image = _flashOverlay.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.color = color;

            // Flash in
            float halfDur = duration * 0.5f;
            float elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.deltaTime;
                _flashOverlay.alpha = Mathf.Lerp(0f, 1f, elapsed / halfDur);
                yield return null;
            }

            // Flash out
            elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.deltaTime;
                _flashOverlay.alpha = Mathf.Lerp(1f, 0f, elapsed / halfDur);
                yield return null;
            }

            _flashOverlay.alpha = 0f;
        }

        private IEnumerator FadeToBlack(float duration)
        {
            yield return StartCoroutine(FadeToColor(Color.black, duration));
        }

        private IEnumerator FadeToColor(Color color, float duration)
        {
            if (_fadeOverlay == null) yield break;

            var image = _fadeOverlay.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.color = color;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _fadeOverlay.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }

            _fadeOverlay.alpha = 1f;
        }

        private void SetFadeAlpha(float alpha)
        {
            if (_fadeOverlay != null)
                _fadeOverlay.alpha = alpha;
        }

        #endregion

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 1f);

            // Draw phasing positions
            if (phasePoints != null)
            {
                Gizmos.color = new Color(0.5f, 0f, 1f, 0.6f);
                for (int i = 0; i < phasePoints.Length; i++)
                {
                    if (phasePoints[i] != null && phasePoints[i].position != null)
                    {
                        Gizmos.DrawWireSphere(phasePoints[i].position.position, 0.5f);
                        if (i > 0 && phasePoints[i - 1] != null && phasePoints[i - 1].position != null)
                        {
                            Gizmos.DrawLine(phasePoints[i - 1].position.position, phasePoints[i].position.position);
                        }
                    }
                }
            }

            // Draw return position
            if (returnPosition != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(returnPosition.position, 0.5f);
                Gizmos.DrawLine(transform.position, returnPosition.position);
            }
        }
    }
}
