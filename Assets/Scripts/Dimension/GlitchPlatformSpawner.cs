using UnityEngine;
using System.Collections.Generic;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Spawns multiple GlitchPlatforms in a defined area.
    /// </summary>
    public class GlitchPlatformSpawner : MonoBehaviour
    {
        [Header("Spawn Area")]
        [SerializeField] private Vector3 spawnAreaSize = new Vector3(80f, 40f, 80f);
        [SerializeField] private float spawnRadius = 50f;

        [Header("Platform Counts")]
        [SerializeField] private int walkablePlatformCount = 12;
        [SerializeField] private int backgroundPlatformCount = 25;

        [Header("Walkable Platform Size")]
        [SerializeField] private Vector2 walkableWidthRange = new Vector2(4f, 10f);
        [SerializeField] private Vector2 walkableDepthRange = new Vector2(3f, 7f);
        [SerializeField] private Vector2 walkableHeightRange = new Vector2(2f, 5f);

        [Header("Background Platform Size")]
        [SerializeField] private Vector2 backgroundWidthRange = new Vector2(2f, 8f);
        [SerializeField] private Vector2 backgroundDepthRange = new Vector2(2f, 6f);
        [SerializeField] private Vector2 backgroundHeightRange = new Vector2(1f, 4f);
        [SerializeField] private Vector2 backgroundYOffsetRange = new Vector2(-15f, 15f);

        [Header("Layer Structure")]
        [SerializeField, Range(4, 10)] private int maxLayerCount = 8;
        [SerializeField, Range(0.05f, 0.15f)] private float taperPerLayer = 0.08f;
        [SerializeField, Range(0.1f, 0.25f)] private float cornerRemovalChance = 0.15f;
        [SerializeField, Range(0f, 0.4f)] private float floatingFragmentChance = 0.2f;

        [Header("Colors")]
        [SerializeField] private Color mainColor = new Color(0.224f, 0.243f, 0.584f);
        [SerializeField] private Color[] neonColors = new Color[]
        {
            new Color(0.914f, 0.118f, 0.725f),
            new Color(0.463f, 0.859f, 0.718f),
            new Color(0.949f, 0.353f, 0.259f),
            new Color(0.984f, 0.851f, 0.239f),
            new Color(0.784f, 0.894f, 0.902f),
        };

        [Header("Light Lines")]
        [SerializeField] private Transform whiteHoleTarget;
        [SerializeField, Range(3, 12)] private int lightLineCount = 6;
        [SerializeField, Range(1f, 5f)] private float emissionIntensity = 2f;

        [Header("Generation")]
        [SerializeField] private int seed = 12345;
        [SerializeField] private float minPlatformSpacing = 3f;
        [SerializeField] private bool generateOnStart = true;

        private List<GlitchPlatform> _spawnedPlatforms = new List<GlitchPlatform>();
        private Transform _platformContainer;

        private void Start()
        {
            if (generateOnStart)
                GenerateAllPlatforms();
        }

        [ContextMenu("Generate Platforms")]
        public void GenerateAllPlatforms()
        {
            ClearPlatforms();
            Random.InitState(seed);

            _platformContainer = new GameObject("Glitch Platforms").transform;
            _platformContainer.SetParent(transform);
            _platformContainer.localPosition = Vector3.zero;

            SpawnPlatforms(GlitchPlatform.PlatformType.Walkable, walkablePlatformCount,
                walkableWidthRange, walkableDepthRange, walkableHeightRange);

            SpawnPlatforms(GlitchPlatform.PlatformType.Background, backgroundPlatformCount,
                backgroundWidthRange, backgroundDepthRange, backgroundHeightRange);
        }

        [ContextMenu("Clear Platforms")]
        public void ClearPlatforms()
        {
            foreach (var platform in _spawnedPlatforms)
            {
                if (platform != null)
                    DestroyImmediate(platform.gameObject);
            }
            _spawnedPlatforms.Clear();

            if (_platformContainer != null)
                DestroyImmediate(_platformContainer.gameObject);
        }

        private void SpawnPlatforms(GlitchPlatform.PlatformType type, int count,
            Vector2 widthRange, Vector2 depthRange, Vector2 heightRange)
        {
            var positions = new List<Vector3>();

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = Vector3.zero;
                bool validPos = false;
                int attempts = 0;

                while (!validPos && attempts < 50)
                {
                    pos = new Vector3(
                        Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
                        Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f),
                        Random.Range(-spawnAreaSize.z * 0.5f, spawnAreaSize.z * 0.5f)
                    );

                    // Check within radius if specified
                    if (spawnRadius > 0 && pos.magnitude > spawnRadius)
                    {
                        attempts++;
                        continue;
                    }

                    validPos = true;
                    foreach (var existingPos in positions)
                    {
                        if (Vector3.Distance(pos, existingPos) < minPlatformSpacing)
                        {
                            validPos = false;
                            break;
                        }
                    }
                    attempts++;
                }

                if (!validPos) continue;
                positions.Add(pos);

                // Apply extra random Y offset for background platforms
                if (type == GlitchPlatform.PlatformType.Background)
                {
                    pos.y += Random.Range(backgroundYOffsetRange.x, backgroundYOffsetRange.y);
                }

                var platformGO = new GameObject($"Platform_{type}_{i}");
                platformGO.transform.SetParent(_platformContainer);
                platformGO.transform.localPosition = pos;

                // Random rotation - walkable stays flat, background can tilt
                if (type == GlitchPlatform.PlatformType.Walkable)
                {
                    platformGO.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                }
                else
                {
                    platformGO.transform.localRotation = Quaternion.Euler(
                        Random.Range(-20f, 20f),
                        Random.Range(0f, 360f),
                        Random.Range(-20f, 20f)
                    );
                }

                platformGO.AddComponent<MeshFilter>();
                platformGO.AddComponent<MeshRenderer>();

                var platform = platformGO.AddComponent<GlitchPlatform>();
                ConfigurePlatform(platform, type, widthRange, depthRange, heightRange);

                _spawnedPlatforms.Add(platform);
            }
        }

        private void ConfigurePlatform(GlitchPlatform platform, GlitchPlatform.PlatformType type,
            Vector2 widthRange, Vector2 depthRange, Vector2 heightRange)
        {
            platform.Type = type;
            platform.Width = Random.Range(widthRange.x, widthRange.y);
            platform.Depth = Random.Range(depthRange.x, depthRange.y);
            platform.Height = Random.Range(heightRange.x, heightRange.y);
            platform.LayerCount = Random.Range(4, maxLayerCount + 1);
            platform.TaperPerLayer = Random.Range(0.05f, taperPerLayer);
            platform.CornerRemovalChance = cornerRemovalChance;
            platform.FloatingFragmentChance = floatingFragmentChance;
            platform.Seed = Random.Range(1, 99999);
            platform.MainColor = mainColor;
            platform.NeonColors = (Color[])neonColors.Clone();
            platform.WhiteHoleTarget = whiteHoleTarget;
            platform.LightLineCount = lightLineCount;
            platform.EmissionIntensity = emissionIntensity;
            platform.AddCollider = true;
        }

        private void OnDrawGizmos()
        {
            // Draw spawn area
            Gizmos.color = new Color(0.5f, 0.3f, 1f, 0.2f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, spawnAreaSize);

            Gizmos.color = new Color(0.5f, 0.3f, 1f, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, spawnAreaSize);

            // Draw spawn radius
            if (spawnRadius > 0)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(Vector3.zero, spawnRadius);
            }

            Gizmos.matrix = Matrix4x4.identity;

            if (whiteHoleTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, whiteHoleTarget.position);
                Gizmos.DrawWireSphere(whiteHoleTarget.position, 2f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            // Walkable size example
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.4f);
            Vector3 walkableSize = new Vector3(
                (walkableWidthRange.x + walkableWidthRange.y) * 0.5f,
                (walkableHeightRange.x + walkableHeightRange.y) * 0.5f,
                (walkableDepthRange.x + walkableDepthRange.y) * 0.5f
            );
            Gizmos.DrawCube(Vector3.left * spawnAreaSize.x * 0.3f, walkableSize);

            // Background size example
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.4f);
            Vector3 bgSize = new Vector3(
                (backgroundWidthRange.x + backgroundWidthRange.y) * 0.5f,
                (backgroundHeightRange.x + backgroundHeightRange.y) * 0.5f,
                (backgroundDepthRange.x + backgroundDepthRange.y) * 0.5f
            );
            Gizmos.DrawCube(Vector3.right * spawnAreaSize.x * 0.3f, bgSize);
        }
    }
}
