using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FlightTestRunner : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private AircraftInput input;
        [SerializeField] private EngineController engine;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private FlapController flaps;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private ApproachGuidanceController approach;
        [SerializeField] private LandingController landing;
        [SerializeField] private StallController stall;
        [SerializeField] private AircraftResetController resetController;
        [SerializeField] private AirportLocationManager locationManager;
        [SerializeField] private FlightTelemetryRecorder recorder;

        [Header("Controls")]
        [SerializeField] private Key previousScenarioKey = Key.F6;
        [SerializeField] private Key nextScenarioKey = Key.F7;
        [Tooltip("F9 is reserved for Wind Control.")]
        [SerializeField] private Key runScenarioKey = Key.F12;

        [Header("Pass thresholds")]
        [SerializeField] private Vector2 idleCreepRange = new Vector2(2f, 5f);
        [SerializeField] private Vector2 taxiSpeedRange = new Vector2(8f, 15f);
        [SerializeField] private Vector2 takeoffSpeedRange = new Vector2(55f, 70f);
        [SerializeField] private Vector2 takeoffPitchRange = new Vector2(5f, 12f);
        [SerializeField] private Vector2 stallWarningSpeedRange = new Vector2(38f, 45f);
        [SerializeField] private Vector2 stallSpeedRange = new Vector2(35f, 40f);
        [SerializeField] private float maximumTurnLateralVelocity = 8f;
        [SerializeField] private float maximumTurnDescentRate = 5f;
        [SerializeField] private Vector2 approachSpeedRange = new Vector2(50f, 60f);
        [SerializeField] private Vector2 approachVerticalSpeedRange = new Vector2(-3f, -1f);
        [SerializeField] private float maximumApproachAlignmentError = 6f;
        [SerializeField] private float maximumApproachOffset = 20f;
        [SerializeField] private Vector2 landingSpeedRange = new Vector2(45f, 55f);
        [SerializeField] private float maximumLandingSinkRate = 3f;
        [SerializeField] private float maximumTouchdownBank = 5f;
        [SerializeField] private Vector2 brakeStopTimeRange =
            new Vector2(5f, 8f);
        [SerializeField] private Vector2 brakeStopDistanceRange =
            new Vector2(60f, 120f);

        public event Action ScenarioChanged;
        public event Action ScenarioFinished;
        public FlightTestScenario[] Scenarios { get; private set; }
        public int SelectedScenarioIndex { get; private set; }
        public FlightTestScenario SelectedScenario =>
            Scenarios == null || Scenarios.Length == 0
                ? null
                : Scenarios[Mathf.Clamp(
                    SelectedScenarioIndex, 0, Scenarios.Length - 1)];
        public bool IsRunning { get; private set; }
        public float TestTime { get; private set; }
        public string LastResult { get; private set; } = string.Empty;

        private Rigidbody body;
        private Vector3 startPosition;
        private float startAltitude;
        private bool stallWarningSeen;
        private bool stallSeen;
        private bool recoveryStarted;
        private float warningSpeed;
        private float warningAoA;
        private float stallSpeed;
        private float stallAoA;
        private float recoveryStartTime;
        private float touchdownTime = -1f;
        private float lastCleanLiftoffSpeed = -1f;
        private float lastCleanLiftoffDistance = -1f;

        private void Awake()
        {
            // Existing scenes serialized the old F9 value. Migrate it at runtime
            // so opening Wind Control can never start/reset an automated test.
            if (runScenarioKey == Key.F9) runScenarioKey = Key.F12;
            body = GetComponent<Rigidbody>();
            ResolveReferences();
            Scenarios = BuildScenarios();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (!IsRunning &&
                    keyboard[previousScenarioKey].wasPressedThisFrame)
                    SelectPreviousScenario();
                if (!IsRunning &&
                    keyboard[nextScenarioKey].wasPressedThisFrame)
                    SelectNextScenario();
                if (keyboard[runScenarioKey].wasPressedThisFrame)
                {
                    if (IsRunning) StopScenarioEarly();
                    else RunSelectedScenario();
                }
            }

            if (!IsRunning || SelectedScenario == null) return;
            TestTime += Time.deltaTime;
            RunScenarioControls(SelectedScenario.type);
            if (ShouldEndScenario(SelectedScenario))
                FinishScenario();
        }

        public void SelectPreviousScenario()
        {
            if (Scenarios == null || Scenarios.Length == 0) return;
            SelectedScenarioIndex =
                (SelectedScenarioIndex - 1 + Scenarios.Length) %
                Scenarios.Length;
            ScenarioChanged?.Invoke();
        }

        public void SelectNextScenario()
        {
            if (Scenarios == null || Scenarios.Length == 0) return;
            SelectedScenarioIndex =
                (SelectedScenarioIndex + 1) % Scenarios.Length;
            ScenarioChanged?.Invoke();
        }

        public void SelectScenario(int index)
        {
            if (IsRunning || Scenarios == null || Scenarios.Length == 0) return;
            SelectedScenarioIndex = Mathf.Clamp(index, 0, Scenarios.Length - 1);
            ScenarioChanged?.Invoke();
        }

        public void RunSelectedScenario()
        {
            if (IsRunning || SelectedScenario == null || recorder == null) return;
            ResetRunState();
            SetupScenario(SelectedScenario.type);
            startPosition = body.position;
            startAltitude = body.position.y;
            IsRunning = true;
            recorder.StartRecording(SelectedScenario.name, true);
        }

        public void StopScenarioEarly()
        {
            if (!IsRunning) return;
            input?.ClearTestControls();
            IsRunning = false;
            LastResult = "WARNING";
            recorder?.StopRecording(
                "WARNING",
                BuildMetrics(SelectedScenario.type),
                new[] { "Scenario stopped early with F12." });
            ScenarioFinished?.Invoke();
        }

        private void SetupScenario(FlightTestScenarioType type)
        {
            input?.ClearTestControls();
            switch (type)
            {
                case FlightTestScenarioType.IdleCreep:
                    SpawnRunway();
                    engine?.SetRunningForTest(0f);
                    throttle?.SetThrottleForTest(0f);
                    break;
                case FlightTestScenarioType.LowThrottleTaxi:
                    SpawnRunway();
                    engine?.SetRunningForTest(0.3f);
                    throttle?.SetThrottleForTest(0.3f);
                    break;
                case FlightTestScenarioType.CleanTakeoff:
                    SpawnRunway();
                    engine?.SetRunningForTest(1f);
                    throttle?.SetThrottleForTest(1f);
                    flaps?.SetFlapsForTest(0f);
                    break;
                case FlightTestScenarioType.Flaps10Takeoff:
                    SpawnRunway();
                    engine?.SetRunningForTest(1f);
                    throttle?.SetThrottleForTest(1f);
                    flaps?.SetFlapsForTest(10f);
                    break;
                case FlightTestScenarioType.Flaps30Stall:
                    resetController?.SpawnAirborneTest();
                    flaps?.SetFlapsForTest(30f);
                    engine?.SetRunningForTest(0.35f);
                    throttle?.SetThrottleForTest(0.35f);
                    break;
                case FlightTestScenarioType.BankedTurn:
                    resetController?.SpawnAirborneTest();
                    engine?.SetRunningForTest(0.68f);
                    throttle?.SetThrottleForTest(0.68f);
                    break;
                case FlightTestScenarioType.ApproachStability:
                case FlightTestScenarioType.Landing:
                    SpawnFinal();
                    flaps?.SetFlapsForTest(
                        type == FlightTestScenarioType.Landing ? 30f : 20f);
                    engine?.SetRunningForTest(0.45f);
                    throttle?.SetThrottleForTest(0.45f);
                    break;
                case FlightTestScenarioType.BrakeStopping:
                    SpawnRunway();
                    engine?.SetRunningForTest(0f);
                    throttle?.SetThrottleForTest(0f);
                    body.linearVelocity = transform.forward * (40f / 1.943844f);
                    break;
            }
            Physics.SyncTransforms();
        }

        private void RunScenarioControls(FlightTestScenarioType type)
        {
            float steering = 0f;
            float elevator = 0f;
            bool brake = false;
            switch (type)
            {
                case FlightTestScenarioType.CleanTakeoff:
                case FlightTestScenarioType.Flaps10Takeoff:
                    if (flightPhysics != null &&
                        flightPhysics.AirspeedKnots >= 52f)
                        elevator = 0.28f;
                    break;
                case FlightTestScenarioType.Flaps30Stall:
                    if (!stallSeen)
                    {
                        elevator = Mathf.Lerp(0.18f, 0.72f,
                            Mathf.Clamp01(TestTime / 20f));
                        if (stall != null && stall.StallWarning &&
                            !stallWarningSeen)
                        {
                            stallWarningSeen = true;
                            warningSpeed = flightPhysics.AirspeedKnots;
                            warningAoA = flightPhysics.AngleOfAttack;
                        }
                        if (stall != null &&
                            stall.State == StallController.StallState.Stalled)
                        {
                            stallSeen = true;
                            recoveryStarted = true;
                            recoveryStartTime = TestTime;
                            stallSpeed = flightPhysics.AirspeedKnots;
                            stallAoA = flightPhysics.AngleOfAttack;
                            throttle?.SetThrottleForTest(1f);
                        }
                    }
                    else
                    {
                        elevator = -0.3f;
                    }
                    break;
                case FlightTestScenarioType.BankedTurn:
                    if (TestTime < 5f &&
                        Mathf.Abs(flightPhysics.BankAngle) < 29f)
                        steering = 0.48f;
                    elevator = flightPhysics.VerticalSpeed < -2f ? 0.1f : 0f;
                    break;
                case FlightTestScenarioType.ApproachStability:
                case FlightTestScenarioType.Landing:
                    steering = CalculateApproachSteering();
                    elevator = CalculateApproachPitch(type);
                    if (type == FlightTestScenarioType.Landing &&
                        landing != null &&
                        landing.Quality != LandingController.LandingQuality.None)
                    {
                        if (touchdownTime < 0f) touchdownTime = TestTime;
                        throttle?.SetThrottleForTest(0f);
                        brake = landing.NoseWheelTouching ||
                                TestTime - touchdownTime > 1.2f;
                    }
                    break;
                case FlightTestScenarioType.BrakeStopping:
                    brake = true;
                    break;
            }
            input?.SetTestControls(0f, steering, elevator, brake);
        }

        private float CalculateApproachSteering()
        {
            if (approach == null) return 0f;
            return Mathf.Clamp(
                -approach.AlignmentError * 0.025f -
                approach.LateralOffset * 0.006f,
                -0.35f,
                0.35f);
        }

        private float CalculateApproachPitch(FlightTestScenarioType type)
        {
            if (flightPhysics == null) return 0f;
            if (type == FlightTestScenarioType.Landing &&
                landing != null && landing.HeightAboveRunway < 9f &&
                landing.Airborne)
                return 0.28f;
            float targetVerticalSpeed = -1.7f;
            float verticalCorrection =
                (targetVerticalSpeed - flightPhysics.VerticalSpeed) * 0.08f;
            float speedCorrection =
                (flightPhysics.AirspeedKnots - 55f) * 0.012f;
            return Mathf.Clamp(
                verticalCorrection + speedCorrection, -0.22f, 0.24f);
        }

        private bool ShouldEndScenario(FlightTestScenario scenario)
        {
            if (TestTime >= scenario.maximumDuration) return true;
            switch (scenario.type)
            {
                case FlightTestScenarioType.CleanTakeoff:
                case FlightTestScenarioType.Flaps10Takeoff:
                    return flightPhysics != null &&
                           flightPhysics.State ==
                           FlightPhysicsController.AircraftState.Airborne;
                case FlightTestScenarioType.Flaps30Stall:
                    return stallSeen && stall != null &&
                           stall.State == StallController.StallState.Normal &&
                           flightPhysics.AirspeedKnots > 50f;
                case FlightTestScenarioType.ApproachStability:
                    return approach != null &&
                           approach.DistanceToThreshold < 120f;
                case FlightTestScenarioType.Landing:
                case FlightTestScenarioType.BrakeStopping:
                    return TestTime > 2f &&
                           flightPhysics.HorizontalGroundSpeed * 1.943844f < 1f;
                default:
                    return false;
            }
        }

        private void FinishScenario()
        {
            input?.ClearTestControls();
            IsRunning = false;
            recorder?.CaptureNow();
            EvaluateScenario(
                SelectedScenario.type,
                out string result,
                out string[] warnings);
            LastResult = result;
            recorder?.StopRecording(
                result, BuildMetrics(SelectedScenario.type), warnings);
            ScenarioFinished?.Invoke();
        }

        private void EvaluateScenario(
            FlightTestScenarioType type,
            out string result,
            out string[] warnings)
        {
            List<string> issues = new List<string>();
            IReadOnlyList<FlightTelemetrySample> data = recorder.Samples;
            float averageGround = Average(data, s => s.groundSpeedKnots);
            float stableGround =
                AverageTail(data, s => s.groundSpeedKnots, 0.4f);
            float averageAir = Average(data, s => s.airspeedKnots);
            float averageVertical = Average(data, s => s.verticalSpeed);
            float averageAlignment =
                AverageAbsolute(data, s => s.runwayAlignmentError);
            float averageOffset =
                AverageAbsolute(data, s => s.centerlineOffset);
            bool pass = false;
            bool completed = true;

            switch (type)
            {
                case FlightTestScenarioType.IdleCreep:
                    pass = InRange(stableGround, idleCreepRange);
                    if (!pass) issues.Add(
                        $"Stable creep {stableGround:0.0} kt is outside target.");
                    break;
                case FlightTestScenarioType.LowThrottleTaxi:
                    pass = InRange(stableGround, taxiSpeedRange);
                    if (!pass) issues.Add(
                        $"Stable taxi speed {stableGround:0.0} kt is outside target.");
                    break;
                case FlightTestScenarioType.CleanTakeoff:
                case FlightTestScenarioType.Flaps10Takeoff:
                    FlightTelemetrySample liftoff = FindFirstAirborne(data);
                    completed = liftoff != null;
                    pass = completed &&
                           InRange(liftoff.airspeedKnots, takeoffSpeedRange) &&
                           InRange(liftoff.pitchAngle, takeoffPitchRange);
                    if (!completed) issues.Add("Aircraft did not lift off.");
                    else if (!pass) issues.Add(
                        $"Liftoff was {liftoff.airspeedKnots:0.0} kt at " +
                        $"{liftoff.pitchAngle:0.0}° pitch.");
                    break;
                case FlightTestScenarioType.Flaps30Stall:
                    completed = stallSeen;
                    pass = stallWarningSeen && stallSeen &&
                           InRange(warningSpeed, stallWarningSpeedRange) &&
                           InRange(stallSpeed, stallSpeedRange);
                    if (!stallWarningSeen) issues.Add("No stall warning detected.");
                    if (!stallSeen) issues.Add("No full stall detected.");
                    break;
                case FlightTestScenarioType.BankedTurn:
                    float maxLateral =
                        MaximumAbsolute(data, s => s.lateralVelocity);
                    float minVertical = Minimum(data, s => s.verticalSpeed);
                    pass = maxLateral <= maximumTurnLateralVelocity &&
                           minVertical >= -maximumTurnDescentRate;
                    if (!pass) issues.Add(
                        $"Turn reached {maxLateral:0.0} m/s lateral and " +
                        $"{minVertical:0.0} m/s vertical.");
                    break;
                case FlightTestScenarioType.ApproachStability:
                    pass = InRange(averageAir, approachSpeedRange) &&
                           InRange(averageVertical, approachVerticalSpeedRange) &&
                           averageAlignment <= maximumApproachAlignmentError &&
                           averageOffset <= maximumApproachOffset;
                    if (!pass) issues.Add("Approach averages exceeded target limits.");
                    break;
                case FlightTestScenarioType.Landing:
                    completed = landing != null &&
                                landing.Quality !=
                                LandingController.LandingQuality.None;
                    if (!completed)
                    {
                        pass = false;
                        issues.Add("No touchdown detected.");
                    }
                    else
                    {
                        bool verticalGood =
                            landing.TouchdownVerticalSpeed >=
                            -maximumLandingSinkRate;
                        bool speedGood = InRange(
                            landing.TouchdownAirspeed * 1.943844f,
                            landingSpeedRange);
                        bool bankGood =
                            Mathf.Abs(landing.TouchdownBank) <=
                            maximumTouchdownBank;
                        pass = landing.MainWheelsTouchedFirst &&
                               verticalGood && speedGood && bankGood;
                        if (!landing.MainWheelsTouchedFirst)
                            issues.Add("Nose wheel touched before the main wheels.");
                        if (!verticalGood)
                            issues.Add("Touchdown vertical speed was too high.");
                        if (!speedGood)
                            issues.Add("Touchdown airspeed was outside target.");
                        if (!bankGood)
                            issues.Add(
                                "Smooth vertical touchdown, but bank angle high.");
                    }
                    break;
                case FlightTestScenarioType.BrakeStopping:
                    completed =
                        flightPhysics.HorizontalGroundSpeed * 1.943844f < 1f;
                    float stoppingDistance =
                        Vector3.Distance(startPosition, body.position);
                    pass = completed &&
                           InRange(TestTime, brakeStopTimeRange) &&
                           InRange(stoppingDistance, brakeStopDistanceRange);
                    if (!completed)
                        issues.Add("Aircraft did not stop.");
                    if (!InRange(TestTime, brakeStopTimeRange))
                        issues.Add(
                            $"Stopping time was {TestTime:0.0} seconds.");
                    if (!InRange(stoppingDistance, brakeStopDistanceRange))
                        issues.Add(
                            $"Stopping distance was {stoppingDistance:0.0} m.");
                    break;
            }

            result = pass ? "PASS" : completed ? "WARNING" : "FAIL";
            warnings = issues.ToArray();
        }

        private FlightTestMetric[] BuildMetrics(FlightTestScenarioType type)
        {
            IReadOnlyList<FlightTelemetrySample> data = recorder.Samples;
            List<FlightTestMetric> metrics = new List<FlightTestMetric>
            {
                Metric("distance_travelled",
                    Vector3.Distance(startPosition, body.position), "m"),
                Metric("max_ground_speed",
                    Maximum(data, s => s.groundSpeedKnots), "kt"),
                Metric("average_ground_speed",
                    Average(data, s => s.groundSpeedKnots), "kt"),
                Metric("max_airspeed",
                    Maximum(data, s => s.airspeedKnots), "kt"),
                Metric("average_airspeed",
                    Average(data, s => s.airspeedKnots), "kt"),
                Metric("average_vertical_speed",
                    Average(data, s => s.verticalSpeed), "m/s"),
                Metric("altitude_change", body.position.y - startAltitude, "m")
            };

            if (type == FlightTestScenarioType.CleanTakeoff ||
                type == FlightTestScenarioType.Flaps10Takeoff)
            {
                metrics.Add(Metric("time_to_30kt",
                    TimeAtSpeed(data, 30f), "s"));
                metrics.Add(Metric("time_to_50kt",
                    TimeAtSpeed(data, 50f), "s"));
                FlightTelemetrySample rotation = FindRotationSample(data);
                FlightTelemetrySample airborne = FindFirstAirborne(data);
                metrics.Add(Metric("rotation_speed",
                    rotation?.airspeedKnots ?? 0f, "kt"));
                metrics.Add(Metric("liftoff_speed",
                    airborne?.airspeedKnots ?? 0f, "kt"));
                metrics.Add(Metric("pitch_at_liftoff",
                    airborne?.pitchAngle ?? 0f, "deg"));
                metrics.Add(Metric("liftoff_distance",
                    airborne == null ? 0f :
                        Vector3.Distance(startPosition, airborne.position), "m"));
                metrics.Add(Metric("max_acceleration",
                    MaximumAcceleration(data), "m/s2"));
                float liftoffSpeedValue = airborne?.airspeedKnots ?? 0f;
                float liftoffDistanceValue = airborne == null
                    ? 0f
                    : Vector3.Distance(startPosition, airborne.position);
                if (type == FlightTestScenarioType.CleanTakeoff &&
                    airborne != null)
                {
                    lastCleanLiftoffSpeed = liftoffSpeedValue;
                    lastCleanLiftoffDistance = liftoffDistanceValue;
                }
                else if (type == FlightTestScenarioType.Flaps10Takeoff &&
                         lastCleanLiftoffSpeed >= 0f)
                {
                    metrics.Add(Metric("vs_clean_liftoff_speed",
                        liftoffSpeedValue - lastCleanLiftoffSpeed, "kt"));
                    metrics.Add(Metric("vs_clean_liftoff_distance",
                        liftoffDistanceValue - lastCleanLiftoffDistance, "m"));
                }
            }
            else if (type == FlightTestScenarioType.Flaps30Stall)
            {
                metrics.Add(Metric("stall_warning_speed", warningSpeed, "kt"));
                metrics.Add(Metric("stall_warning_aoa", warningAoA, "deg"));
                metrics.Add(Metric("stall_speed", stallSpeed, "kt"));
                metrics.Add(Metric("stall_aoa", stallAoA, "deg"));
                metrics.Add(Metric("recovery_time",
                    recoveryStarted ? TestTime - recoveryStartTime : 0f, "s"));
            }
            else if (type == FlightTestScenarioType.LowThrottleTaxi)
            {
                metrics.Add(Metric("stable_taxi_speed",
                    AverageTail(data, s => s.groundSpeedKnots, 0.4f), "kt"));
                metrics.Add(Metric("max_lateral_velocity",
                    MaximumAbsolute(data, s => s.lateralVelocity), "m/s"));
                metrics.Add(Metric("average_acceleration",
                    AverageAcceleration(data), "m/s2"));
            }
            else if (type == FlightTestScenarioType.BankedTurn)
            {
                metrics.Add(Metric("average_bank",
                    AverageAbsolute(data, s => s.bankAngle), "deg"));
                metrics.Add(Metric("average_turn_rate",
                    CalculateAverageHeadingTurnRate(data), "deg/s"));
                metrics.Add(Metric("max_lateral_velocity",
                    MaximumAbsolute(data, s => s.lateralVelocity), "m/s"));
                metrics.Add(Metric("average_forward_velocity",
                    Average(data, s => s.forwardVelocity), "m/s"));
            }
            else if (type == FlightTestScenarioType.ApproachStability)
            {
                metrics.Add(Metric("average_alignment_error",
                    AverageAbsolute(data, s => s.runwayAlignmentError), "deg"));
                metrics.Add(Metric("average_centerline_offset",
                    AverageAbsolute(data, s => s.centerlineOffset), "m"));
                metrics.Add(Metric("average_glide_slope",
                    Average(data, s => s.glideSlopeAngle), "deg"));
            }
            else if (type == FlightTestScenarioType.Landing)
            {
                FlightTelemetrySample touchdownSample = FindTouchdown(data);
                metrics.Add(Metric("touchdown_speed",
                    landing.TouchdownAirspeed * 1.943844f, "kt"));
                metrics.Add(Metric("touchdown_vertical_speed",
                    landing.TouchdownVerticalSpeed, "m/s"));
                metrics.Add(Metric("touchdown_pitch",
                    landing.TouchdownPitch, "deg"));
                metrics.Add(Metric("touchdown_bank",
                    landing.TouchdownBank, "deg"));
                metrics.Add(Metric("rollout_distance",
                    touchdownSample == null ? 0f :
                        Vector3.Distance(
                            touchdownSample.position, body.position), "m"));
            }
            else if (type == FlightTestScenarioType.BrakeStopping)
            {
                metrics.Add(Metric("stopping_time", TestTime, "s"));
                metrics.Add(Metric("max_deceleration",
                    -MinimumAcceleration(data), "m/s2"));
            }
            return metrics.ToArray();
        }

        private void SpawnRunway()
        {
            if (locationManager != null) locationManager.SpawnAtRunway();
            else resetController?.ResetAircraft();
        }

        private void SpawnFinal()
        {
            if (locationManager != null)
                locationManager.SpawnOnFinalApproach();
            else
                resetController?.SpawnFinalApproach();
        }

        private void ResetRunState()
        {
            TestTime = 0f;
            LastResult = string.Empty;
            stallWarningSeen = false;
            stallSeen = false;
            recoveryStarted = false;
            warningSpeed = 0f;
            warningAoA = 0f;
            stallSpeed = 0f;
            stallAoA = 0f;
            recoveryStartTime = 0f;
            touchdownTime = -1f;
        }

        private void ResolveReferences()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (engine == null) engine = GetComponent<EngineController>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            if (flaps == null) flaps = GetComponent<FlapController>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (approach == null)
                approach = GetComponent<ApproachGuidanceController>();
            if (landing == null) landing = GetComponent<LandingController>();
            if (stall == null) stall = GetComponent<StallController>();
            if (resetController == null)
                resetController = GetComponent<AircraftResetController>();
            if (locationManager == null)
                locationManager = FindAnyObjectByType<AirportLocationManager>();
            if (recorder == null)
                recorder = GetComponent<FlightTelemetryRecorder>();
        }

        private static FlightTestScenario[] BuildScenarios() => new[]
        {
            Scenario("1. Idle Creep Test",
                "Engine idle, brakes released for 15 seconds.",
                FlightTestScenarioType.IdleCreep, 15f),
            Scenario("2. Low Throttle Taxi Test",
                "Thirty-percent power taxi for 20 seconds.",
                FlightTestScenarioType.LowThrottleTaxi, 20f),
            Scenario("3. Clean Full-Throttle Takeoff",
                "Clean takeoff roll and automatic gentle rotation.",
                FlightTestScenarioType.CleanTakeoff, 40f),
            Scenario("4. Flaps 10 Takeoff",
                "Takeoff roll with ten degrees of flap.",
                FlightTestScenarioType.Flaps10Takeoff, 40f),
            Scenario("5. Flaps 30 Slow Flight / Stall",
                "Slow flight, stall warning, stall and recovery.",
                FlightTestScenarioType.Flaps30Stall, 40f),
            Scenario("6. Banked Turn Test",
                "Establish approximately thirty degrees of bank.",
                FlightTestScenarioType.BankedTurn, 20f),
            Scenario("7. Approach Stability Test",
                "Automated stabilized final-approach control inputs.",
                FlightTestScenarioType.ApproachStability, 40f),
            Scenario("8. Landing Test",
                "Approach, flare, touchdown and braking rollout.",
                FlightTestScenarioType.Landing, 50f),
            Scenario("9. Brake Stopping Test",
                "Full braking from approximately forty knots.",
                FlightTestScenarioType.BrakeStopping, 20f)
        };

        private static FlightTestScenario Scenario(
            string name,
            string description,
            FlightTestScenarioType type,
            float duration) => new FlightTestScenario
        {
            name = name,
            description = description,
            type = type,
            maximumDuration = duration
        };

        private static FlightTestMetric Metric(
            string name, float value, string unit) =>
            new FlightTestMetric { name = name, value = value, unit = unit };

        private static bool InRange(float value, Vector2 range) =>
            value >= range.x && value <= range.y;

        private static float Average(
            IReadOnlyList<FlightTelemetrySample> data,
            Func<FlightTelemetrySample, float> selector)
        {
            if (data == null || data.Count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < data.Count; i++) sum += selector(data[i]);
            return sum / data.Count;
        }

        private static float AverageAbsolute(
            IReadOnlyList<FlightTelemetrySample> data,
            Func<FlightTelemetrySample, float> selector)
        {
            if (data == null || data.Count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < data.Count; i++)
                sum += Mathf.Abs(selector(data[i]));
            return sum / data.Count;
        }

        private static float AverageTail(
            IReadOnlyList<FlightTelemetrySample> data,
            Func<FlightTelemetrySample, float> selector,
            float fraction)
        {
            if (data == null || data.Count == 0) return 0f;
            int start = Mathf.Clamp(
                Mathf.FloorToInt(data.Count * (1f - fraction)),
                0,
                data.Count - 1);
            float sum = 0f;
            int count = 0;
            for (int i = start; i < data.Count; i++)
            {
                sum += selector(data[i]);
                count++;
            }
            return count == 0 ? 0f : sum / count;
        }

        private static float Maximum(
            IReadOnlyList<FlightTelemetrySample> data,
            Func<FlightTelemetrySample, float> selector)
        {
            if (data == null || data.Count == 0) return 0f;
            float value = float.MinValue;
            for (int i = 0; i < data.Count; i++)
                value = Mathf.Max(value, selector(data[i]));
            return value;
        }

        private static float MaximumAbsolute(
            IReadOnlyList<FlightTelemetrySample> data,
            Func<FlightTelemetrySample, float> selector)
        {
            if (data == null || data.Count == 0) return 0f;
            float value = 0f;
            for (int i = 0; i < data.Count; i++)
                value = Mathf.Max(value, Mathf.Abs(selector(data[i])));
            return value;
        }

        private static float Minimum(
            IReadOnlyList<FlightTelemetrySample> data,
            Func<FlightTelemetrySample, float> selector)
        {
            if (data == null || data.Count == 0) return 0f;
            float value = float.MaxValue;
            for (int i = 0; i < data.Count; i++)
                value = Mathf.Min(value, selector(data[i]));
            return value;
        }

        private static FlightTelemetrySample FindFirstAirborne(
            IReadOnlyList<FlightTelemetrySample> data)
        {
            if (data == null) return null;
            for (int i = 0; i < data.Count; i++)
                if (data[i].flightState == "Airborne") return data[i];
            return null;
        }

        private static FlightTelemetrySample FindRotationSample(
            IReadOnlyList<FlightTelemetrySample> data)
        {
            if (data == null) return null;
            for (int i = 0; i < data.Count; i++)
                if (data[i].pitchInput > 0.1f ||
                    data[i].pitchAngle >= 2f)
                    return data[i];
            return null;
        }

        private static FlightTelemetrySample FindTouchdown(
            IReadOnlyList<FlightTelemetrySample> data)
        {
            if (data == null) return null;
            for (int i = 0; i < data.Count; i++)
                if (data[i].landingQuality != "None" &&
                    !string.IsNullOrWhiteSpace(data[i].landingQuality))
                    return data[i];
            return null;
        }

        private static float TimeAtSpeed(
            IReadOnlyList<FlightTelemetrySample> data,
            float speed)
        {
            if (data == null) return -1f;
            for (int i = 0; i < data.Count; i++)
                if (data[i].airspeedKnots >= speed)
                    return data[i].elapsedTime;
            return -1f;
        }

        private static float MaximumAcceleration(
            IReadOnlyList<FlightTelemetrySample> data)
        {
            float maximum = 0f;
            if (data == null) return maximum;
            for (int i = 1; i < data.Count; i++)
            {
                float dt = data[i].elapsedTime - data[i - 1].elapsedTime;
                if (dt <= 0f) continue;
                float dv =
                    (data[i].groundSpeedKnots -
                     data[i - 1].groundSpeedKnots) / 1.943844f;
                maximum = Mathf.Max(maximum, dv / dt);
            }
            return maximum;
        }

        private static float CalculateAverageHeadingTurnRate(
            IReadOnlyList<FlightTelemetrySample> data)
        {
            // The first sample is captured during reset/setup and can contain a
            // discontinuous heading. Start from sample 1 and unwrap each later
            // heading delta so crossing 359/0 degrees remains continuous.
            if (data == null || data.Count < 3) return 0f;
            const int start = 1;
            float unwrappedHeadingChange = 0f;
            for (int i = start + 1; i < data.Count; i++)
            {
                unwrappedHeadingChange += Mathf.DeltaAngle(
                    data[i - 1].heading,
                    data[i].heading);
            }
            float duration =
                data[data.Count - 1].elapsedTime - data[start].elapsedTime;
            return duration <= 0f
                ? 0f
                : Mathf.Abs(unwrappedHeadingChange / duration);
        }

        private static float AverageAcceleration(
            IReadOnlyList<FlightTelemetrySample> data)
        {
            if (data == null || data.Count < 2) return 0f;
            float dt =
                data[data.Count - 1].elapsedTime - data[0].elapsedTime;
            if (dt <= 0f) return 0f;
            float dv =
                (data[data.Count - 1].groundSpeedKnots -
                 data[0].groundSpeedKnots) / 1.943844f;
            return dv / dt;
        }

        private static float MinimumAcceleration(
            IReadOnlyList<FlightTelemetrySample> data)
        {
            float minimum = 0f;
            if (data == null) return minimum;
            for (int i = 1; i < data.Count; i++)
            {
                float dt = data[i].elapsedTime - data[i - 1].elapsedTime;
                if (dt <= 0f) continue;
                float dv =
                    (data[i].groundSpeedKnots -
                     data[i - 1].groundSpeedKnots) / 1.943844f;
                minimum = Mathf.Min(minimum, dv / dt);
            }
            return minimum;
        }
    }
}
