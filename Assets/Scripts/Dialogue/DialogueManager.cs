using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using OutOfPhase.Items;
using OutOfPhase.Interaction;
using OutOfPhase.Player;
using OutOfPhase.Dimension;
using OutOfPhase.Inventory;

namespace OutOfPhase.Dialogue
{
    /// <summary>
    /// Singleton that drives dialogue playback and owns the dialogue UI.
    /// Call DialogueManager.Instance.StartDialogue() from any NPC.
    /// Handles typewriter text, choice buttons, item reward drops,
    /// and locks player input while dialogue is active.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("Typewriter")]
        [Tooltip("Characters revealed per second")]
        [SerializeField] private float charsPerSecond = 40f;

        [Header("Item Drop")]
        [Tooltip("How far in front of the NPC items are dropped")]
        [SerializeField] private float itemDropRadius = 1.5f;
        [Tooltip("Upward force on dropped reward items")]
        [SerializeField] private float itemDropUpForce = 2f;

        [Header("Audio")]
        [Tooltip("Sound played per character during typewriter (optional)")]
        [SerializeField] private AudioClip typeSoundClip;
        [Tooltip("Chars between type sounds")]
        [SerializeField] private int typeSoundInterval = 3;
        [SerializeField] private float typeSoundVolume = 0.15f;

        [Header("Colors")]
        [SerializeField] private Color bgColor = new Color(0.02f, 0.02f, 0.06f, 0.92f);
        [SerializeField] private Color textColor = new Color(0.9f, 0.95f, 1f, 1f);
        [SerializeField] private Color speakerColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color choiceBgColor = new Color(0.05f, 0.12f, 0.18f, 0.95f);
        [SerializeField] private Color choiceHoverColor = new Color(0.08f, 0.25f, 0.35f, 1f);
        [SerializeField] private Color choiceTextColor = new Color(0.8f, 0.95f, 1f, 1f);

        // UI elements (auto-created)
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private GameObject _dialoguePanel;
        private TextMeshProUGUI _speakerText;
        private TextMeshProUGUI _bodyText;
        private TextMeshProUGUI _continueHint;
        private GameObject _choicesContainer;
        private Button[] _choiceButtons;
        private TextMeshProUGUI[] _choiceLabels;
        private const int MaxChoices = 4;

        // State
        private DialogueData _currentDialogue;
        private int _currentNodeIndex;
        private bool _isTyping;
        private bool _skipRequested;
        private bool _waitingForInput;
        private bool _waitingForChoice;
        private Coroutine _typewriterCoroutine;
        private Transform _npcTransform; // for item drops
        private Action _onDialogueEnd;
        private DialogueChoice[] _currentChoices; // for item cost consumption

        // Input
        private PlayerInputActions _inputActions;

        /// <summary>True while any dialogue is playing.</summary>
        public bool IsDialogueActive { get; private set; }

        /// <summary>Fired when dialogue starts.</summary>
        public event Action OnDialogueStarted;

        /// <summary>Fired when dialogue ends.</summary>
        public event Action OnDialogueEnded;

