using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OutOfPhase.Items;

namespace OutOfPhase.Quest
{
    /// <summary>
    /// Singleton that tracks active and completed quests.
    /// Listens to game events (dialogue, inventory) to auto-complete objectives.
    /// Place on a root GameObject in the scene.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        // ── Active & completed tracking ────────────────────────
        private readonly List<QuestDefinition> _activeQuests = new List<QuestDefinition>();
        private readonly HashSet<string> _completedQuestIds = new HashSet<string>();

        // ── Events ─────────────────────────────────────────────
        /// <summary>Fired when the active quest list changes (quest added or completed).</summary>
        public event Action OnQuestListChanged;

        /// <summary>Fired with the quest that was just completed.</summary>
        public event Action<QuestDefinition> OnQuestCompleted;

        /// <summary>Fired with the quest that was just activated.</summary>
        public event Action<QuestDefinition> OnQuestActivated;

        // ── Public accessors ───────────────────────────────────
        /// <summary>Read-only view of currently active quests.</summary>
        public IReadOnlyList<QuestDefinition> ActiveQuests => _activeQuests;

        /// <summary>Check if a quest has been completed.</summary>
        public bool IsQuestCompleted(string questId) => _completedQuestIds.Contains(questId);

        /// <summary>Check if a quest is currently active.</summary>
        public bool IsQuestActive(string questId) =>
            _activeQuests.Any(q => q.Id == questId);

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

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (Dialogue.DialogueManager.Instance != null)
                Dialogue.DialogueManager.Instance.OnDialogueEnded -= OnDialogueEnded;

            var inventory = FindFirstObjectByType<Inventory.Inventory>();
            if (inventory != null)
                inventory.OnItemAdded -= OnItemAdded;

