using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    public enum LessonObjectiveCondition
    {
        EngineRunning,
        ThrottleAtLeast,
        ThrottleBelow,
        GroundSpeedAbove,
        GroundSpeedBelow,
        SteerLeftAndRight,
        RunwayLinedUp,
        MaintainCenterline,
        AirspeedAtLeast,
        PitchRange,
        Airborne,
        SafeClimb,
        AltitudeAtLeast,
        BankLeft,
        MaintainTurn,
        WingsLevel,
        BankRight,
        NoseUpToSlow,
        StallWarning,
        ReleaseBackPressure,
        LowerNose,
        StallRecovered,
        NormalFlight,
        OnFinalApproach,
        FlapsAtLeast,
        FlapsAtMost,
        SustainedPitchInput,
        TrimActive,
        PitchInputReleased,
        TrimNeutral,
        PitchHoldActive,
        VerticalSpeedHoldActive,
        HoldAssistInactive,
        ApproachSpeed,
        ApproachAligned,
        DescentControlled,
        Flare,
        MainWheelsFirst,
        BrakeToStop,
        NavigationLateralAligned,
        NavigationGlideAligned,
        NavigationApproachStable,
        DistanceToTouchdownBelow,
        AutoLandActive,
        AutoLandComplete
    }

    [Serializable]
    public sealed class LessonObjectiveDefinition
    {
        [TextArea] public string instruction;
        [TextArea] public string hint;
        public LessonObjectiveCondition condition;
        public float value;
        public float secondaryValue;
        [Min(0f)] public float holdDuration;
        public string successMessage;
    }

    [Serializable]
    public sealed class FlightLessonDefinition
    {
        public string title;
        [TextArea] public string description;
        public LessonObjectiveDefinition[] objectives;
        [TextArea] public string completionMessage;
    }

    public sealed class FlightLessonSystem : MonoBehaviour
    {
        [Header("Aircraft systems")]
        [SerializeField] private AircraftInput input;
        [SerializeField] private EngineController engine;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private GroundMovementController groundMovement;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private ApproachGuidanceController approach;
        [SerializeField] private LandingController landing;
        [SerializeField] private StallController stall;
        [SerializeField] private FlapController flaps;
        [SerializeField] private AssistedTrimController trim;
        [SerializeField] private FlightHoldAssistController holdAssist;
        [SerializeField] private AircraftResetController resetController;
        [SerializeField] private AirportLocationManager locationManager;
        [SerializeField] private NavigationManager navigation;
        [SerializeField] private AutoLandController autoLand;

        [Header("Lessons")]
        [SerializeField] private FlightLessonDefinition[] lessons;
        [SerializeField] private int startingLesson;
        [Min(0f)] [SerializeField] private float objectiveSuccessPause = 1.1f;
        [SerializeField] private Key skipObjectiveKey = Key.N;
        [SerializeField] private Key restartLessonKey = Key.R;

        public FlightLessonDefinition[] Lessons => lessons;
        public int CurrentLessonIndex { get; private set; }
        public int CurrentObjectiveIndex { get; private set; }
        public FlightLessonDefinition CurrentLesson =>
            lessons == null || lessons.Length == 0
                ? null
                : lessons[Mathf.Clamp(
                    CurrentLessonIndex, 0, lessons.Length - 1)];
        public LessonObjectiveDefinition CurrentObjective =>
            CurrentLesson?.objectives == null ||
            CurrentObjectiveIndex >= CurrentLesson.objectives.Length
                ? null
                : CurrentLesson.objectives[CurrentObjectiveIndex];
        public bool LessonComplete { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public string WarningMessage { get; private set; } = string.Empty;
        public float ObjectiveProgress { get; private set; }
        public bool PitchCoachRequested =>
            CurrentLessonIndex == 1 ||
            CurrentLessonIndex == 3 ||
            CurrentLessonIndex == 4 ||
            CurrentLessonIndex == 6 ||
            CurrentLessonIndex == 7 ||
            CurrentLessonIndex == 8;
        public bool ApproachCuesRequested =>
            CurrentLessonIndex == 4 || CurrentLessonIndex == 7 ||
            CurrentLessonIndex == 8;
        public bool ApproachAidLessonRequested => CurrentLessonIndex == 7;
        public bool StallCoachRequested => CurrentLessonIndex == 3;

        private float conditionTimer;
        private float successPauseTimer;
        private bool leftSteeringSeen;
        private bool rightSteeringSeen;

        private void Awake()
        {
            ResolveReferences();
            if (lessons == null || lessons.Length < 9)
                lessons = BuildDefaultLessons();
            if (!ExperimentalFeatureFlags.AutoLandEnabled &&
                lessons != null && lessons.Length > 8)
            {
                FlightLessonDefinition[] visibleLessons =
                    new FlightLessonDefinition[8];
                Array.Copy(lessons, visibleLessons, visibleLessons.Length);
                lessons = visibleLessons;
            }
            CurrentLessonIndex = Mathf.Clamp(
                startingLesson, 0, lessons.Length - 1);
            BeginObjective();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard[skipObjectiveKey].wasPressedThisFrame)
                    CompleteCurrentObjective(true);
                if (keyboard[restartLessonKey].wasPressedThisFrame)
                    RestartCurrentLesson();
            }

            UpdateWarningMessage();
            if (LessonComplete || CurrentObjective == null) return;
            if (successPauseTimer > 0f)
            {
                successPauseTimer -= Time.unscaledDeltaTime;
                if (successPauseTimer <= 0f) AdvanceObjective();
                return;
            }

            bool conditionMet = EvaluateCurrentObjective();
            float requiredDuration = CurrentObjective.holdDuration;
            if (conditionMet)
                conditionTimer += Time.deltaTime;
            else
                conditionTimer = 0f;
            ObjectiveProgress = requiredDuration <= 0f
                ? conditionMet ? 1f : 0f
                : Mathf.Clamp01(conditionTimer / requiredDuration);
            if (conditionMet && conditionTimer >= requiredDuration)
                CompleteCurrentObjective(false);
        }

        public void SelectLesson(int index)
        {
            if (lessons == null || lessons.Length == 0) return;
            CurrentLessonIndex = Mathf.Clamp(index, 0, lessons.Length - 1);
            RestartCurrentLesson();
        }

        public void RestartCurrentLesson()
        {
            LessonComplete = false;
            CurrentObjectiveIndex = 0;
            StatusMessage = "LESSON RESTARTED";
            ResetObjectiveState();
            PlaceAircraftForLesson();
        }

        private void PlaceAircraftForLesson()
        {
            switch (CurrentLessonIndex)
            {
                case 0:
                    if (locationManager != null)
                        locationManager.SpawnAtRunway();
                    else
                        resetController?.ResetAircraft();
                    break;
                case 1:
                    if (locationManager != null)
                        locationManager.SpawnAtRunway();
                    else
                        resetController?.ResetAircraft();
                    // Takeoff is a standalone lesson whose requested objectives
                    // begin at lineup rather than repeating engine start.
                    engine?.SetRunningForTest(0f);
                    break;
                case 2:
                case 3:
                case 5:
                case 6:
                    resetController?.SpawnAirborneTest();
                    break;
                case 4:
                    if (locationManager != null)
                        locationManager.SpawnOnFinalApproach();
                    else
                        resetController?.SpawnFinalApproach();
                    break;
                case 7:
                    navigation?.StartApproachPractice();
                    break;
                case 8:
                    navigation?.StartApproachPractice(
                        2800f, 55f, 8f,
                        locationManager == null ? 0 :
                        locationManager.SelectedAirportIndex);
                    autoLand?.Disengage("AUTOLAND READY - PRESS U");
                    break;
            }
        }

        private bool EvaluateCurrentObjective()
        {
            LessonObjectiveDefinition objective = CurrentObjective;
            if (objective == null) return false;
            float groundSpeed = groundMovement == null
                ? flightPhysics == null ? 0f :
                    flightPhysics.HorizontalGroundSpeed * 1.943844f
                : groundMovement.GroundSpeedKnots;
            float airspeed =
                flightPhysics == null ? 0f : flightPhysics.AirspeedKnots;

            switch (objective.condition)
            {
                case LessonObjectiveCondition.EngineRunning:
                    return engine != null &&
                           engine.State == EngineController.EngineState.Running;
                case LessonObjectiveCondition.ThrottleAtLeast:
                    return throttle != null &&
                           throttle.Throttle >= objective.value;
                case LessonObjectiveCondition.ThrottleBelow:
                    return throttle != null &&
                           throttle.Throttle <= objective.value;
                case LessonObjectiveCondition.GroundSpeedAbove:
                    return groundSpeed >= objective.value;
                case LessonObjectiveCondition.GroundSpeedBelow:
                    return groundSpeed <= objective.value;
                case LessonObjectiveCondition.SteerLeftAndRight:
                    if (groundSpeed > 2f && input != null)
                    {
                        if (input.SteeringInput < -0.25f)
                            leftSteeringSeen = true;
                        if (input.SteeringInput > 0.25f)
                            rightSteeringSeen = true;
                    }
                    ObjectiveProgress =
                        (leftSteeringSeen ? 0.5f : 0f) +
                        (rightSteeringSeen ? 0.5f : 0f);
                    return leftSteeringSeen && rightSteeringSeen;
                case LessonObjectiveCondition.RunwayLinedUp:
                    return approach != null &&
                           Mathf.Abs(approach.LateralOffset) <= objective.value &&
                           Mathf.Abs(approach.AlignmentError) <=
                           objective.secondaryValue;
                case LessonObjectiveCondition.MaintainCenterline:
                    return approach != null && groundSpeed > 5f &&
                           Mathf.Abs(approach.LateralOffset) <= objective.value &&
                           Mathf.Abs(approach.AlignmentError) <=
                           objective.secondaryValue;
                case LessonObjectiveCondition.AirspeedAtLeast:
                    return airspeed >= objective.value;
                case LessonObjectiveCondition.PitchRange:
                    return flightPhysics != null &&
                           flightPhysics.PitchAngle >= objective.value &&
                           flightPhysics.PitchAngle <= objective.secondaryValue;
                case LessonObjectiveCondition.Airborne:
                    return flightPhysics != null &&
                           flightPhysics.State ==
                           FlightPhysicsController.AircraftState.Airborne;
                case LessonObjectiveCondition.SafeClimb:
                    return flightPhysics != null &&
                           flightPhysics.State ==
                           FlightPhysicsController.AircraftState.Airborne &&
                           airspeed >= objective.value &&
                           flightPhysics.VerticalSpeed >=
                           objective.secondaryValue;
                case LessonObjectiveCondition.AltitudeAtLeast:
                    return approach != null
                        ? approach.AltitudeAboveRunway >= objective.value
                        : flightPhysics != null &&
                          flightPhysics.Altitude >= objective.value;
                case LessonObjectiveCondition.BankLeft:
                    return input != null && flightPhysics != null &&
                           input.AileronInput < -0.1f &&
                           Mathf.Abs(flightPhysics.BankAngle) >= objective.value &&
                           Mathf.Abs(flightPhysics.BankAngle) <=
                           objective.secondaryValue;
                case LessonObjectiveCondition.BankRight:
                    return input != null && flightPhysics != null &&
                           input.AileronInput > 0.1f &&
                           Mathf.Abs(flightPhysics.BankAngle) >= objective.value &&
                           Mathf.Abs(flightPhysics.BankAngle) <=
                           objective.secondaryValue;
                case LessonObjectiveCondition.MaintainTurn:
                    return flightPhysics != null &&
                           Mathf.Abs(flightPhysics.BankAngle) >= objective.value &&
                           Mathf.Abs(flightPhysics.TurnRate) >=
                           objective.secondaryValue;
                case LessonObjectiveCondition.WingsLevel:
                    return flightPhysics != null &&
                           Mathf.Abs(flightPhysics.BankAngle) <= objective.value;
                case LessonObjectiveCondition.NoseUpToSlow:
                    return flightPhysics != null && input != null &&
                           (flightPhysics.PitchAngle >= objective.value ||
                            input.ElevatorInput > 0.35f);
                case LessonObjectiveCondition.StallWarning:
                    return stall != null && stall.StallWarning;
                case LessonObjectiveCondition.ReleaseBackPressure:
                    return input != null && input.ElevatorInput < 0.1f;
                case LessonObjectiveCondition.LowerNose:
                    return flightPhysics != null && input != null &&
                           (flightPhysics.PitchAngle <= objective.value ||
                            input.ElevatorInput < -0.25f);
                case LessonObjectiveCondition.StallRecovered:
                    return stall != null && flightPhysics != null &&
                           stall.State == StallController.StallState.Normal &&
                           airspeed >= objective.value;
                case LessonObjectiveCondition.NormalFlight:
                    return stall != null && flightPhysics != null &&
                           stall.State == StallController.StallState.Normal &&
                           airspeed >= objective.value &&
                           Mathf.Abs(flightPhysics.AngleOfAttack) <=
                           objective.secondaryValue;
                case LessonObjectiveCondition.OnFinalApproach:
                    return flightPhysics != null && approach != null &&
                           flightPhysics.State ==
                           FlightPhysicsController.AircraftState.Airborne &&
                           approach.DistanceToThreshold >= objective.value &&
                           approach.DistanceToThreshold <=
                           objective.secondaryValue;
                case LessonObjectiveCondition.FlapsAtLeast:
                    return flaps != null &&
                           flaps.ActualAngle >= objective.value;
                case LessonObjectiveCondition.FlapsAtMost:
                    return flaps != null &&
                           flaps.ActualAngle <= objective.value;
                case LessonObjectiveCondition.SustainedPitchInput:
                    return input != null &&
                           Mathf.Abs(input.ElevatorInput) >= objective.value;
                case LessonObjectiveCondition.TrimActive:
                    return trim != null && trim.TrimActive;
                case LessonObjectiveCondition.PitchInputReleased:
                    return input != null &&
                           Mathf.Abs(input.ElevatorInput) <= objective.value;
                case LessonObjectiveCondition.TrimNeutral:
                    return trim != null &&
                           Mathf.Abs(trim.CurrentTrimOffset) <= objective.value &&
                           Mathf.Abs(trim.TargetTrimOffset) <= objective.value;
                case LessonObjectiveCondition.PitchHoldActive:
                    return holdAssist != null && holdAssist.PitchHoldActive;
                case LessonObjectiveCondition.VerticalSpeedHoldActive:
                    return holdAssist != null &&
                           holdAssist.VerticalSpeedHoldActive;
                case LessonObjectiveCondition.HoldAssistInactive:
                    return holdAssist != null &&
                           holdAssist.ActiveMode ==
                           FlightHoldAssistController.AssistMode.None;
                case LessonObjectiveCondition.ApproachSpeed:
                    return airspeed >= objective.value &&
                           airspeed <= objective.secondaryValue;
                case LessonObjectiveCondition.ApproachAligned:
                    return approach != null &&
                           Mathf.Abs(approach.LateralOffset) <= objective.value &&
                           Mathf.Abs(approach.AlignmentError) <=
                           objective.secondaryValue;
                case LessonObjectiveCondition.DescentControlled:
                    return flightPhysics != null &&
                           flightPhysics.VerticalSpeed >= objective.value &&
                           flightPhysics.VerticalSpeed <=
                           objective.secondaryValue;
                case LessonObjectiveCondition.Flare:
                    return landing != null && flightPhysics != null &&
                           (landing.FlareActive ||
                            (landing.HeightAboveRunway <= objective.value &&
                             flightPhysics.PitchAngle >=
                             objective.secondaryValue &&
                             flightPhysics.VerticalSpeed < 0f));
                case LessonObjectiveCondition.MainWheelsFirst:
                    return landing != null &&
                           landing.Quality !=
                           LandingController.LandingQuality.None &&
                           landing.MainWheelsTouchedFirst;
                case LessonObjectiveCondition.BrakeToStop:
                    return groundSpeed <= objective.value &&
                           input != null && input.BrakeHeld;
                case LessonObjectiveCondition.NavigationLateralAligned:
                    return navigation != null &&
                           Mathf.Abs(navigation.LateralOffset) <=
                           objective.value &&
                           Mathf.Abs(navigation.AlignmentError) <=
                           objective.secondaryValue;
                case LessonObjectiveCondition.NavigationGlideAligned:
                    return navigation != null &&
                           Mathf.Abs(navigation.GlideSlopeAltitudeError) <=
                           objective.value;
                case LessonObjectiveCondition.NavigationApproachStable:
                    return navigation != null &&
                           Mathf.Abs(navigation.LateralOffset) <=
                           objective.value &&
                           Mathf.Abs(navigation.GlideSlopeAltitudeError) <=
                           objective.secondaryValue;
                case LessonObjectiveCondition.DistanceToTouchdownBelow:
                    return navigation != null &&
                           navigation.DistanceToTouchdownTarget <=
                           objective.value;
                case LessonObjectiveCondition.AutoLandActive:
                    return autoLand != null && autoLand.Active;
                case LessonObjectiveCondition.AutoLandComplete:
                    return autoLand != null &&
                           autoLand.Phase ==
                           AutoLandController.AutoLandPhase.Complete;
                default:
                    return false;
            }
        }

        private void CompleteCurrentObjective(bool skipped)
        {
            if (LessonComplete || CurrentObjective == null ||
                successPauseTimer > 0f)
                return;
            StatusMessage = skipped
                ? "OBJECTIVE SKIPPED (DEVELOPMENT)"
                : string.IsNullOrWhiteSpace(CurrentObjective.successMessage)
                    ? "OBJECTIVE COMPLETE"
                    : CurrentObjective.successMessage;
            successPauseTimer = objectiveSuccessPause;
            ObjectiveProgress = 1f;
        }

        private void AdvanceObjective()
        {
            successPauseTimer = 0f;
            CurrentObjectiveIndex++;
            if (CurrentLesson == null ||
                CurrentObjectiveIndex >= CurrentLesson.objectives.Length)
            {
                LessonComplete = true;
                CurrentObjectiveIndex =
                    Mathf.Max(0, CurrentLesson.objectives.Length - 1);
                StatusMessage = CurrentLesson.completionMessage;
                ObjectiveProgress = 1f;
                return;
            }
            StatusMessage = string.Empty;
            BeginObjective();
        }

        private void BeginObjective()
        {
            ResetObjectiveState();
            StatusMessage = string.Empty;
        }

        private void ResetObjectiveState()
        {
            conditionTimer = 0f;
            successPauseTimer = 0f;
            ObjectiveProgress = 0f;
            leftSteeringSeen = false;
            rightSteeringSeen = false;
        }

        private void UpdateWarningMessage()
        {
            WarningMessage = string.Empty;
            if (CurrentLessonIndex != 3 && stall != null &&
                stall.State == StallController.StallState.Stalled)
            {
                WarningMessage = "STALL — LOWER THE NOSE AND RECOVER AIRSPEED";
            }
            else if (CurrentLessonIndex == 4 && flightPhysics != null &&
                     approach != null &&
                     flightPhysics.VerticalSpeed < -4f &&
                     approach.AltitudeAboveRunway < 100f)
            {
                WarningMessage = "HIGH SINK RATE — REDUCE DESCENT";
            }
            else if (CurrentLessonIndex == 4 && approach != null &&
                     Mathf.Abs(approach.LateralOffset) > 30f)
            {
                WarningMessage = "RETURN TOWARD RUNWAY CENTERLINE";
            }
        }

        private void ResolveReferences()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (engine == null) engine = GetComponent<EngineController>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            if (groundMovement == null)
                groundMovement = GetComponent<GroundMovementController>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (approach == null)
                approach = GetComponent<ApproachGuidanceController>();
            if (landing == null) landing = GetComponent<LandingController>();
            if (stall == null) stall = GetComponent<StallController>();
            if (flaps == null) flaps = GetComponent<FlapController>();
            if (trim == null) trim = GetComponent<AssistedTrimController>();
            if (holdAssist == null)
                holdAssist = GetComponent<FlightHoldAssistController>();
            if (resetController == null)
                resetController = GetComponent<AircraftResetController>();
            if (locationManager == null)
                locationManager = FindAnyObjectByType<AirportLocationManager>();
            if (navigation == null)
                navigation = FindAnyObjectByType<NavigationManager>();
            if (ExperimentalFeatureFlags.AutoLandEnabled)
            {
                if (autoLand == null)
                    autoLand = GetComponent<AutoLandController>();
                if (autoLand == null)
                    autoLand = gameObject.AddComponent<AutoLandController>();
            }
        }

        private static LessonObjectiveDefinition Objective(
            string instruction,
            string hint,
            LessonObjectiveCondition condition,
            float value = 0f,
            float secondaryValue = 0f,
            float holdDuration = 0f,
            string success = "Good.")
        {
            return new LessonObjectiveDefinition
            {
                instruction = instruction,
                hint = hint,
                condition = condition,
                value = value,
                secondaryValue = secondaryValue,
                holdDuration = holdDuration,
                successMessage = success
            };
        }

        private static FlightLessonDefinition[] BuildDefaultLessons()
        {
            return new[]
            {
                new FlightLessonDefinition
                {
                    title = "1. Engine Start and Taxi",
                    description =
                        "Start the aircraft, taxi under control, steer, and stop.",
                    completionMessage =
                        "TAXI LESSON COMPLETE — You controlled power, direction, and braking.",
                    objectives = new[]
                    {
                        Objective("Press E to start the engine.",
                            "The starter takes a moment. Wait for RUNNING.",
                            LessonObjectiveCondition.EngineRunning,
                            success: "ENGINE RUNNING"),
                        Objective("Increase throttle to 30%.",
                            "Hold Left Shift to raise throttle gradually.",
                            LessonObjectiveCondition.ThrottleAtLeast, 0.30f,
                            success: "POWER SET"),
                        Objective("Begin taxiing above 3 kt.",
                            "Release the brake and let the aircraft accelerate smoothly.",
                            LessonObjectiveCondition.GroundSpeedAbove, 3f,
                            success: "AIRCRAFT TAXIING"),
                        Objective("Steer left and right while moving.",
                            "Tap A and D gently; avoid large rapid inputs.",
                            LessonObjectiveCondition.SteerLeftAndRight,
                            success: "GROUND STEERING CHECK COMPLETE"),
                        Objective("Reduce throttle to idle.",
                            "Hold Left Ctrl until throttle is below 5%.",
                            LessonObjectiveCondition.ThrottleBelow, 0.05f,
                            success: "THROTTLE IDLE"),
                        Objective("Hold Space to brake to a complete stop.",
                            "Brake smoothly and keep the aircraft straight.",
                            LessonObjectiveCondition.BrakeToStop, 1f, 0f, 0.4f,
                            "AIRCRAFT STOPPED")
                    }
                },
                new FlightLessonDefinition
                {
                    title = "2. Takeoff and Rotation",
                    description =
                        "Accelerate on centerline, rotate gently, and establish a safe climb.",
                    completionMessage =
                        "TAKEOFF LESSON COMPLETE — Smooth rotation and positive climb established.",
                    objectives = new[]
                    {
                        Objective("Line up with the runway centerline.",
                            "Use gentle A/D steering. Keep heading close to runway heading.",
                            LessonObjectiveCondition.RunwayLinedUp, 6f, 6f, 0.8f,
                            "LINED UP"),
                        Objective("Apply full throttle.",
                            "Hold Left Shift until throttle exceeds 90%.",
                            LessonObjectiveCondition.ThrottleAtLeast, 0.90f,
                            success: "FULL POWER"),
                        Objective("Maintain the runway centerline.",
                            "Make small steering corrections; authority reduces with speed.",
                            LessonObjectiveCondition.MaintainCenterline, 9f, 8f, 2f,
                            "CENTERLINE MAINTAINED"),
                        Objective("Accelerate to at least 50 kt.",
                            "Keep the aircraft straight and wait for rotation speed.",
                            LessonObjectiveCondition.AirspeedAtLeast, 50f,
                            success: "ROTATION SPEED"),
                        Objective("Press S gently and rotate to 5–10° nose-up.",
                            "Use a short, smooth input. Do not pull abruptly.",
                            LessonObjectiveCondition.PitchRange, 5f, 10f, 0.5f,
                            "ROTATION ATTITUDE"),
                        Objective("Allow the aircraft to lift off.",
                            "Hold a gentle attitude; physics will determine liftoff.",
                            LessonObjectiveCondition.Airborne,
                            success: "AIRBORNE"),
                        Objective("Establish a safe climb above 60 kt.",
                            "Keep positive vertical speed and avoid excessive pitch.",
                            LessonObjectiveCondition.SafeClimb, 60f, 1f, 2f,
                            "SAFE CLIMB ESTABLISHED")
                    }
                },
                new FlightLessonDefinition
                {
                    title = "3. Basic Turns",
                    description =
                        "Bank left and right, observe the turn, then level the wings.",
                    completionMessage =
                        "TURN LESSON COMPLETE — Bank creates turn; opposite input levels the wings.",
                    objectives = new[]
                    {
                        Objective("Climb above 200 m runway altitude.",
                            "Use S for gentle nose-up pitch and maintain safe airspeed.",
                            LessonObjectiveCondition.AltitudeAtLeast, 200f,
                            success: "SAFE ALTITUDE"),
                        Objective("Hold A and bank left around 20°.",
                            "Release A as the bank approaches 20°.",
                            LessonObjectiveCondition.BankLeft, 14f, 28f, 0.5f,
                            "LEFT BANK SET"),
                        Objective("Maintain the turn for two seconds.",
                            "Bank should persist after releasing A. Add slight S if descending.",
                            LessonObjectiveCondition.MaintainTurn, 12f, 0.7f, 2f,
                            "LEFT TURN MAINTAINED"),
                        Objective("Use opposite input to level the wings.",
                            "Tap D until bank is within 5° of level.",
                            LessonObjectiveCondition.WingsLevel, 5f, 0f, 1f,
                            "WINGS LEVEL"),
                        Objective("Hold D and bank right around 20°.",
                            "Release D near 20° of bank.",
                            LessonObjectiveCondition.BankRight, 14f, 28f, 0.5f,
                            "RIGHT BANK SET"),
                        Objective("Recover to wings level.",
                            "Tap A until bank returns within 5°.",
                            LessonObjectiveCondition.WingsLevel, 5f, 0f, 1f,
                            "WINGS LEVEL")
                    }
                },
                new FlightLessonDefinition
                {
                    title = "4. Stall Recovery",
                    description =
                        "Experience a stall warning, reduce angle of attack, and recover.",
                    completionMessage =
                        "STALL LESSON COMPLETE — Lower AoA first, then rebuild speed and lift.",
                    objectives = new[]
                    {
                        Objective("Reduce throttle below 25%.",
                            "Hold Left Ctrl and maintain a safe altitude.",
                            LessonObjectiveCondition.ThrottleBelow, 0.25f,
                            success: "POWER REDUCED"),
                        Objective("Hold S gently to raise the nose and slow down.",
                            "Watch airspeed and AoA in the Pitch Coach.",
                            LessonObjectiveCondition.NoseUpToSlow, 6f,
                            success: "AIRCRAFT SLOWING"),
                        Objective("Continue carefully until STALL WARNING appears.",
                            "Low speed plus high angle of attack creates the stall.",
                            LessonObjectiveCondition.StallWarning,
                            success: "STALL RECOGNIZED"),
                        Objective("Release S/back pressure.",
                            "Neutral elevator is the first recovery step.",
                            LessonObjectiveCondition.ReleaseBackPressure, holdDuration: 0.5f,
                            success: "BACK PRESSURE RELEASED"),
                        Objective("Press W gently to lower the nose.",
                            "Reduce angle of attack; do not push into a steep dive.",
                            LessonObjectiveCondition.LowerNose, 2f, holdDuration: 0.5f,
                            success: "ANGLE OF ATTACK REDUCED"),
                        Objective("Recover airspeed above 52 kt.",
                            "Add power and let speed rebuild before pulling up.",
                            LessonObjectiveCondition.StallRecovered, 52f,
                            success: "STALL RECOVERED"),
                        Objective("Return to normal flight above 58 kt.",
                            "Raise the nose gently and settle the aircraft.",
                            LessonObjectiveCondition.NormalFlight, 58f, 7f, 2f,
                            "NORMAL FLIGHT RESTORED")
                    }
                },
                new FlightLessonDefinition
                {
                    title = "5. Approach and Landing",
                    description =
                        "Configure the aircraft, stabilize the approach, flare, and stop.",
                    completionMessage =
                        "LANDING LESSON COMPLETE — Stable approach, main-wheel touchdown, and rollout.",
                    objectives = new[]
                    {
                        Objective("Establish final approach 1–2 km from the runway.",
                            "The lesson restart places you on final. F3 also respawns final.",
                            LessonObjectiveCondition.OnFinalApproach, 500f, 2200f,
                            success: "ON FINAL APPROACH"),
                        Objective("Extend flaps to 20° or 30° with F.",
                            "Flaps add lift and drag; stay below flap extension speed.",
                            LessonObjectiveCondition.FlapsAtLeast, 19f,
                            success: "LANDING FLAPS SET"),
                        Objective("Maintain 50–70 kt approach speed.",
                            "Use pitch for speed and throttle to manage the descent path.",
                            LessonObjectiveCondition.ApproachSpeed, 50f, 70f, 2f,
                            "APPROACH SPEED STABLE"),
                        Objective("Align with runway centerline.",
                            "Use gentle bank corrections and watch alignment cues.",
                            LessonObjectiveCondition.ApproachAligned, 18f, 7f, 2f,
                            "RUNWAY ALIGNED"),
                        Objective("Control descent between -3.0 and -0.4 m/s.",
                            "Avoid chasing the runway. Make small pitch and power changes.",
                            LessonObjectiveCondition.DescentControlled, -3f, -0.4f, 2f,
                            "DESCENT STABLE"),
                        Objective("Flare below 8 m with gentle nose-up pitch.",
                            "Reduce sink rate without climbing or holding excessive pitch.",
                            LessonObjectiveCondition.Flare, 8f, 2.5f,
                            success: "FLARE"),
                        Objective("Touch down on the main wheels first.",
                            "Hold the nose slightly up and let the mains meet the runway.",
                            LessonObjectiveCondition.MainWheelsFirst,
                            success: "MAIN-WHEEL TOUCHDOWN"),
                        Objective("Hold Space and brake below 1 kt.",
                            "Lower the nose wheel, stay straight, and brake smoothly.",
                            LessonObjectiveCondition.BrakeToStop, 1f, 0f, 0.5f,
                            "AIRCRAFT STOPPED")
                    }
                },
                new FlightLessonDefinition
                {
                    title = "6. Flaps",
                    description =
                        "Deploy flaps in stages and feel their low-speed lift and drag.",
                    completionMessage =
                        "FLAPS LESSON COMPLETE — You configured and retracted the flaps safely.",
                    objectives = new[]
                    {
                        Objective("Settle into safe flight between 55 and 80 kt.",
                            "Use small pitch and throttle changes before changing configuration.",
                            LessonObjectiveCondition.ApproachSpeed, 55f, 80f, 2f,
                            "SPEED STABLE"),
                        Objective("Press F once to extend flaps to 10°.",
                            "Flaps move gradually. Notice the extra lift and drag.",
                            LessonObjectiveCondition.FlapsAtLeast, 9f,
                            success: "FLAPS 10"),
                        Objective("Keep the aircraft between 50 and 75 kt.",
                            "Adjust pitch and power gently as the aircraft slows.",
                            LessonObjectiveCondition.ApproachSpeed, 50f, 75f, 2f,
                            "CONFIGURATION STABLE"),
                        Objective("Press F again to extend flaps to 20°.",
                            "Expect more drag and a small change in pitch balance.",
                            LessonObjectiveCondition.FlapsAtLeast, 19f,
                            success: "FLAPS 20"),
                        Objective("Extend full flaps to 30°.",
                            "Use full flaps only at a safe speed.",
                            LessonObjectiveCondition.FlapsAtLeast, 29f,
                            success: "FULL FLAPS"),
                        Objective("Press V until the flaps retract to 0°.",
                            "Retract in stages and keep enough airspeed.",
                            LessonObjectiveCondition.FlapsAtMost, 1f,
                            success: "FLAPS UP")
                    }
                },
                new FlightLessonDefinition
                {
                    title = "7. Beginner Flight Holds",
                    description =
                        "Use clearly labeled pitch and vertical-speed assists, then override them safely.",
                    completionMessage =
                        "FLIGHT HOLDS COMPLETE — You used and manually overrode both beginner assists.",
                    objectives = new[]
                    {
                        Objective("Press P in stable flight to engage Pitch Hold.",
                            "Pitch Hold requires safe airspeed, modest bank, and no stall warning.",
                            LessonObjectiveCondition.PitchHoldActive,
                            success: "PITCH HOLD ACTIVE"),
                        Objective("Press W or S firmly to override Pitch Hold.",
                            "Strong manual pitch input immediately disengages the assist.",
                            LessonObjectiveCondition.HoldAssistInactive,
                            success: "PITCH HOLD OVERRIDDEN"),
                        Objective("Press Alt+P to engage Vertical Speed Hold.",
                            "The current climb or descent rate is captured within safe limits.",
                            LessonObjectiveCondition.VerticalSpeedHoldActive,
                            success: "VS HOLD ACTIVE"),
                        Objective("Press W or S firmly to override VS Hold.",
                            "The assist must always yield to the pilot.",
                            LessonObjectiveCondition.HoldAssistInactive,
                            success: "VS HOLD OVERRIDDEN")
                    }
                },
                new FlightLessonDefinition
                {
                    title = "8. Glide Path and Runway Alignment",
                    description =
                        "Practice using the side-view glide profile and top-down lateral aid from a long final.",
                    completionMessage =
                        "APPROACH AIDS COMPLETE — You captured the centerline and glide path using visual guidance.",
                    objectives = new[]
                    {
                        Objective(
                            "Settle between 50 and 70 kt on the long final.",
                            "The lesson starts slightly right and above the ideal path. Use gentle inputs.",
                            LessonObjectiveCondition.ApproachSpeed,
                            50f, 70f, 2f, "APPROACH SPEED SET"),
                        Objective(
                            "Use the top-down aid to capture the runway centerline.",
                            "Bank gently toward the funnel center, then level before crossing it.",
                            LessonObjectiveCondition.NavigationLateralAligned,
                            35f, 7f, 2f, "LATERAL PATH CAPTURED"),
                        Objective(
                            "Use the side-view aid to capture the green glide path.",
                            "HIGH: descend gently. LOW: reduce descent or add a little power.",
                            LessonObjectiveCondition.NavigationGlideAligned,
                            10f, 0f, 2f, "GLIDE PATH CAPTURED"),
                        Objective(
                            "Maintain both aids near center for five seconds.",
                            "Make small corrections. Avoid chasing every tiny movement.",
                            LessonObjectiveCondition.NavigationApproachStable,
                            30f, 9f, 5f, "STABLE FINAL"),
                        Objective(
                            "Continue inside 800 m while remaining stable.",
                            "Watch the helper zoom in and keep the airplane close to the path.",
                            LessonObjectiveCondition.DistanceToTouchdownBelow,
                            800f, holdDuration: 1f,
                            success: "CLOSE APPROACH"),
                        Objective(
                            "Continue inside 300 m and observe the flare/touchdown zone.",
                            "The green path crosses the threshold above the runway and ends in the touchdown zone.",
                            LessonObjectiveCondition.DistanceToTouchdownBelow,
                            300f, holdDuration: 1f,
                            success: "FLARE VIEW REACHED")
                    }
                },
                new FlightLessonDefinition
                {
                    title = "9. Experimental Autoland",
                    description =
                        "Start on a stable final, engage autoland, and monitor the automatic landing and rollout.",
                    completionMessage =
                        "AUTOLAND LESSON COMPLETE - Aircraft landed and stopped automatically.",
                    objectives = new[]
                    {
                        Objective(
                            "Press U to engage Experimental Autoland.",
                            "The aircraft starts inside the capture envelope. Autoland takes exclusive control until U is pressed again.",
                            LessonObjectiveCondition.AutoLandActive,
                            success: "AUTOLAND ENGAGED"),
                        Objective(
                            "Monitor the approach, flare, touchdown, and rollout.",
                            "Do not use flight controls. Press U only if you need to disconnect the system.",
                            LessonObjectiveCondition.AutoLandComplete,
                            success: "AUTOLAND COMPLETE")
                    }
                }
            };
        }
    }
}
