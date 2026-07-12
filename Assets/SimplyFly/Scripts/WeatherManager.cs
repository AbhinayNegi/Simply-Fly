using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    public enum WeatherPreset
    {
        Clear,
        FewClouds,
        Overcast,
        Fog,
        LightRain,
        Sunset
    }

    /// <summary>
    /// Visual atmosphere only. Wind remains owned by WindController and aircraft
    /// physics continue to consume that controller exactly as before.
    /// </summary>
    public sealed class WeatherManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Light sun;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private WindController wind;
        [SerializeField] private AirportLocationManager airports;

        [Header("Weather")]
        [SerializeField] private WeatherPreset activePreset = WeatherPreset.Clear;
        [Min(0.1f)] [SerializeField] private float transitionSpeed = 0.55f;
        [SerializeField] private bool applyPresetWindDefaults = true;
        [SerializeField] private bool showDebugReadout;
        [SerializeField] private Key debugToggleKey = Key.F11;

        [Header("Clouds - lightweight camera local particles")]
        [Range(8, 64)] [SerializeField] private int maximumCloudParticles = 28;
        [Min(100f)] [SerializeField] private float cloudLayerHeight = 320f;
        [Min(300f)] [SerializeField] private float cloudArea = 2400f;

        [Header("Rain")]
        [Range(300, 3000)] [SerializeField] private int maximumRainParticles = 1800;
        [Range(0f, 1f)] [SerializeField] private float rainAudioVolume = 0.42f;
        [Min(0.1f)] [SerializeField] private float rainFadeSpeed = 2.5f;

        [Header("Wet surfaces")]
        [Range(0f, 1f)] [SerializeField] private float wetDarkening = 0.22f;
        [Range(0f, 1f)] [SerializeField] private float wetSmoothness = 0.72f;

        public WeatherPreset ActivePreset => activePreset;
        public float VisibilityMeters { get; private set; } = 10000f;
        public float CloudAmount { get; private set; }
        public float RainIntensity { get; private set; }
        public float TimeOfDayHours { get; private set; } = 12f;
        public string PresetDisplayName => FormatPreset(activePreset);

        private WeatherProfile current;
        private WeatherProfile target;
        private ParticleSystem cloudSystem;
        private ParticleSystem rainSystem;
        private AudioSource rainAudio;
        private Material cloudMaterial;
        private Material rainMaterial;
        private Material runtimeSkybox;
        private Color defaultCameraColor;
        private readonly List<SurfaceState> wetSurfaces = new();
        private readonly List<LightState> airportLights = new();
        private GUIStyle debugStyle;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private void Awake()
        {
            ResolveReferences();
            CaptureSceneState();
            BuildEffects();
            current = BuildProfile(activePreset);
            target = current;
            ApplyProfile(current, true);
        }

        private void Start()
        {
            // Re-scan after airport roots have initialized.
            CaptureWetSurfaces();
            CaptureAirportLights();
            if (airports != null) airports.AirportChanged += HandleAirportChanged;
        }

        private void OnDestroy()
        {
            if (airports != null) airports.AirportChanged -= HandleAirportChanged;
        }

        private void Update()
        {
            if (Keyboard.current != null &&
                Keyboard.current[debugToggleKey].wasPressedThisFrame)
                showDebugReadout = !showDebugReadout;

            float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
            current = WeatherProfile.Lerp(current, target, blend);
            ApplyProfile(current, false);
            UpdateEffectAnchors();
        }

        public void SetPreset(WeatherPreset preset, bool instant = false)
        {
            activePreset = preset;
            target = BuildProfile(preset);
            if (instant) current = target;
            if (applyPresetWindDefaults && wind != null)
            {
                wind.SetWindEnabled(true);
                wind.SetWind(target.windFromHeading, target.windSpeedKnots);
            }
            if (instant) ApplyProfile(current, true);
        }

        public void CyclePreset()
        {
            int count = Enum.GetValues(typeof(WeatherPreset)).Length;
            SetPreset((WeatherPreset)(((int)activePreset + 1) % count));
        }

        private void ResolveReferences()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (wind == null) wind = FindAnyObjectByType<WindController>();
            if (airports == null) airports = FindAnyObjectByType<AirportLocationManager>();
            if (sun == null)
            {
                foreach (Light candidate in FindObjectsByType<Light>())
                {
                    if (candidate.type != LightType.Directional) continue;
                    sun = candidate;
                    break;
                }
            }
        }

        private void CaptureSceneState()
        {
            defaultCameraColor = targetCamera == null
                ? new Color(0.55f, 0.72f, 0.88f)
                : targetCamera.backgroundColor;
            if (RenderSettings.skybox != null)
            {
                runtimeSkybox = new Material(RenderSettings.skybox)
                {
                    name = RenderSettings.skybox.name + " (Weather Runtime)"
                };
                RenderSettings.skybox = runtimeSkybox;
            }
            CaptureWetSurfaces();
            CaptureAirportLights();
        }

        private void HandleAirportChanged()
        {
            CaptureWetSurfaces();
            CaptureAirportLights();
        }

        private void CaptureWetSurfaces()
        {
            wetSurfaces.Clear();
            foreach (Renderer renderer in FindObjectsByType<Renderer>())
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                string lower = renderer.name.ToLowerInvariant();
                if (!lower.Contains("runway") && !lower.Contains("taxi") &&
                    !lower.Contains("apron") && !lower.Contains("road")) continue;
                Material material = renderer.sharedMaterial;
                Color color = material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : material.HasProperty(ColorId)
                        ? material.GetColor(ColorId)
                        : Color.white;
                wetSurfaces.Add(new SurfaceState(renderer, color));
            }
        }

        private void CaptureAirportLights()
        {
            airportLights.Clear();
            foreach (Light light in FindObjectsByType<Light>())
            {
                if (light == null || light == sun) continue;
                string lower = light.name.ToLowerInvariant();
                if (!lower.Contains("runway") && !lower.Contains("approach") &&
                    !lower.Contains("airport") && !lower.Contains("edge")) continue;
                airportLights.Add(new LightState(light, Mathf.Max(0.2f, light.intensity)));
            }
        }

        private void BuildEffects()
        {
            GameObject effectsRoot = new("Weather Effects");
            effectsRoot.transform.SetParent(transform, false);

            cloudSystem = CreateParticleSystem("Cloud Layer", effectsRoot.transform);
            ParticleSystem.MainModule cloudMain = cloudSystem.main;
            cloudMain.loop = true;
            cloudMain.startLifetime = new ParticleSystem.MinMaxCurve(26f, 42f);
            cloudMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            cloudMain.startSize = new ParticleSystem.MinMaxCurve(220f, 540f);
            cloudMain.maxParticles = maximumCloudParticles;
            cloudMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.ShapeModule cloudShape = cloudSystem.shape;
            cloudShape.enabled = true;
            cloudShape.shapeType = ParticleSystemShapeType.Box;
            cloudShape.scale = new Vector3(cloudArea, 115f, cloudArea);
            cloudMaterial = CreateTransparentParticleMaterial(
                "Weather Cloud Material", CreateCloudTexture());
            ParticleSystemRenderer cloudRenderer =
                cloudSystem.GetComponent<ParticleSystemRenderer>();
            cloudRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            cloudRenderer.sortMode = ParticleSystemSortMode.Distance;
            cloudRenderer.material = cloudMaterial;

            rainSystem = CreateParticleSystem("Local Rain", effectsRoot.transform);
            ParticleSystem.MainModule rainMain = rainSystem.main;
            rainMain.loop = true;
            rainMain.startLifetime = 0.82f;
            rainMain.startSpeed = 0f;
            rainMain.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
            rainMain.maxParticles = maximumRainParticles;
            rainMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            rainMain.gravityModifier = 0f;
            ParticleSystem.ShapeModule rainShape = rainSystem.shape;
            rainShape.enabled = true;
            rainShape.shapeType = ParticleSystemShapeType.Box;
            rainShape.scale = new Vector3(44f, 3f, 44f);
            ParticleSystem.VelocityOverLifetimeModule velocity = rainSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = -58f;
            velocity.x = 1.5f;
            ParticleSystemRenderer rainRenderer = rainSystem.GetComponent<ParticleSystemRenderer>();
            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.lengthScale = 1.35f;
            rainRenderer.velocityScale = 0.035f;
            rainMaterial = CreateTransparentParticleMaterial(
                "Weather Rain Material", CreateRainTexture());
            rainRenderer.material = rainMaterial;

            rainAudio = effectsRoot.AddComponent<AudioSource>();
            rainAudio.loop = true;
            rainAudio.playOnAwake = true;
            rainAudio.spatialBlend = 0f;
            rainAudio.volume = 0f;
            rainAudio.clip = CreateRainLoop();
            rainAudio.Play();
        }

        private static ParticleSystem CreateParticleSystem(string name, Transform parent)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            system.Play();
            return system;
        }

        private static Material CreateTransparentParticleMaterial(
            string name,
            Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                            Shader.Find("Particles/Standard Unlit") ??
                            Shader.Find("Unlit/Transparent");
            Material material = new(shader) { name = name };
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetOverrideTag("Queue", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            SetMaterialColor(material, Color.white);
            return material;
        }

        private static Texture2D CreateCloudTexture()
        {
            Texture2D texture = new(64, 64, TextureFormat.RGBA32, false)
            {
                name = "Soft Layered Cloud Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[64 * 64];
            Vector3[] puffs =
            {
                new(-0.47f, -0.08f, 0.48f),
                new(-0.18f, 0.12f, 0.58f),
                new(0.17f, 0.2f, 0.52f),
                new(0.48f, -0.05f, 0.43f),
                new(0.05f, -0.18f, 0.68f)
            };
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                Vector2 p = new((x + 0.5f) / 64f * 2f - 1f, (y + 0.5f) / 64f * 2f - 1f);
                float alpha = 0f;
                foreach (Vector3 puff in puffs)
                {
                    Vector2 delta = p - new Vector2(puff.x, puff.y);
                    delta.y *= 1.35f;
                    float softPuff = Mathf.SmoothStep(
                        0f, 1f, Mathf.Clamp01(1f - delta.magnitude / puff.z));
                    alpha = Mathf.Max(alpha, softPuff * softPuff);
                }
                float edgeFade = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((1f - Mathf.Abs(p.x)) * 5f)) *
                    Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01((1f - Mathf.Abs(p.y)) * 5f));
                alpha *= edgeFade;
                pixels[y * 64 + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateRainTexture()
        {
            Texture2D texture = new(16, 64, TextureFormat.RGBA32, false)
            {
                name = "Fine Rain Streak Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[16 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 16; x++)
            {
                float nx = Mathf.Abs((x + 0.5f) / 16f * 2f - 1f);
                float ny = Mathf.Abs((y + 0.5f) / 64f * 2f - 1f);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - nx), 5f) *
                              Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - ny) * 5f));
                pixels[y * 16 + x] = new Color(1f, 1f, 1f, alpha * 0.72f);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }

        private void UpdateEffectAnchors()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            cloudSystem.transform.position = targetCamera.transform.position + Vector3.up * cloudLayerHeight;
            rainSystem.transform.position = targetCamera.transform.position + Vector3.up * 13f;
        }

        private void ApplyProfile(WeatherProfile p, bool instant)
        {
            VisibilityMeters = p.visibility;
            CloudAmount = p.cloudAmount;
            RainIntensity = p.rain;
            TimeOfDayHours = p.timeOfDay;

            RenderSettings.fog = p.fogAmount > 0.005f;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogStartDistance = Mathf.Max(30f, p.visibility * 0.12f);
            RenderSettings.fogEndDistance = p.visibility;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;
            RenderSettings.ambientIntensity = p.ambientIntensity;

            if (targetCamera != null) targetCamera.backgroundColor = p.skyColor;
            if (runtimeSkybox != null)
            {
                if (runtimeSkybox.HasProperty("_Tint"))
                    runtimeSkybox.SetColor("_Tint", Color.Lerp(Color.white, p.skyColor, 0.55f));
                if (runtimeSkybox.HasProperty("_SkyTint"))
                    runtimeSkybox.SetColor("_SkyTint", Color.Lerp(p.skyColor, Color.white, 0.18f));
                if (runtimeSkybox.HasProperty("_GroundColor"))
                    runtimeSkybox.SetColor("_GroundColor", p.ambientGround);
                if (runtimeSkybox.HasProperty("_Exposure"))
                    runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(0.68f, 1.25f, p.sunIntensity / 1.1f));
                if (runtimeSkybox.HasProperty("_AtmosphereThickness"))
                    runtimeSkybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.65f, 1.2f, p.fogAmount));
            }
            if (sun != null)
            {
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
            }

            ParticleSystem.EmissionModule cloudEmission = cloudSystem.emission;
            cloudEmission.rateOverTime = p.cloudAmount * 2.2f;
            ParticleSystem.MainModule cloudMain = cloudSystem.main;
            cloudMain.startColor = Color.white;
            SetMaterialColor(cloudMaterial, new Color(
                p.cloudColor.r, p.cloudColor.g, p.cloudColor.b,
                Mathf.SmoothStep(0f, 0.36f, p.cloudAmount)));

            ParticleSystem.EmissionModule rainEmission = rainSystem.emission;
            rainEmission.rateOverTime = p.rain * 1450f;
            SetMaterialColor(rainMaterial, new Color(
                0.62f, 0.74f, 0.84f, p.rain * 0.38f));
            if (p.cloudAmount <= 0.002f) cloudSystem.Clear();
            if (p.rain <= 0.002f) rainSystem.Clear();
            float audioBlend = instant ? 1f : 1f - Mathf.Exp(-rainFadeSpeed * Time.unscaledDeltaTime);
            rainAudio.volume = Mathf.Lerp(rainAudio.volume, p.rain * rainAudioVolume, audioBlend);

            ApplyWetSurfaces(p.wetness);
            ApplyAirportLights(p.airportLights);
        }

        private void ApplyWetSurfaces(float wetness)
        {
            foreach (SurfaceState state in wetSurfaces)
            {
                if (state.renderer == null) continue;
                MaterialPropertyBlock block = new();
                state.renderer.GetPropertyBlock(block);
                Color wetColor = Color.Lerp(state.baseColor,
                    state.baseColor * (1f - wetDarkening), wetness);
                block.SetColor(BaseColorId, wetColor);
                block.SetColor(ColorId, wetColor);
                block.SetFloat(SmoothnessId, Mathf.Lerp(0.2f, wetSmoothness, wetness));
                state.renderer.SetPropertyBlock(block);
            }
        }

        private void ApplyAirportLights(float amount)
        {
            foreach (LightState state in airportLights)
            {
                if (state.light == null) continue;
                state.light.enabled = amount > 0.03f;
                state.light.intensity = state.baseIntensity * amount;
            }
        }

        private WeatherProfile BuildProfile(WeatherPreset preset)
        {
            return preset switch
            {
                WeatherPreset.FewClouds => new WeatherProfile
                {
                    skyColor = new Color(0.48f, 0.68f, 0.88f), fogColor = new Color(0.62f, 0.75f, 0.84f),
                    fogAmount = 0.12f, visibility = 8000f, cloudAmount = 0.32f, cloudColor = new Color(0.92f, 0.94f, 0.96f),
                    ambientSky = new Color(0.58f, 0.67f, 0.76f), ambientEquator = new Color(0.5f, 0.57f, 0.62f), ambientGround = new Color(0.27f, 0.3f, 0.28f),
                    ambientIntensity = 0.95f, sunColor = new Color(1f, 0.96f, 0.88f), sunIntensity = 1.05f, sunEuler = new Vector3(48f, -32f, 0f),
                    timeOfDay = 12.5f, windFromHeading = 170f, windSpeedKnots = 7f
                },
                WeatherPreset.Overcast => new WeatherProfile
                {
                    skyColor = new Color(0.43f, 0.48f, 0.52f), fogColor = new Color(0.49f, 0.53f, 0.55f),
                    fogAmount = 0.45f, visibility = 5200f, cloudAmount = 0.92f, cloudColor = new Color(0.56f, 0.59f, 0.61f),
                    ambientSky = new Color(0.43f, 0.47f, 0.5f), ambientEquator = new Color(0.4f, 0.43f, 0.44f), ambientGround = new Color(0.24f, 0.26f, 0.25f),
                    ambientIntensity = 0.78f, sunColor = new Color(0.83f, 0.87f, 0.9f), sunIntensity = 0.48f, sunEuler = new Vector3(52f, -28f, 0f),
                    timeOfDay = 13f, airportLights = 0.25f, windFromHeading = 210f, windSpeedKnots = 10f
                },
                WeatherPreset.Fog => new WeatherProfile
                {
                    skyColor = new Color(0.7f, 0.73f, 0.73f), fogColor = new Color(0.7f, 0.73f, 0.73f),
                    fogAmount = 0.9f, visibility = 1350f, cloudAmount = 0.64f, cloudColor = new Color(0.73f, 0.75f, 0.75f),
                    ambientSky = new Color(0.62f, 0.64f, 0.64f), ambientEquator = new Color(0.58f, 0.6f, 0.6f), ambientGround = new Color(0.4f, 0.42f, 0.4f),
                    ambientIntensity = 0.9f, sunColor = new Color(0.9f, 0.9f, 0.86f), sunIntensity = 0.32f, sunEuler = new Vector3(48f, -32f, 0f),
                    timeOfDay = 10f, wetness = 0.18f, airportLights = 0.7f, windFromHeading = 90f, windSpeedKnots = 3f
                },
                WeatherPreset.LightRain => new WeatherProfile
                {
                    skyColor = new Color(0.33f, 0.4f, 0.46f), fogColor = new Color(0.42f, 0.48f, 0.51f),
                    fogAmount = 0.62f, visibility = 3000f, cloudAmount = 1f, cloudColor = new Color(0.43f, 0.46f, 0.49f), rain = 0.72f,
                    ambientSky = new Color(0.36f, 0.41f, 0.45f), ambientEquator = new Color(0.34f, 0.38f, 0.4f), ambientGround = new Color(0.2f, 0.23f, 0.22f),
                    ambientIntensity = 0.68f, sunColor = new Color(0.72f, 0.78f, 0.83f), sunIntensity = 0.3f, sunEuler = new Vector3(52f, -24f, 0f),
                    timeOfDay = 14f, wetness = 1f, airportLights = 0.55f, windFromHeading = 220f, windSpeedKnots = 12f
                },
                WeatherPreset.Sunset => new WeatherProfile
                {
                    skyColor = new Color(0.19f, 0.23f, 0.43f), fogColor = new Color(0.43f, 0.31f, 0.42f),
                    fogAmount = 0.25f, visibility = 6200f, cloudAmount = 0.36f, cloudColor = new Color(0.68f, 0.47f, 0.58f),
                    ambientSky = new Color(0.3f, 0.29f, 0.48f), ambientEquator = new Color(0.47f, 0.31f, 0.34f), ambientGround = new Color(0.2f, 0.18f, 0.25f),
                    ambientIntensity = 0.82f, sunColor = new Color(1f, 0.49f, 0.25f), sunIntensity = 0.72f, sunEuler = new Vector3(9f, -68f, 0f),
                    timeOfDay = 18.5f, airportLights = 1f, windFromHeading = 250f, windSpeedKnots = 6f
                },
                _ => new WeatherProfile
                {
                    skyColor = defaultCameraColor, fogColor = new Color(0.62f, 0.76f, 0.86f),
                    fogAmount = 0f, visibility = 10000f, cloudAmount = 0f, cloudColor = Color.white,
                    ambientSky = new Color(0.55f, 0.65f, 0.75f), ambientEquator = new Color(0.49f, 0.55f, 0.58f), ambientGround = new Color(0.28f, 0.31f, 0.28f),
                    ambientIntensity = 1f, sunColor = new Color(1f, 0.96f, 0.88f), sunIntensity = 1.1f, sunEuler = new Vector3(48f, -32f, 0f),
                    timeOfDay = 12f, windFromHeading = 180f, windSpeedKnots = 7f
                }
            };
        }

        private static AudioClip CreateRainLoop()
        {
            const int sampleRate = 22050;
            const int length = sampleRate * 2;
            float[] samples = new float[length];
            System.Random random = new(91827);
            float filtered = 0f;
            for (int i = 0; i < length; i++)
            {
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, noise, 0.18f);
                float patter = Mathf.Sin(i * 0.017f) * 0.04f;
                samples[i] = Mathf.Clamp(filtered * 0.32f + patter, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("Procedural Light Rain Loop", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnGUI()
        {
            if (!showDebugReadout) return;
            debugStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                padding = new RectOffset(10, 10, 8, 8),
                normal = { textColor = Color.white }
            };
            string text = "WEATHER\n" +
                          $"Preset       {PresetDisplayName}\n" +
                          $"Visibility   {VisibilityMeters:0} m\n" +
                          $"Cloud amount {CloudAmount * 100f:0}%\n" +
                          $"Rain         {RainIntensity * 100f:0}%\n" +
                          $"Time         {TimeOfDayHours:00.0}:00\n" +
                          "F11 Hide weather debug";
            GUI.Box(new Rect(Screen.width - 245f, 18f, 225f, 135f), text, debugStyle);
        }

        public static string FormatPreset(WeatherPreset preset) => preset switch
        {
            WeatherPreset.FewClouds => "Few Clouds",
            WeatherPreset.LightRain => "Light Rain",
            _ => preset.ToString()
        };

        private readonly struct SurfaceState
        {
            public readonly Renderer renderer;
            public readonly Color baseColor;
            public SurfaceState(Renderer renderer, Color baseColor)
            {
                this.renderer = renderer;
                this.baseColor = baseColor;
            }
        }

        private readonly struct LightState
        {
            public readonly Light light;
            public readonly float baseIntensity;
            public LightState(Light light, float baseIntensity)
            {
                this.light = light;
                this.baseIntensity = baseIntensity;
            }
        }

        private struct WeatherProfile
        {
            public Color skyColor, fogColor, cloudColor, ambientSky, ambientEquator, ambientGround, sunColor;
            public float fogAmount, visibility, cloudAmount, rain, ambientIntensity, sunIntensity;
            public float timeOfDay, wetness, airportLights, windFromHeading, windSpeedKnots;
            public Vector3 sunEuler;

            public static WeatherProfile Lerp(WeatherProfile a, WeatherProfile b, float t) => new()
            {
                skyColor = Color.Lerp(a.skyColor, b.skyColor, t), fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                cloudColor = Color.Lerp(a.cloudColor, b.cloudColor, t), ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t),
                ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t), ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t),
                sunColor = Color.Lerp(a.sunColor, b.sunColor, t), fogAmount = Mathf.Lerp(a.fogAmount, b.fogAmount, t),
                visibility = Mathf.Lerp(a.visibility, b.visibility, t), cloudAmount = Mathf.Lerp(a.cloudAmount, b.cloudAmount, t),
                rain = Mathf.Lerp(a.rain, b.rain, t), ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t),
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t), timeOfDay = Mathf.Lerp(a.timeOfDay, b.timeOfDay, t),
                wetness = Mathf.Lerp(a.wetness, b.wetness, t), airportLights = Mathf.Lerp(a.airportLights, b.airportLights, t),
                windFromHeading = Mathf.LerpAngle(a.windFromHeading, b.windFromHeading, t), windSpeedKnots = Mathf.Lerp(a.windSpeedKnots, b.windSpeedKnots, t),
                sunEuler = new Vector3(Mathf.LerpAngle(a.sunEuler.x, b.sunEuler.x, t), Mathf.LerpAngle(a.sunEuler.y, b.sunEuler.y, t), 0f)
            };
        }
    }
}
