using UnityEngine;

namespace OutOfPhase.Dimension
{
    /// <summary>
    /// Creates the illusion of distant figures entering and exiting through space tears.
    /// Each figure instance manages itself independently with its own random timer.
    /// Uses the SpaceTear component for realistic dimensional rift effects.
    /// Attach to a dimension-specific GameObject so it only appears in one dimension.
    /// </summary>
    public class DistantFigureEffect : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Number of figure instances to create")]
        [SerializeField] private int figureCount = 25;
        [Tooltip("Minimum time before a figure spawns")]
        [SerializeField] private float minSpawnDelay = 0.5f;
        [Tooltip("Maximum time before a figure spawns")]
        [SerializeField] private float maxSpawnDelay = 5f;

        [Header("Animation Timing")]
        [Tooltip("Duration for tear opening")]
        [SerializeField] private float minTearOpenDuration = 0.3f;
        [SerializeField] private float maxTearOpenDuration = 0.6f;
        [Tooltip("Duration for figure emerging from tear")]
        [SerializeField] private float minEmergeDuration = 0.4f;
        [SerializeField] private float maxEmergeDuration = 0.8f;
        [Tooltip("Duration for walking between tears")]
        [SerializeField] private float minWalkDuration = 1.5f;
        [SerializeField] private float maxWalkDuration = 4f;
        [Tooltip("Duration for figure entering exit tear")]
        [SerializeField] private float minEnterExitDuration = 0.4f;
        [SerializeField] private float maxEnterExitDuration = 0.8f;

        [Header("Walking Settings")]
        [Tooltip("Minimum distance the figure walks between tears")]
        [SerializeField] private float minWalkDistance = 5f;
        [Tooltip("Maximum distance the figure walks between tears")]
        [SerializeField] private float maxWalkDistance = 20f;

        [Header("Distance & Position")]
        [Tooltip("Minimum distance from camera")]
        [SerializeField] private float minDistance = 80f;
        [Tooltip("Maximum distance from camera")]
        [SerializeField] private float maxDistance = 200f;
        [Tooltip("Minimum angle from camera forward (degrees)")]
        [SerializeField] private float minAngle = -80f;
        [Tooltip("Maximum angle from camera forward (degrees)")]
        [SerializeField] private float maxAngle = 80f;

        [Header("Tear Appearance")]
        [Tooltip("Base scale of the space tear")]
        [SerializeField] private float tearScale = 0.5f;

        [Header("Figure Appearance")]
        [Tooltip("Base scale of the figure silhouette")]
        [SerializeField] private float figureScale = 3f;
        [Tooltip("Figure silhouette color")]
        [SerializeField] private Color figureColor = new Color(0.02f, 0.01f, 0.05f, 0.9f);
        [Tooltip("Subtle glow around the figure")]
        [SerializeField] private Color figureGlowColor = new Color(0.4f, 0.1f, 0.6f, 0.3f);

        [Header("Particle Settings")]
        [SerializeField] private int glowParticleCount = 20;

        // Runtime state
        private Camera _camera;
        private Material _additiveMat;
        private Material _alphaMat;
        private FigureController[] _figures;

        private const float HDR_GLOW = 2f;

        private enum AnimationPhase
        {
            Waiting,
            EntryTearOpening,
            Emerging,
            Walking,
            ExitTearOpening,
            EnteringExit
        }

        private class FigureController
        {
            public DistantFigureEffect owner;
            public int index;
            
            // GameObjects
            public GameObject root;
            public GameObject entryTearRoot;
            public GameObject exitTearRoot;
            public GameObject figureRoot;
            
            // SpaceTear components for the actual tears
            public SpaceTear entryTear;
            public SpaceTear exitTear;
            
            // Figure particle systems
            public ParticleSystem figurePS;
            public ParticleSystem figureGlowPS;
            
            // State
            public AnimationPhase phase = AnimationPhase.Waiting;
            public float timer;
            public float targetTime;
            
            // Animation data (randomized each cycle)
            public float tearOpenDuration;
            public float emergeDuration;
            public float walkDuration;
            public float enterExitDuration;
            public float distance;
            public Vector3 entryPosition;
            public Vector3 exitPosition;
            
