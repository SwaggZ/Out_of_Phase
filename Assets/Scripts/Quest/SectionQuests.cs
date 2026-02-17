using UnityEngine;
using OutOfPhase.Progression;

namespace OutOfPhase.Quest
{
    /// <summary>
    /// Attach to a section root GameObject (alongside DimensionContainer).
    /// When the player enters this section, its quests are activated.
    /// When the player leaves (advances to another section), quests are removed.
    /// </summary>
    public class SectionQuests : MonoBehaviour
    {
        [Tooltip("Quests for this section. Leave empty for sections with no quests.")]
        [SerializeField] private QuestDefinition[] quests;

        private bool _activated;
        private bool _subscribedToSection;
        private int _mySectionIndex = -1;

        private void OnEnable()
        {
            TrySubscribe();
            TryActivate();
        }

        private void Update()
        {
            if (!_subscribedToSection)
                TrySubscribe();

            if (!_activated)
                TryActivate();
        }

        private void TrySubscribe()
        {
            if (_subscribedToSection) return;
            if (SectionManager.Instance == null) return;

            // Figure out which section index we belong to
            _mySectionIndex = -1;
            for (int i = 0; i < SectionManager.Instance.SectionCount; i++)
            {
                var sec = SectionManager.Instance.GetSection(i);
                if (sec != null && sec.SectionRoot == gameObject)
                {
                    _mySectionIndex = i;
                    break;
                }
            }

            SectionManager.Instance.OnSectionChanged += OnSectionChanged;
            _subscribedToSection = true;
        }

        private void TryActivate()
        {
            if (_activated) return;
            if (quests == null || quests.Length == 0) return;
            if (QuestManager.Instance == null) return;
            if (SectionManager.Instance == null) return;

            // Only activate if this is the current section
            if (_mySectionIndex >= 0 && SectionManager.Instance.CurrentSectionIndex != _mySectionIndex)
                return;

            QuestManager.Instance.ActivateQuests(quests);
            _activated = true;
        }

        private void OnSectionChanged(int oldIndex, int newIndex)
        {
            // Player left our section — remove quests
            if (_activated && _mySectionIndex >= 0 && newIndex != _mySectionIndex)
            {
                QuestManager.Instance?.DeactivateQuests(quests);
                _activated = false;
            }

            // Player entered our section — add quests
            if (!_activated && _mySectionIndex >= 0 && newIndex == _mySectionIndex)
            {
                TryActivate();
            }
        }

        private void OnDisable()
        {
            if (_activated && QuestManager.Instance != null && quests != null)
            {
                QuestManager.Instance.DeactivateQuests(quests);
                _activated = false;
            }

            if (_subscribedToSection && SectionManager.Instance != null)
            {
                SectionManager.Instance.OnSectionChanged -= OnSectionChanged;
                _subscribedToSection = false;
            }
        }
    }
}
