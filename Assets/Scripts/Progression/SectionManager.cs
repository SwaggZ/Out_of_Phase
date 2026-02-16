using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfPhase.Progression
{
    /// <summary>
    /// Manages linear game sections. Each section is a root GameObject
    /// containing all content for that area.  Only the active section
    /// (and optionally the adjacent one) are loaded; everything else
    /// is disabled to save memory and GPU time.
    /// When the player crosses a SectionGate, the old section is
    /// locked off and can be unloaded.
    /// </summary>
    public class SectionManager : MonoBehaviour
    {
        public static SectionManager Instance { get; private set; }

        [Header("Sections (in order)")]
        [Tooltip("Drag the root GameObject for each section here, in play-order.")]
        [SerializeField] private SectionDefinition[] sections;

        [Header("Settings")]
        [Tooltip("Keep the next section loaded ahead of time.")]
        [SerializeField] private bool preloadNextSection = true;

        [Tooltip("How many previous sections to keep loaded (0 = unload immediately).")]
        [SerializeField] private int keepPreviousSections = 2;

        // ── State ──────────────────────────────────────────────
        private int _currentSectionIndex;
        private HashSet<int> _completedSections = new HashSet<int>();

        // ── Events ─────────────────────────────────────────────
        /// <summary>(oldIndex, newIndex)</summary>
        public event Action<int, int> OnSectionChanged;

        /// <summary>Fired when a section is completed / locked.</summary>
        public event Action<int> OnSectionCompleted;

        // ── Properties ─────────────────────────────────────────
        public int CurrentSectionIndex => _currentSectionIndex;
        public int SectionCount => sections != null ? sections.Length : 0;
        public SectionDefinition CurrentSection =>
            sections != null && _currentSectionIndex < sections.Length
                ? sections[_currentSectionIndex] : null;

        // ── Lifecycle ──────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Activate only the starting section (no teleport — player walks in naturally)
            ActivateSection(_currentSectionIndex);
        }

        // ── Public API ─────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="SectionGate"/> when the player crosses into
        /// the next section.
        /// </summary>
        public void AdvanceToSection(int newIndex)
        {
            if (newIndex < 0 || newIndex >= sections.Length) return;
            if (newIndex <= _currentSectionIndex) return; // can't go back

            int oldIndex = _currentSectionIndex;

            // Mark old section as completed
            _completedSections.Add(oldIndex);
            OnSectionCompleted?.Invoke(oldIndex);

            _currentSectionIndex = newIndex;
            ActivateSection(newIndex);

            // Auto-save checkpoint for the new section (no teleport — player walked in)
            var sec = sections[newIndex];
            if (sec.autoSaveOnEnter)
            {
                if (CheckpointManager.Instance != null)
                    CheckpointManager.Instance.SaveCheckpoint(sec.SectionName);
            }

            OnSectionChanged?.Invoke(oldIndex, newIndex);
        }

        /// <summary>
        /// Reset the current section (called on checkpoint reset).
        /// Re-enables the section root so all default states are restored
        /// (provided objects use OnEnable for initialization).
        /// </summary>
        public void ResetCurrentSection()
        {
            if (sections == null || _currentSectionIndex >= sections.Length) return;

            var sec = sections[_currentSectionIndex];
            if (sec.SectionRoot != null)
            {
                sec.SectionRoot.SetActive(false);
                sec.SectionRoot.SetActive(true);
            }
        }

        /// <summary>
        /// Returns the section definition at the given index.
        /// </summary>
        public SectionDefinition GetSection(int index)
        {
            if (sections == null || index < 0 || index >= sections.Length)
                return null;
            return sections[index];
        }

        /// <summary> Is this section completed (player crossed the gate)? </summary>
        public bool IsSectionCompleted(int index) => _completedSections.Contains(index);

        /// <summary>
        /// Force-set section (used when loading a save).
        /// Teleports the player to the checkpoint spawn point.
        /// </summary>
        public void SetSection(int index, HashSet<int> completed)
        {
            _currentSectionIndex = Mathf.Clamp(index, 0, sections.Length - 1);
            _completedSections = completed ?? new HashSet<int>();
            ActivateSection(_currentSectionIndex);

            // Teleport player to the checkpoint when loading a save
            var sec = sections[_currentSectionIndex];
            if (sec.CheckpointSpawnPoint != null)
                TeleportPlayerToCheckpoint(sec);
        }

        /// <summary>
        /// Get the checkpoint spawn position for a section.
        /// Falls back to the section root's position if no spawn point is set.
        /// </summary>
        public Vector3 GetCheckpointPosition(int index)
        {
            var sec = GetSection(index);
            if (sec == null) return Vector3.zero;
            if (sec.CheckpointSpawnPoint != null)
                return sec.CheckpointSpawnPoint.position;
            if (sec.SectionRoot != null)
                return sec.SectionRoot.transform.position;
            return Vector3.zero;
        }

        /// <summary>
        /// Get the checkpoint spawn rotation (Y-axis only) for a section.
        /// </summary>
        public Quaternion GetCheckpointRotation(int index)
        {
            var sec = GetSection(index);
            if (sec?.CheckpointSpawnPoint != null)
                return sec.CheckpointSpawnPoint.rotation;
            return Quaternion.identity;
        }

        // ── Internal ───────────────────────────────────────────

        private void TeleportPlayerToCheckpoint(SectionDefinition sec)
        {
            if (sec.CheckpointSpawnPoint == null) return;

            var player = FindFirstObjectByType<Player.PlayerMovement>();
            if (player == null) return;

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = sec.CheckpointSpawnPoint.position;
            player.transform.rotation = Quaternion.Euler(
                0f, sec.CheckpointSpawnPoint.eulerAngles.y, 0f);

            if (cc != null) cc.enabled = true;

            var look = FindFirstObjectByType<Player.PlayerLook>();
            if (look != null)
                look.SnapToRotation(sec.CheckpointSpawnPoint.eulerAngles.y,
                    sec.CheckpointSpawnPoint.eulerAngles.x);
        }

        private void ActivateSection(int index)
        {
            if (sections == null) return;

            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i].SectionRoot == null) continue;

                bool shouldBeActive = false;

                // Current section is always active
                if (i == index) shouldBeActive = true;

                // Optionally preload next
                if (preloadNextSection && i == index + 1) shouldBeActive = true;

                // Keep N previous sections loaded
                if (i < index && i >= index - keepPreviousSections) shouldBeActive = true;

                sections[i].SectionRoot.SetActive(shouldBeActive);
            }
        }
    }

    /// <summary>
    /// Defines one game section — a root GameObject plus metadata.
    /// </summary>
    [Serializable]
    public class SectionDefinition
    {
        [Tooltip("Root GameObject that holds all content for this section.")]
        public GameObject SectionRoot;

        [Tooltip("Display name (shown in UI / loading screen).")]
        public string SectionName = "Section";

        [Tooltip("Which dimension is active when entering this section for the first time. -1 = keep current.")]
        public int startingDimension = -1;

        [Header("Checkpoint")]
        [Tooltip("Spawn point for this section's checkpoint. Player respawns here on reset.")]
        public Transform CheckpointSpawnPoint;

        [Tooltip("Auto-save when the player enters this section.")]
        public bool autoSaveOnEnter = true;
    }
}
