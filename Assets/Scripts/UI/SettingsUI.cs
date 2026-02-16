using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OutOfPhase.UI
{
    /// <summary>
    /// Auto-created settings panel with sliders and toggles.
    /// Used by both MainMenuUI and PauseMenuUI.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        private Color _accentColor;
        private Color _buttonColor;
        private Color _buttonHoverColor;
        private Color _textColor;
        private Color _bgColor;
        private Action _onBack;

        // Slider refs for live preview
        private Slider _brightnessSlider;
        private Slider _fovSlider;
        private Slider _sensitivitySlider;
        private Slider _masterVolumeSlider;
        private Slider _musicVolumeSlider;
        private Slider _ambienceVolumeSlider;
        private Slider _sfxVolumeSlider;
        private Toggle _epilepsyToggle;

        // Value labels
        private TextMeshProUGUI _brightnessValue;
        private TextMeshProUGUI _fovValue;
        private TextMeshProUGUI _sensitivityValue;
        private TextMeshProUGUI _masterValue;
        private TextMeshProUGUI _musicValue;
        private TextMeshProUGUI _ambienceValue;
        private TextMeshProUGUI _sfxValue;

        private SettingsData _workingCopy;
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

        private void OnEnable()
        {
            if (!_initialized) return;

            // Load current settings into working copy
            if (SettingsManager.Instance != null)
            {
                _workingCopy = SettingsManager.Instance.Current.Clone();
            }
            else
            {
                _workingCopy = new SettingsData();
            }

            RefreshUI();
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
            scrollRect.anchorMin = new Vector2(0.2f, 0.12f);
            scrollRect.anchorMax = new Vector2(0.8f, 0.88f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var scrollView = scrollArea.AddComponent<ScrollRect>();
            scrollView.vertical = true;
            scrollView.horizontal = false;

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollArea.transform, false);
            RectTransform vpRect = viewport.AddComponent<RectTransform>();
            SetFullScreen(vpRect);
            viewport.AddComponent<RectMask2D>();

            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 700);

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollView.content = contentRect;
            scrollView.viewport = vpRect;

            // Title
            CreateSectionHeader(content.transform, "SETTINGS");

            // --- DISPLAY ---
            CreateSectionHeader(content.transform, "DISPLAY");

            (_brightnessSlider, _brightnessValue) = CreateSliderRow(content.transform, "Brightness", 0.5f, 2f, true,
                v => { _workingCopy.brightness = v; ApplyLive(); });

            (_fovSlider, _fovValue) = CreateSliderRow(content.transform, "Field of View", 50f, 120f, true,
                v => { _workingCopy.fov = v; ApplyLive(); });

            _epilepsyToggle = CreateToggleRow(content.transform, "Reduce Flashing (Epilepsy)",
                v => { _workingCopy.epilepsyMode = v; ApplyLive(); });

            // --- CONTROLS ---
            CreateSectionHeader(content.transform, "CONTROLS");

            (_sensitivitySlider, _sensitivityValue) = CreateSliderRow(content.transform, "Mouse Sensitivity", 0.1f, 10f, false,
                v => { _workingCopy.mouseSensitivity = v; ApplyLive(); });

            // --- AUDIO ---
            CreateSectionHeader(content.transform, "AUDIO");

            (_masterVolumeSlider, _masterValue) = CreateSliderRow(content.transform, "Master Volume", 0f, 1f, false,
                v => { _workingCopy.masterVolume = v; ApplyLive(); });

            (_musicVolumeSlider, _musicValue) = CreateSliderRow(content.transform, "Music Volume", 0f, 1f, false,
                v => { _workingCopy.musicVolume = v; ApplyLive(); });

            (_ambienceVolumeSlider, _ambienceValue) = CreateSliderRow(content.transform, "Ambience Volume", 0f, 1f, false,
                v => { _workingCopy.ambienceVolume = v; ApplyLive(); });

            (_sfxVolumeSlider, _sfxValue) = CreateSliderRow(content.transform, "SFX Volume", 0f, 1f, false,
                v => { _workingCopy.sfxVolume = v; ApplyLive(); });

            // ---  Bottom buttons ---
            GameObject btnRow = new GameObject("ButtonRow");
            btnRow.transform.SetParent(content.transform, false);
            RectTransform btnRowRect = btnRow.AddComponent<RectTransform>();
            btnRowRect.sizeDelta = new Vector2(0, 60);

            var btnLayout = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnLayout.spacing = 20;
            btnLayout.childAlignment = TextAnchor.MiddleCenter;
            btnLayout.childForceExpandWidth = false;
            btnLayout.childForceExpandHeight = false;
            btnLayout.padding = new RectOffset(0, 0, 10, 0);

            CreateButton(btnRow.transform, "RESET DEFAULTS", 200, () =>
            {
                _workingCopy = new SettingsData();
                ApplyLive();
                RefreshUI();
            });

            CreateButton(btnRow.transform, "BACK", 160, () =>
            {
                // Save on back
                if (SettingsManager.Instance != null)
                    SettingsManager.Instance.Apply(_workingCopy);
                _onBack?.Invoke();
            });
        }

        private void ApplyLive()
        {
            // Apply immediately for live preview
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.Apply(_workingCopy);
        }

        private void RefreshUI()
        {
            if (_brightnessSlider != null)
            {
                _brightnessSlider.SetValueWithoutNotify(_workingCopy.brightness);
                _brightnessValue.text = _workingCopy.brightness.ToString("F1");
            }
            if (_fovSlider != null)
            {
                _fovSlider.SetValueWithoutNotify(_workingCopy.fov);
                _fovValue.text = Mathf.RoundToInt(_workingCopy.fov).ToString();
            }
            if (_sensitivitySlider != null)
            {
                _sensitivitySlider.SetValueWithoutNotify(_workingCopy.mouseSensitivity);
                _sensitivityValue.text = _workingCopy.mouseSensitivity.ToString("F1");
            }
            if (_masterVolumeSlider != null)
            {
                _masterVolumeSlider.SetValueWithoutNotify(_workingCopy.masterVolume);
                _masterValue.text = Mathf.RoundToInt(_workingCopy.masterVolume * 100).ToString() + "%";
            }
            if (_musicVolumeSlider != null)
            {
                _musicVolumeSlider.SetValueWithoutNotify(_workingCopy.musicVolume);
                _musicValue.text = Mathf.RoundToInt(_workingCopy.musicVolume * 100).ToString() + "%";
            }
            if (_ambienceVolumeSlider != null)
            {
                _ambienceVolumeSlider.SetValueWithoutNotify(_workingCopy.ambienceVolume);
                _ambienceValue.text = Mathf.RoundToInt(_workingCopy.ambienceVolume * 100).ToString() + "%";
            }
            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.SetValueWithoutNotify(_workingCopy.sfxVolume);
                _sfxValue.text = Mathf.RoundToInt(_workingCopy.sfxVolume * 100).ToString() + "%";
            }
            if (_epilepsyToggle != null)
            {
                _epilepsyToggle.SetIsOnWithoutNotify(_workingCopy.epilepsyMode);
            }
        }

        #region UI Creation Helpers

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
            tmp.fontSize = text == "SETTINGS" ? 36 : 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = text == "SETTINGS" ? _accentColor : new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.7f);
            tmp.alignment = text == "SETTINGS" ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        }

        private (Slider slider, TextMeshProUGUI valueLabel) CreateSliderRow(
            Transform parent, string label, float min, float max, bool wholeNumbers,
            UnityEngine.Events.UnityAction<float> onChanged)
        {
            // Row container
            GameObject row = new GameObject(label + "_Row");
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 40);
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 40;

            // Label (left)
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0.35f, 1);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 20;
            labelTmp.color = _textColor;
            labelTmp.alignment = TextAlignmentOptions.Left;
            labelTmp.enableAutoSizing = false;
            labelTmp.overflowMode = TextOverflowModes.Ellipsis;

            // Slider (center)
            GameObject sliderObj = CreateSliderObject(row.transform);
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.37f, 0.2f);
            sliderRect.anchorMax = new Vector2(0.85f, 0.8f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;

            // Value label (right)
            GameObject valueObj = new GameObject("Value");
            valueObj.transform.SetParent(row.transform, false);
            RectTransform valueRect = valueObj.AddComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0.87f, 0);
            valueRect.anchorMax = new Vector2(1, 1);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            TextMeshProUGUI valueTmp = valueObj.AddComponent<TextMeshProUGUI>();
            valueTmp.fontSize = 18;
            valueTmp.color = _accentColor;
            valueTmp.alignment = TextAlignmentOptions.Center;

            // Wire up
            slider.onValueChanged.AddListener(v =>
            {
                onChanged(v);
                // Update value text
                if (wholeNumbers)
                    valueTmp.text = Mathf.RoundToInt(v).ToString();
                else if (max <= 1f)
                    valueTmp.text = Mathf.RoundToInt(v * 100).ToString() + "%";
                else
                    valueTmp.text = v.ToString("F1");
            });

            return (slider, valueTmp);
        }

        private GameObject CreateSliderObject(Transform parent)
        {
            // Slider root
            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.35f);
            bgRect.anchorMax = new Vector2(1, 0.65f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.35f);
            fillAreaRect.anchorMax = new Vector2(1, 0.65f);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = _accentColor;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Handle
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            SetFullScreen(handleAreaRect);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(16, 24);

            // Wire slider
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            var sliderColors = slider.colors;
            sliderColors.normalColor = Color.white;
            sliderColors.highlightedColor = _accentColor;
            sliderColors.pressedColor = _accentColor;
            slider.colors = sliderColors;

            return sliderObj;
        }

        private Toggle CreateToggleRow(Transform parent, string label, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            // Row
            GameObject row = new GameObject(label + "_Row");
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 40);
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 40;

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0.75f, 1);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 20;
            labelTmp.color = _textColor;
            labelTmp.alignment = TextAlignmentOptions.Left;

            // Toggle
            GameObject toggleObj = new GameObject("Toggle");
            toggleObj.transform.SetParent(row.transform, false);
            RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0.8f, 0.15f);
            toggleRect.anchorMax = new Vector2(0.9f, 0.85f);
            toggleRect.offsetMin = Vector2.zero;
            toggleRect.offsetMax = Vector2.zero;

            Toggle toggle = toggleObj.AddComponent<Toggle>();

            // Background box
            Image toggleBg = toggleObj.AddComponent<Image>();
            toggleBg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            // Checkmark
            GameObject checkObj = new GameObject("Checkmark");
            checkObj.transform.SetParent(toggleObj.transform, false);
            RectTransform checkRect = checkObj.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.15f, 0.15f);
            checkRect.anchorMax = new Vector2(0.85f, 0.85f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            Image checkImage = checkObj.AddComponent<Image>();
            checkImage.color = _accentColor;

            toggle.targetGraphic = toggleBg;
            toggle.graphic = checkImage;
            toggle.onValueChanged.AddListener(onChanged);

            return toggle;
        }

        private void CreateButton(Transform parent, string label, float width, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(label + "_Button");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 45);

            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 45;

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = _buttonColor;

            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = _buttonColor;
            colors.highlightedColor = _buttonHoverColor;
            colors.pressedColor = _accentColor;
            btn.colors = colors;
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            SetFullScreen(labelRect);

            TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
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

        #endregion
    }
}
