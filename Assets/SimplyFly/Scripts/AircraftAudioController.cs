using System.Text;
using UnityEngine;
using UnityEngine.Audio;

namespace SimplyFly.GroundOps
{
    public sealed class AircraftAudioController : MonoBehaviour
    {
        [Header("Aircraft telemetry")]
        [SerializeField] private EngineController engine;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private GroundMovementController groundMovement;
        [SerializeField] private BrakeController brakes;
        [SerializeField] private StallController stall;
        [SerializeField] private LandingController landing;
        [SerializeField] private FlapController flaps;
        [SerializeField] private RudderController rudder;

        [Header("Audio clips")]
        [SerializeField] private AudioClip engineIdleLoop;
        [SerializeField] private AudioClip enginePowerLoop;
        [SerializeField] private AudioClip propellerLoop;
        [SerializeField] private AudioClip windLoop;
        [SerializeField] private AudioClip strongWindLoop;
        [SerializeField] private AudioClip descentWindLoop;
        [SerializeField] private AudioClip runwayRumbleLoop;
        [SerializeField] private AudioClip stallWarningLoop;
        [SerializeField] private AudioClip stallBuffetLoop;
        [SerializeField] private AudioClip brakeLoop;
        [SerializeField] private AudioClip flapMotorLoop;
        [SerializeField] private AudioClip touchdownSoftClip;
        [SerializeField] private AudioClip touchdownFirmClip;
        [SerializeField] private AudioClip touchdownHardClip;
        [SerializeField] private AudioClip tireChirpClip;
        [SerializeField] private AudioClip engineStartClip;
        [SerializeField] private AudioClip engineStopClip;

        [Header("Audio mixer routing (optional)")]
        [SerializeField] private AudioMixerGroup aircraftMixerGroup;
        [SerializeField] private AudioMixerGroup engineMixerGroup;
        [SerializeField] private AudioMixerGroup windMixerGroup;
        [SerializeField] private AudioMixerGroup warningMixerGroup;
        [SerializeField] private AudioMixerGroup uiMixerGroup;

        [Header("Engine")]
        [Min(0.01f)] [SerializeField] private float engineFadeSpeed = 3.5f;
        [Range(0f, 1f)] [SerializeField] private float idleMinVolume = 0.18f;
        [Range(0f, 1f)] [SerializeField] private float idleMaxVolume = 0.48f;
        [Range(0f, 1f)] [SerializeField] private float powerMinVolume = 0.02f;
        [Range(0f, 1f)] [SerializeField] private float powerMaxVolume = 0.72f;
        [Min(0.01f)] [SerializeField] private float enginePitchMin = 0.72f;
        [Min(0.01f)] [SerializeField] private float enginePitchMax = 1.42f;

        [Header("Propeller")]
        [Range(0f, 1f)] [SerializeField] private float propMinVolume = 0.05f;
        [Range(0f, 1f)] [SerializeField] private float propMaxVolume = 0.62f;
        [Min(0.01f)] [SerializeField] private float propPitchMin = 0.65f;
        [Min(0.01f)] [SerializeField] private float propPitchMax = 1.5f;

        [Header("Wind")]
        [Min(0f)] [SerializeField] private float windStartSpeedKt = 20f;
        [Min(1f)] [SerializeField] private float windMaxSpeedKt = 130f;
        [Range(0f, 1f)] [SerializeField] private float windMinVolume = 0.015f;
        [Range(0f, 1f)] [SerializeField] private float windMaxVolume = 0.68f;
        [Min(0.01f)] [SerializeField] private float windPitchMin = 0.68f;
        [Min(0.01f)] [SerializeField] private float windPitchMax = 1.35f;
        [Range(0f, 1f)] [SerializeField] private float descentWindBoost = 0.22f;
        [Min(0.01f)] [SerializeField] private float windSmoothing = 2.8f;
        [Range(0f, 0.3f)] [SerializeField] private float slipWindBoost = 0.1f;