            // For tear scale animation
            public float entryTearTargetScale;
            public float exitTearTargetScale;

            public void Initialize()
            {
                // Start with random delay so they don't all spawn at once
                phase = AnimationPhase.Waiting;
                timer = 0f;
                targetTime = Random.Range(owner.minSpawnDelay, owner.maxSpawnDelay);
                
                // Stagger initial spawns
                targetTime *= (float)index / owner.figureCount;
            }

            public void Update(Camera cam)
            {
                if (cam == null) return;
                
                timer += Time.deltaTime;
                Vector3 camPos = cam.transform.position;

                switch (phase)
                {
                    case AnimationPhase.Waiting:
                        if (timer >= targetTime)
                        {
                            StartNewCycle(cam);
                        }
                        break;

                    case AnimationPhase.EntryTearOpening:
                        // Scale tear from 0 to full size
                        float tearT = Mathf.Clamp01(timer / tearOpenDuration);
                        float easeT = EaseOutQuad(tearT);
                        entryTearRoot.transform.localScale = Vector3.one * entryTearTargetScale * easeT;
                        
                        if (timer >= tearOpenDuration)
                        {
                            phase = AnimationPhase.Emerging;
                            timer = 0f;
                            figureRoot.SetActive(true);
                            if (figurePS != null) figurePS.Play();
                            if (figureGlowPS != null) figureGlowPS.Play();
                        }
                        break;

                    case AnimationPhase.Emerging:
                        float emergeT = EaseOutQuad(Mathf.Clamp01(timer / emergeDuration));
                        SetFigureEmission(emergeT);
                        figureRoot.transform.position = entryPosition;
                        FaceCameraHorizontal(figureRoot.transform, camPos);
                        
                        if (timer >= emergeDuration)
                        {
                            phase = AnimationPhase.Walking;
                            timer = 0f;
                        }
                        break;

                    case AnimationPhase.Walking:
                        float walkT = EaseInOutQuad(Mathf.Clamp01(timer / walkDuration));
                        Vector3 walkPos = Vector3.Lerp(entryPosition, exitPosition, walkT);
                        figureRoot.transform.position = walkPos;
                        FaceCameraHorizontal(figureRoot.transform, camPos);
                        SetFigureEmission(1f);
                        
                        // Shrink entry tear as figure walks away
                        float entryFade = 1f - Mathf.Clamp01(walkT / 0.5f);
                        entryTearRoot.transform.localScale = Vector3.one * entryTearTargetScale * entryFade;
                        if (entryFade <= 0f && entryTearRoot.activeSelf)
                        {
                            entryTearRoot.SetActive(false);
                        }
                        
                        // Open exit tear at 50% through walk
                        if (walkT > 0.5f && !exitTearRoot.activeSelf)
                        {
                            exitTearRoot.SetActive(true);
                            exitTearRoot.transform.localScale = Vector3.zero;
                        }
                        if (exitTearRoot.activeSelf)
                        {
                            float exitTearT = (walkT - 0.5f) / 0.5f;
                            exitTearRoot.transform.localScale = Vector3.one * exitTearTargetScale * EaseOutQuad(exitTearT);
                        }
                        
                        if (timer >= walkDuration)
                        {
                            phase = AnimationPhase.ExitTearOpening;
                            timer = 0f;
                        }
                        break;

                    case AnimationPhase.ExitTearOpening:
                        // Brief pause at full size
                        exitTearRoot.transform.localScale = Vector3.one * exitTearTargetScale;
                        figureRoot.transform.position = exitPosition;
                        FaceCameraHorizontal(figureRoot.transform, camPos);
                        if (timer >= 0.2f)
                        {
                            phase = AnimationPhase.EnteringExit;
                            timer = 0f;
                        }
                        break;

                    case AnimationPhase.EnteringExit:
                        float exitT = Mathf.Clamp01(timer / enterExitDuration);
                        SetFigureEmission(1f - EaseInQuad(exitT));
                        
                        // Shrink exit tear as figure enters
                        float exitFade = 1f - EaseInQuad(exitT);
                        exitTearRoot.transform.localScale = Vector3.one * exitTearTargetScale * Mathf.Lerp(1f, 0.3f, exitT);
                        
                        figureRoot.transform.position = exitPosition;
                        FaceCameraHorizontal(figureRoot.transform, camPos);
                        
                        if (timer >= enterExitDuration)
                        {
                            EndCycle();
                        }
                        break;
                }
            }