            _subscribed = false;
        }

        private bool _subscribed;

        private void TrySubscribe()
        {
            if (_subscribed) return;

            bool ready = true;

            if (Dialogue.DialogueManager.Instance != null)
                Dialogue.DialogueManager.Instance.OnDialogueEnded += OnDialogueEnded;
            else
                ready = false;

            var inventory = FindFirstObjectByType<Inventory.Inventory>();
            if (inventory != null)
                inventory.OnItemAdded += OnItemAdded;
            else
                ready = false;

            _subscribed = ready;
        }

        private void Update()
        {
            if (!_subscribed)
                TrySubscribe();
        }

        // ── Public API ─────────────────────────────────────────

        /// <summary>
        /// Activate a set of quests (typically called by SectionQuests when a section loads).
        /// Already-completed quests are skipped.
        /// </summary>
        public void ActivateQuests(QuestDefinition[] quests)
        {
            if (quests == null) return;

            bool changed = false;
            foreach (var quest in quests)
            {
                if (quest == null) continue;
                if (_completedQuestIds.Contains(quest.Id)) continue;
                if (_activeQuests.Any(q => q.Id == quest.Id)) continue;

                _activeQuests.Add(quest);
                OnQuestActivated?.Invoke(quest);
                changed = true;

                // Check if already satisfied (e.g. FindItem when player already has the item)
                if (quest.autoComplete)
                    CheckAutoComplete(quest);
            }

            if (changed)
                OnQuestListChanged?.Invoke();
        }

        /// <summary>
        /// Remove quests from the active list without completing them
        /// (e.g. when a section is deactivated and quests should disappear).
        /// </summary>
        public void DeactivateQuests(QuestDefinition[] quests)
        {
            if (quests == null) return;

            bool changed = false;
            foreach (var quest in quests)
            {
                if (quest == null) continue;
                if (_activeQuests.RemoveAll(q => q.Id == quest.Id) > 0)
                    changed = true;
            }

            if (changed)
                OnQuestListChanged?.Invoke();
        }

        /// <summary>
        /// Mark a quest as completed by its ID.
        /// </summary>
        public void CompleteQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;
            if (_completedQuestIds.Contains(questId)) return;

            QuestDefinition quest = _activeQuests.FirstOrDefault(q => q.Id == questId);

            _completedQuestIds.Add(questId);
            _activeQuests.RemoveAll(q => q.Id == questId);

            Debug.Log($"[Quest] Completed: {questId}");

            if (quest != null)
                OnQuestCompleted?.Invoke(quest);

            OnQuestListChanged?.Invoke();
        }

        /// <summary>
        /// Mark a quest as completed by reference.
        /// </summary>
        public void CompleteQuest(QuestDefinition quest)
        {
            if (quest != null)
                CompleteQuest(quest.Id);
        }

        /// <summary>
        /// Clear all active quests and completion history (used on new game).
        /// </summary>
        public void ResetAll()
        {
            _activeQuests.Clear();
            _completedQuestIds.Clear();
            OnQuestListChanged?.Invoke();
        }

        // ── Save / Load helpers ────────────────────────────────

        /// <summary>Returns all completed quest IDs for serialization.</summary>
        public string[] GetCompletedQuestIds() => _completedQuestIds.ToArray();

        /// <summary>Returns all active quest IDs for serialization.</summary>
        public string[] GetActiveQuestIds() => _activeQuests.Select(q => q.Id).ToArray();

        /// <summary>Restores completed quest IDs from save data.</summary>
        public void RestoreCompletedQuests(string[] ids)
        {
            _completedQuestIds.Clear();
            if (ids != null)
            {
                foreach (var id in ids)
                    _completedQuestIds.Add(id);
            }
        }

        /// <summary>Restores active quests from save data (needs a lookup array).</summary>
        public void RestoreActiveQuests(string[] ids, QuestDefinition[] allQuests)
        {
            _activeQuests.Clear();
            if (ids == null || allQuests == null) return;

            foreach (var id in ids)
            {
                var quest = allQuests.FirstOrDefault(q => q != null && q.Id == id);
                if (quest != null && !_completedQuestIds.Contains(id))
                    _activeQuests.Add(quest);
            }

            OnQuestListChanged?.Invoke();
        }

        // ── Event handlers ─────────────────────────────────────

        private void OnDialogueEnded()
        {
            // Check all active TalkTo quests
            for (int i = _activeQuests.Count - 1; i >= 0; i--)
            {
                var quest = _activeQuests[i];
                if (quest.questType != QuestType.TalkTo) continue;
                if (!quest.autoComplete) continue;

                // If the quest specifies a target dialogue, we can't easily verify
                // which dialogue just ended from this event alone.
                // Instead we rely on NPCController calling NotifyTalkedTo().
                // If no specific NPC/dialogue is set, complete on any dialogue end.
                if (string.IsNullOrEmpty(quest.targetNPCName) && quest.targetDialogue == null)
                {
                    CompleteQuest(quest.Id);
                }
            }
        }

        /// <summary>
        /// Called by NPCController after dialogue with a specific NPC ends.
        /// Completes any matching TalkTo quest.
        /// </summary>
        public void NotifyTalkedTo(string npcName, Dialogue.DialogueData dialogue)
        {
            for (int i = _activeQuests.Count - 1; i >= 0; i--)
            {
                var quest = _activeQuests[i];
                if (quest.questType != QuestType.TalkTo) continue;
                if (!quest.autoComplete) continue;

                bool nameMatch = string.IsNullOrEmpty(quest.targetNPCName) ||
                                 quest.targetNPCName == npcName;
                bool dialogueMatch = quest.targetDialogue == null ||
                                     quest.targetDialogue == dialogue;

                if (nameMatch && dialogueMatch)
                {
                    CompleteQuest(quest.Id);
                }
            }
        }

        /// <summary>
        /// Called by QuestZone when the player enters a quest area.
        /// </summary>
        public void NotifyReachedArea(string questId)
        {
            if (!IsQuestActive(questId)) return;

            var quest = _activeQuests.FirstOrDefault(q => q.Id == questId);
            if (quest != null && quest.questType == QuestType.ReachArea && quest.autoComplete)
            {
                CompleteQuest(questId);
            }
        }

        private void OnItemAdded(int slotIndex, ItemDefinition item, int quantity)
        {
            // Check all active FindItem quests
            var inventory = FindFirstObjectByType<Inventory.Inventory>();
            if (inventory == null) return;

            for (int i = _activeQuests.Count - 1; i >= 0; i--)
            {
                var quest = _activeQuests[i];
                if (quest.questType != QuestType.FindItem) continue;
                if (!quest.autoComplete) continue;
                if (quest.targetItem == null) continue;

                int count = inventory.GetItemCount(quest.targetItem);
                if (count >= quest.targetItemCount)
                {
                    CompleteQuest(quest.Id);
                }
            }
        }

        /// <summary>
        /// Check if a quest's objective is already satisfied right now.
        /// </summary>
        private void CheckAutoComplete(QuestDefinition quest)
        {
            if (quest.questType == QuestType.FindItem && quest.targetItem != null)
            {
                var inventory = FindFirstObjectByType<Inventory.Inventory>();
                if (inventory != null)
                {
                    int count = inventory.GetItemCount(quest.targetItem);
                    if (count >= quest.targetItemCount)
                    {
                        CompleteQuest(quest.Id);
                    }
                }
            }
        }
    }
}