        [Header("Runway rumble")]
        [Min(0f)] [SerializeField] private float rumbleStartSpeedKt = 2f;
        [Min(1f)] [SerializeField] private float rumbleMaxSpeedKt = 65f;
        [Range(0f, 1f)] [SerializeField] private float rumbleMinVolume = 0.025f;
        [Range(0f, 1f)] [SerializeField] private float rumbleMaxVolume = 0.56f;
        [Min(0.01f)] [SerializeField] private float rumblePitchMin = 0.65f;
        [Min(0.01f)] [SerializeField] private float rumblePitchMax = 1.35f;

        [Header("Stall")]
        [Range(0f, 1f)] [SerializeField] private float stallWarningVolume = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float buffetVolume = 0.48f;
        [Min(0.01f)] [SerializeField] private float stallFadeSpeed = 5f;

        [Header("Touchdown")]
        [Min(0f)] [SerializeField] private float touchdownSoftVs = 1f;
        [Min(0.1f)] [SerializeField] private float touchdownHardVs = 5f;
        [Range(0f, 1f)] [SerializeField] private float touchdownMinVolume = 0.18f;
        [Range(0f, 1f)] [SerializeField] private float touchdownMaxVolume = 1f;

        [Header("Brakes and flaps")]
        [Min(0f)] [SerializeField] private float brakeMinSpeedKt = 3f;
        [Range(0f, 1f)] [SerializeField] private float brakeMaxVolume = 0.42f;
        [Range(0f, 1f)] [SerializeField] private float flapMotorVolume = 0.32f;
        [Min(0.01f)] [SerializeField] private float effectsSmoothing = 5f;

        [Header("Category levels")]
        [Range(0f, 1f)] [SerializeField] private float engineVolumeMultiplier = 1f;
        [Range(0f, 1f)] [SerializeField] private float windVolumeMultiplier = 1f;
        [Range(0f, 1f)] [SerializeField] private float warningVolumeMultiplier = 1f;

        public float EngineIdleVolume { get; private set; }
        public float EnginePowerVolume { get; private set; }
        public float PropellerVolume { get; private set; }
        public float WindVolume { get; private set; }
        public float RumbleVolume { get; private set; }
        public bool StallWarningAudioActive { get; private set; }
        public bool BuffetAudioActive { get; private set; }
        public float BrakeVolume { get; private set; }
        public float LastTouchdownSoundVolume { get; private set; }

        private AudioSource idleSource;
        private AudioSource powerSource;
        private AudioSource propSource;
        private AudioSource windSource;
        private AudioSource strongWindSource;
        private AudioSource descentWindSource;
        private AudioSource rumbleSource;
        private AudioSource stallWarningSource;
        private AudioSource buffetSource;
        private AudioSource brakeSource;
        private AudioSource flapSource;
        private AudioSource oneShotSource;
        private EngineController.EngineState previousEngineState;
        private bool previousMainWheelContact;
        private bool configured;

        private void Awake()
        {
            ResolveReferences();
            previousEngineState = engine == null
                ? EngineController.EngineState.Off
                : engine.State;
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            ConfigureSources();
            previousMainWheelContact =
                landing != null && landing.MainWheelsTouching;
        }

        private void Update()
        {
            if (!configured) return;

            float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            float rpm = engine == null ? 0f : engine.NormalizedRPM;
            float throttleAmount = throttle == null ? 0f : throttle.Throttle;
            float airspeed = flightPhysics == null
                ? 0f
                : flightPhysics.AirspeedKnots;
            float groundSpeed = groundMovement != null
                ? groundMovement.GroundSpeedKnots
                : flightPhysics == null
                    ? 0f
                    : flightPhysics.HorizontalGroundSpeed * 1.943844f;
            bool grounded = landing != null
                ? !landing.Airborne
                : groundMovement != null && groundMovement.IsGrounded;
            bool engineRunning =
                engine != null &&
                engine.State == EngineController.EngineState.Running;

            HandleEngineStateChanges();
            UpdateEngine(engineRunning, rpm, throttleAmount, deltaTime);
            UpdateWind(airspeed, deltaTime);
            UpdateGround(grounded, groundSpeed, deltaTime);
            UpdateStall(deltaTime);
            UpdateFlaps(deltaTime);
            DetectTouchdown();
        }

