using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OutOfPhase.Quest
{
    /// <summary>
    /// Displays the active quest list on the middle-left of the screen.
    /// Auto-creates its own Canvas and UI elements.
    /// Completed quests show a strikethrough / checkmark briefly before removal.
    /// </summary>
    public class QuestUI : MonoBehaviour
    {
        [Header("Appearance")]
        [Tooltip("How long a completed quest stays visible with checkmark before fading.")]
        [SerializeField] private float completedDisplayTime = 1.5f;

        [Header("Colors")]
        [SerializeField] private Color activeTextColor = new Color(0.85f, 0.92f, 1f, 1f);
        [SerializeField] private Color completedTextColor = new Color(0.4f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color headerColor = new Color(0f, 0.85f, 1f, 1f);
        [SerializeField] private Color bgColor = new Color(0.02f, 0.02f, 0.06f, 0.55f);

        // UI references
        private Canvas _canvas;
        private RectTransform _listContainer;
        private Image _bgImage;
        private TextMeshProUGUI _headerText;
        private readonly List<QuestEntryUI> _entries = new List<QuestEntryUI>();

        // Fade-out tracking for completed quests
        private readonly List<CompletedEntry> _completedEntries = new List<CompletedEntry>();

        private bool _subscribed;

        private void Awake()
        {
            CreateUI();
        }

        private void OnEnable()
        {
            TrySubscribe();
            RebuildList();
        }

        private void OnDisable()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestListChanged -= RebuildList;
                QuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
            }
            _subscribed = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (QuestManager.Instance == null) return;

            QuestManager.Instance.OnQuestListChanged += RebuildList;
            QuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
            _subscribed = true;
        }

        private void Update()
        {
            // Late-subscribe if QuestManager wasn't ready at OnEnable
            if (!_subscribed)
            {
                TrySubscribe();
                if (_subscribed) RebuildList();
            }

            // Tick completed entries and fade them out
            for (int i = _completedEntries.Count - 1; i >= 0; i--)
            {
                var entry = _completedEntries[i];
                entry.timer -= Time.unscaledDeltaTime;

                if (entry.timer <= 0f)
                {
                    if (entry.textObj != null)
                        Destroy(entry.textObj);
                    _completedEntries.RemoveAt(i);
                    UpdateVisibility();
                }
                else if (entry.timer < 0.5f && entry.tmp != null)
                {
                    // Fade out in last 0.5s
                    Color c = entry.tmp.color;
                    c.a = entry.timer / 0.5f;
                    entry.tmp.color = c;
                }
            }
        }

        // ── List rebuild ───────────────────────────────────────

        private void RebuildList()
        {
            // Destroy old active entries
            foreach (var entry in _entries)
            {
                if (entry.textObj != null)
                    Destroy(entry.textObj);
            }
            _entries.Clear();

            if (QuestManager.Instance == null)
            {
                UpdateVisibility();
                return;
            }

            var active = QuestManager.Instance.ActiveQuests;
            foreach (var quest in active)
            {
                var entry = CreateEntry(quest);
                _entries.Add(entry);
            }

            UpdateVisibility();
        }

        private QuestEntryUI CreateEntry(QuestDefinition quest)
        {
            GameObject obj = new GameObject($"Quest_{quest.Id}");
            obj.transform.SetParent(_listContainer, false);

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"  \u25CB  {quest.title}"; // ○ bullet
            tmp.fontSize = 16;
            tmp.color = activeTextColor;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.margin = new Vector4(4, 0, 4, 0);

            // Layout element for proper sizing
            var layout = obj.AddComponent<LayoutElement>();
            layout.preferredHeight = 28;
            layout.flexibleWidth = 1;

            return new QuestEntryUI { questId = quest.Id, textObj = obj, tmp = tmp };
        }

        // ── Completion effect ──────────────────────────────────

        private void OnQuestCompleted(QuestDefinition quest)
        {
            // Find and restyle the entry instead of immediately removing it
            QuestEntryUI found = null;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].questId == quest.Id)
                {
                    found = _entries[i];
                    _entries.RemoveAt(i);
                    break;
                }
            }

            if (found != null && found.textObj != null)
            {
                // Restyle to completed look
                found.tmp.text = $"  \u2713  <s>{quest.title}</s>"; // ✓ checkmark + strikethrough
                found.tmp.color = completedTextColor;

                _completedEntries.Add(new CompletedEntry
                {
                    textObj = found.textObj,
                    tmp = found.tmp,
                    timer = completedDisplayTime
                });
            }
        }

        // ── Visibility ─────────────────────────────────────────

        private void UpdateVisibility()
        {
            bool hasContent = _entries.Count > 0 || _completedEntries.Count > 0;
            if (_canvas != null)
                _canvas.gameObject.SetActive(hasContent);
        }

        // ── UI Creation ────────────────────────────────────────

        private void CreateUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("QuestListCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40; // Below interaction prompt (50)

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Container panel — middle-left
            GameObject panelObj = new GameObject("QuestPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0.5f);
            panelRect.anchorMax = new Vector2(0f, 0.5f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.anchoredPosition = new Vector2(20f, 0f);
            panelRect.sizeDelta = new Vector2(300f, 400f);

            // Background
            _bgImage = panelObj.AddComponent<Image>();
            _bgImage.color = bgColor;

            // Content size fitter so panel shrinks to content
            var sizeFitter = panelObj.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Vertical layout
            var vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 2;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Header
            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(panelObj.transform, false);
            _headerText = headerObj.AddComponent<TextMeshProUGUI>();
            _headerText.text = "OBJECTIVES";
            _headerText.fontSize = 18;
            _headerText.fontStyle = FontStyles.Bold;
            _headerText.color = headerColor;
            _headerText.alignment = TextAlignmentOptions.Left;
            _headerText.margin = new Vector4(4, 0, 4, 4);

            var headerLayout = headerObj.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 28;

            // Divider line
            GameObject dividerObj = new GameObject("Divider");
            dividerObj.transform.SetParent(panelObj.transform, false);
            Image divider = dividerObj.AddComponent<Image>();
            divider.color = new Color(headerColor.r, headerColor.g, headerColor.b, 0.3f);
            var divLayout = dividerObj.AddComponent<LayoutElement>();
            divLayout.preferredHeight = 1;
            divLayout.flexibleWidth = 1;

            // List container (quest entries go here)
            GameObject listObj = new GameObject("QuestEntries");
            listObj.transform.SetParent(panelObj.transform, false);
            _listContainer = listObj.AddComponent<RectTransform>();

            var listVlg = listObj.AddComponent<VerticalLayoutGroup>();
            listVlg.spacing = 2;
            listVlg.childAlignment = TextAnchor.UpperLeft;
            listVlg.childControlWidth = true;
            listVlg.childControlHeight = true;
            listVlg.childForceExpandWidth = true;
            listVlg.childForceExpandHeight = false;

            var listFitter = listObj.AddComponent<ContentSizeFitter>();
            listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var listLayout = listObj.AddComponent<LayoutElement>();
            listLayout.flexibleWidth = 1;

            // Start hidden
            canvasObj.SetActive(false);
        }

        // ── Helper types ───────────────────────────────────────

        private class QuestEntryUI
        {
            public string questId;
            public GameObject textObj;
            public TextMeshProUGUI tmp;
        }

        private class CompletedEntry
        {
            public GameObject textObj;
            public TextMeshProUGUI tmp;
            public float timer;
        }
    }
}
