using UnityEngine;
using UnityEngine.UI;

namespace OutOfPhase.UI
{
    /// <summary>
    /// Simple crosshair UI that auto-creates at screen center.
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("Crosshair Settings")]
        [SerializeField] private float size = 20f;
        [SerializeField] private float thickness = 2f;
        [SerializeField] private float gap = 6f;
        [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField] private bool showDot = true;
        [SerializeField] private float dotSize = 4f;

        [Header("Auto Setup")]
        [SerializeField] private bool autoCreate = true;

        private Image[] _crosshairParts;

        private void Awake()
        {
            if (autoCreate)
            {
                CreateCrosshair();
            }
        }

        private void CreateCrosshair()
        {
            // Find or create canvas
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("UI Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create crosshair container
            GameObject crosshairObj = new GameObject("Crosshair");
            crosshairObj.transform.SetParent(canvas.transform, false);

            RectTransform containerRect = crosshairObj.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(size * 2, size * 2);

            _crosshairParts = new Image[showDot ? 5 : 4];

            // Top line
            _crosshairParts[0] = CreateLine(crosshairObj.transform, "Top", 
                new Vector2(0, gap + size / 2), new Vector2(thickness, size));

            // Bottom line
            _crosshairParts[1] = CreateLine(crosshairObj.transform, "Bottom", 
                new Vector2(0, -(gap + size / 2)), new Vector2(thickness, size));

            // Left line
            _crosshairParts[2] = CreateLine(crosshairObj.transform, "Left", 
                new Vector2(-(gap + size / 2), 0), new Vector2(size, thickness));

            // Right line
            _crosshairParts[3] = CreateLine(crosshairObj.transform, "Right", 
                new Vector2(gap + size / 2, 0), new Vector2(size, thickness));

            // Center dot
            if (showDot)
            {
                _crosshairParts[4] = CreateLine(crosshairObj.transform, "Dot", 
                    Vector2.zero, new Vector2(dotSize, dotSize));
            }
        }

        private Image CreateLine(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject lineObj = new GameObject(name);
            lineObj.transform.SetParent(parent, false);

            RectTransform rect = lineObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image img = lineObj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            return img;
        }

        /// <summary>
        /// Change crosshair color at runtime.
        /// </summary>
        public void SetColor(Color newColor)
        {
            color = newColor;
            if (_crosshairParts != null)
            {
                foreach (var part in _crosshairParts)
                {
                    if (part != null)
                        part.color = newColor;
                }
            }
        }

        /// <summary>
        /// Show/hide crosshair.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_crosshairParts != null)
            {
                foreach (var part in _crosshairParts)
                {
                    if (part != null)
                        part.enabled = visible;
                }
            }
        }
    }
}
