using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OutOfPhase.UI
{
    /// <summary>
    /// Auto-created controls/keybindings panel.
    /// Used by both MainMenuUI and PauseMenuUI.
    /// </summary>
    public class ControlsUI : MonoBehaviour
    {
        private Color _accentColor;
        private Color _buttonColor;
        private Color _buttonHoverColor;
        private Color _textColor;
        private Color _bgColor;
        private Action _onBack;

        private bool _initialized;

        public void Initialize(Color accent, Color button, Color buttonHover, Color text, Color bg, Action onBack)
        {
            _accentColor = accent;
            _buttonColor = button;
            _buttonHoverColor = buttonHover;
            _textColor = text;
            _bgColor = bg;
            _onBack = onBack;
            _initialized = true;

            CreateUI();
        }

        private void CreateUI()
        {
            // Background
            Image bg = gameObject.AddComponent<Image>();
            bg.color = new Color(_bgColor.r, _bgColor.g, _bgColor.b, 0.95f);

            // Scrollable content area
            GameObject scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(transform, false);
            RectTransform scrollRect = scrollArea.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.15f, 0.15f);
            scrollRect.anchorMax = new Vector2(0.85f, 0.85f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            Image scrollBg = scrollArea.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0.3f);

            ScrollRect scrollView = scrollArea.AddComponent<ScrollRect>();
            scrollView.horizontal = false;
            scrollView.vertical = true;
            scrollView.scrollSensitivity = 30f;

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollArea.transform, false);
            RectTransform vpRect = viewport.AddComponent<RectTransform>();
            SetFullScreen(vpRect);

            Image vpMask = viewport.AddComponent<Image>();
            vpMask.color = Color.white;
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            // Content container
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 20, 20);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollView.content = contentRect;
            scrollView.viewport = vpRect;

            // Title
            CreateSectionHeader(content.transform, "CONTROLS");

            // --- MOVEMENT ---
            CreateSectionHeader(content.transform, "MOVEMENT");
            CreateKeyRow(content.transform, "Move Forward", "W");
            CreateKeyRow(content.transform, "Move Backward", "S");
            CreateKeyRow(content.transform, "Move Left", "A");
            CreateKeyRow(content.transform, "Move Right", "D");
            CreateKeyRow(content.transform, "Jump", "Space");

            // --- ACTIONS ---
            CreateSectionHeader(content.transform, "ACTIONS");
            CreateKeyRow(content.transform, "Interact", "E");
            CreateKeyRow(content.transform, "Use Item", "LMB");
            CreateKeyRow(content.transform, "Drop Item", "Q");

            // --- DIMENSION ---
            CreateSectionHeader(content.transform, "DIMENSION");
            CreateKeyRow(content.transform, "Dimension Wheel", "Tab (Hold)");

            // --- DIALOGUE ---
            CreateSectionHeader(content.transform, "DIALOGUE");
            CreateKeyRow(content.transform, "Advance / Skip", "E / LMB / Space / Enter");

            // --- OTHER ---
            CreateSectionHeader(content.transform, "OTHER");
            CreateKeyRow(content.transform, "Return to Checkpoint", "R (Hold 2s)");
            CreateKeyRow(content.transform, "Pause Menu", "Escape");
            CreateKeyRow(content.transform, "Cycle Hotbar", "Scroll Wheel / 1-5");

            // Back button
            CreateBackButton();
        }

        private void CreateSectionHeader(Transform parent, string text)
        {
            GameObject obj = new GameObject(text + "_Header");
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 40);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 40;

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = _accentColor;
            tmp.alignment = TextAlignmentOptions.Left;
        }

        private void CreateKeyRow(Transform parent, string action, string key)
        {
            // Row container
            GameObject row = new GameObject(action + "_Row");
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 35);
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 35;

            // Action label (left)
            GameObject actionObj = new GameObject("Action");
            actionObj.transform.SetParent(row.transform, false);
            RectTransform actionRect = actionObj.AddComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(0, 0);
            actionRect.anchorMax = new Vector2(0.6f, 1);
            actionRect.offsetMin = Vector2.zero;
            actionRect.offsetMax = Vector2.zero;

            TextMeshProUGUI actionTmp = actionObj.AddComponent<TextMeshProUGUI>();
            actionTmp.text = action;
            actionTmp.fontSize = 20;
            actionTmp.color = _textColor;
            actionTmp.alignment = TextAlignmentOptions.Left;

            // Key label (right)
            GameObject keyObj = new GameObject("Key");
            keyObj.transform.SetParent(row.transform, false);
            RectTransform keyRect = keyObj.AddComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0.6f, 0);
            keyRect.anchorMax = new Vector2(1, 1);
            keyRect.offsetMin = Vector2.zero;
            keyRect.offsetMax = Vector2.zero;

            TextMeshProUGUI keyTmp = keyObj.AddComponent<TextMeshProUGUI>();
            keyTmp.text = key;
            keyTmp.fontSize = 20;
            keyTmp.fontStyle = FontStyles.Bold;
            keyTmp.color = _accentColor;
            keyTmp.alignment = TextAlignmentOptions.Right;
        }

        private void CreateBackButton()
        {
            GameObject btnObj = new GameObject("BackButton");
            btnObj.transform.SetParent(transform, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.05f);
            rect.anchorMax = new Vector2(0.5f, 0.05f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(200, 45);
            rect.anchoredPosition = Vector2.zero;

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = _buttonColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.normalColor = _buttonColor;
            colors.highlightedColor = _buttonHoverColor;
            colors.pressedColor = _accentColor;
            colors.selectedColor = _buttonHoverColor;
            btn.colors = colors;
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(() => _onBack?.Invoke());

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            SetFullScreen(labelRect);

            TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "BACK";
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = _textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        private void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
