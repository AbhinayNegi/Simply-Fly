using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FlightTelemetryRecorder : MonoBehaviour
    {
        [Header("Aircraft systems")]
        [SerializeField] private AircraftInput input;
        [SerializeField] private EngineController engine;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private GroundMovementController groundMovement;
        [SerializeField] private BrakeController brakes;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private AssistedTrimController trim;
        [SerializeField] private FlightHoldAssistController holdAssist;
        [SerializeField] private FlapController flaps;
        [SerializeField] private StallController stall;
        [SerializeField] private ApproachGuidanceController approach;
        [SerializeField] private LandingController landing;
        [SerializeField] private RudderController rudder;
        [SerializeField] private AirportLocationManager locationManager;
        [SerializeField] private NavigationManager navigation;
        [SerializeField] private NavigationUI navigationUI;
        [SerializeField] private ApproachTrajectoryVisualizer trajectoryVisualizer;
        [SerializeField] private WindController wind;

        [Header("Recording")]
        [Min(0.02f)] [SerializeField] private float logInterval = 0.25f;
        [SerializeField] private Key manualRecordKey = Key.F8;

        public event Action ExportCompleted;
        public bool IsRecording { get; private set; }
        public bool IsAutomatedRun { get; private set; }
        public string ScenarioName { get; private set; } = string.Empty;
        public float ElapsedTime { get; private set; }
        public string LastCsvPath { get; private set; } = string.Empty;
        public string LastJsonPath { get; private set; } = string.Empty;
        public string LastResult { get; private set; } = string.Empty;
        public IReadOnlyList<FlightTelemetrySample> Samples => samples;
        public float LogInterval => logInterval;

        private readonly List<FlightTelemetrySample> samples =
            new List<FlightTelemetrySample>(512);
        private Rigidbody body;
        private float sampleTimer;
        private string startTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ResolveReferences();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                keyboard[manualRecordKey].wasPressedThisFrame)
            {
                if (IsRecording && !IsAutomatedRun)
                    StopRecording(
                        "MANUAL",
                        null,
                        Array.Empty<string>());
                else
                {
                    if (!IsRecording)
                        StartRecording("Manual Flight", false);
                }
            }

            if (!IsRecording) return;
            ElapsedTime += Time.unscaledDeltaTime;
            sampleTimer += Time.unscaledDeltaTime;
            while (sampleTimer >= logInterval)
            {
                sampleTimer -= logInterval;
                CaptureSample();
            }
        }

        public void StartRecording(string scenarioName, bool automated)
        {
            if (IsRecording)
                StopRecording(
                    "WARNING",
                    null,
                    new[] { "Recording replaced by a new test run." });
            samples.Clear();
            ScenarioName = string.IsNullOrWhiteSpace(scenarioName)
                ? "Unnamed Test"
                : scenarioName;
            IsAutomatedRun = automated;
            IsRecording = true;
            ElapsedTime = 0f;
            sampleTimer = 0f;
            startTime = DateTime.Now.ToString("O");
            LastResult = string.Empty;
            CaptureSample();
        }

        public FlightTestSummary StopRecording(
            string result,
            FlightTestMetric[] metrics,
            string[] warnings)
        {
            if (!IsRecording) return null;
            CaptureSample();
            IsRecording = false;
            IsAutomatedRun = false;
            LastResult = result;
            FlightTestSummary summary =
                BuildSummary(result, metrics, warnings);
            FlightTestExporter.Export(
                samples, summary, out string csvPath, out string jsonPath);
            LastCsvPath = csvPath;
            LastJsonPath = jsonPath;
            Debug.Log(
                $"Simply Fly flight test exported:\nCSV: {csvPath}\nJSON: {jsonPath}");
            ExportCompleted?.Invoke();
            return summary;
        }

        public void CaptureNow()
        {
            if (IsRecording) CaptureSample();
        }

        private void CaptureSample()
        {
            if (body == null) return;
            FlightTelemetrySample sample = new FlightTelemetrySample
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                elapsedTime = ElapsedTime,
                scenario = ScenarioName,
                position = body.position,
                velocity = body.linearVelocity,
                flightState = flightPhysics == null
                    ? "Unknown"
                    : flightPhysics.State.ToString(),
                engineState = engine == null
                    ? "Unknown"
                    : engine.State.ToString(),
                grounded =
                    (flightPhysics != null &&
                     flightPhysics.IsPhysicallyGrounded) ||
                    (groundMovement != null && groundMovement.IsGrounded),
                throttlePercent =
                    (throttle == null ? 0f : throttle.Throttle) * 100f,
                engineRPM = engine == null ? 0f : engine.CurrentRPM,
                thrustForce = groundMovement == null
                    ? 0f
                    : groundMovement.AppliedThrust,
                airspeedKnots =
                    flightPhysics == null ? 0f : flightPhysics.AirspeedKnots,
                groundSpeedKnots = flightPhysics == null
                    ? groundMovement == null ? 0f :
                        groundMovement.GroundSpeedKnots
                    : flightPhysics.HorizontalGroundSpeed * 1.943844f,
                forwardVelocity = flightPhysics == null
                    ? rudder == null ? 0f : rudder.LocalForwardVelocity
                    : rudder == null
                        ? flightPhysics.ForwardVelocity
                        : rudder.LocalForwardVelocity,
                lateralVelocity = flightPhysics == null
                    ? rudder == null ? 0f : rudder.LocalLateralVelocity
                    : rudder == null
                        ? flightPhysics.LateralVelocity
                        : rudder.LocalLateralVelocity,
                pitchAngle =
                    flightPhysics == null ? 0f : flightPhysics.PitchAngle,
                bankAngle =
                    flightPhysics == null ? 0f : flightPhysics.BankAngle,
                heading =
                    flightPhysics == null ? 0f : flightPhysics.Heading,
                pitchRate =
                    flightPhysics == null ? 0f :
                        flightPhysics.PitchRateDegrees,
                rollRate =
                    flightPhysics == null ? 0f : flightPhysics.RollRate,
                yawRate =
                    rudder == null
                        ? flightPhysics == null
                            ? 0f
                            : flightPhysics.YawRate * Mathf.Rad2Deg
                        : rudder.YawRateDegrees,
                altitude =
                    flightPhysics == null ? body.position.y :
                        flightPhysics.Altitude,
                altitudeAboveRunway = approach == null
                    ? body.position.y
                    : approach.AltitudeAboveRunway,
                verticalSpeed = flightPhysics == null
                    ? body.linearVelocity.y
                    : flightPhysics.VerticalSpeed,
                flightPathAngle = flightPhysics == null
                    ? 0f
                    : flightPhysics.FlightPathAngle,
                angleOfAttack = flightPhysics == null
                    ? 0f
                    : flightPhysics.AngleOfAttack,
                liftForce =
                    flightPhysics == null ? 0f : flightPhysics.LiftForce,
                liftPercentWeight = flightPhysics == null
                    ? 0f
                    : flightPhysics.LiftPercentOfWeight,
                verticalLiftPercent = flightPhysics == null
                    ? 0f
                    : flightPhysics.VerticalLiftPercentOfWeight,
                sideLiftForce =
                    flightPhysics == null ? 0f :
                        flightPhysics.SideLiftForce,
                dragForce =
                    flightPhysics == null ? 0f : flightPhysics.DragForce,
                rollingResistance = groundMovement == null
                    ? 0f
                    : groundMovement.RollingResistanceForce,
                pitchInput = input == null ? 0f : input.ElevatorInput,
                rollInput = input == null ? 0f : input.AileronInput,
                steeringInput = input == null ? 0f : input.SteeringInput,
                rudderInput = rudder == null
                    ? input == null ? 0f : input.RudderInput
                    : rudder.RudderInput,
                slipAmount = rudder == null ? 0f : rudder.SlipAmount,
                slipBallPosition =
                    rudder == null ? 0f : rudder.SlipBallPosition,
                brakeInput = brakes == null ? 0f : brakes.BrakeAmount,
                trimActive = trim != null && trim.TrimActive,
                trimValue =
                    trim == null ? 0f : trim.CurrentTrimOffset,
                trimEnabled = trim != null && trim.TrimEnabled,
                pitchHoldEnabled =
                    holdAssist != null && holdAssist.PitchHoldEnabled,
                pitchHoldActive =
                    holdAssist != null && holdAssist.PitchHoldActive,
                pitchHoldTargetPitch =
                    holdAssist == null ? 0f : holdAssist.TargetPitch,
                pitchHoldElevatorCommand =
                    holdAssist == null ? 0f :
                        holdAssist.PitchHoldElevatorCommand,
                vsHoldEnabled =
                    holdAssist != null && holdAssist.VerticalSpeedHoldEnabled,
                vsHoldActive =
                    holdAssist != null && holdAssist.VerticalSpeedHoldActive,
                vsHoldTargetVerticalSpeed =
                    holdAssist == null ? 0f :
                        holdAssist.TargetVerticalSpeed,
                vsHoldElevatorCommand =
                    holdAssist == null ? 0f :
                        holdAssist.VerticalSpeedHoldElevatorCommand,
                activeAssistMode =
                    holdAssist == null ? "None" :
                        holdAssist.ActiveMode.ToString(),
                assistDisengageReason =
                    holdAssist == null ? string.Empty :
                        holdAssist.DisengageReason,
                flapAngle =
                    flaps == null ? 0f : flaps.ActualAngle,
                stallState =
                    stall == null ? "Unknown" : stall.State.ToString(),
                stallWarning = stall != null && stall.StallWarning,
                currentStallSpeed =
                    stall == null ? 0f : stall.EffectiveStallSpeed,
                controlAuthorityMultiplier = stall == null
                    ? 1f
                    : stall.ControlAuthorityMultiplier,
                stallLiftMultiplier =
                    stall == null ? 1f : stall.LiftMultiplier,
                airport =
                    locationManager?.CurrentAirport?.airportName ?? "Unknown",
                distanceToThreshold = approach == null
                    ? 0f
                    : approach.DistanceToThreshold,
                runwayAlignmentError = approach == null
                    ? 0f
                    : approach.AlignmentError,
                centerlineOffset =
                    approach == null ? 0f : approach.LateralOffset,
                glideSlopeAngle =
                    approach == null ? 0f : approach.GlideSlopeAngle,
                landingState =
                    landing == null ? "Unknown" : landing.Phase.ToString(),
                mainWheelsTouching =
                    landing != null && landing.MainWheelsTouching,
                noseWheelTouching =
                    landing != null && landing.NoseWheelTouching,
                allWheelsTouching =
                    landing != null && landing.AllWheelsTouching,
                touchdownVerticalSpeed = landing == null
                    ? 0f
                    : landing.TouchdownVerticalSpeed,
                touchdownAirspeed = landing == null
                    ? 0f
                    : landing.TouchdownAirspeed * 1.943844f,
                touchdownPitch =
                    landing == null ? 0f : landing.TouchdownPitch,
                touchdownBank =
                    landing == null ? 0f : landing.TouchdownBank,
                landingQuality =
                    landing == null ? "None" : landing.Quality.ToString(),
                mainWheelsFirst =
                    landing != null && landing.MainWheelsTouchedFirst,
                noseFirstWarning =
                    landing != null && landing.NoseWheelFirstWarning,
                bounceVelocity =
                    landing == null ? 0f : landing.BounceVelocity,
                navigationActive =
                    navigation != null && navigation.IsActive,
                navigationPhase =
                    navigation == null ? "Inactive" : navigation.Phase.ToString(),
                departureAirport =
                    navigation?.DepartureAirport?.airportName ?? string.Empty,
                arrivalAirport =
                    navigation?.ArrivalAirport?.airportName ?? string.Empty,
                nextWaypoint =
                    navigation?.CurrentWaypoint?.name ?? string.Empty,
                navigationDistance =
                    navigation == null ? 0f : navigation.DistanceToWaypoint,
                desiredHeading =
                    navigation == null ? 0f : navigation.DesiredHeading,
                navigationHeadingError =
                    navigation == null ? 0f : navigation.HeadingError,
                routeProgressPercent =
                    navigation == null ? 0f : navigation.RouteProgress * 100f,
                navigationRunwayHeading =
                    navigation == null ? 0f : navigation.RunwayHeading,
                navigationThresholdDistance =
                    navigation == null ? 0f : navigation.DistanceToThreshold,
                navigationLateralOffset =
                    navigation == null ? 0f : navigation.LateralOffset,
                navigationAlignmentError =
                    navigation == null ? 0f : navigation.AlignmentError,
                targetGlideSlopeAngle =
                    navigation == null ? 0f : navigation.GlideSlopeAngle,
                targetGlideSlopeAltitude =
                    navigation == null ? 0f :
                        navigation.TargetGlideSlopeAltitude,
                glideSlopeAltitudeError =
                    navigation == null ? 0f :
                        navigation.GlideSlopeAltitudeError,
                approachHelperActive =
                    navigationUI != null && navigationUI.ApproachHelperActive,
                sideViewHelperStatus =
                    navigationUI?.SideViewStatus ?? "HIDDEN",
                topDownHelperStatus =
                    navigationUI?.TopDownStatus ?? "HIDDEN",
                approachValidationEnabled =
                    navigation != null &&
                    navigation.ApproachValidationEnabled,
                selectedArrivalRunway =
                    navigation?.SelectedArrivalRunway ?? string.Empty,
                approachClear =
                    navigation == null || navigation.ApproachClear,
                approachBlockedReason =
                    navigation?.ApproachBlockedReason ?? string.Empty,
                blockingObjectName =
                    navigation?.BlockingObjectName ?? string.Empty,
                blockingObjectDistanceFromThreshold =
                    navigation == null ? 0f :
                        navigation.BlockingObjectDistanceFromThreshold,
                terrainClearanceMin =
                    navigation == null ? 0f :
                        navigation.TerrainClearanceMin,
                approachCorridorWidth =
                    navigation == null ? 0f :
                        navigation.SelectedApproachCorridorWidth,
                finalApproachFixDistance =
                    navigation == null ? 0f :
                        navigation.FinalApproachFixDistance,
                selectedGlideSlopeDegrees =
                    navigation == null ? 0f :
                        navigation.SelectedGlideSlopeDegrees,
                touchdownTargetDistanceFromThreshold =
                    navigation == null ? 0f :
                        navigation.TouchdownTargetDistanceFromThreshold,
                distanceToTouchdownTarget =
                    navigation == null ? 0f :
                        navigation.DistanceToTouchdownTarget,
                thresholdCrossingHeight =
                    navigation == null ? 0f :
                        navigation.ThresholdCrossingHeight,
                sideViewZoomLevel =
                    navigationUI == null ? 1f :
                        navigationUI.SideViewZoomLevel,
                sideViewMode =
                    navigationUI?.SideViewMode ?? "HIDDEN",
                currentTargetAltitude =
                    navigation == null ? 0f :
                        navigation.CurrentTargetAltitude,
                altitudeReferenceType =
                    navigation == null ? "MSL" :
                        navigation.CurrentAltitudeReference.ToString(),
                currentAltitudeMSL =
                    navigation == null ? body.position.y :
                        navigation.CurrentAltitude,
                currentAltitudeAGL =
                    navigation == null ? 0f :
                        navigation.CurrentAltitudeAGL,
                navigationAltitudeError =
                    navigation == null ? 0f :
                        navigation.AltitudeError,
                verticalNavigationPhase =
                    navigation?.VerticalNavigationPhase ?? "INACTIVE",
                suggestedVerticalSpeed =
                    navigation == null ? 0f :
                        navigation.SuggestedVerticalSpeed,
                routeProfileDistance =
                    navigation == null ? 0f :
                        navigation.RouteProfileDistance,
                routeProfileTargetAltitude =
                    navigation == null ? 0f :
                        navigation.RouteProfileTargetAltitude,
                terrainClearanceBelowAircraft =
                    navigation == null ? 0f :
                        navigation.TerrainClearanceBelowAircraft,
                minimumTerrainClearanceCurrentSegment =
                    navigation == null ? 0f :
                        navigation.CurrentSegmentMinimumTerrainClearance,
                approachGuidanceMode =
                    navigationUI == null
                        ? "Unknown"
                        : navigationUI.GuidanceMode.ToString(),
                trajectoryVisualizerActive =
                    trajectoryVisualizer != null && trajectoryVisualizer.Active,
                predictedImpactValid =
                    trajectoryVisualizer != null &&
                    trajectoryVisualizer.PredictedImpactValid,
                predictedImpactWorldPosition =
                    trajectoryVisualizer == null
                        ? Vector3.zero
                        : trajectoryVisualizer.PredictedImpactWorldPosition,
                predictedImpactDistanceToThreshold =
                    trajectoryVisualizer == null
                        ? 0f
                        : trajectoryVisualizer.PredictedImpactDistanceToThreshold,
                predictedImpactDistanceFromCenterline =
                    trajectoryVisualizer == null
                        ? 0f
                        : trajectoryVisualizer.PredictedImpactDistanceFromCenterline,
                predictedImpactStatus =
                    trajectoryVisualizer == null
                        ? "Unknown"
                        : trajectoryVisualizer.Status.ToString(),
                predictionTimeToImpact =
                    trajectoryVisualizer == null
                        ? 0f
                        : trajectoryVisualizer.PredictionTimeToImpact,
                trajectoryPredictionSeconds =
                    trajectoryVisualizer == null
                        ? 0f
                        : trajectoryVisualizer.PredictionSeconds,
                currentFlightPathAngle =
                    trajectoryVisualizer == null
                        ? flightPhysics == null ? 0f : flightPhysics.FlightPathAngle
                        : trajectoryVisualizer.CurrentFlightPathAngle,
                currentGroundTrack =
                    trajectoryVisualizer == null
                        ? 0f
                        : trajectoryVisualizer.CurrentGroundTrack,
                currentVerticalSpeed =
                    trajectoryVisualizer == null
                        ? flightPhysics == null ? body.linearVelocity.y : flightPhysics.VerticalSpeed
                        : trajectoryVisualizer.CurrentVerticalSpeed,
                windFromHeading = wind == null ? 0f : wind.WindFromHeading,
                windSpeedKnots = wind == null ? 0f : wind.WindSpeedKnots,
                headwindKnots = wind == null ? 0f : wind.HeadwindKnots,
                crosswindKnots = wind == null ? 0f : wind.CrosswindKnots
            };
            samples.Add(sample);
        }

        private FlightTestSummary BuildSummary(
            string result,
            FlightTestMetric[] metrics,
            string[] warnings)
        {
            FlightTestSummary summary = new FlightTestSummary
            {
                scenarioName = ScenarioName,
                startTime = startTime,
                duration = ElapsedTime,
                result = result,
                sampleCount = samples.Count,
                keyMetrics = metrics ?? Array.Empty<FlightTestMetric>(),
                warnings = warnings ?? Array.Empty<string>()
            };
            if (samples.Count == 0) return summary;
            FlightTelemetrySample finalSample = samples[samples.Count - 1];
            summary.landingQuality = finalSample.landingQuality;
            summary.mainWheelsFirst = finalSample.mainWheelsFirst;
            summary.noseFirstWarning = finalSample.noseFirstWarning;

            float sumAir = 0f;
            float sumGround = 0f;
            float sumVertical = 0f;
            summary.minimumAirspeed = float.MaxValue;
            summary.maximumAirspeed = float.MinValue;
            summary.minimumGroundSpeed = float.MaxValue;
            summary.maximumGroundSpeed = float.MinValue;
            summary.minimumVerticalSpeed = float.MaxValue;
            summary.maximumVerticalSpeed = float.MinValue;
            summary.minimumAltitude = float.MaxValue;
            summary.maximumAltitude = float.MinValue;
            foreach (FlightTelemetrySample sample in samples)
            {
                sumAir += sample.airspeedKnots;
                sumGround += sample.groundSpeedKnots;
                sumVertical += sample.verticalSpeed;
                summary.minimumAirspeed =
                    Mathf.Min(summary.minimumAirspeed, sample.airspeedKnots);
                summary.maximumAirspeed =
                    Mathf.Max(summary.maximumAirspeed, sample.airspeedKnots);
                summary.minimumGroundSpeed =
                    Mathf.Min(summary.minimumGroundSpeed, sample.groundSpeedKnots);
                summary.maximumGroundSpeed =
                    Mathf.Max(summary.maximumGroundSpeed, sample.groundSpeedKnots);
                summary.minimumVerticalSpeed =
                    Mathf.Min(summary.minimumVerticalSpeed, sample.verticalSpeed);
                summary.maximumVerticalSpeed =
                    Mathf.Max(summary.maximumVerticalSpeed, sample.verticalSpeed);
                summary.minimumAltitude =
                    Mathf.Min(summary.minimumAltitude, sample.altitude);
                summary.maximumAltitude =
                    Mathf.Max(summary.maximumAltitude, sample.altitude);
            }
            summary.averageAirspeed = sumAir / samples.Count;
            summary.averageGroundSpeed = sumGround / samples.Count;
            summary.averageVerticalSpeed = sumVertical / samples.Count;
            return summary;
        }

        private void ResolveReferences()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (engine == null) engine = GetComponent<EngineController>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            if (groundMovement == null)
                groundMovement = GetComponent<GroundMovementController>();
            if (brakes == null) brakes = GetComponent<BrakeController>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (trim == null) trim = GetComponent<AssistedTrimController>();
            if (holdAssist == null)
                holdAssist = GetComponent<FlightHoldAssistController>();
            if (flaps == null) flaps = GetComponent<FlapController>();
            if (stall == null) stall = GetComponent<StallController>();
            if (approach == null)
                approach = GetComponent<ApproachGuidanceController>();
            if (landing == null) landing = GetComponent<LandingController>();
            if (locationManager == null)
                locationManager = FindAnyObjectByType<AirportLocationManager>();
            if (rudder == null) rudder = GetComponent<RudderController>();
            if (navigation == null)
                navigation = FindAnyObjectByType<NavigationManager>();
            if (navigationUI == null)
                navigationUI = FindAnyObjectByType<NavigationUI>();
            if (trajectoryVisualizer == null)
                trajectoryVisualizer =
                    GetComponent<ApproachTrajectoryVisualizer>() ??
                    FindAnyObjectByType<ApproachTrajectoryVisualizer>();
            if (wind == null)
                wind = FindAnyObjectByType<WindController>();
        }
    }
}
