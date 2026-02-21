using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using OutOfPhase.Items;
using OutOfPhase.Quest;

namespace OutOfPhase.Progression
{
    /// <summary>
    /// Handles saving / loading game state via checkpoints.
    /// Only one save slot ("current checkpoint") — overwritten each time.
    /// Stores: player position/rotation, section, dimension, inventory, game flags.
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        private const string SAVE_KEY = "OutOfPhase_Checkpoint";

        [Header("Item Database")]
        [Tooltip("All ItemDefinitions in the game. Needed to reconstruct inventory on load.")]
        [SerializeField] private ItemDefinition[] allItems;

        [Header("Quest Database")]
        [Tooltip("All QuestDefinitions in the game. Needed to reconstruct active quests on load.")]
        [SerializeField] private QuestDefinition[] allQuests;

        // ── Game Flags (key-value store) ──
        private Dictionary<string, bool> _gameFlags = new Dictionary<string, bool>();

        // ── Pending Load ──
        private bool _pendingLoadOnSceneReady = false;
        
        /// <summary>True if checkpoint is currently being loaded. Zones should not force changes during this time.</summary>
        public static bool IsCheckpointLoading { get; private set; }

        // ── Events ──
        public event Action OnCheckpointSaved;
        public event Action OnCheckpointLoaded;
        public event Action OnCheckpointReset;

        // ── Properties ──
        /// <summary>True if a checkpoint exists (DISABLED - save system off, always false)</summary>
        public bool HasCheckpoint => false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Subscribe to scene loaded event
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[Checkpoint] OnSceneLoaded - scene: {scene.name}, pendingLoad: {_pendingLoadOnSceneReady}");
            
            // If we're flagged to load on scene ready, do it now
            if (_pendingLoadOnSceneReady)
            {
                _pendingLoadOnSceneReady = false;
                StartCoroutine(LoadCheckpointAfterFrame());
            }
        }

        private IEnumerator LoadCheckpointAfterFrame()
        {
            Debug.Log("[Checkpoint] LoadCheckpointAfterFrame - waiting one frame");
            // Wait a frame to ensure all managers are initialized
            yield return null;
            Debug.Log("[Checkpoint] LoadCheckpointAfterFrame - calling LoadCheckpoint now");
            LoadCheckpoint();
        }

        /// <summary>
        /// Call this before loading the game scene to load checkpoint after scene loads.
        /// </summary>
        public void PrepareLoadOnSceneReady()
        {
            _pendingLoadOnSceneReady = true;
        }

        // ══════════════════════════════════════════════════════
        //  SAVE
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Captures the full game state and writes it to PlayerPrefs.
        /// DISABLED: Save system is turned off - game runs fresh each time.
        /// </summary>
        public void SaveCheckpoint(string label = "Checkpoint")
        {
            // SAVE SYSTEM DISABLED - Game runs fresh each time
            Debug.Log($"[Checkpoint] Save disabled: {label}");
        }

        // ══════════════════════════════════════════════════════
        //  LOAD
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Restores full game state from the last saved checkpoint.
        /// DISABLED: Save system is turned off - game always starts fresh.
        /// </summary>
        public bool LoadCheckpoint()
        {
            // SAVE SYSTEM DISABLED - Always start fresh with no previous save
            Debug.Log("[Checkpoint] Load disabled - starting fresh");
            IsCheckpointLoading = false;
            return false;
        }

        // ══════════════════════════════════════════════════════
        //  RESET SECTION
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Resets the current section and teleports the player back
        /// to the checkpoint position. Keeps inventory as it was at save.
        /// </summary>
        public bool ResetToCheckpoint()
        {
            bool loaded = LoadCheckpoint();
            if (loaded)
            {
                // Also tell the section manager to re-initialize the section
                SectionManager.Instance?.ResetCurrentSection();
                OnCheckpointReset?.Invoke();
            }
            return loaded;
        }

        /// <summary> Deletes the checkpoint. (DISABLED - save system turned off) </summary>
        public void ClearCheckpoint()
        {
            // SAVE SYSTEM DISABLED - No checkpoints to clear
            Debug.Log("[Checkpoint] Clear disabled");
        }

        // ══════════════════════════════════════════════════════
        //  GAME FLAGS
        // ══════════════════════════════════════════════════════

        /// <summary> Set a named flag (e.g. "npc_spoke_to_witness"). </summary>
        public void SetFlag(string key, bool value = true)
        {
            _gameFlags[key] = value;
        }

        /// <summary> Read a named flag. Returns false if not set. </summary>
        public bool GetFlag(string key)
        {
            return _gameFlags.TryGetValue(key, out bool val) && val;
        }

        /// <summary> Check if a flag exists. </summary>
        public bool HasFlag(string key) => _gameFlags.ContainsKey(key);

        /// <summary> Clear all flags. </summary>
        public void ClearFlags() => _gameFlags.Clear();

        // ══════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════

        private ItemDefinition FindItemByName(string assetName)
        {
            if (allItems == null) return null;
            foreach (var item in allItems)
            {
                if (item != null && item.name == assetName)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Re-applies dimension locks and hides that the player is inside.
        /// Called after checkpoint load since OnTriggerEnter won't fire if player already exists in the collider.
        /// </summary>
        private void ReapplyDimensionZones()
        {
            var player = FindFirstObjectByType<Player.PlayerMovement>();
            if (player == null) return;

            // Use a small sphere overlap to find all colliders near the player
            var colliders = Physics.OverlapSphere(player.transform.position, 1f);
            
            foreach (var col in colliders)
            {
                if (col == null) continue;

                // Check for DimensionLockVolume
                var lockVolume = col.GetComponent<Dimension.DimensionLockVolume>();
                if (lockVolume != null)
                {
                    Debug.Log($"[Checkpoint] Reapplying DimensionLockVolume: {lockVolume.gameObject.name}");
                    lockVolume.ReapplyLocksIfPlayerInside();
                }

                // Check for DimensionHideVolume
                var hideVolume = col.GetComponent<Dimension.DimensionHideVolume>();
                if (hideVolume != null)
                {
                    Debug.Log($"[Checkpoint] Reapplying DimensionHideVolume: {hideVolume.gameObject.name}");
                    hideVolume.ReapplyHidesIfPlayerInside();
                }
            }
        }
    }
}
