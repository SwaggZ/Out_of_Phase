using UnityEngine;
using System.Collections.Generic;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Creates a white hole particle effect - the opposite of a black hole.
    /// Light from dimensional tears flows inward like rivers toward a blinding core.
    /// Tears randomly spawn, open, stream their light, and close.
    /// </summary>
    public class WhiteHole : MonoBehaviour
    {
        [Header("Overall")]
        [Tooltip("Scale multiplier for the entire effect.")]
        [SerializeField] private float scale = 1f;

        [Header("White Hole Core")]
        [SerializeField] private float coreRadius = 0.8f;
        [SerializeField] private int coreParticleCount = 120;
        [SerializeField] private float coreIntensity = 8f;

        [Header("Accretion Disk")]
        [Tooltip("Swirling ring of light being pulled in.")]
        [SerializeField] private float diskInnerRadius = 1.5f;
        [SerializeField] private float diskOuterRadius = 4f;
        [SerializeField] private int diskParticleCount = 200;
        [SerializeField] private float diskRotationSpeed = 2f;

        [Header("Dimensional Tears")]
        [Tooltip("How many tears can exist at once.")]
        [SerializeField] private int maxActiveTears = 8;
        [SerializeField] private float tearSpawnRadius = 12f;
        [SerializeField] private float tearOpenDuration = 0.8f;
        [SerializeField] private float tearCloseDuration = 0.6f;

        [Header("Light Rivers")]
        [Tooltip("Streams of light flowing from tears to core.")]
        [SerializeField] private int riverParticlesPerTear = 60;
        [SerializeField] private float riverWidth = 0.15f;
        [SerializeField] private float riverFlowSpeed = 5f;
        [SerializeField] private float riverWaveAmplitude = 0.1f;
        [SerializeField] private float riverWaveFrequency = 1.5f;
        [Tooltip("How long the river takes to fade in after tear opens.")]
        [SerializeField] private float riverFadeInDuration = 1.0f;
        [Tooltip("Min wait time after river connects before tear can close.")]
        [SerializeField] private float minWaitAfterConnect = 1.0f;
        [Tooltip("Max wait time after river connects before tear can close.")]
        [SerializeField] private float maxWaitAfterConnect = 3.0f;

        [Header("Manual Light Sources")]
        [Tooltip("Fixed positions where light rivers originate (in addition to random tears).")]
        [SerializeField] private Transform[] fixedLightSources;

        [Header("Glitch Effects")]
        [SerializeField] private float glitchIntensity = 0.5f;
        [SerializeField] private float glitchFrequency = 3f;
        [SerializeField] private int glitchParticleCount = 40;

        [Header("Color Palette")]
        [SerializeField] private Color coreColor = Color.white;
        [SerializeField] private Color[] tearColors = new Color[]
        {
            new Color(1f, 0.3f, 0.5f),    // Pink
            new Color(0.3f, 0.8f, 1f),    // Cyan
            new Color(1f, 0.6f, 0.2f),    // Orange
            new Color(0.6f, 0.3f, 1f),    // Purple
            new Color(0.3f, 1f, 0.5f),    // Green
            new Color(1f, 1f, 0.3f),      // Yellow
            new Color(1f, 0.2f, 0.2f),    // Red
            new Color(0.2f, 0.4f, 1f),    // Blue
        };

        // HDR intensities
        private const float HDR_CORE = 6f;
        private const float HDR_BRIGHT = 4f;
        private const float HDR_MID = 2.5f;
        private const float HDR_SOFT = 1.5f;

        // Lower HDR for rivers to prevent additive white-out at close range
        private const float HDR_RIVER = 1.5f;
        private const float HDR_RIVER_BRIGHT = 2f;

        // Materials
        private Material _additiveMat;

        // Tear tracking
        private enum TearPhase
        {
            Opening,        // Tear fading in
            RiverStarting,  // River beginning to flow
            RiverFlowing,   // River actively flowing, waiting to connect
            Connected,      // River reached core, waiting random interval
            Closing         // Tear and river fading out
        }

        private class TearInstance
        {
            public Vector3 position;
            public Color color;
            public float spawnTime;
            public float openProgress; // 0-1 for fade in
            public float closeProgress; // 0-1 for fade out
            public TearPhase phase;
            public float riverStartTime;
            public float riverConnectTime; // When river first reached core
            public float waitAfterConnect; // Random wait duration
            public float riverFadeProgress; // 0-1 for river fade in
            public ParticleSystem tearPS;
            public ParticleSystem riverPS;
            public RiverFlowController riverController;
        }
        private List<TearInstance> _activeTears = new List<TearInstance>();
        private float _nextTearSpawnTime;

        // Particle systems
        private ParticleSystem _corePS;
        private ParticleSystem _corePulsePS;
        private ParticleSystem _diskPS;
        private ParticleSystem _glitchPS;
        private ParticleSystem _rayBurstPS;

        private void Awake()
        {
            _additiveMat = CreateMaterial();

            BuildWhiteHoleCore();
            BuildCorePulse();
            BuildAccretionDisk();
            BuildGlitchEffect();
            BuildRayBurst();

            // Create rivers for fixed light sources
            if (fixedLightSources != null)
            {
                foreach (var source in fixedLightSources)
                {
                    if (source != null)
                        CreateFixedRiver(source);
                }
            }
        }

        private void Update()
        {
            UpdateTears();
            UpdateGlitchPulse();
        }

        private Material CreateMaterial()
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null) shader = Shader.Find("Particles/Additive");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            var mat = new Material(shader);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4000;
            return mat;
        }

        private ParticleSystem CreateSubSystem(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = _additiveMat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sortingOrder = 1;

            return ps;
        }

        private static Color HDR(Color c, float intensity)
        {
            return new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a);
        }

        private static Gradient MakeGradient(params (float t, Color c, float a)[] keys)
        {
            var g = new Gradient();
            var cks = new GradientColorKey[keys.Length];
            var aks = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                cks[i] = new GradientColorKey(keys[i].c, keys[i].t);
                aks[i] = new GradientAlphaKey(keys[i].a, keys[i].t);
            }
            g.SetKeys(cks, aks);
            return g;
        }

        // ================================================================
        //  WHITE HOLE CORE - blinding bright center
        // ================================================================
        private void BuildWhiteHoleCore()
        {
            var ps = CreateSubSystem("White Hole Core");
            _corePS = ps;

            var main = ps.main;
            main.maxParticles = coreParticleCount * 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(-0.5f * scale, 0.5f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(coreRadius * 0.3f * scale, coreRadius * scale);
            main.startColor = HDR(coreColor, coreIntensity);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(coreColor, HDR_CORE), 0.0f),
                    (0.1f, HDR(coreColor, HDR_CORE), 1.0f),
                    (0.5f, HDR(coreColor, HDR_BRIGHT), 0.9f),
                    (0.8f, HDR(new Color(0.9f, 0.95f, 1f), HDR_MID), 0.5f),
                    (1.0f, HDR(new Color(0.8f, 0.9f, 1f), HDR_SOFT), 0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = coreParticleCount;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = coreRadius * 0.3f * scale;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var pulse = new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f),
                new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, pulse);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.2f * scale);
            noise.frequency = 2f;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sortingOrder = 10;
        }

        // ================================================================
        //  CORE PULSE - periodic bright flashes
        // ================================================================
        private void BuildCorePulse()
        {
            var ps = CreateSubSystem("Core Pulse");
            _corePulsePS = ps;

            var main = ps.main;
            main.maxParticles = 20;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f * scale, 5f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(coreRadius * 1.5f * scale, coreRadius * 3f * scale);
            main.startColor = HDR(coreColor, HDR_CORE * 1.5f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(coreColor, HDR_CORE * 2f), 1.0f),
                    (0.3f, HDR(coreColor, HDR_CORE), 0.8f),
                    (0.7f, HDR(coreColor, HDR_MID), 0.3f),
                    (1.0f, HDR(coreColor, HDR_SOFT), 0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 3, 6, 0, 0.8f)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f * scale;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 2f));

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sortingOrder = 9;
        }

        // ================================================================
        //  ACCRETION DISK - swirling light being pulled in
        // ================================================================
        private void BuildAccretionDisk()
        {
            var ps = CreateSubSystem("Accretion Disk");
            _diskPS = ps;

            var main = ps.main;
            main.maxParticles = diskParticleCount * 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f * scale, 0.4f * scale);

            // Rainbow colors from all dimensions
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(tearColors[0], HDR_BRIGHT), 0.0f),
                    (0.15f, HDR(tearColors[1], HDR_BRIGHT), 0.8f),
                    (0.35f, HDR(tearColors[2], HDR_MID), 0.9f),
                    (0.55f, HDR(tearColors[3], HDR_MID), 0.8f),
                    (0.75f, HDR(tearColors[4], HDR_SOFT), 0.5f),
                    (0.9f, HDR(coreColor, HDR_BRIGHT), 0.3f),
                    (1.0f, HDR(coreColor, HDR_CORE), 0.0f)
                ),
                MakeGradient(
                    (0.0f, HDR(tearColors[5], HDR_BRIGHT), 0.0f),
                    (0.15f, HDR(tearColors[6], HDR_BRIGHT), 0.8f),
                    (0.35f, HDR(tearColors[7], HDR_MID), 0.9f),
                    (0.55f, HDR(tearColors[0], HDR_MID), 0.8f),
                    (0.75f, HDR(tearColors[1], HDR_SOFT), 0.5f),
                    (0.9f, HDR(coreColor, HDR_BRIGHT), 0.3f),
                    (1.0f, HDR(coreColor, HDR_CORE), 0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = diskParticleCount * 0.5f;

            // Spawn in a ring
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = diskOuterRadius * scale;
            shape.donutRadius = (diskOuterRadius - diskInnerRadius) * 0.5f * scale;
            shape.rotation = new Vector3(90f, 0f, 0f); // Horizontal disk

            // Spiral inward
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(diskRotationSpeed * scale, diskRotationSpeed * 1.5f * scale);
            vel.radial = new ParticleSystem.MinMaxCurve(-1.5f * scale, -2.5f * scale); // Pull toward center

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            // Trails for stream effect
            var trails = ps.trails;
            trails.enabled = true;
            trails.ratio = 0.6f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.3f);
            trails.minVertexDistance = 0.05f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            trails.inheritParticleColor = true;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.trailMaterial = _additiveMat;
            rend.sortingOrder = 5;
        }

        // ================================================================
        //  GLITCH EFFECT - random digital artifacts
        // ================================================================
        private void BuildGlitchEffect()
        {
            var ps = CreateSubSystem("Glitch Effect");
            _glitchPS = ps;

            var main = ps.main;
            main.maxParticles = glitchParticleCount * 2;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f * scale, 15f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f * scale, 0.15f * scale);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(coreColor, HDR_CORE), 1.0f),
                    (0.5f, HDR(new Color(0f, 1f, 1f), HDR_BRIGHT), 0.8f),
                    (1.0f, HDR(new Color(1f, 0f, 1f), HDR_MID), 0.0f)
                ),
                MakeGradient(
                    (0.0f, HDR(coreColor, HDR_CORE), 1.0f),
                    (0.5f, HDR(new Color(1f, 0f, 0.5f), HDR_BRIGHT), 0.8f),
                    (1.0f, HDR(new Color(0f, 0.5f, 1f), HDR_MID), 0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 5, 15, 0, 0.15f)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = coreRadius * 2f * scale;

            // Stretched rectangles for digital look
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 3f;
            rend.velocityScale = 0.1f;
            rend.sortingOrder = 8;
        }

        // ================================================================
        //  RAY BURST - bright rays shooting out periodically
        // ================================================================
        private void BuildRayBurst()
        {
            var ps = CreateSubSystem("Ray Burst");
            _rayBurstPS = ps;

            var main = ps.main;
            main.maxParticles = 50;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f * scale, 15f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f * scale, 0.12f * scale);
            main.startColor = HDR(coreColor, HDR_CORE);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(coreColor, HDR_CORE * 1.5f), 1.0f),
                    (0.2f, HDR(coreColor, HDR_CORE), 0.9f),
                    (0.6f, HDR(new Color(0.9f, 0.95f, 1f), HDR_BRIGHT), 0.5f),
                    (1.0f, HDR(new Color(0.8f, 0.9f, 1f), HDR_SOFT), 0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 8, 16, 0, 1.5f)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f * scale;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 4f;
            rend.velocityScale = 0.2f;
            rend.sortingOrder = 7;

            // Trails
            var trails = ps.trails;
            trails.enabled = true;
            trails.ratio = 0.8f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.15f);
            trails.minVertexDistance = 0.02f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            trails.inheritParticleColor = true;
            rend.trailMaterial = _additiveMat;
        }

        // ================================================================
        //  TEAR MANAGEMENT - spawning & lifecycle
        // ================================================================
        private void UpdateTears()
        {
            // Spawn new tears
            if (_activeTears.Count < maxActiveTears && Time.time >= _nextTearSpawnTime)
            {
                SpawnTear();
                _nextTearSpawnTime = Time.time + Random.Range(0.5f, 2f);
            }

            // Update existing tears
            for (int i = _activeTears.Count - 1; i >= 0; i--)
            {
                var tear = _activeTears[i];
                float now = Time.time;

                switch (tear.phase)
                {
                    case TearPhase.Opening:
                        // Fade in the tear
                        tear.openProgress = Mathf.Clamp01((now - tear.spawnTime) / tearOpenDuration);
                        UpdateTearVisuals(tear);

                        // Once fully open, start the river
                        if (tear.openProgress >= 1f)
                        {
                            tear.phase = TearPhase.RiverStarting;
                            tear.riverStartTime = now;
                            // Enable river emission
                            if (tear.riverPS != null)
                            {
                                var emission = tear.riverPS.emission;
                                emission.enabled = true;
                            }
                        }
                        break;

                    case TearPhase.RiverStarting:
                        // Gradually increase river emission (fade in)
                        tear.riverFadeProgress = Mathf.Clamp01((now - tear.riverStartTime) / riverFadeInDuration);
                        UpdateRiverEmission(tear);

                        if (tear.riverFadeProgress >= 1f)
                        {
                            tear.phase = TearPhase.RiverFlowing;
                        }
                        break;

                    case TearPhase.RiverFlowing:
                        // Check if river has reached the core
                        float riverTravelTime = CalculateRiverLifetime(tear.position);
                        if (now - tear.riverStartTime >= riverTravelTime)
                        {
                            tear.phase = TearPhase.Connected;
                            tear.riverConnectTime = now;
                            tear.waitAfterConnect = Random.Range(minWaitAfterConnect, maxWaitAfterConnect);
                        }
                        break;

                    case TearPhase.Connected:
                        // Wait random interval after connection
                        if (now - tear.riverConnectTime >= tear.waitAfterConnect)
                        {
                            tear.phase = TearPhase.Closing;
                            tear.closeProgress = 0f;
                        }
                        break;

                    case TearPhase.Closing:
                        // Fade out both tear and river
                        float closeStartTime = tear.riverConnectTime + tear.waitAfterConnect;
                        tear.closeProgress = Mathf.Clamp01((now - closeStartTime) / tearCloseDuration);
                        UpdateTearVisuals(tear);
                        UpdateRiverEmission(tear);

                        if (tear.closeProgress >= 1f)
                        {
                            // Destroy tear
                            if (tear.tearPS != null) Destroy(tear.tearPS.gameObject);
                            if (tear.riverPS != null) Destroy(tear.riverPS.gameObject);
                            _activeTears.RemoveAt(i);
                        }
                        break;
                }
            }
        }

        private void SpawnTear()
        {
            // Random position around the white hole
            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y *= 0.3f; // Flatten vertically
            randomDir.Normalize();

            Vector3 pos = transform.position + randomDir * tearSpawnRadius * scale;
            Color col = tearColors[Random.Range(0, tearColors.Length)];

            var tear = new TearInstance
            {
                position = pos,
                color = col,
                spawnTime = Time.time,
                openProgress = 0f,
                closeProgress = 0f,
                phase = TearPhase.Opening,
                riverFadeProgress = 0f
            };

            // Create tear particle effect (mini version of SpaceTear)
            tear.tearPS = CreateTearEffect(pos, col);
            tear.tearPS.transform.localScale = Vector3.zero; // Start invisible

            // Create river but disable emission until tear is open
            tear.riverPS = CreateRiverEffect(pos, col);
            var riverEmission = tear.riverPS.emission;
            riverEmission.enabled = false;

            // Add flow controller to river
            tear.riverController = tear.riverPS.gameObject.AddComponent<RiverFlowController>();
            tear.riverController.Initialize(this, tear.riverPS, riverFlowSpeed * scale, riverWaveAmplitude * 0.3f);

            _activeTears.Add(tear);
        }

        private void UpdateTearVisuals(TearInstance tear)
        {
            float visibility;
            if (tear.phase == TearPhase.Closing)
                visibility = 1f - Mathf.SmoothStep(0f, 1f, tear.closeProgress);
            else
                visibility = Mathf.SmoothStep(0f, 1f, tear.openProgress);

            if (tear.tearPS != null)
            {
                tear.tearPS.transform.localScale = Vector3.one * visibility;

                // Also fade the emission rate for smoother fade
                var emission = tear.tearPS.emission;
                emission.rateOverTime = 20f * visibility;
            }
        }

        private void UpdateRiverEmission(TearInstance tear)
        {
            if (tear.riverPS == null) return;

            var emission = tear.riverPS.emission;
            
            if (tear.phase == TearPhase.Closing)
            {
                // Fade out river emission during closing
                float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, tear.closeProgress);
                emission.rateOverTime = riverParticlesPerTear * fadeOut;
            }
            else
            {
                // Fade in river emission
                float fadeIn = Mathf.SmoothStep(0f, 1f, tear.riverFadeProgress);
                emission.rateOverTime = riverParticlesPerTear * fadeIn;
            }
        }

        private ParticleSystem CreateTearEffect(Vector3 worldPos, Color color)
        {
            var go = new GameObject("Dimensional Tear");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 30;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f * scale, 0.5f * scale);
            main.startColor = HDR(color, HDR_BRIGHT);

            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            col2.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(Color.white, HDR_CORE), 0.0f),
                    (0.15f, HDR(Color.white, HDR_CORE), 1.0f),
                    (0.4f, HDR(color, HDR_BRIGHT), 0.9f),
                    (0.7f, HDR(color, HDR_MID), 0.5f),
                    (1.0f, HDR(color, HDR_SOFT), 0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 20;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f * scale;

            // Swirl effect
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(2f, 4f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.3f * scale);
            noise.frequency = 3f;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = _additiveMat;
            rend.sortingOrder = 3;

            return ps;
        }

        private ParticleSystem CreateRiverEffect(Vector3 startPos, Color color)
        {
            var go = new GameObject("Light River");
            go.transform.SetParent(transform, false);
            go.transform.position = startPos;

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = riverParticlesPerTear * 2;
            main.startLifetime = CalculateRiverLifetime(startPos);
            main.startSpeed = 0.1f; // Very low initial speed, controller will handle velocity
            main.startSize = new ParticleSystem.MinMaxCurve(riverWidth * 0.8f * scale, riverWidth * scale);
            main.startColor = HDR(color, HDR_RIVER);

            // Tight emission point for unified stream
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f * scale; // Very small spawn area for tight stream

            // Fade in, bright middle, fade out as approaches core
            // Use lower HDR values to prevent additive white-out at close range
            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            col2.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(color, HDR_RIVER), 0.0f),
                    (0.05f, HDR(color, HDR_RIVER_BRIGHT), 0.7f),
                    (0.3f, HDR(color, HDR_RIVER_BRIGHT), 0.8f),
                    (0.7f, HDR(Color.Lerp(color, Color.white, 0.3f), HDR_RIVER_BRIGHT), 0.8f),
                    (0.9f, HDR(Color.white, HDR_MID), 0.5f),
                    (1.0f, HDR(Color.white, HDR_BRIGHT), 0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = riverParticlesPerTear;

            // Disable built-in velocity - we'll use the controller for precise direction
            var vel = ps.velocityOverLifetime;
            vel.enabled = false;

            // Minimal noise for subtle organic movement without spreading the stream
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(riverWaveAmplitude * 0.3f * scale);
            noise.frequency = riverWaveFrequency;
            noise.scrollSpeed = 0.5f;
            noise.octaveCount = 1;
            noise.damping = true; // Dampen noise over lifetime

            // Size: consistent through most of journey, shrink near core
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.8f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.8f, 1f),
                new Keyframe(1f, 0.4f)
            ));

            // Longer trails for continuous ribbon/river look
            var trails = ps.trails;
            trails.enabled = true;
            trails.ratio = 0.8f; // Most particles have trails
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.3f);
            trails.minVertexDistance = 0.02f;
            trails.worldSpace = true;
            trails.dieWithParticles = false; // Trails persist slightly after particle dies
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.7f, 0.8f),
                new Keyframe(1f, 0.2f)
            ));
            trails.inheritParticleColor = true;
            // Lower trail alpha to prevent white-out from additive stacking
            trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, Color.white, 0.6f),
                    (0.5f, Color.white, 0.4f),
                    (1.0f, Color.white, 0.1f)
                )
            );

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = _additiveMat;
            rend.trailMaterial = _additiveMat;
            rend.sortingOrder = 4;

            return ps;
        }

        private float CalculateRiverLifetime(Vector3 startPos)
        {
            float dist = Vector3.Distance(startPos, transform.position);
            return dist / (riverFlowSpeed * scale) * 1.2f; // Slight buffer
        }

        // ================================================================
        //  FIXED LIGHT SOURCES - manual river origins
        // ================================================================
        private void CreateFixedRiver(Transform source)
        {
            Color col = tearColors[Random.Range(0, tearColors.Length)];

            // Create a persistent tear at the source
            var tearPS = CreateTearEffect(source.position, col);

            // Create river
            var riverGo = new GameObject("Fixed Light River");
            riverGo.transform.SetParent(source, false);

            var ps = riverGo.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = riverParticlesPerTear * 2;
            main.startSpeed = 0.1f; // Low initial speed, controller handles velocity
            main.startSize = new ParticleSystem.MinMaxCurve(riverWidth * 0.8f * scale, riverWidth * scale);
            main.startColor = HDR(col, HDR_RIVER);

            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            col2.color = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, HDR(col, HDR_RIVER), 0.0f),
                    (0.05f, HDR(col, HDR_RIVER_BRIGHT), 0.7f),
                    (0.3f, HDR(col, HDR_RIVER_BRIGHT), 0.8f),
                    (0.7f, HDR(Color.Lerp(col, Color.white, 0.3f), HDR_RIVER_BRIGHT), 0.8f),
                    (0.9f, HDR(Color.white, HDR_MID), 0.5f),
                    (1.0f, HDR(Color.white, HDR_BRIGHT), 0.0f)
                )
            );

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = riverParticlesPerTear;

            // Tight spawn point for unified stream
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f * scale;

            // Disable built-in velocity - helper handles direction
            var velocityModule = ps.velocityOverLifetime;
            velocityModule.enabled = false;

            // Attach a helper to update velocity direction
            var helper = riverGo.AddComponent<RiverFlowHelper>();
            helper.Initialize(this, ps, riverFlowSpeed * scale, riverWaveAmplitude * 0.3f);

            // Minimal noise for subtle organic movement
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(riverWaveAmplitude * 0.3f * scale);
            noise.frequency = riverWaveFrequency;
            noise.damping = true;

            // Size: consistent through journey, shrink near core
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.8f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.8f, 1f),
                new Keyframe(1f, 0.4f)
            ));

            // Trails for continuous ribbon effect
            var trails = ps.trails;
            trails.enabled = true;
            trails.ratio = 0.8f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.3f);
            trails.minVertexDistance = 0.02f;
            trails.worldSpace = true;
            trails.dieWithParticles = false;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.7f, 0.8f),
                new Keyframe(1f, 0.2f)
            ));
            trails.inheritParticleColor = true;
            trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(
                MakeGradient(
                    (0.0f, Color.white, 0.6f),
                    (0.5f, Color.white, 0.4f),
                    (1.0f, Color.white, 0.1f)
                )
            );

            var rend = riverGo.GetComponent<ParticleSystemRenderer>();
            rend.material = _additiveMat;
            rend.trailMaterial = _additiveMat;
            rend.sortingOrder = 4;

            // Dynamic lifetime based on distance
            StartCoroutine(UpdateFixedRiverLifetime(ps, source));
        }

        private System.Collections.IEnumerator UpdateFixedRiverLifetime(ParticleSystem ps, Transform source)
        {
            while (ps != null && source != null)
            {
                var main = ps.main;
                main.startLifetime = CalculateRiverLifetime(source.position);
                yield return new WaitForSeconds(0.5f);
            }
        }

        // ================================================================
        //  GLITCH PULSE
        // ================================================================
        private void UpdateGlitchPulse()
        {
            if (_glitchPS == null) return;

            // Random intensity spikes
            float glitchPulse = Mathf.PerlinNoise(Time.time * glitchFrequency, 0f);
            if (glitchPulse > 0.7f)
            {
                var emission = _glitchPS.emission;
                emission.rateOverTime = glitchParticleCount * glitchIntensity * (glitchPulse - 0.7f) * 3f;
            }
            else
            {
                var emission = _glitchPS.emission;
                emission.rateOverTime = 0;
            }
        }

        // ================================================================
        //  GIZMOS
        // ================================================================
        private void OnDrawGizmos()
        {
            // Core
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, coreRadius * scale);

            // Accretion disk
            Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
            DrawDiskGizmo(diskInnerRadius * scale);
            DrawDiskGizmo(diskOuterRadius * scale);

            // Tear spawn radius
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, tearSpawnRadius * scale);

            // Fixed light sources
            if (fixedLightSources != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var src in fixedLightSources)
                {
                    if (src != null)
                    {
                        Gizmos.DrawWireSphere(src.position, 0.3f);
                        Gizmos.DrawLine(src.position, transform.position);
                    }
                }
            }
        }

        private void DrawDiskGizmo(float radius)
        {
            int segments = 32;
            Vector3 prev = transform.position + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }

    /// <summary>
    /// Controls river particles to flow smoothly toward the white hole core.
    /// Creates a unified stream effect by ensuring all particles consistently move toward the target.
    /// </summary>
    public class RiverFlowController : MonoBehaviour
    {
        private WhiteHole _whiteHole;
        private ParticleSystem _ps;
        private float _baseSpeed;
        private float _waveAmp;
        private ParticleSystem.Particle[] _particles;
        private Vector3 _flowDirection;

        public void Initialize(WhiteHole whiteHole, ParticleSystem ps, float speed, float waveAmp)
        {
            _whiteHole = whiteHole;
            _ps = ps;
            _baseSpeed = speed;
            _waveAmp = waveAmp;

            // Pre-calculate main flow direction
            if (_whiteHole != null)
            {
                _flowDirection = (_whiteHole.transform.position - transform.position).normalized;
            }
        }

        private void LateUpdate()
        {
            if (_whiteHole == null || _ps == null) return;

            // Allocate particle array
            if (_particles == null || _particles.Length < _ps.main.maxParticles)
                _particles = new ParticleSystem.Particle[_ps.main.maxParticles];

            int count = _ps.GetParticles(_particles);
            if (count == 0) return;

            Vector3 corePos = _whiteHole.transform.position;
            float time = Time.time;

            for (int i = 0; i < count; i++)
            {
                Vector3 particlePos = _particles[i].position;
                Vector3 toCore = (corePos - particlePos).normalized;
                float distToCore = Vector3.Distance(particlePos, corePos);

                // Speed increases slightly as particles get closer to core (gravitational pull effect)
                float speedMultiplier = 1f + (1f - Mathf.Clamp01(distToCore / 15f)) * 0.5f;
                float speed = _baseSpeed * speedMultiplier;

                // Very subtle wave motion - keeps stream unified
                // Use particle's lifetime progress for consistent wave phase along the stream
                float lifeProgress = 1f - (_particles[i].remainingLifetime / _particles[i].startLifetime);
                float wavePhase = lifeProgress * Mathf.PI * 4f + time * 0.5f;
                float wave = Mathf.Sin(wavePhase) * _waveAmp * (1f - lifeProgress * 0.5f); // Reduce wave near core

                // Calculate perpendicular direction for wave
                Vector3 perpendicular = Vector3.Cross(toCore, Vector3.up);
                if (perpendicular.sqrMagnitude < 0.01f)
                    perpendicular = Vector3.Cross(toCore, Vector3.forward);
                perpendicular.Normalize();

                // Set velocity: main direction toward core + subtle wave
                _particles[i].velocity = toCore * speed + perpendicular * wave;
            }

            _ps.SetParticles(_particles, count);
        }
    }

    /// <summary>
    /// Helper component to update river flow direction toward the white hole.
    /// Used for fixed light sources.
    /// </summary>
    public class RiverFlowHelper : MonoBehaviour
    {
        private WhiteHole _whiteHole;
        private ParticleSystem _ps;
        private float _speed;
        private float _waveAmp;
        private ParticleSystem.Particle[] _particles;

        public void Initialize(WhiteHole whiteHole, ParticleSystem ps, float speed, float waveAmp)
        {
            _whiteHole = whiteHole;
            _ps = ps;
            _speed = speed;
            _waveAmp = waveAmp;
        }

        private void LateUpdate()
        {
            if (_whiteHole == null || _ps == null) return;

            // Allocate particle array
            if (_particles == null || _particles.Length < _ps.main.maxParticles)
                _particles = new ParticleSystem.Particle[_ps.main.maxParticles];

            int count = _ps.GetParticles(_particles);
            if (count == 0) return;

            Vector3 corePos = _whiteHole.transform.position;
            float time = Time.time;

            for (int i = 0; i < count; i++)
            {
                Vector3 particlePos = _particles[i].position;
                Vector3 toCore = (corePos - particlePos).normalized;
                float distToCore = Vector3.Distance(particlePos, corePos);

                // Speed increases as particles approach core
                float speedMultiplier = 1f + (1f - Mathf.Clamp01(distToCore / 15f)) * 0.5f;
                float speed = _speed * speedMultiplier;

                // Subtle wave motion
                float lifeProgress = 1f - (_particles[i].remainingLifetime / _particles[i].startLifetime);
                float wavePhase = lifeProgress * Mathf.PI * 4f + time * 0.5f;
                float wave = Mathf.Sin(wavePhase) * _waveAmp * 0.3f * (1f - lifeProgress * 0.5f);

                Vector3 perpendicular = Vector3.Cross(toCore, Vector3.up);
                if (perpendicular.sqrMagnitude < 0.01f)
                    perpendicular = Vector3.Cross(toCore, Vector3.forward);
                perpendicular.Normalize();

                _particles[i].velocity = toCore * speed + perpendicular * wave;
            }

            _ps.SetParticles(_particles, count);
        }
    }
}
