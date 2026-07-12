using System;
using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class PilotAwarenessAssist : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private ApproachGuidanceController approach;
        [SerializeField] private StallController stall;
        [SerializeField] private AssistedTrimController trim;
        [SerializeField] private LandingController landing;
        [SerializeField] private EngineController engine;
        [SerializeField] private AircraftInput input;

        [Header("Settings")]
        [SerializeField] private bool pilotAwarenessAssist = true;
        [SerializeField] private bool hudAwarenessCues = true;
        [SerializeField] private bool audioCues = true;
        [Range(0f, 2f)] [SerializeField] private float motionCuesStrength = 1f;
        [SerializeField] private bool speedSensationEnabled = true;
        [SerializeField] private bool peripheralSpeedCuesEnabled = true;

        [Header("Awareness thresholds")]
        [Range(10f, 70f)] [SerializeField] private float bankAwarenessAngle = 35f;
        [Min(0.5f)] [SerializeField] private float highSinkRateThreshold = 3.5f;
        [Min(0f)] [SerializeField] private float lowSpeedMargin = 6f;
        [Range(1f, 45f)] [SerializeField] private float approachAlignmentThreshold = 8f;
        [Min(1f)] [SerializeField] private float centerlineOffsetThreshold = 18f;
        [Min(100f)] [SerializeField] private float approachCueDistance = 3000f;
        [Min(20f)] [SerializeField] private float approachCueAltitude = 400f;

        [Header("Motion cues")]
        [Range(0f, 0.35f)] [SerializeField] private float cameraLeanStrength = 0.08f;
        [Range(0f, 0.5f)] [SerializeField] private float stallShakeStrength = 0.08f;
        [Range(0f, 0.3f)] [SerializeField] private float groundRumbleStrength = 0.035f;
        [Range(0f, 0.8f)] [SerializeField] private float touchdownShakeStrength = 0.16f;
        [Range(0f, 0.2f)] [SerializeField] private float engineVibrationStrength = 0.025f;
        [Range(0f, 0.25f)] [SerializeField] private float windShakeStrength = 0.025f;
        [Header("Speed sensation")]
        [Min(0.1f)] [SerializeField] private float speedTrendResponse = 2.5f;
        [Min(0.1f)] [SerializeField] private float speedTrendDeadband = 0.45f;
        [Min(1f)] [SerializeField] private float airflowStartSpeed = 42f;
        [Min(1f)] [SerializeField] private float airflowFullSpeed = 125f;
        [Min(0f)] [SerializeField] private float lowEnergySpeedMargin = 9f;
        [Range(0f, 0.35f)] [SerializeField] private float accelerationCameraStrength = 0.11f;

        [Header("Audio cues")]
        [SerializeField] private AnimationCurve windVolumeCurve =
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(45f, 0.03f),
                new Keyframe(90f, 0.18f),
                new Keyframe(150f, 0.35f));
        [Range(0f, 1f)] [SerializeField] private float stallWarningVolume = 0.22f;
        [Range(0f, 1f)] [SerializeField] private float groundRumbleVolume = 0.16f;
        [Range(0f, 1f)] [SerializeField] private float touchdownVolume = 0.45f;
        [Min(0.1f)] [SerializeField] private float audioResponseSpeed = 4f;
        [Range(0f, 1f)] [SerializeField] private float windVolumeMultiplier = 1f;

        public bool Enabled => pilotAwarenessAssist;
        public bool HUDCuesEnabled => pilotAwarenessAssist && hudAwarenessCues;
        public string AwarenessCue { get; private set; } = string.Empty;
        public float CameraLeanDegrees { get; private set; }
        public float CameraShake { get; private set; }
        public float TouchdownPulse { get; private set; }
        public float AirspeedTrend { get; private set; }
        public float AirflowIntensity { get; private set; }
        public float LongitudinalCameraCue { get; private set; }
        public string EnergyCue { get; private set; } = "SPEED STEADY";
        public string ControlPressureCue { get; private set; } = string.Empty;
        public bool PeripheralSpeedCuesEnabled =>
            pilotAwarenessAssist && speedSensationEnabled &&
            peripheralSpeedCuesEnabled;

        private AudioSource windSource;
        private AudioSource rumbleSource;
        private AudioSource stallSource;
        private AudioSource touchdownSource;
        private LandingController.LandingQuality previousLandingQuality;
        private bool audioConfigured;
        private float previousAirspeed;
        private bool speedInitialized;

        private void Awake()
        {
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (approach == null)
                approach = GetComponent<ApproachGuidanceController>();
            if (stall == null) stall = GetComponent<StallController>();
            if (trim == null) trim = GetComponent<AssistedTrimController>();
            if (landing == null) landing = GetComponent<LandingController>();
            if (engine == null) engine = GetComponent<EngineController>();
            if (input == null) input = GetComponent<AircraftInput>();
        }

        private void Start()
        {
            // Audio objects are runtime-only. Creating child GameObjects from Awake
            // also runs when this component is added by the editor scene builder,
            // which leaves those children outside Unity's Undo transaction.
            if (Application.isPlaying && audioCues) ConfigureAudio();
        }

        private void Update()
        {
            if (!pilotAwarenessAssist)
            {
                AwarenessCue = string.Empty;
                CameraLeanDegrees = 0f;
                CameraShake = 0f;
                TouchdownPulse = 0f;
                AirspeedTrend = 0f;
                AirflowIntensity = 0f;
                LongitudinalCameraCue = 0f;
                UpdateAudio(0f, 0f, false);
                return;
            }

            UpdateSpeedSensation();
            UpdateAwarenessCue();
            UpdateMotionCues();
            UpdateTouchdown();
            UpdateAudioCues();
        }

        private void UpdateSpeedSensation()
        {
            if (!speedSensationEnabled || flightPhysics == null)
            {
                AirspeedTrend = 0f;
                AirflowIntensity = 0f;
                LongitudinalCameraCue = 0f;
                EnergyCue = string.Empty;
                ControlPressureCue = string.Empty;
                return;
            }

            bool groundMode = flightPhysics.IsPhysicallyGrounded;
            float speed = groundMode
                ? flightPhysics.HorizontalGroundSpeed * 1.943844f
                : flightPhysics.AirspeedKnots;
            if (!speedInitialized)
            {
                previousAirspeed = speed;
                speedInitialized = true;
            }
            float rawTrend =
                (speed - previousAirspeed) /
                Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            previousAirspeed = speed;
            float blend =
                1f - Mathf.Exp(-speedTrendResponse * Time.unscaledDeltaTime);
            AirspeedTrend = Mathf.Lerp(AirspeedTrend, rawTrend, blend);
            AirflowIntensity = Mathf.InverseLerp(
                airflowStartSpeed, airflowFullSpeed, speed);
            LongitudinalCameraCue = Mathf.Clamp(
                AirspeedTrend / 12f, -1f, 1f) *
                accelerationCameraStrength * motionCuesStrength;

            float stallSpeed = stall == null ? 42f : stall.EffectiveStallSpeed;
            if (groundMode && speed < 1f)
            {
                EnergyCue = flightPhysics.AirspeedKnots >= 1f
                    ? "WIND AIRFLOW"
                    : "PARKED";
                LongitudinalCameraCue = 0f;
                return;
            }
            if (speed <= stallSpeed + lowEnergySpeedMargin &&
                AirspeedTrend < -speedTrendDeadband)
            {
                EnergyCue = "ENERGY LOW — SPEED FALLING";
            }
            else if (AirspeedTrend > speedTrendDeadband)
            {
                EnergyCue = "ACCELERATING ↑";
            }
            else if (AirspeedTrend < -speedTrendDeadband)
            {
                EnergyCue = "SLOWING ↓";
            }
            else
            {
                EnergyCue = "SPEED STEADY";
            }

            float controlInput = input == null
                ? 0f
                : Mathf.Max(
                    Mathf.Abs(input.ElevatorInput),
                    Mathf.Abs(input.AileronInput));
            if (controlInput < 0.15f)
            {
                ControlPressureCue = trim != null &&
                                     trim.Recommendation != "TRIMMED"
                    ? trim.Recommendation
                    : string.Empty;
            }
            else if (speed < stallSpeed + 12f)
            {
                ControlPressureCue = "CONTROLS SOFT — LOW AIRFLOW";
            }
            else if (speed > 105f)
            {
                ControlPressureCue = "HIGH CONTROL LOAD";
            }
            else
            {
                ControlPressureCue = "CONTROL PRESSURE";
            }
        }

        private void UpdateAwarenessCue()
        {
            if (!hudAwarenessCues || flightPhysics == null)
            {
                AwarenessCue = string.Empty;
                return;
            }

            bool airborne =
                flightPhysics.State == FlightPhysicsController.AircraftState.Airborne;
            bool onApproach = airborne && approach != null &&
                              approach.HasRunwayTarget &&
                              approach.DistanceToThreshold <= approachCueDistance &&
                              approach.AltitudeAboveRunway <= approachCueAltitude;

            if (stall != null && stall.StallWarning)
            {
                AwarenessCue = stall.State == StallController.StallState.Stalled
                    ? "STALL — LOWER NOSE"
                    : "LOW SPEED";
                return;
            }

            if (onApproach &&
                flightPhysics.VerticalSpeed < -highSinkRateThreshold)
            {
                AwarenessCue = "HIGH SINK RATE";
                return;
            }

            if (onApproach &&
                Mathf.Abs(approach.AlignmentError) >
                approachAlignmentThreshold)
            {
                AwarenessCue = "NOT ALIGNED WITH RUNWAY";
                return;
            }

            if (onApproach &&
                Mathf.Abs(approach.LateralOffset) >
                centerlineOffsetThreshold)
            {
                AwarenessCue = approach.LateralOffset > 0f
                    ? "RIGHT OF CENTERLINE"
                    : "LEFT OF CENTERLINE";
                return;
            }

            float effectiveStallSpeed =
                stall == null ? 42f : stall.EffectiveStallSpeed;
            if (airborne &&
                flightPhysics.AirspeedKnots <
                effectiveStallSpeed + lowSpeedMargin)
            {
                AwarenessCue = "LOW SPEED";
                return;
            }

            if (Mathf.Abs(flightPhysics.BankAngle) >= bankAwarenessAngle)
            {
                AwarenessCue = flightPhysics.BankAngle > 0f
                    ? "BANK RIGHT"
                    : "BANK LEFT";
                return;
            }

            if (airborne &&
                flightPhysics.VerticalSpeed < -highSinkRateThreshold * 0.65f)
            {
                AwarenessCue = "DESCENDING";
                return;
            }

            if (trim != null &&
                !string.IsNullOrWhiteSpace(trim.Recommendation) &&
                trim.Recommendation != "TRIMMED")
            {
                AwarenessCue = trim.Recommendation;
                return;
            }

            AwarenessCue = string.Empty;
        }

        private void UpdateMotionCues()
        {
            if (flightPhysics == null)
            {
                CameraLeanDegrees = 0f;
                CameraShake = 0f;
                return;
            }

            CameraLeanDegrees =
                -flightPhysics.BankAngle * cameraLeanStrength *
                motionCuesStrength;

            float shake = 0f;
            if (stall != null && stall.StallWarning)
                shake += stallShakeStrength;

            bool grounded =
                flightPhysics.State != FlightPhysicsController.AircraftState.Airborne;
            if (grounded)
            {
                float taxiBlend = Mathf.InverseLerp(
                    2f, 45f, flightPhysics.HorizontalGroundSpeed * 1.943844f);
                shake += groundRumbleStrength * taxiBlend;
            }

            float rpmBlend = engine == null ? 0f : engine.NormalizedRPM;
            shake += engineVibrationStrength *
                     Mathf.Lerp(0.35f, 1f, rpmBlend) *
                     (engine != null && engine.CurrentRPM > 50f ? 1f : 0f);
            if (!grounded)
            {
                shake += windShakeStrength *
                         Mathf.InverseLerp(70f, 150f, flightPhysics.AirspeedKnots);
            }

            CameraShake = shake * motionCuesStrength + TouchdownPulse;
        }

        private void UpdateTouchdown()
        {
            TouchdownPulse = Mathf.MoveTowards(
                TouchdownPulse, 0f, Time.unscaledDeltaTime * 0.7f);
            if (landing == null) return;

            LandingController.LandingQuality quality = landing.Quality;
            if (quality != LandingController.LandingQuality.None &&
                quality != previousLandingQuality)
            {
                float hardness = Mathf.InverseLerp(
                    0.5f, 6f, Mathf.Abs(landing.TouchdownVerticalSpeed));
                TouchdownPulse =
                    touchdownShakeStrength * Mathf.Lerp(0.35f, 1f, hardness) *
                    motionCuesStrength;
                if (audioCues && touchdownSource != null)
                {
                    touchdownSource.volume =
                        touchdownVolume * Mathf.Lerp(0.45f, 1f, hardness);
                    touchdownSource.Play();
                }
            }
            previousLandingQuality = quality;
        }

        private void UpdateAudioCues()
        {
            float airspeed = flightPhysics == null
                ? 0f
                : flightPhysics.AirspeedKnots;
            bool grounded = flightPhysics == null ||
                            flightPhysics.State !=
                            FlightPhysicsController.AircraftState.Airborne;
            float groundSpeed = flightPhysics == null
                ? 0f
                : flightPhysics.HorizontalGroundSpeed * 1.943844f;
            float windVolume = audioCues
                ? Mathf.Max(0f, windVolumeCurve.Evaluate(airspeed)) *
                  windVolumeMultiplier
                : 0f;
            if (windSource != null)
            {
                float targetPitch = Mathf.Lerp(
                    0.72f, 1.28f, AirflowIntensity);
                float pitchBlend =
                    1f - Mathf.Exp(-audioResponseSpeed *
                                   Time.unscaledDeltaTime);
                windSource.pitch = Mathf.Lerp(
                    windSource.pitch, targetPitch, pitchBlend);
            }
            float rumbleVolume = audioCues && grounded
                ? groundRumbleVolume * Mathf.InverseLerp(2f, 35f, groundSpeed)
                : 0f;
            bool stallWarning =
                audioCues && stall != null && stall.StallWarning;
            UpdateAudio(windVolume, rumbleVolume, stallWarning);
        }

        private void UpdateAudio(
            float windVolume,
            float rumbleVolume,
            bool stallWarning)
        {
            float blend =
                1f - Mathf.Exp(-audioResponseSpeed * Time.unscaledDeltaTime);
            if (windSource != null)
                windSource.volume =
                    Mathf.Lerp(windSource.volume, windVolume, blend);
            if (rumbleSource != null)
                rumbleSource.volume =
                    Mathf.Lerp(rumbleSource.volume, rumbleVolume, blend);
            if (stallSource != null)
                stallSource.volume = Mathf.Lerp(
                    stallSource.volume,
                    stallWarning ? stallWarningVolume : 0f,
                    blend);
        }

        private void ConfigureAudio()
        {
            if (audioConfigured) return;
            audioConfigured = true;
            windSource = CreateLoopSource(
                "Awareness Wind", CreateNoiseLoop("Wind Noise", 0.035f, 0.55f));
            rumbleSource = CreateLoopSource(
                "Awareness Ground Rumble",
                CreateRumbleLoop());
            stallSource = CreateLoopSource(
                "Awareness Stall Tone",
                CreateToneLoop("Stall Warning Tone", 440f, 3f));

            GameObject touchdownObject = new GameObject("Awareness Touchdown");
            touchdownObject.transform.SetParent(transform, false);
            touchdownSource = touchdownObject.AddComponent<AudioSource>();
            touchdownSource.playOnAwake = false;
            touchdownSource.spatialBlend = 0.15f;
            touchdownSource.clip = CreateTouchdownClip();
        }

        private AudioSource CreateLoopSource(string name, AudioClip clip)
        {
            GameObject audioObject = new GameObject(name);
            audioObject.transform.SetParent(transform, false);
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
            return source;
        }

        private static AudioClip CreateNoiseLoop(
            string name,
            float amplitude,
            float smoothing)
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(8127);
            float value = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                value = Mathf.Lerp(noise, value, smoothing);
                samples[i] = value * amplitude;
            }
            AudioClip clip = AudioClip.Create(
                name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateRumbleLoop()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float rumble =
                    Mathf.Sin(t * Mathf.PI * 2f * 31f) * 0.55f +
                    Mathf.Sin(t * Mathf.PI * 2f * 47f) * 0.28f;
                samples[i] = rumble * 0.08f;
            }
            AudioClip clip = AudioClip.Create(
                "Runway Rumble", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateToneLoop(
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
                    Mathf.Sin(t * Mathf.PI * 2f * pulsesPerSecond) > -0.2f
                        ? 1f
                        : 0f;
                samples[i] =
                    Mathf.Sin(t * Mathf.PI * 2f * frequency) *
                    pulse * 0.16f;
            }
            AudioClip clip = AudioClip.Create(
                name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
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
                float envelope = Mathf.Exp(-t * 13f);
                float thump = Mathf.Sin(t * Mathf.PI * 2f * 72f) * 0.75f;
                float texture = (float)(random.NextDouble() * 2.0 - 1.0) * 0.2f;
                samples[i] = (thump + texture) * envelope * 0.5f;
            }
            AudioClip clip = AudioClip.Create(
                "Touchdown Thump", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void SetAssistEnabled(bool enabled)
        {
            pilotAwarenessAssist = enabled;
        }

        public void SetMotionCueStrength(float strength)
        {
            motionCuesStrength = Mathf.Clamp(strength, 0f, 2f);
        }

        public void SetWindVolumeMultiplier(float value)
        {
            windVolumeMultiplier = Mathf.Clamp01(value);
        }

        public void SetAudioCuesEnabled(bool enabled)
        {
            audioCues = enabled;
        }
    }
}
