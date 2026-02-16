using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Red-tinted glitch effect matching the dimension transition style.
    /// Attach to the root canvas — animates overlays, text corruption, and displacement.
    /// Disabled entirely when epilepsy mode is active.
    /// </summary>
    public class DimensionSyncGlitchEffect : MonoBehaviour
    {
        private TextMeshProUGUI _tmp;
        private RawImage _overlay;
        private RawImage _glitchOverlay;
        private RawImage _scanlineOverlay;
        private CanvasGroup _group;
        private float _timer;
        private float _duration = 1.5f;
        private string _originalText;

        private static readonly char[] GlitchChars = "█▓▒░╔╗╚╝║═╬╣╠╩╦".ToCharArray();

        private void Awake()
        {
            _tmp = GetComponentInChildren<TextMeshProUGUI>();
            if (_tmp != null)
                _originalText = _tmp.text;

            var images = GetComponentsInChildren<RawImage>();
            foreach (var img in images)
            {
                if (img.gameObject.name == "RedOverlay") _overlay = img;
                else if (img.gameObject.name == "GlitchOverlay") _glitchOverlay = img;
                else if (img.gameObject.name == "ScanlineOverlay") _scanlineOverlay = img;
            }

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            float t = _timer / _duration;

            // Fade envelope: quick in, hold, quick out
            float alpha;
            if (t < 0.1f)
                alpha = t / 0.1f;
            else if (t > 0.85f)
                alpha = Mathf.InverseLerp(1f, 0.85f, t);
            else
                alpha = 1f;

            if (_group != null)
                _group.alpha = alpha;

            // Epilepsy-safe mode: show text only, no flashing/glitch
            if (UI.SettingsManager.EpilepsyMode)
            {
                // Hide overlays
                if (_overlay != null) _overlay.color = Color.clear;
                if (_glitchOverlay != null) _glitchOverlay.color = Color.clear;
                if (_scanlineOverlay != null) _scanlineOverlay.color = Color.clear;

                // Show static text with steady color
                if (_tmp != null && _originalText != null)
                {
                    _tmp.text = _originalText;
                    _tmp.rectTransform.anchoredPosition = Vector2.zero;
                    _tmp.color = Color.red;
                }
                return;
            }

            // Overlay pulse
            if (_overlay != null)
            {
                float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 15f) * 0.15f;
                _overlay.color = new Color(0.3f, 0f, 0f, pulse * alpha);
            }

            // Glitch bar displacement
            if (_glitchOverlay != null)
            {
                float dispX = Random.Range(-20f, 20f);
                float dispY = Random.Range(-5f, 5f);
                _glitchOverlay.rectTransform.anchoredPosition = new Vector2(dispX, dispY);

                Color gc = Color.Lerp(
                    new Color(1f, 0f, 0f, 0.4f),
                    new Color(1f, 0.3f, 0f, 0.3f),
                    Mathf.PingPong(Time.unscaledTime * 10f, 1f)
                );
                _glitchOverlay.color = gc;
            }

            // Scanline flicker
            if (_scanlineOverlay != null)
            {
                float scanAlpha = 0.1f + Mathf.PingPong(Time.unscaledTime * 5f, 0.15f);
                _scanlineOverlay.color = new Color(1f, 0f, 0f, scanAlpha);
            }

            // Glitchy text corruption (like DimensionTransitionEffect)
            if (_tmp != null && _originalText != null)
            {
                if (Random.value < 0.35f)
                {
                    _tmp.text = CorruptText(_originalText);
                }
                else
                {
                    _tmp.text = _originalText;
                }

                // Jitter position
                float jitter = Mathf.Sin(Time.unscaledTime * 40f) * 3f + Random.Range(-3f, 3f);
                _tmp.rectTransform.anchoredPosition = new Vector2(jitter, Random.Range(-1f, 1f));

                // Color flicker between red and white
                _tmp.color = Color.Lerp(Color.red, Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 1f));
            }
        }

        private string CorruptText(string original)
        {
            char[] result = original.ToCharArray();
            int glitchCount = Random.Range(1, Mathf.Max(2, result.Length / 3));
            for (int i = 0; i < glitchCount; i++)
            {
                int idx = Random.Range(0, result.Length);
                if (result[idx] != '\n' && result[idx] != '<' && result[idx] != '>')
                    result[idx] = GlitchChars[Random.Range(0, GlitchChars.Length)];
            }
            return new string(result);
        }
    }
}