        private void UpdateEngine(
            bool running,
            float rpm,
            float throttleAmount,
            float deltaTime)
        {
            float idleBlend = Mathf.Clamp01(1f - throttleAmount * 1.35f);
            float idleTarget = running
                ? Mathf.Lerp(idleMinVolume, idleMaxVolume, idleBlend) *
                  engineVolumeMultiplier
                : 0f;
            float powerTarget = running
                ? Mathf.Lerp(powerMinVolume, powerMaxVolume,
                    Mathf.SmoothStep(0f, 1f, throttleAmount)) *
                  engineVolumeMultiplier
                : 0f;
            float propTarget = running
                ? Mathf.Lerp(propMinVolume, propMaxVolume, rpm) *
                  engineVolumeMultiplier
                : 0f;
            float enginePitch =
                Mathf.Lerp(enginePitchMin, enginePitchMax, rpm);
            float propellerPitch =
                Mathf.Lerp(propPitchMin, propPitchMax, rpm);

            SmoothSource(
                idleSource, idleTarget, enginePitch, engineFadeSpeed, deltaTime);
            SmoothSource(
                powerSource, powerTarget, enginePitch, engineFadeSpeed, deltaTime);
            SmoothSource(
                propSource, propTarget, propellerPitch, engineFadeSpeed, deltaTime);
            EngineIdleVolume = idleSource.volume;
            EnginePowerVolume = powerSource.volume;
            PropellerVolume = propSource.volume;
        }

        private void UpdateWind(float airspeed, float deltaTime)
        {
            float speedBlend = Mathf.InverseLerp(
                windStartSpeedKt, windMaxSpeedKt, airspeed);
            float descentBlend = flightPhysics == null
                ? 0f
                : Mathf.InverseLerp(0f, 10f, -flightPhysics.VerticalSpeed);
            float targetVolume = speedBlend <= 0f
                ? 0f
                : Mathf.Lerp(windMinVolume, windMaxVolume, speedBlend);
            targetVolume = Mathf.Clamp01(targetVolume) * windVolumeMultiplier;
            float targetPitch =
                Mathf.Lerp(windPitchMin, windPitchMax, speedBlend);
            float softTarget =
                targetVolume * Mathf.Lerp(1f, 0.3f, speedBlend);
            float strongTarget =
                targetVolume * Mathf.SmoothStep(0f, 1f, speedBlend);
            float descentTarget =
                descentBlend * descentWindBoost * windVolumeMultiplier;
            float slipTarget =
                (rudder == null ? 0f : Mathf.Abs(rudder.SlipAmount)) *
                slipWindBoost * windVolumeMultiplier;
            SmoothSource(
                windSource, softTarget, targetPitch, windSmoothing, deltaTime);
            SmoothSource(
                strongWindSource, strongTarget, targetPitch,
                windSmoothing, deltaTime);
            SmoothSource(
                descentWindSource, descentTarget + slipTarget,
                Mathf.Lerp(0.8f, 1.2f, descentBlend),
                windSmoothing, deltaTime);
            WindVolume = Mathf.Clamp01(
                windSource.volume +
                strongWindSource.volume +
                descentWindSource.volume);
        }

        private void UpdateGround(
            bool grounded,
            float groundSpeed,
            float deltaTime)
        {
            float speedBlend = Mathf.InverseLerp(
                rumbleStartSpeedKt, rumbleMaxSpeedKt, groundSpeed);
            float rumbleTarget = grounded && speedBlend > 0f
                ? Mathf.Lerp(rumbleMinVolume, rumbleMaxVolume, speedBlend)
                : 0f;
            float rumblePitch =
                Mathf.Lerp(rumblePitchMin, rumblePitchMax, speedBlend);
            SmoothSource(
                rumbleSource, rumbleTarget, rumblePitch,
                grounded ? effectsSmoothing : effectsSmoothing * 2f,
                deltaTime);

            float brakeAmount = brakes == null ? 0f : brakes.BrakeAmount;
            float brakeSpeedBlend = Mathf.InverseLerp(
                brakeMinSpeedKt, rumbleMaxSpeedKt, groundSpeed);
            float brakeTarget = grounded
                ? brakeAmount * brakeSpeedBlend * brakeMaxVolume
                : 0f;
            SmoothSource(
                brakeSource, brakeTarget,
                Mathf.Lerp(0.75f, 1.25f, brakeSpeedBlend),
                effectsSmoothing, deltaTime);
            RumbleVolume = rumbleSource.volume;
            BrakeVolume = brakeSource.volume;
        }