        /// <summary>Fired when a dialogue node with a triggerID is reached. Args: triggerID string.</summary>
        public event Action<string> OnNodeTrigger;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _inputActions = new PlayerInputActions();
            CreateDialogueUI();
            HideUI();
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Player.Disable();
        }

        private void Update()
        {
            if (!IsDialogueActive) return;

            // E key, Space, Enter, or LMB to advance / skip typewriter
            bool advancePressed = Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || 
                                                                Keyboard.current.spaceKey.wasPressedThisFrame ||
                                                                Keyboard.current.enterKey.wasPressedThisFrame);
            bool clickPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            if (advancePressed || clickPressed)
            {
                if (_isTyping)
                {
                    _skipRequested = true;
                }
                else if (_waitingForInput && !_waitingForChoice)
                {
                    AdvanceDialogue();
                }
            }
        }

        /// <summary>
        /// Starts a dialogue conversation.
        /// </summary>
        /// <param name="dialogue">The dialogue data to play.</param>
        /// <param name="npcTransform">Transform of the NPC (used for item drop positioning).</param>
        /// <param name="onEnd">Optional callback when dialogue finishes.</param>
        public void StartDialogue(DialogueData dialogue, Transform npcTransform, Action onEnd = null)
        {
            if (dialogue == null || !dialogue.IsValid)
            {
                Debug.LogWarning("[DialogueManager] Attempted to start null/empty dialogue.");
                return;
            }

            if (IsDialogueActive)
            {
                Debug.LogWarning("[DialogueManager] Dialogue already active, ignoring.");
                return;
            }

            _currentDialogue = dialogue;
            _npcTransform = npcTransform;
            _onDialogueEnd = onEnd;
            _currentNodeIndex = 0;
            IsDialogueActive = true;

            // Lock player input
            LockPlayerInput(true);

            ShowUI();
            ShowNode(_currentNodeIndex);
            OnDialogueStarted?.Invoke();
        }

        /// <summary>
        /// Force-ends the current dialogue.
        /// </summary>
        public void EndDialogue()
        {
            if (!IsDialogueActive) return;

            if (_typewriterCoroutine != null)
                StopCoroutine(_typewriterCoroutine);

            IsDialogueActive = false;
            _isTyping = false;
            _waitingForInput = false;
            _waitingForChoice = false;

            HideUI();
            LockPlayerInput(false);

            _onDialogueEnd?.Invoke();
            _onDialogueEnd = null;
            OnDialogueEnded?.Invoke();
        }

        private void ShowNode(int index)
        {
            if (_currentDialogue == null || _currentDialogue.nodes == null
                || index < 0 || index >= _currentDialogue.nodes.Length)
            {
                EndDialogue();
                return;
            }

            var node = _currentDialogue.nodes[index];
            _currentNodeIndex = index;

            // Speaker name
            string speaker = string.IsNullOrEmpty(node.speakerNameOverride)
                ? _currentDialogue.defaultSpeakerName
                : node.speakerNameOverride;
            _speakerText.text = speaker;

            // Hide choices initially
            _choicesContainer.SetActive(false);
            _continueHint.gameObject.SetActive(false);
            _waitingForInput = false;
            _waitingForChoice = false;

            // Spawn item rewards
            if (node.itemRewards != null)
            {
                foreach (var reward in node.itemRewards)
                {
                    if (reward.item != null && reward.quantity > 0)
                    {
                        SpawnItemReward(reward);
                    }
                }
            }

            // Fire node trigger event if triggerID is set
            if (!string.IsNullOrEmpty(node.triggerID))
            {
                OnNodeTrigger?.Invoke(node.triggerID);
            }

            // Start typewriter
            if (_typewriterCoroutine != null)
                StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(TypewriterEffect(node));
        }

        private IEnumerator TypewriterEffect(DialogueNode node)
        {
            _isTyping = true;
            _skipRequested = false;
            _bodyText.text = "";
            _bodyText.maxVisibleCharacters = 0;

            // Set full text so TMP can calculate layout, then reveal char by char
            _bodyText.text = node.text;
            _bodyText.ForceMeshUpdate();
            int totalChars = _bodyText.textInfo.characterCount;
            _bodyText.maxVisibleCharacters = 0;

            float interval = 1f / Mathf.Max(1f, charsPerSecond);
            int charsSinceSound = 0;

            for (int i = 0; i <= totalChars; i++)
            {
                if (_skipRequested)
                {
                    _bodyText.maxVisibleCharacters = totalChars;
                    break;
                }

                _bodyText.maxVisibleCharacters = i;

                // Type sound
                charsSinceSound++;
                if (typeSoundClip != null && charsSinceSound >= typeSoundInterval)
                {
                    SFXPlayer.PlayAtPoint(typeSoundClip, Camera.main != null
                        ? Camera.main.transform.position : Vector3.zero, typeSoundVolume);
                    charsSinceSound = 0;
                }

                yield return new WaitForSecondsRealtime(interval);
            }

            _isTyping = false;
            _skipRequested = false;

            // Show choices or continue hint
            if (node.HasChoices)
            {
                ShowChoices(node);
            }
            else
            {
                _continueHint.gameObject.SetActive(true);
                _continueHint.text = node.nextNodeIndex < 0 ? "[E] End" : "[E] Continue";
                _waitingForInput = true;
            }
        }

        private void ShowChoices(DialogueNode node)
        {
            _waitingForChoice = true;
            _choicesContainer.SetActive(true);
            _currentChoices = node.choices;

            var inventory = FindFirstObjectByType<Inventory.Inventory>();

            int count = Mathf.Min(node.choices.Length, MaxChoices);
            for (int i = 0; i < MaxChoices; i++)
            {
                if (i < count)
                {
                    var choice = node.choices[i];
                    _choiceButtons[i].gameObject.SetActive(true);

                    // Check if player can afford this choice
                    bool canAfford = CanAffordChoice(choice, inventory);

                    // Build display text with cost info
                    string displayText = choice.choiceText;
                    if (choice.itemCosts != null && choice.itemCosts.Length > 0)
                    {
                        string costText = GetCostDisplayText(choice.itemCosts, inventory);
                        displayText += $" <size=80%><color={(canAfford ? "#90EE90" : "#FF6B6B")}>{costText}</color></size>";
                    }
                    _choiceLabels[i].text = displayText;

                    // Enable/disable button based on affordability
                    _choiceButtons[i].interactable = canAfford;

                    int capturedIndex = i; // closure capture for choice index
                    _choiceButtons[i].onClick.RemoveAllListeners();
                    _choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(capturedIndex));
                }
                else
                {
                    _choiceButtons[i].gameObject.SetActive(false);
                }
            }

            // Unlock cursor for choice buttons
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Checks if the player has all required items for a dialogue choice.
        /// </summary>
        private bool CanAffordChoice(DialogueChoice choice, Inventory.Inventory inventory)
        {
            if (inventory == null) return true;

            // Check required items (must have but not consumed)
            if (choice.requiredItems != null)
            {
                foreach (var req in choice.requiredItems)
                {
                    if (req.item != null && !inventory.HasItem(req.item, req.quantity))
                        return false;
                }
            }

            // Check item costs (must have and will be consumed)
            if (choice.itemCosts != null)
            {
                foreach (var cost in choice.itemCosts)
                {
                    if (cost.item != null && !inventory.HasItem(cost.item, cost.quantity))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Builds a display string showing item costs.
        /// </summary>
        private string GetCostDisplayText(ItemCost[] costs, Inventory.Inventory inventory)
        {
            if (costs == null || costs.Length == 0) return "";

            var parts = new System.Collections.Generic.List<string>();
            foreach (var cost in costs)
            {
                if (cost.item == null) continue;
                int have = inventory != null ? inventory.GetItemCount(cost.item) : 0;
                parts.Add($"[{have}/{cost.quantity} {cost.item.ItemName}]");
            }

            return string.Join(" ", parts);
        }

        private void OnChoiceSelected(int choiceIndex)
        {
            _waitingForChoice = false;
            _choicesContainer.SetActive(false);

            if (_currentChoices == null || choiceIndex < 0 || choiceIndex >= _currentChoices.Length)
            {
                EndDialogue();
                return;
            }

            var choice = _currentChoices[choiceIndex];

            // Consume item costs
            if (choice.itemCosts != null && choice.itemCosts.Length > 0)
            {
                var inventory = FindFirstObjectByType<Inventory.Inventory>();
                if (inventory != null)
                {
                    foreach (var cost in choice.itemCosts)
                    {
                        if (cost.item != null && cost.quantity > 0)
                        {
                            inventory.RemoveItem(cost.item, cost.quantity);
                        }
                    }
                }
            }

            int targetNodeIndex = choice.targetNodeIndex;
            if (targetNodeIndex < 0)
            {
                EndDialogue();
            }
            else
            {
                ShowNode(targetNodeIndex);
            }
        }

        private void AdvanceDialogue()
        {
            _waitingForInput = false;
            var node = _currentDialogue.nodes[_currentNodeIndex];

            if (node.nextNodeIndex < 0)
            {
                EndDialogue();
            }
            else
            {
                ShowNode(node.nextNodeIndex);
            }
        }

        /// <summary>
        /// Spawns an item on the ground near the NPC as a pickup.
        /// </summary>
        private void SpawnItemReward(ItemReward reward)
        {
            if (reward.item.WorldPrefab == null)
            {
                Debug.LogWarning($"[DialogueManager] Cannot spawn reward '{reward.item.ItemName}' — no WorldPrefab.");
                return;
            }

            // Position near the NPC
            Vector3 npcPos = _npcTransform != null ? _npcTransform.position : Vector3.zero;
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * itemDropRadius;
            randomOffset.y = 0f;
            Vector3 spawnPos = npcPos + randomOffset + Vector3.up * 1f;

            for (int i = 0; i < reward.quantity; i++)
            {
                GameObject dropped = Instantiate(reward.item.WorldPrefab, spawnPos, Quaternion.identity);

                // Add pickup component
                var pickup = dropped.GetComponent<ItemPickup>();
                if (pickup == null)
                    pickup = dropped.AddComponent<ItemPickup>();
                pickup.SetItem(reward.item, 1);

                // Ensure collider
                if (dropped.GetComponent<Collider>() == null)
                {
                    var box = dropped.AddComponent<BoxCollider>();
                    var rend = dropped.GetComponentInChildren<Renderer>();
                    if (rend != null)
                    {
                        box.center = dropped.transform.InverseTransformPoint(rend.bounds.center);
                        box.size = dropped.transform.InverseTransformVector(rend.bounds.size);
                    }
                }

                // Rigidbody for physics pop
                var rb = dropped.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = dropped.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.mass = 0.5f;
                rb.linearDamping = 1f;
                rb.angularDamping = 2f;

                // Pop upward + random spread
                Vector3 force = Vector3.up * itemDropUpForce
                    + UnityEngine.Random.insideUnitSphere * 0.5f;
                rb.AddForce(force, ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 1.5f, ForceMode.Impulse);

                // Offset spawn slightly per item so they don't stack
                spawnPos += new Vector3(0.3f, 0f, 0.3f);
            }
        }

        #region Player Input Lock

        private void LockPlayerInput(bool locked)
        {
            // Disable player movement / look
            var movement = FindFirstObjectByType<Player.PlayerMovement>();
            if (movement != null) movement.enabled = !locked;

            var look = FindFirstObjectByType<Player.PlayerLook>();
            if (look != null) look.enabled = !locked;

            // Cursor state
            if (locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        #endregion

        #region UI Creation

        private void ShowUI()
        {
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
        }

        private void HideUI()
        {
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private void CreateDialogueUI()
        {
            // Canvas
            var canvasObj = new GameObject("DialogueCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            _canvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // Main panel — bottom of screen
            _dialoguePanel = CreatePanel(canvasObj.transform, "DialoguePanel",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 280f),
                bgColor);

            var panelRect = _dialoguePanel.GetComponent<RectTransform>();
            panelRect.anchoredPosition = new Vector2(0f, 20f);

            // Inner content with padding
            var content = CreatePanel(_dialoguePanel.transform, "Content",
                Vector2.zero, Vector2.one,
                new Vector2(40f, 15f), new Vector2(-40f, -15f),
                Color.clear);

            // Speaker name — top-left of content
            var speakerObj = new GameObject("SpeakerName");
            speakerObj.transform.SetParent(content.transform);
            var speakerRect = speakerObj.AddComponent<RectTransform>();
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0f, 1f);
            speakerRect.anchoredPosition = Vector2.zero;
            speakerRect.sizeDelta = new Vector2(0f, 35f);
            _speakerText = speakerObj.AddComponent<TextMeshProUGUI>();
            _speakerText.fontSize = 22;
            _speakerText.fontStyle = FontStyles.Bold;
            _speakerText.color = speakerColor;
            _speakerText.alignment = TextAlignmentOptions.TopLeft;
            _speakerText.textWrappingMode = TextWrappingModes.NoWrap;
            _speakerText.overflowMode = TextOverflowModes.Ellipsis;

            // Body text — below speaker
            var bodyObj = new GameObject("BodyText");
            bodyObj.transform.SetParent(content.transform);
            var bodyRect = bodyObj.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(0f, 40f);   // bottom padding for continue hint
            bodyRect.offsetMax = new Vector2(0f, -38f);   // below speaker
            _bodyText = bodyObj.AddComponent<TextMeshProUGUI>();
            _bodyText.fontSize = 20;
            _bodyText.color = textColor;
            _bodyText.alignment = TextAlignmentOptions.TopLeft;
            _bodyText.textWrappingMode = TextWrappingModes.Normal;
            _bodyText.overflowMode = TextOverflowModes.Overflow;

            // Continue hint — bottom-right
            var hintObj = new GameObject("ContinueHint");
            hintObj.transform.SetParent(content.transform);
            var hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.anchoredPosition = Vector2.zero;
            hintRect.sizeDelta = new Vector2(200f, 30f);
            _continueHint = hintObj.AddComponent<TextMeshProUGUI>();
            _continueHint.fontSize = 16;
            _continueHint.color = new Color(speakerColor.r, speakerColor.g, speakerColor.b, 0.6f);
            _continueHint.alignment = TextAlignmentOptions.BottomRight;
            _continueHint.fontStyle = FontStyles.Italic;
            _continueHint.text = "[E] Continue";

            // Choices container — above the dialogue panel
            _choicesContainer = new GameObject("ChoicesContainer");
            _choicesContainer.transform.SetParent(canvasObj.transform);
            var choicesRect = _choicesContainer.AddComponent<RectTransform>();
            choicesRect.anchorMin = new Vector2(0.5f, 0f);
            choicesRect.anchorMax = new Vector2(0.5f, 0f);
            choicesRect.pivot = new Vector2(0.5f, 0f);
            choicesRect.anchoredPosition = new Vector2(0f, 310f);
            choicesRect.sizeDelta = new Vector2(600f, 220f);

            var vlg = _choicesContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // Create choice buttons
            _choiceButtons = new Button[MaxChoices];
            _choiceLabels = new TextMeshProUGUI[MaxChoices];

            for (int i = 0; i < MaxChoices; i++)
            {
                var btnObj = new GameObject($"Choice_{i}");
                btnObj.transform.SetParent(_choicesContainer.transform);

                var btnImg = btnObj.AddComponent<Image>();
                btnImg.color = choiceBgColor;

                var btn = btnObj.AddComponent<Button>();
                var colors = btn.colors;
                colors.normalColor = choiceBgColor;
                colors.highlightedColor = choiceHoverColor;
                colors.pressedColor = new Color(choiceHoverColor.r, choiceHoverColor.g, choiceHoverColor.b, 1f);
                colors.selectedColor = choiceHoverColor;
                btn.colors = colors;

                var le = btnObj.AddComponent<LayoutElement>();
                le.preferredHeight = 45f;
                le.minHeight = 40f;

                // Label
                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(btnObj.transform);
                var labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(20f, 5f);
                labelRect.offsetMax = new Vector2(-20f, -5f);

                var labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
                labelTmp.fontSize = 18;
                labelTmp.color = choiceTextColor;
                labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
                labelTmp.textWrappingMode = TextWrappingModes.Normal;

                _choiceButtons[i] = btn;
                _choiceLabels[i] = labelTmp;
            }

            _choicesContainer.SetActive(false);
        }

        private GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax,
            Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            if (color.a > 0f)
            {
                var img = obj.AddComponent<Image>();
                img.color = color;
            }

            return obj;
        }

        #endregion
    }
}
