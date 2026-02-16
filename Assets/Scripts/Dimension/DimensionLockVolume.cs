using UnityEngine;

namespace OutOfPhase.Dimension
{
    [RequireComponent(typeof(Collider))]
    public class DimensionLockVolume : MonoBehaviour
    {
        [Header("Lock Settings")]
        [SerializeField] private bool lockSwitching = true;
        [SerializeField] private bool forceDimension = false;
        [SerializeField] private int targetDimension = 0;
        [SerializeField] private bool[] lockedDimensions;

        [Header("Debug")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0.5f, 0.3f);

        private int _playersInside = 0;
        private bool _locksApplied = false;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playersInside++;
            if (_playersInside == 1)
                OnPlayerEnter();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playersInside--;
            if (_playersInside <= 0)
            {
                _playersInside = 0;
                OnPlayerExit();
            }
        }

        private void OnPlayerEnter()
        {
            if (DimensionManager.Instance == null) return;

            ApplyDimensionLocks();

            if (lockSwitching)
                DimensionManager.Instance.LockSwitching();

            int fallback = -1;
            if (forceDimension)
            {
                fallback = targetDimension;
            }
            else if (DimensionManager.Instance.IsDimensionLocked(DimensionManager.Instance.CurrentDimension))
            {
                fallback = DimensionManager.Instance.FindNextUnlockedDimension(DimensionManager.Instance.CurrentDimension);
            }

            if (fallback >= 0)
                ShowUnstableSyncMessageAndShift(fallback);
        }

        private void OnPlayerExit()
        {
            if (DimensionManager.Instance == null) return;

            RemoveDimensionLocks();

            if (lockSwitching)
                DimensionManager.Instance.UnlockSwitching();
        }

        private void ShowUnstableSyncMessageAndShift(int targetDim)
        {
            // Create dedicated canvas (like DimensionTransitionEffect)
            var canvasObj = new GameObject("SyncUnstableCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 998;
            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen dark red overlay
            var overlayObj = new GameObject("RedOverlay");
            overlayObj.transform.SetParent(canvasObj.transform, false);
            var overlay = overlayObj.AddComponent<UnityEngine.UI.RawImage>();
            overlay.color = new Color(0.3f, 0f, 0f, 0.6f);
            SetFullScreen(overlay.rectTransform);

            // Scanline overlay (red-tinted)
            var scanTex = CreateScanlineTexture();
            var scanObj = new GameObject("ScanlineOverlay");
            scanObj.transform.SetParent(canvasObj.transform, false);
            var scanImg = scanObj.AddComponent<UnityEngine.UI.RawImage>();
            scanImg.texture = scanTex;
            scanImg.uvRect = new Rect(0, 0, 1, 270);
            scanImg.color = new Color(1f, 0f, 0f, 0.15f);
            SetFullScreen(scanImg.rectTransform);

            // Glitch bar overlay (red/dark red)
            var glitchTex = CreateGlitchTexture();
            var glitchObj = new GameObject("GlitchOverlay");
            glitchObj.transform.SetParent(canvasObj.transform, false);
            var glitchImg = glitchObj.AddComponent<UnityEngine.UI.RawImage>();
            glitchImg.texture = glitchTex;
            glitchImg.uvRect = new Rect(0, 0, 30, 1);
            glitchImg.color = new Color(1f, 0f, 0.2f, 0.4f);
            SetFullScreen(glitchImg.rectTransform);

            // Center text (like the dimension transition text)
            var textObj = new GameObject("SyncUnstableText");
            textObj.transform.SetParent(canvasObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(600, 200);
            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "SYNC UNSTABLE\n<size=48>DIMENSION SHIFTING</size>";
            tmp.fontSize = 24;
            tmp.color = Color.red;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontStyle = TMPro.FontStyles.Bold;

            // Attach glitch effect animator
            canvasObj.AddComponent<DimensionSyncGlitchEffect>();

            StartCoroutine(ShiftAfterDelay(canvasObj, targetDim, 1.5f));
        }

        private void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Texture2D CreateScanlineTexture()
        {
            var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.SetPixels(new Color[]
            {
                new Color(0, 0, 0, 0.3f),
                new Color(0, 0, 0, 0f),
                new Color(0, 0, 0, 0f),
                new Color(0, 0, 0, 0f)
            });
            tex.Apply();
            return tex;
        }

        private Texture2D CreateGlitchTexture()
        {
            var tex = new Texture2D(64, 256, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[64 * 256];
            int y = 0;
            while (y < 256)
            {
                bool isGlitch = Random.value < 0.15f;
                int barHeight = isGlitch ? Random.Range(1, 8) : Random.Range(5, 30);
                Color barColor = isGlitch
                    ? new Color(1f, Random.Range(0f, 0.2f), Random.Range(0f, 0.1f), Random.Range(0.2f, 0.6f))
                    : new Color(0, 0, 0, 0);
                for (int by = 0; by < barHeight && y + by < 256; by++)
                    for (int x = 0; x < 64; x++)
                        pixels[(y + by) * 64 + x] = barColor;
                y += barHeight;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private System.Collections.IEnumerator ShiftAfterDelay(GameObject panelObj, int targetDim, float delay)
        {
            yield return new WaitForSeconds(delay);
            Destroy(panelObj);
            if (DimensionManager.Instance != null)
                DimensionManager.Instance.ForceSwitchToDimension(targetDim);
        }

        private void OnDisable()
        {
            if (_playersInside > 0 && lockSwitching && DimensionManager.Instance != null)
                DimensionManager.Instance.UnlockSwitching();

            if (_playersInside > 0 && DimensionManager.Instance != null)
                RemoveDimensionLocks();

            _playersInside = 0;
        }

        private void ApplyDimensionLocks()
        {
            if (_locksApplied) return;
            if (DimensionManager.Instance == null) return;
            DimensionManager.Instance.AddDimensionLocks(lockedDimensions);
            _locksApplied = true;
        }

        private void RemoveDimensionLocks()
        {
            if (!_locksApplied) return;
            if (DimensionManager.Instance == null) return;
            DimensionManager.Instance.RemoveDimensionLocks(lockedDimensions);
            _locksApplied = false;
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;
            Gizmos.color = gizmoColor;
            if (col is BoxCollider box)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = oldMatrix;
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
                Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
            }
            else
            {
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
        }

        private void OnValidate()
        {
            targetDimension = Mathf.Max(0, targetDimension);
            
            // Only initialize array if null — don't resize or recreate
            // so the Inspector doesn't fight with user edits
            if (lockedDimensions == null)
            {
                lockedDimensions = new bool[5];
            }
        }
    }
}