        private void UpdateStall(float deltaTime)
        {
            StallWarningAudioActive =
                stall != null && stall.StallWarning;
            BuffetAudioActive =
                stall != null &&
                stall.State == StallController.StallState.Stalled;
            float warningTarget = StallWarningAudioActive
                ? stallWarningVolume * warningVolumeMultiplier
                : 0f;
            float buffetTarget = BuffetAudioActive
                ? buffetVolume * warningVolumeMultiplier
                : 0f;
            SmoothSource(
                stallWarningSource, warningTarget, 1f,
                stallFadeSpeed, deltaTime);
            SmoothSource(
                buffetSource, buffetTarget, 1f,
                stallFadeSpeed, deltaTime);
        }

        private void UpdateFlaps(float deltaTime)
        {
            bool moving =
                flaps != null &&
                Mathf.Abs(flaps.SelectedAngle - flaps.ActualAngle) > 0.05f;
            SmoothSource(
                flapSource,
                moving ? flapMotorVolume : 0f,
                1f,
                effectsSmoothing,
                deltaTime);
        }

        private void HandleEngineStateChanges()
        {
            if (engine == null || engine.State == previousEngineState) return;
            if (engine.State == EngineController.EngineState.Starting)
                PlayOneShot(engineStartClip, 0.75f * engineVolumeMultiplier);
            else if (engine.State == EngineController.EngineState.Off &&
                     previousEngineState != EngineController.EngineState.Off)
                PlayOneShot(engineStopClip, 0.65f * engineVolumeMultiplier);
            previousEngineState = engine.State;
        }

        private void DetectTouchdown()
        {
            if (landing == null) return;
            bool mainContact = landing.MainWheelsTouching;
            if (mainContact && !previousMainWheelContact &&
                landing.Quality != LandingController.LandingQuality.None)
            {
                float impactSpeed =
                    Mathf.Max(0f, -landing.TouchdownVerticalSpeed);
                float hardness = Mathf.InverseLerp(
                    touchdownSoftVs, touchdownHardVs, impactSpeed);
                LastTouchdownSoundVolume = Mathf.Lerp(
                    touchdownMinVolume, touchdownMaxVolume, hardness);
                AudioClip impactClip = hardness < 0.35f
                    ? touchdownSoftClip
                    : hardness < 0.75f
                        ? touchdownFirmClip
                        : touchdownHardClip;
                PlayOneShot(impactClip, LastTouchdownSoundVolume);
                if (tireChirpClip != null &&
                    landing.TouchdownAirspeed * 1.943844f > 25f)
                {
                    PlayOneShot(
                        tireChirpClip,
                        Mathf.Lerp(0.12f, 0.4f, hardness));
                }
            }
            previousMainWheelContact = mainContact;
        }