            private void StartNewCycle(Camera cam)
            {
                Vector3 camPos = cam.transform.position;
                
                // Randomize timing
                tearOpenDuration = Random.Range(owner.minTearOpenDuration, owner.maxTearOpenDuration);
                emergeDuration = Random.Range(owner.minEmergeDuration, owner.maxEmergeDuration);
                walkDuration = Random.Range(owner.minWalkDuration, owner.maxWalkDuration);
                enterExitDuration = Random.Range(owner.minEnterExitDuration, owner.maxEnterExitDuration);
                
                // Random position
                float angle = Random.Range(owner.minAngle, owner.maxAngle);
                distance = Random.Range(owner.minDistance, owner.maxDistance);
                
                Vector3 forward = cam.transform.forward;
                forward.y = 0;
                forward.Normalize();
                
                Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
                Vector3 direction = rotation * forward;
                direction.y = 0;
                direction.Normalize();
                
                // Entry position
                entryPosition = camPos + direction * distance;
                entryPosition.y = camPos.y;
                
                // Exit position - offset laterally
                float walkDistance = Random.Range(owner.minWalkDistance, owner.maxWalkDistance);
                float walkDir = Random.value > 0.5f ? 1f : -1f;
                Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
                exitPosition = entryPosition + perpendicular * walkDistance * walkDir;
                exitPosition.y = camPos.y;
                
                // Scale based on distance (further = larger to maintain apparent size)
                float distanceScale = distance / 100f;
                entryTearTargetScale = owner.tearScale * distanceScale;
                exitTearTargetScale = owner.tearScale * distanceScale;
                
                // Position and scale root
                root.transform.localScale = Vector3.one;
                
                // Position tears
                entryTearRoot.transform.position = entryPosition;
                FaceCameraHorizontal(entryTearRoot.transform, camPos);
                entryTearRoot.transform.localScale = Vector3.zero; // Start at zero, animate to full
                
                exitTearRoot.transform.position = exitPosition;
                FaceCameraHorizontal(exitTearRoot.transform, camPos);
                exitTearRoot.transform.localScale = Vector3.zero;
                
                // Scale figure based on distance
                figureRoot.transform.position = entryPosition;
                figureRoot.transform.localScale = Vector3.one * distanceScale;
                FaceCameraHorizontal(figureRoot.transform, camPos);
                
                // Activate entry tear only
                root.SetActive(true);
                entryTearRoot.SetActive(true);
                exitTearRoot.SetActive(false);
                figureRoot.SetActive(false);
                
                phase = AnimationPhase.EntryTearOpening;
                timer = 0f;
            }

            private void EndCycle()
            {
                // Stop figure particles
                if (figurePS != null) figurePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (figureGlowPS != null) figureGlowPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                
                // Hide everything
                entryTearRoot.SetActive(false);
                exitTearRoot.SetActive(false);
                figureRoot.SetActive(false);
                root.SetActive(false);
                
                // Wait for next cycle
                phase = AnimationPhase.Waiting;
                timer = 0f;
                targetTime = Random.Range(owner.minSpawnDelay, owner.maxSpawnDelay);
            }

            private void SetFigureEmission(float intensity)
            {
                if (figurePS == null) return;
                var emission = figurePS.emission;
                emission.rateOverTime = intensity * 30f;
                var main = figurePS.main;
                var col = owner.figureColor;
                col.a *= intensity;
                main.startColor = col;
            }

            private void FaceCameraHorizontal(Transform t, Vector3 camPos)
            {
                Vector3 toCamera = camPos - t.position;
                toCamera.y = 0;
                if (toCamera.sqrMagnitude > 0.01f)
                    t.rotation = Quaternion.LookRotation(toCamera);
            }

            private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
            private float EaseInQuad(float t) => t * t;
            private float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }

