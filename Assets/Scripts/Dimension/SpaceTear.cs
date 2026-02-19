using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Attach to an empty GameObject to procedurally generate a stretched-star
    /// shaped "tear in space" that looks like a window into another dimension.
    /// Bright white-pink core with radiating star rays; the interior is filled
    /// with a swirling cyberpunk nebula landscape (pink / magenta / cyan /
    /// violet with floating dark fragments).
    /// No materials, textures or prefabs needed – everything is built at runtime.
    /// </summary>
    public class SpaceTear : MonoBehaviour
    {
        [Header("Overall")]
        [Tooltip("Scale multiplier for the entire effect.")]
        [SerializeField] private float scale = 1f;

        [Tooltip("Number of points on the star burst.")]
        [SerializeField, Range(4, 12)] private int starPoints = 4;

        [Header("Core Glow")]
        [SerializeField] private int   coreCount    = 80;
        [SerializeField] private float coreLifetime = 1.6f;
        [SerializeField] private float coreSize     = 0.6f;

        [Header("Star Rays")]
        [SerializeField] private int   rayCount       = 12;
        [SerializeField] private float rayLifetime    = 1.0f;
        [SerializeField] private float rayLength      = 2.5f;
        [SerializeField] private float rayWidth       = 0.12f;
        [SerializeField] private float raySpeed       = 1.5f;

        [Header("Inner Nebula (portal landscape)")]
        [SerializeField] private int   nebulaCount    = 100;
        [SerializeField] private float nebulaLifetime = 2.5f;
        [SerializeField] private float nebulaSize     = 0.35f;

        [Header("Floating Fragments (dark blocks)")]
        [SerializeField] private int   fragmentCount  = 18;
        [SerializeField] private float fragmentLife   = 3f;
        [SerializeField] private float fragmentSize   = 0.18f;

        [Header("Edge Shimmer")]
        [SerializeField] private int   shimmerCount   = 50;
        [SerializeField] private float shimmerLife    = 0.7f;
        [SerializeField] private float shimmerSize    = 0.08f;
        [SerializeField] private float shimmerSpeed   = 3f;

        [Header("Energy Wisps")]
        [SerializeField] private int   wispCount      = 35;
        [SerializeField] private float wispLifetime   = 1.2f;
        [SerializeField] private float wispSize       = 0.14f;
        #pragma warning disable CS0414 // Reserved for future wisp orbit speed
        [SerializeField] private float wispSpeed      = 1.2f;
        #pragma warning restore CS0414

        [Header("Center Star Shape")]
        [Tooltip("Radius of the vertical (top/bottom) star tips.")]
        [SerializeField] private float starVerticalRadius   = 0.9f;
        [Tooltip("Radius of the horizontal (left/right) star tips.")]
        [SerializeField] private float starHorizontalRadius = 0.45f;
        [Tooltip("Inner radius at the valleys between tips.")]
        [SerializeField] private float starInnerRadius = 0.2f;
        [Tooltip("How much the edges curve inward (0 = straight, 1 = deep concave).")]
        [SerializeField, Range(0f, 1f)] private float starConcavity = 0.35f;

        [Header("Breathing Animation")]
        [Tooltip("Speed of the breathing cycle.")]
        [SerializeField] private float breathSpeed = 0.6f;
        [Tooltip("How much larger the star gets at peak breath (multiplier).")]
        [SerializeField] private float breathScaleBoost = 0.25f;
        [Tooltip("Maximum concavity at peak breath.")]
        [SerializeField, Range(0f, 1f)] private float breathMaxConcavity = 0.7f;
        [Tooltip("How dark-purple the star tints at peak breath.")]
        [SerializeField, Range(0f, 1f)] private float breathTintStrength = 0.4f;

        // ── colour palette ─────────────────────────────────────────────
        private static readonly Color ColWhiteCore      = new Color(1f,    0.96f, 1f,    1f);
        private static readonly Color ColSoftPink       = new Color(1f,    0.75f, 0.88f, 1f);
        private static readonly Color ColHotPink        = new Color(1f,    0.08f, 0.58f, 1f);
        private static readonly Color ColMagenta        = new Color(0.93f, 0.05f, 0.93f, 1f);
        private static readonly Color ColElectricPurple = new Color(0.69f, 0.13f, 1f,    1f);
        private static readonly Color ColDeepViolet     = new Color(0.33f, 0.05f, 0.55f, 1f);
        private static readonly Color ColCyan           = new Color(0f,    0.9f,  1f,    1f);
        private static readonly Color ColElectricBlue   = new Color(0.1f,  0.4f,  1f,    1f);
        private static readonly Color ColNeonGreen      = new Color(0.2f,  1f,    0.4f,  0.6f);

        // Dark fragment colours (like floating blocks in the dimension)
        private static readonly Color ColDarkBlock      = new Color(0.06f, 0.02f, 0.12f, 0.85f);
        private static readonly Color ColDarkPurple     = new Color(0.15f, 0.03f, 0.25f, 0.7f);

        private const float HDR_CORE  = 5f;
        private const float HDR_RAY   = 3.5f;
        private const float HDR_MID   = 2.5f;
        private const float HDR_SOFT  = 1.5f;

        private Material _additiveMat;
        private Material _alphaMat;   // for opaque-ish dark fragments

        // breathing state
        private Mesh _starMesh;
        private ParticleSystem _starPS;
        private ParticleSystemRenderer _starRend;
        private Material _starMat;
        private float _baseConcavity;
        private Color _starBaseColor;

        // ────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _additiveMat = CreateMaterial(additive: true);
            _alphaMat    = CreateMaterial(additive: false);
            _baseConcavity = starConcavity;
            _starBaseColor = HDR(ColWhiteCore, HDR_CORE);

            BuildCenterStarMesh();
            BuildCoreGlow();
            BuildStarRays();
            BuildInnerNebula();
            BuildFloatingFragments();
            BuildEdgeShimmer();
            BuildEnergyWisps();
        }

        // ── Breathing update ───────────────────────────────────────────
        private void Update()
        {
            if (_starPS == null || _starMesh == null) return;

            // smooth sine breath: 0 at rest, 1 at peak
            float breath = (Mathf.Sin(Time.time * breathSpeed * Mathf.PI * 2f) + 1f) * 0.5f;

            // 1. Scale: grow and shrink
            float s = 1f + breath * breathScaleBoost;
            _starPS.transform.localScale = new Vector3(s, s, s);

            // 2. Concavity: lerp from base to max
            starConcavity = Mathf.Lerp(_baseConcavity, breathMaxConcavity, breath);
            RegenerateStarMesh();

            // 3. Colour tint: white-pink at rest → dark purple at peak
            var tintedColor = Color.Lerp(_starBaseColor, HDR(ColDeepViolet, HDR_MID), breath * breathTintStrength);
            _starMat.SetColor("_TintColor", tintedColor);
        }

        // ── Materials ──────────────────────────────────────────────────
        private Material CreateMaterial(bool additive)
        {
            // Use legacy shader for reliable ZTest override (renders through objects)
            Shader shader;
            if (additive)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
                if (shader == null) shader = Shader.Find("Particles/Additive");
            }
            else
            {
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                if (shader == null) shader = Shader.Find("Particles/Alpha Blended");
            }

            // Fallback to URP if legacy not available
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            var mat = new Material(shader);

            // Disable depth testing - renders through all objects
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4000; // Overlay queue - renders after everything

            return mat;
        }

        // ── Helpers ────────────────────────────────────────────────────
        private ParticleSystem CreateSubSystem(string name, bool useAlpha = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake     = true;
            main.loop            = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode     = ParticleSystemScalingMode.Hierarchy;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material    = useAlpha ? _alphaMat : _additiveMat;
            rend.renderMode  = ParticleSystemRenderMode.Billboard;
            rend.sortingOrder = 1;

            return ps;
        }

        private static Color HDR(Color c, float intensity)
        {
            return new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a);
        }

        private static Gradient MakeGradient(params (float t, Color c, float a)[] keys)
        {
            var g    = new Gradient();
            var cks  = new GradientColorKey[keys.Length];
            var aks  = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                cks[i] = new GradientColorKey(keys[i].c, keys[i].t);
                aks[i] = new GradientAlphaKey(keys[i].a, keys[i].t);
            }
            g.SetKeys(cks, aks);
            return g;
        }

        // ================================================================
        //  0. CENTER STAR MESH  – single stretched-star particle
        // ================================================================
        private void BuildCenterStarMesh()
        {
            var ps   = CreateSubSystem("Center Star");
            var main = ps.main;
            main.maxParticles  = 1;
            main.startLifetime = Mathf.Infinity;
            main.startSpeed    = 0f;
            main.startSize     = 1f;  // mesh uses actual vertex positions
            main.startColor    = HDR(ColWhiteCore, HDR_CORE);

            // emit exactly one particle, never again
            var emission = ps.emission;
            emission.enabled     = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            // no shape spread — single point
            var shape = ps.shape;
            shape.enabled = false;

            // render as mesh with its own material for breathing tint
            _starMat = new Material(_additiveMat);
            _starMat.SetColor("_TintColor", HDR(ColWhiteCore, HDR_CORE));

            _starRend = ps.GetComponent<ParticleSystemRenderer>();
            _starRend.renderMode  = ParticleSystemRenderMode.Mesh;
            _starMesh = GenerateStarMesh();
            _starRend.mesh        = _starMesh;
            _starRend.material    = _starMat;
            _starRend.sortingOrder = 6; // topmost

            _starPS = ps;
        }

        // ── Procedural star mesh ───────────────────────────────────────
        private Mesh GenerateStarMesh()
        {
            int pts       = starPoints;
            int segments  = 8;  // subdivisions per edge for the curve
            int totalEdge = pts * 2;           // tip-valley pairs
            int vertCount = 1 + totalEdge * segments; // center + ring verts

            var verts  = new Vector3[vertCount];
            var colors = new Color[vertCount];
            var uvs    = new Vector2[vertCount];

            // center vertex
            verts[0]  = Vector3.zero;
            colors[0] = HDR(ColWhiteCore, HDR_CORE);
            uvs[0]    = new Vector2(0.5f, 0.5f);

            float angleStep = 360f / (pts * 2); // alternating tip/valley
            float maxRadius = Mathf.Max(starVerticalRadius, starHorizontalRadius) * scale;

            // build ring positions (tip, valley, tip, valley ...)
            var ringPositions = new Vector3[pts * 2];
            for (int i = 0; i < pts * 2; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                if (i % 2 == 0)
                {
                    // tip vertex – pick radius based on direction
                    // vertical tips point up/down (cosine-dominant), horizontal point left/right (sine-dominant)
                    float cosA = Mathf.Abs(Mathf.Cos(angle));
                    float sinA = Mathf.Abs(Mathf.Sin(angle));
                    float r = Mathf.Lerp(starHorizontalRadius, starVerticalRadius, cosA / (cosA + sinA + 0.001f)) * scale;
                    ringPositions[i] = new Vector3(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r, 0f);
                }
                else
                {
                    // valley vertex
                    float r = starInnerRadius * scale;
                    ringPositions[i] = new Vector3(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r, 0f);
                }
            }

            // interpolate between ring positions with inward curve
            int vi = 1;
            for (int i = 0; i < pts * 2; i++)
            {
                var from = ringPositions[i];
                var to   = ringPositions[(i + 1) % (pts * 2)];

                for (int s = 0; s < segments; s++)
                {
                    float t = (float)s / segments;
                    // linear interpolation
                    var pos = Vector3.Lerp(from, to, t);

                    // pull inward toward center for concavity
                    float concaveAmount = Mathf.Sin(t * Mathf.PI) * starConcavity;
                    pos = Vector3.Lerp(pos, Vector3.zero, concaveAmount);

                    verts[vi] = pos;

                    // colour: tips are brighter, valleys are pinker
                    float distRatio = pos.magnitude / (maxRadius + 0.001f);
                    colors[vi] = Color.Lerp(
                        HDR(ColWhiteCore, HDR_CORE),
                        HDR(ColSoftPink,  HDR_RAY),
                        distRatio * 0.5f);

                    uvs[vi] = new Vector2(
                        0.5f + pos.x / (maxRadius * 2f + 0.001f),
                        0.5f + pos.y / (maxRadius * 2f + 0.001f));

                    vi++;
                }
            }

            // build triangles: fan from center to each adjacent pair on ring
            int triCount = (vertCount - 1);
            var tris = new int[triCount * 3];
            for (int i = 0; i < triCount; i++)
            {
                int curr = 1 + i;
                int next = 1 + (i + 1) % (vertCount - 1);
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = curr;
                tris[i * 3 + 2] = next;
            }

            var mesh = new Mesh { name = "SpaceTear_Star" };
            mesh.vertices  = verts;
            mesh.colors    = colors;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Update existing star mesh vertices in-place for breathing animation.
        /// Avoids GC allocation every frame.
        /// </summary>
        private void RegenerateStarMesh()
        {
            if (_starMesh == null) return;

            int pts      = starPoints;
            int segments = 8;
            float angleStep = 360f / (pts * 2);
            float maxRadius = Mathf.Max(starVerticalRadius, starHorizontalRadius) * scale;

            // rebuild ring positions
            var ringPositions = new Vector3[pts * 2];
            for (int i = 0; i < pts * 2; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                if (i % 2 == 0)
                {
                    float cosA = Mathf.Abs(Mathf.Cos(angle));
                    float sinA = Mathf.Abs(Mathf.Sin(angle));
                    float r = Mathf.Lerp(starHorizontalRadius, starVerticalRadius, cosA / (cosA + sinA + 0.001f)) * scale;
                    ringPositions[i] = new Vector3(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r, 0f);
                }
                else
                {
                    float r = starInnerRadius * scale;
                    ringPositions[i] = new Vector3(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r, 0f);
                }
            }

            var verts = _starMesh.vertices;
            verts[0] = Vector3.zero;

            int vi = 1;
            for (int i = 0; i < pts * 2; i++)
            {
                var from = ringPositions[i];
                var to   = ringPositions[(i + 1) % (pts * 2)];

                for (int s = 0; s < segments; s++)
                {
                    float t = (float)s / segments;
                    var pos = Vector3.Lerp(from, to, t);
                    float concaveAmount = Mathf.Sin(t * Mathf.PI) * starConcavity;
                    pos = Vector3.Lerp(pos, Vector3.zero, concaveAmount);
                    verts[vi] = pos;
                    vi++;
                }
            }

            _starMesh.vertices = verts;
            _starMesh.RecalculateBounds();
            _starRend.mesh = _starMesh;
        }

        // ================================================================
        //  1. CORE GLOW  – bright white-pink centre ball
        // ================================================================
        private void BuildCoreGlow()
        {
            var ps   = CreateSubSystem("Core Glow");
            var main = ps.main;
            main.maxParticles  = coreCount * 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(coreLifetime * 0.6f, coreLifetime);
            main.startSpeed    = 0f;
            main.startSize     = new ParticleSystem.MinMaxCurve(coreSize * 0.4f * scale, coreSize * scale);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(ColWhiteCore, HDR_CORE), 0.0f),
                    (0.15f, HDR(ColWhiteCore, HDR_CORE), 1.0f),
                    (0.4f, HDR(ColSoftPink,   HDR_CORE), 0.9f),
                    (0.7f, HDR(ColHotPink,    HDR_MID),  0.5f),
                    (1.0f, HDR(ColMagenta,    1f),       0.0f)
                ),
                MakeGradient(
                    (0.0f, HDR(ColWhiteCore, HDR_CORE), 0.0f),
                    (0.15f, HDR(ColWhiteCore, HDR_CORE), 1.0f),
                    (0.4f, HDR(ColSoftPink,   HDR_CORE), 0.9f),
                    (0.7f, HDR(ColMagenta,    HDR_MID),  0.5f),
                    (1.0f, HDR(ColElectricPurple, 1f),   0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled     = true;
            emission.rateOverTime = coreCount;

            // emit from center point
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.01f * scale;

            // pulse size
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var pulse = new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f),
                new Keyframe(0.5f, 0.85f), new Keyframe(1f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, pulse);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sortingOrder = 5; // on top
        }

        // ================================================================
        //  2. STAR RAYS  – stretched billboard spikes radiating outward
        // ================================================================
        private void BuildStarRays()
        {
            // Create one sub-system per star point, each rotated evenly
            float angleStep = 360f / starPoints;

            for (int i = 0; i < starPoints; i++)
            {
                float angle = i * angleStep;
                float angleRad = angle * Mathf.Deg2Rad;

                // determine if this tip is more vertical or horizontal
                float cosA = Mathf.Abs(Mathf.Cos(angleRad));
                float sinA = Mathf.Abs(Mathf.Sin(angleRad));
                float tipRadius = Mathf.Lerp(starHorizontalRadius, starVerticalRadius, cosA / (cosA + sinA + 0.001f));
                float lengthMul = tipRadius / Mathf.Max(starVerticalRadius, 0.001f);

                var ps = CreateSubSystem($"Star Ray {i}");
                ps.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

                var main = ps.main;
                main.maxParticles  = rayCount * 3;
                main.startLifetime = new ParticleSystem.MinMaxCurve(rayLifetime * 0.6f, rayLifetime);
                main.startSpeed    = new ParticleSystem.MinMaxCurve(
                    raySpeed * 0.6f * lengthMul * scale,
                    raySpeed * lengthMul * scale);
                main.startSize     = new ParticleSystem.MinMaxCurve(
                    rayWidth * 0.5f * scale,
                    rayWidth * scale);

                // colour: white core → soft pink → hot pink → fade
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(
                    MakeGradient(
                        (0.0f, HDR(ColWhiteCore, HDR_RAY),  0.0f),
                        (0.05f, HDR(ColWhiteCore, HDR_RAY), 1.0f),
                        (0.25f, HDR(ColSoftPink, HDR_RAY),  0.9f),
                        (0.55f, HDR(ColHotPink,  HDR_MID),  0.5f),
                        (0.8f, HDR(ColMagenta,   HDR_SOFT), 0.2f),
                        (1.0f, HDR(ColDeepViolet, 0.5f),    0.0f)
                    ),
                    MakeGradient(
                        (0.0f, HDR(ColWhiteCore, HDR_RAY),  0.0f),
                        (0.05f, HDR(ColWhiteCore, HDR_RAY), 1.0f),
                        (0.25f, HDR(ColSoftPink, HDR_RAY),  0.9f),
                        (0.55f, HDR(ColMagenta,  HDR_MID),  0.5f),
                        (0.8f, HDR(ColElectricPurple, HDR_SOFT), 0.2f),
                        (1.0f, HDR(ColDeepViolet, 0.5f),    0.0f)
                    )
                );

                var emission = ps.emission;
                emission.enabled     = true;
                emission.rateOverTime = rayCount;

                // shape: very narrow cone from center
                var shape = ps.shape;
                shape.enabled   = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle     = 3f;                  // near-parallel rays
                shape.radius    = 0.01f * scale;
                shape.rotation  = new Vector3(-90f, 0f, 0f); // emit along local Y

                // stretched billboard so each particle is a long spike
                var rend = ps.GetComponent<ParticleSystemRenderer>();
                rend.renderMode    = ParticleSystemRenderMode.Stretch;
                rend.lengthScale   = rayLength * lengthMul;
                rend.velocityScale = 0.15f;
                rend.sortingOrder  = 3;

                // size over lifetime – taper off
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f,
                    AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

                // subtle noise so rays shimmer
                var noise = ps.noise;
                noise.enabled   = true;
                noise.strength  = new ParticleSystem.MinMaxCurve(0.08f * scale);
                noise.frequency = 3f;
            }
        }

        // ================================================================
        //  3. INNER NEBULA  – swirling portal landscape filling the star
        // ================================================================
        private void BuildInnerNebula()
        {
            var ps   = CreateSubSystem("Inner Nebula");
            var main = ps.main;
            main.maxParticles  = nebulaCount * 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(nebulaLifetime * 0.5f, nebulaLifetime);
            main.startSpeed    = 0f;
            main.startSize     = new ParticleSystem.MinMaxCurve(nebulaSize * 0.3f * scale, nebulaSize * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            // Two gradient blend – pink/magenta branch vs cyan/blue branch
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(ColHotPink,        HDR_MID),  0.0f),
                    (0.1f, HDR(ColHotPink,        HDR_MID),  0.7f),
                    (0.35f, HDR(ColMagenta,       HDR_MID),  0.8f),
                    (0.6f, HDR(ColElectricPurple, HDR_MID),  0.6f),
                    (0.85f, HDR(ColDeepViolet,    HDR_SOFT), 0.3f),
                    (1.0f, HDR(ColDeepViolet,     0.5f),     0.0f)
                ),
                MakeGradient(
                    (0.0f, HDR(ColCyan,         HDR_MID),  0.0f),
                    (0.1f, HDR(ColCyan,         HDR_MID),  0.6f),
                    (0.35f, HDR(ColElectricBlue, HDR_MID), 0.7f),
                    (0.6f, HDR(ColElectricPurple, HDR_SOFT), 0.5f),
                    (0.85f, HDR(ColDeepViolet,   HDR_SOFT), 0.25f),
                    (1.0f, HDR(ColDeepViolet,    0.5f),     0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled     = true;
            emission.rateOverTime = nebulaCount;

            // spawn slightly offset so orbital motion creates circles
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.25f * scale;

            // slow rotation for swirling look
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-30f * Mathf.Deg2Rad, 30f * Mathf.Deg2Rad);

            // strong orbital velocity for circular motion, negative radial keeps them from escaping
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(0.8f * scale, 1.8f * scale);
            vel.radial   = new ParticleSystem.MinMaxCurve(-0.3f * scale); // pull inward to keep circular

            // size pulse
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var pulse = new AnimationCurve(
                new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f),
                new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, pulse);

            // noise for organic nebula motion
            var noise = ps.noise;
            noise.enabled     = true;
            noise.strength    = new ParticleSystem.MinMaxCurve(0.25f * scale);
            noise.frequency   = 1f;
            noise.octaveCount = 3;
            noise.scrollSpeed = 0.8f;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sortingOrder = 0; // behind core, behind rays
        }

        // ================================================================
        //  4. FLOATING FRAGMENTS  – dark tumbling blocks inside the portal
        // ================================================================
        private void BuildFloatingFragments()
        {
            var ps   = CreateSubSystem("Floating Fragments", useAlpha: true);
            var main = ps.main;
            main.maxParticles  = fragmentCount * 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(fragmentLife * 0.5f, fragmentLife);
            main.startSpeed    = 0f;
            main.startSize     = new ParticleSystem.MinMaxCurve(fragmentSize * 0.4f * scale, fragmentSize * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor    = new ParticleSystem.MinMaxGradient(ColDarkBlock, ColDarkPurple);

            // colour over lifetime – dark with subtle edge glow, then fade
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, ColDarkBlock,                         0.0f),
                    (0.1f, ColDarkBlock,                         0.8f),
                    (0.5f, ColDarkPurple,                        0.85f),
                    (0.8f, HDR(ColElectricPurple, 0.4f),         0.5f),
                    (1.0f, ColDarkBlock,                          0.0f)
                ),
                MakeGradient(
                    (0.0f, ColDarkPurple,                        0.0f),
                    (0.1f, ColDarkPurple,                        0.7f),
                    (0.5f, ColDarkBlock,                         0.8f),
                    (0.8f, HDR(ColMagenta, 0.3f),               0.4f),
                    (1.0f, ColDarkBlock,                          0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled     = true;
            emission.rateOverTime = fragmentCount * 0.3f;

            // spawn offset so they orbit at different radii
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.2f * scale;

            // tumble rotation
            var rot = ps.rotationOverLifetime;
            rot.enabled     = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);
            rot.y = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);
            rot.z = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);

            // circular orbital motion
            var vel = ps.velocityOverLifetime;
            vel.enabled  = true;
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(0.4f * scale, 0.9f * scale);
            vel.radial   = new ParticleSystem.MinMaxCurve(-0.15f * scale);

            // stretched for block/shard feel
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale   = 1.5f;
            rend.velocityScale = 0.05f;
            rend.sortingOrder  = 2; // in front of nebula, behind core

            // noise for random drifting
            var noise = ps.noise;
            noise.enabled   = true;
            noise.strength  = new ParticleSystem.MinMaxCurve(0.15f * scale);
            noise.frequency = 0.8f;
        }

        // ================================================================
        //  5. EDGE SHIMMER  – fast bright sparks along the star boundary
        // ================================================================
        private void BuildEdgeShimmer()
        {
            var ps   = CreateSubSystem("Edge Shimmer");
            var main = ps.main;
            main.maxParticles  = shimmerCount * 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(shimmerLife * 0.3f, shimmerLife);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(shimmerSpeed * 0.5f * scale, shimmerSpeed * scale);
            main.startSize     = new ParticleSystem.MinMaxCurve(shimmerSize * 0.3f * scale, shimmerSize * scale);
            main.gravityModifier = 0.15f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(ColWhiteCore,  HDR_RAY),  0.0f),
                    (0.05f, HDR(ColWhiteCore, HDR_RAY),  1.0f),
                    (0.3f, HDR(ColSoftPink,   HDR_MID),  0.8f),
                    (0.6f, HDR(ColHotPink,    HDR_SOFT), 0.4f),
                    (1.0f, HDR(ColMagenta,    0.5f),     0.0f)
                ),
                MakeGradient(
                    (0.0f, HDR(ColWhiteCore, HDR_RAY),  0.0f),
                    (0.05f, HDR(ColCyan,     HDR_RAY),  1.0f),
                    (0.3f, HDR(ColCyan,      HDR_MID),  0.8f),
                    (0.6f, HDR(ColElectricBlue, HDR_SOFT), 0.4f),
                    (1.0f, HDR(ColDeepViolet, 0.5f),    0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled     = true;
            emission.rateOverTime = shimmerCount * 0.4f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)4, (short)10, 4, 0.4f)
            });

            // emit from center
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.01f * scale;

            // size shrink
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // trails for spark streaks
            var trails = ps.trails;
            trails.enabled            = true;
            trails.ratio              = 0.8f;
            trails.lifetime           = new ParticleSystem.MinMaxCurve(0.15f);
            trails.minVertexDistance   = 0.02f;
            trails.widthOverTrail     = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0f));
            trails.inheritParticleColor = true;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.trailMaterial = _additiveMat;
            rend.sortingOrder  = 4;
        }

        // ================================================================
        //  6. ENERGY WISPS  – slow coloured tendrils with trails
        // ================================================================
        private void BuildEnergyWisps()
        {
            var ps   = CreateSubSystem("Energy Wisps");
            var main = ps.main;
            main.maxParticles  = wispCount * 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(wispLifetime * 0.5f, wispLifetime);
            main.startSpeed    = 0f;
            main.startSize     = new ParticleSystem.MinMaxCurve(wispSize * 0.4f * scale, wispSize * scale);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(ColMagenta,        HDR_MID), 0.0f),
                    (0.1f, HDR(ColHotPink,        HDR_MID), 0.8f),
                    (0.4f, HDR(ColElectricPurple, HDR_MID), 0.7f),
                    (0.7f, HDR(ColDeepViolet,     HDR_SOFT), 0.3f),
                    (1.0f, HDR(ColDeepViolet,     0.5f),    0.0f)
                ),
                MakeGradient(
                    (0.0f, HDR(ColCyan,         HDR_MID), 0.0f),
                    (0.1f, HDR(ColCyan,         HDR_MID), 0.8f),
                    (0.4f, HDR(ColElectricBlue, HDR_MID), 0.7f),
                    (0.7f, HDR(ColElectricPurple, HDR_SOFT), 0.3f),
                    (1.0f, HDR(ColDeepViolet,   0.5f),     0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled     = true;
            emission.rateOverTime = wispCount * 0.4f;

            // spawn offset for orbital circles
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.3f * scale;
            var vel = ps.velocityOverLifetime;
            vel.enabled  = true;
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(0.8f * scale, 1.5f * scale);
            vel.radial   = new ParticleSystem.MinMaxCurve(-0.25f * scale);

            // trails
            var trails = ps.trails;
            trails.enabled            = true;
            trails.ratio              = 0.7f;
            trails.lifetime           = new ParticleSystem.MinMaxCurve(0.25f);
            trails.minVertexDistance   = 0.04f;
            trails.widthOverTrail     = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            trails.inheritParticleColor = true;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.trailMaterial = _additiveMat;
            rend.sortingOrder  = 2;

            // noise for organic movement
            var noise = ps.noise;
            noise.enabled     = true;
            noise.strength    = new ParticleSystem.MinMaxCurve(0.4f * scale);
            noise.frequency   = 1.5f;
            noise.octaveCount = 2;
            noise.scrollSpeed = 1f;

            // size taper
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 0.7f, 1f, 0f));
        }

        // ── Gizmo ──────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            // draw star outline
            int pts = starPoints;
            float valleyAngleStep = 360f / (pts * 2);

            var ringPos = new Vector3[pts * 2];
            for (int i = 0; i < pts * 2; i++)
            {
                float a = i * valleyAngleStep * Mathf.Deg2Rad;
                float r;
                if (i % 2 == 0)
                {
                    float cosA = Mathf.Abs(Mathf.Cos(a));
                    float sinA = Mathf.Abs(Mathf.Sin(a));
                    r = Mathf.Lerp(starHorizontalRadius, starVerticalRadius, cosA / (cosA + sinA + 0.001f)) * scale;
                }
                else
                {
                    r = starInnerRadius * scale;
                }
                ringPos[i] = transform.position + new Vector3(Mathf.Sin(a) * r, Mathf.Cos(a) * r, 0f);
            }

            Gizmos.color = ColHotPink;
            for (int i = 0; i < pts * 2; i++)
            {
                Gizmos.DrawLine(ringPos[i], ringPos[(i + 1) % (pts * 2)]);
            }

            // core sphere
            Gizmos.color = ColWhiteCore;
            Gizmos.DrawWireSphere(transform.position, 0.05f * scale);
        }
    }
}