        private void ConfigureSources()
        {
            if (configured) return;
            configured = true;

            AudioClip sharedEngine = engineIdleLoop != null
                ? engineIdleLoop
                : CreateEngineLoop();
            idleSource = CreateLoopSource(
                "Audio - Engine Idle", sharedEngine, engineMixerGroup, 0.55f);
            powerSource = CreateLoopSource(
                "Audio - Engine Power",
                enginePowerLoop != null ? enginePowerLoop : sharedEngine,
                engineMixerGroup, 0.55f);
            propSource = CreateLoopSource(
                "Audio - Propeller",
                propellerLoop != null ? propellerLoop : sharedEngine,
                engineMixerGroup, 0.48f);
            windSource = CreateLoopSource(
                "Audio - Wind",
                windLoop != null ? windLoop : CreateNoiseLoop(
                    "Generated Wind", 0.1f, 0.82f, 7103),
                windMixerGroup, 0f);
            strongWindSource = CreateLoopSource(
                "Audio - Strong Wind",
                strongWindLoop != null ? strongWindLoop : windSource.clip,
                windMixerGroup, 0f);
            descentWindSource = CreateLoopSource(
                "Audio - Descent Wind",
                descentWindLoop != null ? descentWindLoop : windSource.clip,
                windMixerGroup, 0f);
            rumbleSource = CreateLoopSource(
                "Audio - Runway Rumble",
                runwayRumbleLoop != null
                    ? runwayRumbleLoop
                    : CreateRumbleLoop("Generated Runway Rumble", 31f, 47f),
                aircraftMixerGroup, 0.25f);
            stallWarningSource = CreateLoopSource(
                "Audio - Stall Warning",
                stallWarningLoop != null
                    ? stallWarningLoop
                    : CreatePulsingTone("Generated Stall Warning", 520f, 3.2f),
                warningMixerGroup, 0f);
            buffetSource = CreateLoopSource(
                "Audio - Stall Buffet",
                stallBuffetLoop != null
                    ? stallBuffetLoop
                    : CreateNoiseLoop(
                        "Generated Stall Buffet", 0.18f, 0.55f, 911),
                warningMixerGroup, 0.15f);
            brakeSource = CreateLoopSource(
                "Audio - Brakes",
                brakeLoop != null
                    ? brakeLoop
                    : CreateNoiseLoop(
                        "Generated Brake Friction", 0.08f, 0.35f, 445),
                aircraftMixerGroup, 0.28f);
            flapSource = CreateLoopSource(
                "Audio - Flap Motor",
                flapMotorLoop != null
                    ? flapMotorLoop
                    : CreateRumbleLoop("Generated Flap Motor", 92f, 138f),
                aircraftMixerGroup, 0.35f);

            oneShotSource = CreateSource(
                "Audio - One Shots", aircraftMixerGroup, 0.35f);
            touchdownSoftClip ??= CreateTouchdownClip();
            touchdownFirmClip ??= touchdownSoftClip;
            touchdownHardClip ??= touchdownFirmClip;
            engineStartClip ??= CreateEngineTransitionClip(
                "Generated Engine Start", true);
            engineStopClip ??= CreateEngineTransitionClip(
                "Generated Engine Stop", false);
        }

        private AudioSource CreateLoopSource(
            string sourceName,
            AudioClip clip,
            AudioMixerGroup mixerGroup,
            float spatialBlend)
        {
            AudioSource source =
                CreateSource(sourceName, mixerGroup, spatialBlend);
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.Play();
            return source;
        }

        private AudioSource CreateSource(
            string sourceName,
            AudioMixerGroup mixerGroup,
            float spatialBlend)
        {
            GameObject child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0.1f;
            source.minDistance = 6f;
            source.maxDistance = 180f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.outputAudioMixerGroup =
                mixerGroup != null ? mixerGroup : aircraftMixerGroup;
            return source;
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (oneShotSource == null || clip == null || volume <= 0f) return;
            oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private static void SmoothSource(
            AudioSource source,
            float targetVolume,
            float targetPitch,
            float response,
            float deltaTime)
        {
            if (source == null) return;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * deltaTime);
            source.volume = Mathf.Lerp(
                source.volume, Mathf.Clamp01(targetVolume), blend);
            source.pitch = Mathf.Lerp(
                source.pitch, Mathf.Max(0.01f, targetPitch), blend);
        }

        public void SetEngineVolumeMultiplier(float value) =>
            engineVolumeMultiplier = Mathf.Clamp01(value);

        public void SetWindVolumeMultiplier(float value) =>
            windVolumeMultiplier = Mathf.Clamp01(value);

        public void SetWarningVolumeMultiplier(float value) =>
            warningVolumeMultiplier = Mathf.Clamp01(value);

