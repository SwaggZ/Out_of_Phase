using UnityEngine;
using System.Collections.Generic;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Procedurally generates blocky voxel-style platforms for the Abstract/Glitch dimension.
    /// Platforms are stacked cuboid layers with tapering bases and embedded neon light lines.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GlitchPlatform : MonoBehaviour
    {
        public enum PlatformType
        {
            Walkable,   // Flat top, tapered bottom, fully solid
            Background  // Can be irregular, may have missing sections
        }

        [Header("Platform Type")]
        [SerializeField] private PlatformType platformType = PlatformType.Walkable;
        public PlatformType Type { get => platformType; set => platformType = value; }

        [Header("Size")]
        [Tooltip("Width of the platform (X axis).")]
        [SerializeField] private float width = 6f;
        public float Width { get => width; set => width = value; }

        [Tooltip("Depth of the platform (Z axis).")]
        [SerializeField] private float depth = 4f;
        public float Depth { get => depth; set => depth = value; }

        [Tooltip("Total height of the platform (Y axis).")]
        [SerializeField] private float height = 3f;
        public float Height { get => height; set => height = value; }

        [Header("Layer Structure")]
        [Tooltip("Number of stacked layers (4-20).")]
        [SerializeField, Range(4, 20)] private int layerCount = 6;
        public int LayerCount { get => layerCount; set => layerCount = Mathf.Clamp(value, 4, 20); }

        [Tooltip("Taper percentage per layer (5-15%).")]
        [SerializeField, Range(0.05f, 0.15f)] private float taperPerLayer = 0.08f;
        public float TaperPerLayer { get => taperPerLayer; set => taperPerLayer = Mathf.Clamp(value, 0.05f, 0.15f); }

        [Tooltip("Chance to remove corner cube per layer (10-25%).")]
        [SerializeField, Range(0.1f, 0.25f)] private float cornerRemovalChance = 0.15f;
        public float CornerRemovalChance { get => cornerRemovalChance; set => cornerRemovalChance = value; }

        [Tooltip("Chance to spawn floating fragments (20%).")]
        [SerializeField, Range(0f, 0.4f)] private float floatingFragmentChance = 0.2f;
        public float FloatingFragmentChance { get => floatingFragmentChance; set => floatingFragmentChance = value; }

        [Header("Background Platform Settings")]
        [Tooltip("Max random Y position offset. Platform Y will range from -offset to +offset from initial position.")]
        [SerializeField] private float yPositionOffsetMax = 0f;
        public float YPositionOffsetMax { get => yPositionOffsetMax; set => yPositionOffsetMax = value; }
        
        // Store initial Y position to apply offset correctly
        private float _initialY;
        private bool _hasStoredInitialY = false;

        [Header("Random Seed")]
        [SerializeField] private int seed = 0;
        public int Seed { get => seed; set => seed = value; }

        [Header("Colors")]
        [SerializeField] private Color mainColor = new Color(0.224f, 0.243f, 0.584f); // #393E95
        public Color MainColor { get => mainColor; set => mainColor = value; }

        [SerializeField] private Color[] neonColors = new Color[]
        {
            new Color(0.914f, 0.118f, 0.725f),  // #E91EB9 - Pink
            new Color(0.463f, 0.859f, 0.718f),  // #76DBB7 - Teal
            new Color(0.949f, 0.353f, 0.259f),  // #F25A42 - Orange-red
            new Color(0.984f, 0.851f, 0.239f),  // #FBD93D - Yellow
            new Color(0.784f, 0.894f, 0.902f),  // #C8E4E6 - Light cyan
        };
        public Color[] NeonColors { get => neonColors; set => neonColors = value; }

        [Header("Light Strikes")]
        [Tooltip("Number of animated light strikes on the sides.")]
        [SerializeField, Range(2, 8)] private int lightStrikeCount = 4;
        public int LightStrikeCount { get => lightStrikeCount; set => lightStrikeCount = value; }

        [Tooltip("Speed of light strike movement.")]
        [SerializeField, Range(0.5f, 5f)] private float lightStrikeSpeed = 2f;
        public float LightStrikeSpeed { get => lightStrikeSpeed; set => lightStrikeSpeed = value; }

        [Tooltip("Width of light strikes.")]
        [SerializeField, Range(0.02f, 0.2f)] private float lightStrikeWidth = 0.08f;
        public float LightStrikeWidth { get => lightStrikeWidth; set => lightStrikeWidth = value; }

        [Header("Legacy Light Lines")]
        [Tooltip("Target for light line flow direction.")]
        [SerializeField] private Transform whiteHoleTarget;
        public Transform WhiteHoleTarget { get => whiteHoleTarget; set => whiteHoleTarget = value; }

        [Tooltip("Number of light lines to generate.")]
        [SerializeField, Range(3, 12)] private int lightLineCount = 6;
        public int LightLineCount { get => lightLineCount; set => lightLineCount = value; }

        [Tooltip("Emission intensity for light lines.")]
        [SerializeField, Range(1f, 5f)] private float emissionIntensity = 2f;
        public float EmissionIntensity { get => emissionIntensity; set => emissionIntensity = value; }

        [Header("Glitch Effects")]
        [Tooltip("Vertex noise amount (0-0.2 units).")]
        [SerializeField, Range(0f, 0.2f)] private float vertexNoise = 0.05f;
        public float VertexNoise { get => vertexNoise; set => vertexNoise = value; }

        [Header("Collision")]
        [SerializeField] private bool addCollider = true;
        public bool AddCollider { get => addCollider; set => addCollider = value; }

        // Runtime components
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _material;
        private List<FloatingFragment> _fragments = new List<FloatingFragment>();
        private List<LightStrike> _lightStrikes = new List<LightStrike>();

        // Voxel unit size
        private const float VOXEL_SIZE = 0.5f;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            GeneratePlatform();
        }

        private void Update()
        {
            UpdateFloatingFragments();
            UpdateLightStrikes();
            UpdateLightLineFlicker();
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            foreach (var frag in _fragments)
            {
                if (frag.gameObject != null) Destroy(frag.gameObject);
            }
            foreach (var strike in _lightStrikes)
            {
                if (strike.gameObject != null) Destroy(strike.gameObject);
            }
        }

        [ContextMenu("Regenerate Platform")]
        public void GeneratePlatform()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

            // Store initial Y position on first generation
            if (!_hasStoredInitialY)
            {
                _initialY = transform.position.y;
                _hasStoredInitialY = true;
            }

            // Generate random seed if not set
            if (seed == 0)
                seed = Random.Range(1, int.MaxValue);
            
            Random.InitState(seed);
            
            // Apply random Y offset if set
            if (yPositionOffsetMax > 0)
            {
                float randomYOffset = Random.Range(-yPositionOffsetMax, yPositionOffsetMax);
                Vector3 pos = transform.position;
                pos.y = _initialY + randomYOffset;
                transform.position = pos;
            }

            // Generate random dimensions if set to 0
            if (width <= 0)
                width = Random.Range(3f, 10f);
            if (height <= 0)
                height = Random.Range(2f, 6f);
            if (depth <= 0)
                depth = Random.Range(2f, 8f);

            ClearFragments();
            ClearLightStrikes();
            GenerateVoxelMesh();
            SetupMaterial();
            SetupCollider();
            GenerateFloatingFragments();
            GenerateLightStrikes();
        }

        private void GenerateVoxelMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();

            float layerHeight = height / layerCount;

            // Calculate flow direction for light lines
            Vector3 flowDir = Vector3.forward;
            if (whiteHoleTarget != null)
            {
                flowDir = (whiteHoleTarget.position - transform.position).normalized;
                flowDir.y = 0;
                if (flowDir.magnitude < 0.1f) flowDir = Vector3.forward;
                flowDir.Normalize();
            }

            // Generate light line data
            var lightLines = GenerateLightLineData(flowDir);

            // For background: generate height offsets for irregular top
            float[,] heightMap = null;
            if (platformType == PlatformType.Background)
            {
                heightMap = GenerateIrregularHeightMap();
            }

            // For walkable: generate random tip offset (bottom point not centered)
            // Keep at least 20% margin from edges
            float tipOffsetX = 0f;
            float tipOffsetZ = 0f;
            if (platformType == PlatformType.Walkable)
            {
                float marginX = width * 0.3f; // 30% margin from each edge
                float marginZ = depth * 0.3f;
                tipOffsetX = Random.Range(-width * 0.5f + marginX, width * 0.5f - marginX);
                tipOffsetZ = Random.Range(-depth * 0.5f + marginZ, depth * 0.5f - marginZ);
            }

            // Build each layer
            for (int layer = 0; layer < layerCount; layer++)
            {
                float t = (float)layer / (layerCount - 1);
                
                // Walkable: consistent taper. Background: random/no taper
                float taper;
                if (platformType == PlatformType.Background)
                {
                    // Random size per layer - no consistent cone shape
                    taper = Random.Range(0.6f, 1.1f);
                }
                else
                {
                    taper = 1f - (t * taperPerLayer * layerCount);
                }

                float layerW = width * taper;
                float layerD = depth * taper;
                float yBottom = height * 0.5f - (layer + 1) * layerHeight;
                float yTop = yBottom + layerHeight;

                // For walkable: top layer must be complete
                bool isTopLayer = (layer == 0);
                bool canRemoveCorners = !isTopLayer || platformType == PlatformType.Background;

                // Background platforms: more aggressive removal and height variation
                if (platformType == PlatformType.Background)
                {
                    GenerateIrregularLayer(vertices, triangles, colors,
                        layerW, layerD, yBottom, yTop, layer, lightLines, flowDir, heightMap,
                        Vector3.zero);
                    
                    // Add random block extrusions on sides
                    int bgExtrusionCount = Random.Range(1, 4);
                    for (int e = 0; e < bgExtrusionCount; e++)
                    {
                        GenerateSideExtrusion(vertices, triangles, colors, 
                            layerW, layerD, yBottom, yTop, lightLines, flowDir, Vector3.zero);
                    }
                }
                else
                {
                    // Determine which corners to remove
                    bool[] removeCorner = new bool[4];
                    if (canRemoveCorners)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            removeCorner[c] = Random.value < cornerRemovalChance;
                        }
                    }

                    // Calculate layer offset toward tip (increases as we go down)
                    float layerOffsetX = tipOffsetX * t;
                    float layerOffsetZ = tipOffsetZ * t;
                    Vector3 layerOffset = new Vector3(layerOffsetX, 0, layerOffsetZ);

                    // Top layer for walkable: use overlapping blocks for interesting look
                    if (isTopLayer)
                    {
                        GenerateWalkableTopLayer(vertices, triangles, colors,
                            layerW, layerD, yBottom, yTop, lightLines, flowDir);
                    }
                    else
                    {
                        // Generate main slab with potential corner removal
                        GenerateLayerSlab(vertices, triangles, colors, 
                            layerW, layerD, yBottom, yTop, layer, lightLines, flowDir, removeCorner, layerOffset);
                    }
                    
                    // Add random block extrusions on sides
                    int extrusionCount = Random.Range(1, 4);
                    for (int e = 0; e < extrusionCount; e++)
                    {
                        GenerateSideExtrusion(vertices, triangles, colors, 
                            layerW, layerD, yBottom, yTop, lightLines, flowDir, layerOffset);
                    }
                }
            }

            CreateMesh(vertices, triangles, colors);
        }

        private float[,] GenerateIrregularHeightMap()
        {
            // Create a 3x3 grid of height offsets for irregular surfaces
            float[,] map = new float[3, 3];
            float maxOffset = height * 0.4f;
            
            for (int x = 0; x < 3; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    map[x, z] = Random.Range(-maxOffset, maxOffset);
                }
            }
            return map;
        }

        private void GenerateIrregularLayer(List<Vector3> verts, List<int> tris, List<Color> colors,
            float w, float d, float yBot, float yTop, int layerIndex,
            List<LightLineData> lightLines, Vector3 flowDir, float[,] heightMap, Vector3 layerOffset)
        {
            float baseH = yTop - yBot;
            
            // MAIN BODY - always present, slightly irregular but connected
            // Core body takes up 60-80% of the layer size
            float coreScaleW = Random.Range(0.6f, 0.85f);
            float coreScaleD = Random.Range(0.6f, 0.85f);
            float coreW = w * coreScaleW;
            float coreD = d * coreScaleD;
            
            // Slight random offset for the core
            float coreOffsetX = Random.Range(-w * 0.1f, w * 0.1f);
            float coreOffsetZ = Random.Range(-d * 0.1f, d * 0.1f);
            
            // Main body height variation
            float coreH = baseH * Random.Range(0.8f, 1.2f);
            float coreYBot = yBot + Random.Range(-baseH * 0.1f, baseH * 0.1f);
            
            // Add the main connected body
            AddVoxelBox(verts, tris, colors,
                new Vector3(coreOffsetX, 0, coreOffsetZ),
                coreW, coreH, coreD,
                coreYBot, lightLines, flowDir);
            
            // Add 1-3 connected extensions to the main body (makes it look chunky/irregular)
            int extensions = Random.Range(1, 4);
            for (int e = 0; e < extensions; e++)
            {
                // Extensions connect to the main body
                int side = Random.Range(0, 4);
                float extW = coreW * Random.Range(0.2f, 0.5f);
                float extD = coreD * Random.Range(0.2f, 0.5f);
                float extH = coreH * Random.Range(0.5f, 1.1f);
                
                Vector3 extPos = new Vector3(coreOffsetX, 0, coreOffsetZ);
                switch (side)
                {
                    case 0: extPos.x -= coreW * 0.4f + extW * 0.3f; break;
                    case 1: extPos.x += coreW * 0.4f + extW * 0.3f; break;
                    case 2: extPos.z -= coreD * 0.4f + extD * 0.3f; break;
                    case 3: extPos.z += coreD * 0.4f + extD * 0.3f; break;
                }
                
                // Small random offset
                extPos.x += Random.Range(-extW * 0.2f, extW * 0.2f);
                extPos.z += Random.Range(-extD * 0.2f, extD * 0.2f);
                
                float extYBot = coreYBot + Random.Range(-baseH * 0.15f, baseH * 0.15f);
                
                AddVoxelBox(verts, tris, colors, extPos, extW, extH, extD, extYBot, lightLines, flowDir);
            }
            
            // FLOATING FRAGMENTS - small disconnected pieces around the main body
            int fragmentCount = Random.Range(2, 6);
            for (int f = 0; f < fragmentCount; f++)
            {
                // Fragments are small - 10-30% of main body size
                float fragW = coreW * Random.Range(0.1f, 0.3f);
                float fragD = coreD * Random.Range(0.1f, 0.3f);
                float fragH = coreH * Random.Range(0.15f, 0.4f);
                
                // Position fragments around the main body (not too far)
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(w * 0.4f, w * 0.7f);
                float fragX = coreOffsetX + Mathf.Cos(angle) * dist;
                float fragZ = coreOffsetZ + Mathf.Sin(angle) * dist * (d / w);
                
                // Fragments can float at different heights
                float fragYBot = yBot + Random.Range(-baseH * 0.3f, baseH * 0.5f);
                
                AddVoxelBox(verts, tris, colors, 
                    new Vector3(fragX, 0, fragZ), 
                    fragW, fragH, fragD, 
                    fragYBot, lightLines, flowDir);
            }
        }

        private List<LightLineData> GenerateLightLineData(Vector3 flowDir)
        {
            var lines = new List<LightLineData>();
            
            // Perpendicular direction for stripe alignment
            Vector3 perpDir = Vector3.Cross(flowDir, Vector3.up).normalized;

            for (int i = 0; i < lightLineCount; i++)
            {
                var line = new LightLineData
                {
                    color = neonColors[Random.Range(0, neonColors.Length)],
                    yPosition = Random.Range(-height * 0.4f, height * 0.4f),
                    thickness = Random.Range(1, 4) * VOXEL_SIZE,
                    length = Random.Range(0.3f, 0.8f), // percentage of surface
                    offset = Random.Range(-0.4f, 0.4f),
                    isHorizontal = Random.value > 0.3f,
                    isBroken = Random.value < 0.3f,
                    flickerOffset = Random.value * 10f
                };
                lines.Add(line);
            }

            return lines;
        }

        /// <summary>
        /// Generates the walkable top layer using multiple semi-overlapping blocks.
        /// All blocks share the same top Y level for a flat walking surface.
        /// </summary>
        private void GenerateWalkableTopLayer(List<Vector3> verts, List<int> tris, List<Color> colors,
            float w, float d, float yBot, float yTop, List<LightLineData> lightLines, Vector3 flowDir)
        {
            float hw = w * 0.5f;
            float hd = d * 0.5f;
            float layerH = yTop - yBot;
            
            // Create a base layer that covers most of the area
            float baseW = w * Random.Range(0.75f, 0.9f);
            float baseD = d * Random.Range(0.75f, 0.9f);
            float baseH = layerH * Random.Range(0.6f, 0.9f);
            float baseYBot = yTop - baseH; // Align top with yTop
            
            AddVoxelBox(verts, tris, colors, Vector3.zero, baseW, baseH, baseD, baseYBot, lightLines, flowDir);
            
            // Generate 4-7 overlapping blocks on top
            int blockCount = Random.Range(4, 8);
            for (int i = 0; i < blockCount; i++)
            {
                // Block size - varying sizes for visual interest
                float blockW = w * Random.Range(0.25f, 0.55f);
                float blockD = d * Random.Range(0.25f, 0.55f);
                float blockH = layerH * Random.Range(0.4f, 1.0f);
                
                // Position - spread across the platform with some overlap
                float posX = Random.Range(-hw + blockW * 0.5f, hw - blockW * 0.5f);
                float posZ = Random.Range(-hd + blockD * 0.5f, hd - blockD * 0.5f);
                
                // All blocks align at yTop for flat walking surface
                float blockYBot = yTop - blockH;
                
                AddVoxelBox(verts, tris, colors, new Vector3(posX, 0, posZ), 
                    blockW, blockH, blockD, blockYBot, lightLines, flowDir);
            }
            
            // Add edge blocks that extend slightly beyond to create interesting silhouette
            int edgeBlocks = Random.Range(2, 5);
            for (int i = 0; i < edgeBlocks; i++)
            {
                float blockW = w * Random.Range(0.15f, 0.35f);
                float blockD = d * Random.Range(0.15f, 0.35f);
                float blockH = layerH * Random.Range(0.3f, 0.7f);
                
                // Position near edges
                int edge = Random.Range(0, 4);
                float posX = 0, posZ = 0;
                switch (edge)
                {
                    case 0: posX = -hw + blockW * 0.3f; posZ = Random.Range(-hd * 0.7f, hd * 0.7f); break;
                    case 1: posX = hw - blockW * 0.3f; posZ = Random.Range(-hd * 0.7f, hd * 0.7f); break;
                    case 2: posX = Random.Range(-hw * 0.7f, hw * 0.7f); posZ = -hd + blockD * 0.3f; break;
                    case 3: posX = Random.Range(-hw * 0.7f, hw * 0.7f); posZ = hd - blockD * 0.3f; break;
                }
                
                float blockYBot = yTop - blockH;
                AddVoxelBox(verts, tris, colors, new Vector3(posX, 0, posZ), 
                    blockW, blockH, blockD, blockYBot, lightLines, flowDir);
            }
        }

        private void GenerateLayerSlab(List<Vector3> verts, List<int> tris, List<Color> colors,
            float w, float d, float yBot, float yTop, int layerIndex, 
            List<LightLineData> lightLines, Vector3 flowDir, bool[] removeCorners, Vector3 offset)
        {
            float hw = w * 0.5f;
            float hd = d * 0.5f;

            // Calculate voxel grid size for this layer
            int voxelsW = Mathf.Max(2, Mathf.RoundToInt(w / VOXEL_SIZE));
            int voxelsD = Mathf.Max(2, Mathf.RoundToInt(d / VOXEL_SIZE));

            // Generate corner regions
            float cornerW = w * 0.25f;
            float cornerD = d * 0.25f;

            // Main body - generate as solid block minus corners
            if (!removeCorners[0] && !removeCorners[1] && !removeCorners[2] && !removeCorners[3])
            {
                // Full slab
                AddVoxelBox(verts, tris, colors, offset, w, yTop - yBot, d, 
                    yBot, lightLines, flowDir);
            }
            else
            {
                // Center cross shape
                // Horizontal bar
                AddVoxelBox(verts, tris, colors, offset, w, yTop - yBot, d - cornerD * 2,
                    yBot, lightLines, flowDir);
                // Vertical bar
                AddVoxelBox(verts, tris, colors, offset, w - cornerW * 2, yTop - yBot, d,
                    yBot, lightLines, flowDir);

                // Add corners that aren't removed
                Vector3[] cornerPositions = {
                    new Vector3(-hw + cornerW * 0.5f, 0, -hd + cornerD * 0.5f) + offset,
                    new Vector3(hw - cornerW * 0.5f, 0, -hd + cornerD * 0.5f) + offset,
                    new Vector3(hw - cornerW * 0.5f, 0, hd - cornerD * 0.5f) + offset,
                    new Vector3(-hw + cornerW * 0.5f, 0, hd - cornerD * 0.5f) + offset
                };

                for (int c = 0; c < 4; c++)
                {
                    if (!removeCorners[c])
                    {
                        AddVoxelBox(verts, tris, colors, cornerPositions[c], 
                            cornerW, yTop - yBot, cornerD, yBot, lightLines, flowDir);
                    }
                }
            }
        }

        private void GenerateSideExtrusion(List<Vector3> verts, List<int> tris, List<Color> colors,
            float w, float d, float yBot, float yTop, List<LightLineData> lightLines, Vector3 flowDir, Vector3 offset)
        {
            // Random extrusion size
            float extW = Random.Range(0.2f, 0.8f);
            float extD = Random.Range(0.2f, 0.8f);
            float extH = Random.Range(0.2f, yTop - yBot);

            // Random position on a side
            int side = Random.Range(0, 4);
            Vector3 pos = Vector3.zero;
            float hw = w * 0.5f;
            float hd = d * 0.5f;

            switch (side)
            {
                case 0: // -X side
                    pos = new Vector3(-hw - extW * 0.5f, 0, Random.Range(-hd + extD, hd - extD));
                    break;
                case 1: // +X side
                    pos = new Vector3(hw + extW * 0.5f, 0, Random.Range(-hd + extD, hd - extD));
                    break;
                case 2: // -Z side
                    pos = new Vector3(Random.Range(-hw + extW, hw - extW), 0, -hd - extD * 0.5f);
                    break;
                case 3: // +Z side
                    pos = new Vector3(Random.Range(-hw + extW, hw - extW), 0, hd + extD * 0.5f);
                    break;
            }

            float extYBot = yBot + Random.Range(0, yTop - yBot - extH);
            AddVoxelBox(verts, tris, colors, pos, extW, extH, extD, extYBot, lightLines, flowDir);
        }

        private void AddVoxelBox(List<Vector3> verts, List<int> tris, List<Color> colors,
            Vector3 center, float w, float h, float d, float yBase,
            List<LightLineData> lightLines, Vector3 flowDir)
        {
            float hw = w * 0.5f;
            float hh = h * 0.5f;
            float hd = d * 0.5f;
            float yCenter = yBase + hh;

            // Apply vertex noise
            float noise = vertexNoise;

            Vector3[] corners = new Vector3[8];
            corners[0] = center + new Vector3(-hw + RandNoise(noise), yCenter - hh + RandNoise(noise), -hd + RandNoise(noise));
            corners[1] = center + new Vector3(hw + RandNoise(noise), yCenter - hh + RandNoise(noise), -hd + RandNoise(noise));
            corners[2] = center + new Vector3(hw + RandNoise(noise), yCenter - hh + RandNoise(noise), hd + RandNoise(noise));
            corners[3] = center + new Vector3(-hw + RandNoise(noise), yCenter - hh + RandNoise(noise), hd + RandNoise(noise));
            corners[4] = center + new Vector3(-hw + RandNoise(noise), yCenter + hh + RandNoise(noise), -hd + RandNoise(noise));
            corners[5] = center + new Vector3(hw + RandNoise(noise), yCenter + hh + RandNoise(noise), -hd + RandNoise(noise));
            corners[6] = center + new Vector3(hw + RandNoise(noise), yCenter + hh + RandNoise(noise), hd + RandNoise(noise));
            corners[7] = center + new Vector3(-hw + RandNoise(noise), yCenter + hh + RandNoise(noise), hd + RandNoise(noise));

            // Face definitions with correct winding
            int[][] faces = new int[][]
            {
                new int[] { 4, 7, 6, 5 }, // Top
                new int[] { 0, 1, 2, 3 }, // Bottom
                new int[] { 3, 2, 6, 7 }, // Front (+Z)
                new int[] { 0, 4, 5, 1 }, // Back (-Z)
                new int[] { 0, 3, 7, 4 }, // Left (-X)
                new int[] { 1, 5, 6, 2 }, // Right (+X)
            };

            foreach (var face in faces)
            {
                int baseIdx = verts.Count;

                Vector3 faceCenter = Vector3.zero;
                for (int i = 0; i < 4; i++)
                {
                    verts.Add(corners[face[i]]);
                    faceCenter += corners[face[i]];
                }
                faceCenter /= 4f;

                // Determine color - check for light lines
                Color faceColor = GetFaceColor(faceCenter, yCenter, lightLines, flowDir);
                for (int i = 0; i < 4; i++)
                {
                    colors.Add(faceColor);
                }

                tris.Add(baseIdx + 0);
                tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 0);
                tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 3);
            }
        }

        private float RandNoise(float amount)
        {
            if (amount <= 0) return 0;
            return Random.Range(-amount, amount);
        }

        private Color GetFaceColor(Vector3 pos, float y, List<LightLineData> lightLines, Vector3 flowDir)
        {
            // For now, return main color for all faces
            // Standard shader doesn't use vertex colors by default
            // Light lines will be handled via emission material overlay in future
            return mainColor;
        }

        private void CreateMesh(List<Vector3> vertices, List<int> triangles, List<Color> colors)
        {
            if (_mesh == null)
                _mesh = new Mesh();
            else
                _mesh.Clear();

            _mesh.name = "GlitchPlatform";
            _mesh.indexFormat = vertices.Count > 65535 
                ? UnityEngine.Rendering.IndexFormat.UInt32 
                : UnityEngine.Rendering.IndexFormat.UInt16;
            _mesh.vertices = vertices.ToArray();
            _mesh.triangles = triangles.ToArray();
            _mesh.colors = colors.ToArray();

            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            _meshFilter.mesh = _mesh;
        }

        private void SetupMaterial()
        {
            // For URP, we need to use URP shaders
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            
            // Fallbacks
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Diffuse");

            if (shader == null)
            {
                Debug.LogError("GlitchPlatform: Could not find any valid shader!");
                return;
            }

            // Destroy existing material if any
            if (_material != null)
            {
                if (Application.isPlaying)
                    Destroy(_material);
                else
                    DestroyImmediate(_material);
            }

            _material = new Material(shader);
            _material.name = "GlitchPlatformMaterial";
            
            // URP uses _BaseColor, Standard uses _Color
            if (_material.HasProperty("_BaseColor"))
                _material.SetColor("_BaseColor", mainColor);
            if (_material.HasProperty("_Color"))
                _material.SetColor("_Color", mainColor);
            _material.color = mainColor;
            
            // URP surface properties
            if (_material.HasProperty("_Smoothness"))
                _material.SetFloat("_Smoothness", 0.2f);
            if (_material.HasProperty("_Metallic"))
                _material.SetFloat("_Metallic", 0f);
            
            // Standard shader properties
            if (_material.HasProperty("_Glossiness"))
                _material.SetFloat("_Glossiness", 0.2f);
            
            _material.enableInstancing = true;
            
            // Apply to renderer
            _meshRenderer.sharedMaterial = _material;
        }

        private void SetupCollider()
        {
            if (!addCollider) return;

            // Try MeshCollider first
            var meshCol = GetComponent<MeshCollider>();
            if (meshCol == null)
                meshCol = gameObject.AddComponent<MeshCollider>();

            meshCol.sharedMesh = _mesh;
            meshCol.convex = false;
        }

        private void GenerateFloatingFragments()
        {
            if (floatingFragmentChance <= 0) return;

            int fragCount = Random.Range(2, 6);
            for (int i = 0; i < fragCount; i++)
            {
                if (Random.value > floatingFragmentChance) continue;

                var fragGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fragGO.name = "FloatingFragment";
                fragGO.transform.SetParent(transform);

                float fragSize = Random.Range(0.2f, 0.6f);
                fragGO.transform.localScale = Vector3.one * fragSize;

                Vector3 fragPos = new Vector3(
                    Random.Range(-width * 0.6f, width * 0.6f),
                    Random.Range(-height * 0.3f, height * 0.5f),
                    Random.Range(-depth * 0.6f, depth * 0.6f)
                );
                fragGO.transform.localPosition = fragPos;
                fragGO.transform.localRotation = Random.rotation;

                // Set fragment color - reuse main material as base
                var fragRenderer = fragGO.GetComponent<MeshRenderer>();
                var fragMat = new Material(_material);
                if (Random.value < 0.3f && neonColors.Length > 0)
                {
                    Color neonColor = neonColors[Random.Range(0, neonColors.Length)];
                    fragMat.color = neonColor;
                    if (fragMat.HasProperty("_EmissionColor"))
                    {
                        fragMat.EnableKeyword("_EMISSION");
                        fragMat.SetColor("_EmissionColor", neonColor * emissionIntensity * 0.5f);
                    }
                }
                else
                {
                    fragMat.color = mainColor * Random.Range(0.7f, 1f);
                }
                fragRenderer.material = fragMat;

                _fragments.Add(new FloatingFragment
                {
                    gameObject = fragGO,
                    baseY = fragPos.y,
                    oscillateSpeed = Random.Range(0.5f, 2f),
                    oscillateAmount = Random.Range(0.1f, 0.3f),
                    phaseOffset = Random.value * Mathf.PI * 2f
                });
            }
        }

        private void ClearFragments()
        {
            foreach (var frag in _fragments)
            {
                if (frag.gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(frag.gameObject);
                    else
                        DestroyImmediate(frag.gameObject);
                }
            }
            _fragments.Clear();
        }

        private void UpdateFloatingFragments()
        {
            float time = Time.time;
            foreach (var frag in _fragments)
            {
                if (frag.gameObject == null) continue;

                float y = frag.baseY + Mathf.Sin(time * frag.oscillateSpeed + frag.phaseOffset) * frag.oscillateAmount;
                var pos = frag.gameObject.transform.localPosition;
                pos.y = y;
                frag.gameObject.transform.localPosition = pos;
            }
        }

        private float _flickerTimer;
        private void UpdateLightLineFlicker()
        {
            if (_material == null || !_material.HasProperty("_EmissionColor")) return;

            _flickerTimer += Time.deltaTime;

            // Subtle emission flicker
            float flicker = 1f + Mathf.Sin(_flickerTimer * 8f) * 0.05f;
            flicker *= 1f + (Mathf.PerlinNoise(_flickerTimer * 2f, 0f) - 0.5f) * 0.1f;

            // Rare glitch pulse
            if (Random.value < 0.001f)
            {
                flicker *= 1.5f;
            }

            // Apply to emission (affects HDR intensity of vertex colors)
            _material.SetColor("_EmissionColor", mainColor * (flicker - 1f) * 0.1f);
        }

        private void ClearLightStrikes()
        {
            foreach (var strike in _lightStrikes)
            {
                if (strike.gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(strike.gameObject);
                    else
                        DestroyImmediate(strike.gameObject);
                }
            }
            _lightStrikes.Clear();
        }

        private void GenerateLightStrikes()
        {
            // Create emissive material for light strikes
            Shader strikeShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (strikeShader == null)
                strikeShader = Shader.Find("Unlit/Color");
            if (strikeShader == null)
                strikeShader = Shader.Find("Sprites/Default");

            for (int i = 0; i < lightStrikeCount; i++)
            {
                var strikeGO = new GameObject($"LightStrike_{i}");
                strikeGO.transform.SetParent(transform);
                strikeGO.transform.localPosition = Vector3.zero;
                strikeGO.transform.localRotation = Quaternion.identity;

                var lineRenderer = strikeGO.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.positionCount = 2;
                lineRenderer.startWidth = lightStrikeWidth;
                lineRenderer.endWidth = lightStrikeWidth;
                lineRenderer.numCapVertices = 2;
                lineRenderer.numCornerVertices = 2;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;

                // Create emissive material
                var strikeMat = new Material(strikeShader);
                Color strikeColor = neonColors[Random.Range(0, neonColors.Length)];
                
                // Set color based on shader type
                if (strikeMat.HasProperty("_BaseColor"))
                    strikeMat.SetColor("_BaseColor", strikeColor * emissionIntensity);
                if (strikeMat.HasProperty("_Color"))
                    strikeMat.SetColor("_Color", strikeColor * emissionIntensity);
                strikeMat.color = strikeColor * emissionIntensity;

                // Enable emission if available
                if (strikeMat.HasProperty("_EmissionColor"))
                {
                    strikeMat.EnableKeyword("_EMISSION");
                    strikeMat.SetColor("_EmissionColor", strikeColor * emissionIntensity * 2f);
                }

                lineRenderer.material = strikeMat;
                lineRenderer.startColor = strikeColor;
                lineRenderer.endColor = strikeColor;

                var strike = new LightStrike
                {
                    gameObject = strikeGO,
                    lineRenderer = lineRenderer,
                    // Walkable platforms: include top surface (side 4-5), Background: sides only (0-3)
                    side = (platformType == PlatformType.Walkable) ? Random.Range(0, 6) : Random.Range(0, 4),
                    progress = Random.value,
                    speed = lightStrikeSpeed * Random.Range(0.7f, 1.3f),
                    yPosition = Random.Range(-height * 0.4f, height * 0.4f),
                    length = Random.Range(0.15f, 0.4f), // Percentage of side length
                    color = strikeColor,
                    movingPositive = true, // Will be set based on target direction
                    topSurfaceOffset = Random.Range(-0.8f, 0.8f) // Normalized offset for top surface
                };

                // Set movement direction towards WhiteHoleTarget
                UpdateStrikeDirectionTowardsTarget(strike);

                _lightStrikes.Add(strike);
                UpdateLightStrikePosition(strike);
            }
        }

        private void UpdateStrikeDirectionTowardsTarget(LightStrike strike)
        {
            if (whiteHoleTarget == null)
            {
                strike.movingPositive = Random.value > 0.5f;
                return;
            }

            // Get direction to target in local space
            Vector3 toTarget = transform.InverseTransformDirection(whiteHoleTarget.position - transform.position);
            
            // For sides 0, 1 (-X, +X): strike moves along Z axis
            // For sides 2, 3 (-Z, +Z): strike moves along X axis
            // For sides 4, 5 (top): moves along X or Z axis towards target
            // Determine if positive progress moves towards target
            switch (strike.side)
            {
                case 0: // -X side, moves along Z
                case 1: // +X side, moves along Z
                    strike.movingPositive = toTarget.z > 0;
                    break;
                case 2: // -Z side, moves along X
                case 3: // +Z side, moves along X
                case 4: // Top surface, moves along X
                    strike.movingPositive = toTarget.x > 0;
                    break;
                case 5: // Top surface, moves along Z
                    strike.movingPositive = toTarget.z > 0;
                    break;
            }
        }

        private void UpdateLightStrikes()
        {
            float dt = Time.deltaTime;

            foreach (var strike in _lightStrikes)
            {
                if (strike.gameObject == null || strike.lineRenderer == null) continue;

                // Update direction towards target (in case target moves)
                UpdateStrikeDirectionTowardsTarget(strike);

                // Move the strike along the surface towards target
                float moveAmount = strike.speed * dt;
                if (strike.movingPositive)
                    strike.progress += moveAmount;
                else
                    strike.progress -= moveAmount;

                // Wrap around when reaching the end - respawn at far edge
                if (strike.progress > 1f + strike.length)
                {
                    strike.progress = -strike.length;
                    // Optionally change side or Y position on wrap
                    if (Random.value < 0.3f)
                    {
                        // Walkable platforms can use sides 0-5, background only 0-3
                        int maxSide = (platformType == PlatformType.Walkable) ? 6 : 4;
                        strike.side = Random.Range(0, maxSide);
                        strike.yPosition = Random.Range(-height * 0.4f, height * 0.4f);
                        strike.topSurfaceOffset = Random.Range(-0.8f, 0.8f);
                        UpdateStrikeDirectionTowardsTarget(strike);
                    }
                }
                else if (strike.progress < -strike.length)
                {
                    strike.progress = 1f + strike.length;
                    if (Random.value < 0.3f)
                    {
                        int maxSide = (platformType == PlatformType.Walkable) ? 6 : 4;
                        strike.side = Random.Range(0, maxSide);
                        strike.yPosition = Random.Range(-height * 0.4f, height * 0.4f);
                        strike.topSurfaceOffset = Random.Range(-0.8f, 0.8f);
                        UpdateStrikeDirectionTowardsTarget(strike);
                    }
                }

                UpdateLightStrikePosition(strike);

                // Occasional flicker
                if (Random.value < 0.02f)
                {
                    float flickerIntensity = Random.Range(0.5f, 1.5f);
                    strike.lineRenderer.startColor = strike.color * flickerIntensity;
                    strike.lineRenderer.endColor = strike.color * flickerIntensity;
                }
                else
                {
                    strike.lineRenderer.startColor = strike.color;
                    strike.lineRenderer.endColor = strike.color;
                }
            }
        }

        private void UpdateLightStrikePosition(LightStrike strike)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            float halfLen = strike.length * 0.5f;

            Vector3 startPos, endPos;
            float surfaceLen;

            // Calculate the taper at this Y position
            float yNormalized = (height * 0.5f - strike.yPosition) / height; // 0 at top, 1 at bottom
            float taper = 1f - (yNormalized * taperPerLayer * layerCount);
            taper = Mathf.Max(taper, 0.2f);
            
            float taperHW = hw * taper;
            float taperHD = hd * taper;

            switch (strike.side)
            {
                case 0: // -X side (moves along Z)
                    surfaceLen = depth * taper;
                    startPos = new Vector3(-taperHW - 0.01f, strike.yPosition, 
                        Mathf.Lerp(-taperHD, taperHD, strike.progress - halfLen));
                    endPos = new Vector3(-taperHW - 0.01f, strike.yPosition, 
                        Mathf.Lerp(-taperHD, taperHD, strike.progress + halfLen));
                    break;
                case 1: // +X side (moves along Z)
                    surfaceLen = depth * taper;
                    startPos = new Vector3(taperHW + 0.01f, strike.yPosition, 
                        Mathf.Lerp(-taperHD, taperHD, strike.progress - halfLen));
                    endPos = new Vector3(taperHW + 0.01f, strike.yPosition, 
                        Mathf.Lerp(-taperHD, taperHD, strike.progress + halfLen));
                    break;
                case 2: // -Z side (moves along X)
                    surfaceLen = width * taper;
                    startPos = new Vector3(Mathf.Lerp(-taperHW, taperHW, strike.progress - halfLen), 
                        strike.yPosition, -taperHD - 0.01f);
                    endPos = new Vector3(Mathf.Lerp(-taperHW, taperHW, strike.progress + halfLen), 
                        strike.yPosition, -taperHD - 0.01f);
                    break;
                case 3: // +Z side (moves along X)
                    surfaceLen = width * taper;
                    startPos = new Vector3(Mathf.Lerp(-taperHW, taperHW, strike.progress - halfLen), 
                        strike.yPosition, taperHD + 0.01f);
                    endPos = new Vector3(Mathf.Lerp(-taperHW, taperHW, strike.progress + halfLen), 
                        strike.yPosition, taperHD + 0.01f);
                    break;
                case 4: // Top surface (moves along X)
                    float topY = height * 0.5f + 0.01f;
                    float topZPos = hd * strike.topSurfaceOffset; // Use stored offset
                    startPos = new Vector3(Mathf.Lerp(-hw, hw, strike.progress - halfLen), 
                        topY, topZPos);
                    endPos = new Vector3(Mathf.Lerp(-hw, hw, strike.progress + halfLen), 
                        topY, topZPos);
                    break;
                default: // case 5: Top surface (moves along Z)
                    float topY2 = height * 0.5f + 0.01f;
                    float topXPos = hw * strike.topSurfaceOffset; // Use stored offset
                    startPos = new Vector3(topXPos, topY2, 
                        Mathf.Lerp(-hd, hd, strike.progress - halfLen));
                    endPos = new Vector3(topXPos, topY2, 
                        Mathf.Lerp(-hd, hd, strike.progress + halfLen));
                    break;
            }

            strike.lineRenderer.SetPosition(0, startPos);
            strike.lineRenderer.SetPosition(1, endPos);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = platformType == PlatformType.Walkable
                ? new Color(0.2f, 0.8f, 0.2f, 0.5f)
                : new Color(0.5f, 0.5f, 1f, 0.3f);

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, height, depth));

            // Show taper
            float bottomTaper = 1f - (taperPerLayer * layerCount);
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireCube(Vector3.down * height * 0.4f, 
                new Vector3(width * bottomTaper, height * 0.2f, depth * bottomTaper));

            Gizmos.matrix = Matrix4x4.identity;

            if (whiteHoleTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, whiteHoleTarget.position);
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying && _mesh != null)
            {
                GeneratePlatform();
            }
        }

        // Helper structs
        private struct LightLineData
        {
            public Color color;
            public float yPosition;
            public float thickness;
            public float length;
            public float offset;
            public bool isHorizontal;
            public bool isBroken;
            public float flickerOffset;
        }

        private class FloatingFragment
        {
            public GameObject gameObject;
            public float baseY;
            public float oscillateSpeed;
            public float oscillateAmount;
            public float phaseOffset;
        }

        private class LightStrike
        {
            public GameObject gameObject;
            public LineRenderer lineRenderer;
            public int side; // 0 = -X, 1 = +X, 2 = -Z, 3 = +Z, 4 = top (along X), 5 = top (along Z)
            public float progress; // 0-1 position along the surface
            public float speed;
            public float yPosition; // Y position for sides, secondary axis position for top
            public float length; // Length of the strike line
            public Color color;
            public bool movingPositive; // Direction of movement
            public float topSurfaceOffset; // For top surface: offset along the perpendicular axis
        }
    }
}