        private void Awake()
        {
            _additiveMat = CreateMaterial(additive: true);
            _alphaMat = CreateMaterial(additive: false);
        }

        private void Start()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var camObj = GameObject.Find("PlayerCamera");
                if (camObj != null) _camera = camObj.GetComponent<Camera>();
            }

            // Create all figure instances
            _figures = new FigureController[figureCount];
            for (int i = 0; i < figureCount; i++)
            {
                _figures[i] = CreateFigure(i);
                _figures[i].Initialize();
            }
        }

        private void Update()
        {
            if (_camera == null || _figures == null) return;

            for (int i = 0; i < _figures.Length; i++)
            {
                _figures[i].Update(_camera);
            }
        }

        private FigureController CreateFigure(int index)
        {
            var fig = new FigureController();
            fig.owner = this;
            fig.index = index;

            fig.root = new GameObject($"DistantFigure_{index}");
            fig.root.transform.SetParent(transform);
            fig.root.SetActive(false);

            // Entry tear - uses SpaceTear component
            fig.entryTearRoot = new GameObject("EntryTear");
            fig.entryTearRoot.transform.SetParent(fig.root.transform);
            fig.entryTear = fig.entryTearRoot.AddComponent<SpaceTear>();

            // Exit tear - uses SpaceTear component
            fig.exitTearRoot = new GameObject("ExitTear");
            fig.exitTearRoot.transform.SetParent(fig.root.transform);
            fig.exitTear = fig.exitTearRoot.AddComponent<SpaceTear>();

            // Figure
            fig.figureRoot = new GameObject("Figure");
            fig.figureRoot.transform.SetParent(fig.root.transform);
            fig.figurePS = CreateFigureSilhouette(fig.figureRoot.transform);
            fig.figureGlowPS = CreateFigureGlow(fig.figureRoot.transform);

            return fig;
        }

        private ParticleSystem CreateFigureSilhouette(Transform parent)
        {
            var figureObj = new GameObject("FigureSilhouette");
            figureObj.transform.SetParent(parent);
            figureObj.transform.localPosition = Vector3.zero;
            figureObj.transform.localScale = Vector3.one * figureScale;

            var ps = figureObj.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;
            main.startColor = figureColor;

            var emission = ps.emission;
            emission.rateOverTime = 30f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.3f, 1f, 0.1f);
            shape.position = new Vector3(0f, 0.5f, 0f);

            var velocityOverLife = ps.velocityOverLifetime;
            velocityOverLife.enabled = true;
            velocityOverLife.y = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.8f);
            sizeCurve.AddKey(0.5f, 1f);
            sizeCurve.AddKey(1f, 0.3f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLife.color = fadeGrad;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 3f;

            var rend = figureObj.GetComponent<ParticleSystemRenderer>();
            rend.material = _alphaMat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sortingOrder = 99;

            return ps;
        }

        private ParticleSystem CreateFigureGlow(Transform parent)
        {
            var glowObj = new GameObject("FigureGlow");
            glowObj.transform.SetParent(parent);
            glowObj.transform.localPosition = Vector3.zero;
            glowObj.transform.localScale = Vector3.one * figureScale * 1.2f;

            var ps = glowObj.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.maxParticles = glowParticleCount * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;
            main.startColor = figureGlowColor * HDR_GLOW;

            var emission = ps.emission;
            emission.rateOverTime = glowParticleCount;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.4f, 1.2f, 0.2f);
            shape.position = new Vector3(0f, 0.5f, 0f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.3f);
            sizeCurve.AddKey(0.5f, 1f);
            sizeCurve.AddKey(1f, 0.1f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.3f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLife.color = fadeGrad;

            var rend = glowObj.GetComponent<ParticleSystemRenderer>();
            rend.material = _additiveMat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sortingOrder = 98;

            return ps;
        }

        private Material CreateMaterial(bool additive)
        {
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

            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            var mat = new Material(shader);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4000;

            return mat;
        }

        private void OnDestroy()
        {
            if (_additiveMat != null) Destroy(_additiveMat);
            if (_alphaMat != null) Destroy(_alphaMat);
        }
    }
}