        public string GetDiagnostics()
        {
            StringBuilder text = new StringBuilder(256);
            text.AppendLine("AIRCRAFT AUDIO");
            text.AppendLine($"Idle volume       {EngineIdleVolume:0.00}");
            text.AppendLine($"Power volume      {EnginePowerVolume:0.00}");
            text.AppendLine($"Prop volume       {PropellerVolume:0.00}");
            text.AppendLine($"Wind volume       {WindVolume:0.00}");
            text.AppendLine($"Rumble volume     {RumbleVolume:0.00}");
            text.AppendLine($"Stall warning     {(StallWarningAudioActive ? "Yes" : "No")}");
            text.AppendLine($"Buffet active     {(BuffetAudioActive ? "Yes" : "No")}");
            text.AppendLine($"Brake volume      {BrakeVolume:0.00}");
            text.Append($"Touchdown volume  {LastTouchdownSoundVolume:0.00}");
            return text.ToString();
        }

        private void ResolveReferences()
        {
            if (engine == null) engine = GetComponent<EngineController>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (groundMovement == null)
                groundMovement = GetComponent<GroundMovementController>();
            if (brakes == null) brakes = GetComponent<BrakeController>();
            if (stall == null) stall = GetComponent<StallController>();
            if (landing == null) landing = GetComponent<LandingController>();
            if (flaps == null) flaps = GetComponent<FlapController>();
            if (rudder == null) rudder = GetComponent<RudderController>();
        }

        private static AudioClip CreateEngineLoop()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                samples[i] = (
                    Mathf.Sin(t * Mathf.PI * 2f * 47f) * 0.65f +
                    Mathf.Sin(t * Mathf.PI * 2f * 94f) * 0.32f +
                    Mathf.Sin(t * Mathf.PI * 2f * 141f) * 0.18f) * 0.22f;
            }
            return CreateClip("Generated Engine Loop", samples, sampleRate);
        }

        private static AudioClip CreateNoiseLoop(
            string name,
            float amplitude,
            float smoothing,
            int seed)
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(seed);
            float value = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                value = Mathf.Lerp(noise, value, smoothing);
                samples[i] = value * amplitude;
            }
            return CreateClip(name, samples, sampleRate);
        }

        private static AudioClip CreateRumbleLoop(
            string name,
            float firstFrequency,
            float secondFrequency)
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                samples[i] = (
                    Mathf.Sin(t * Mathf.PI * 2f * firstFrequency) * 0.55f +
                    Mathf.Sin(t * Mathf.PI * 2f * secondFrequency) * 0.28f) *
                    0.11f;
            }
            return CreateClip(name, samples, sampleRate);
        }

        private static AudioClip CreatePulsingTone(
            string name,
            float frequency,
            float pulsesPerSecond)
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float pulse =
                    Mathf.Sin(t * Mathf.PI * 2f * pulsesPerSecond) > -0.15f
                        ? 1f
                        : 0f;
                samples[i] =
                    Mathf.Sin(t * Mathf.PI * 2f * frequency) * pulse * 0.18f;
            }
            return CreateClip(name, samples, sampleRate);
        }

        private static AudioClip CreateTouchdownClip()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate / 2;
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(419);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 12f);
                float thump = Mathf.Sin(t * Mathf.PI * 2f * 68f) * 0.78f;
                float tire =
                    (float)(random.NextDouble() * 2.0 - 1.0) * 0.22f;
                samples[i] = (thump + tire) * envelope * 0.55f;
            }
            return CreateClip("Generated Touchdown", samples, sampleRate);
        }

        private static AudioClip CreateEngineTransitionClip(
            string name,
            bool starting)
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float progress = t / 2f;
                float envelope = starting
                    ? Mathf.SmoothStep(0f, 1f, progress)
                    : 1f - Mathf.SmoothStep(0f, 1f, progress);
                float frequency = starting
                    ? Mathf.Lerp(22f, 58f, progress)
                    : Mathf.Lerp(58f, 18f, progress);
                samples[i] =
                    Mathf.Sin(t * Mathf.PI * 2f * frequency) *
                    envelope * 0.28f;
            }
            return CreateClip(name, samples, sampleRate);
        }

        private static AudioClip CreateClip(
            string name,
            float[] samples,
            int sampleRate)
        {
            AudioClip clip = AudioClip.Create(
                name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
