using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

        // ── Events ──
        public event Action OnCheckpointSaved;
        public event Action OnCheckpointLoaded;
        public event Action OnCheckpointReset;

        // ── Properties ──
        public bool HasCheckpoint => PlayerPrefs.HasKey(SAVE_KEY);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ══════════════════════════════════════════════════════
        //  SAVE
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Captures the full game state and writes it to PlayerPrefs.
        /// </summary>
        public void SaveCheckpoint(string label = "Checkpoint")
        {
            SaveData data = new SaveData
            {
                saveName = label,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Player transform
            var player = FindFirstObjectByType<Player.PlayerMovement>();
            if (player != null)
            {
                data.SetPlayerPosition(player.transform.position);

                var look = player.GetComponentInChildren<Player.PlayerLook>();
                if (look == null)
                    look = FindFirstObjectByType<Player.PlayerLook>();
                if (look != null)
                {
                    data.SetPlayerRotation(
                        player.transform.eulerAngles.y,
                        look.transform.localEulerAngles.x > 180f
                            ? look.transform.localEulerAngles.x - 360f
                            : look.transform.localEulerAngles.x
                    );
                }
            }

            // Section
            if (SectionManager.Instance != null)
            {
                data.currentSectionIndex = SectionManager.Instance.CurrentSectionIndex;
                var completed = new List<int>();
                for (int i = 0; i < SectionManager.Instance.SectionCount; i++)
                {
                    if (SectionManager.Instance.IsSectionCompleted(i))
                        completed.Add(i);
                }
                data.completedSections = completed.ToArray();
            }

            // Dimension
            if (Dimension.DimensionManager.Instance != null)
                data.currentDimension = Dimension.DimensionManager.Instance.CurrentDimension;

            // Inventory
            var inventory = FindFirstObjectByType<Inventory.Inventory>();
            if (inventory != null)
            {
                var slots = new List<InventorySlotData>();
                for (int i = 0; i < inventory.SlotCount; i++)
                {
                    var slot = inventory.GetSlot(i);
                    if (slot != null && slot.Item != null)
                    {
                        slots.Add(new InventorySlotData
                        {
                            itemId = slot.Item.name, // ScriptableObject asset name
                            quantity = slot.Quantity,
                            durability = slot.HasDurability ? slot.Durability : -1f
                        });
                    }
                    else
                    {
                        slots.Add(new InventorySlotData { itemId = "", quantity = 0, durability = -1f });
                    }
                }
                data.inventorySlots = slots.ToArray();
            }

            // Game flags
            var flagList = new List<StringBoolPair>();
            foreach (var kvp in _gameFlags)
                flagList.Add(new StringBoolPair { key = kvp.Key, value = kvp.Value });
            data.flags = flagList.ToArray();

            // Quests
            if (QuestManager.Instance != null)
            {
                data.completedQuestIds = QuestManager.Instance.GetCompletedQuestIds();
                data.activeQuestIds = QuestManager.Instance.GetActiveQuestIds();
            }

            // Write
            string json = JsonUtility.ToJson(data, false);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();

            Debug.Log($"[Checkpoint] Saved: {label} at section {data.currentSectionIndex}");
            OnCheckpointSaved?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        //  LOAD
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Restores full game state from the last saved checkpoint.
        /// </summary>
        public bool LoadCheckpoint()
        {
            if (!HasCheckpoint)
            {
                Debug.LogWarning("[Checkpoint] No checkpoint found.");
                return false;
            }

            string json = PlayerPrefs.GetString(SAVE_KEY, "");
            if (string.IsNullOrEmpty(json)) return false;

            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return false;

            // Player position
            var player = FindFirstObjectByType<Player.PlayerMovement>();
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                player.transform.position = data.GetPlayerPosition();
                player.transform.rotation = Quaternion.Euler(0f, data.playerRotation[0], 0f);

                if (cc != null) cc.enabled = true;

                var look = player.GetComponentInChildren<Player.PlayerLook>();
                if (look == null) look = FindFirstObjectByType<Player.PlayerLook>();
                if (look != null)
                    look.SnapToRotation(data.playerRotation[0], data.playerRotation[1]);
            }

            // Section
            if (SectionManager.Instance != null)
            {
                var completed = new HashSet<int>(data.completedSections ?? Array.Empty<int>());
                SectionManager.Instance.SetSection(data.currentSectionIndex, completed);
            }

            // Dimension
            if (Dimension.DimensionManager.Instance != null)
                Dimension.DimensionManager.Instance.ForceSwitchToDimension(data.currentDimension);

            // Inventory
            var inventory = FindFirstObjectByType<Inventory.Inventory>();
            if (inventory != null && data.inventorySlots != null)
            {
                inventory.ClearAll();
                for (int i = 0; i < data.inventorySlots.Length && i < inventory.SlotCount; i++)
                {
                    var slotData = data.inventorySlots[i];
                    if (string.IsNullOrEmpty(slotData.itemId)) continue;

                    var item = FindItemByName(slotData.itemId);
                    if (item != null)
                    {
                        inventory.TryAddItemToSlot(i, item, slotData.quantity, slotData.durability);
                    }
                }
            }

            // Game flags
            _gameFlags.Clear();
            if (data.flags != null)
            {
                foreach (var pair in data.flags)
                    _gameFlags[pair.key] = pair.value;
            }

            // Quests
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.RestoreCompletedQuests(data.completedQuestIds);
                QuestManager.Instance.RestoreActiveQuests(data.activeQuestIds, allQuests);
            }

            Debug.Log($"[Checkpoint] Loaded: {data.saveName} (section {data.currentSectionIndex})");
            OnCheckpointLoaded?.Invoke();
            return true;
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

        /// <summary> Deletes the checkpoint. </summary>
        public void ClearCheckpoint()
        {
            PlayerPrefs.DeleteKey(SAVE_KEY);
            PlayerPrefs.Save();
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
    }
}
