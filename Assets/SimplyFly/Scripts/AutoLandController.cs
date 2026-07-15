using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(AircraftInput))]
    public sealed class AutoLandController : MonoBehaviour
    {
        public enum AutoLandPhase
        {
            Off, Capture, Final, Flare, Touchdown, Rollout, Complete
        }

        [Header("References")]
        [SerializeField] private AircraftInput input;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private NavigationManager navigation;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private FlapController flaps;
        [SerializeField] private BrakeController brakes;
        [SerializeField] private LandingController landing;
        [SerializeField] private FlightHoldAssistController holdAssist;
        [SerializeField] private WindController wind;

        [Header("Engagement")]
        [SerializeField] private Key toggleKey = Key.U;
        [Min(500f)] [SerializeField] private float maximumEngagementDistance = 6500f;
        [Min(1f)] [SerializeField] private float maximumLateralOffset = 350f;
        [Min(1f)] [SerializeField] private float maximumAlignmentError = 40f;
        [Min(1f)] [SerializeField] private float maximumGlideError = 120f;

        [Header("Approach")]
        [Min(30f)] [SerializeField] private float targetApproachSpeedKnots = 62f;
        [Min(30f)] [SerializeField] private float targetTouchdownSpeedKnots = 54f;
        [Range(5f, 20f)] [SerializeField] private float maximumBankAngle = 16f;
        [Min(1f)] [SerializeField] private float finalPhaseDistance = 1400f;
        [Min(0.5f)] [SerializeField] private float flareHeight = 12f;

        public AutoLandPhase Phase { get; private set; }
        public bool Active => Phase != AutoLandPhase.Off &&
                              Phase != AutoLandPhase.Complete;
        public string StatusMessage { get; private set; } = "AUTOLAND OFF";
        public string AvailabilityMessage => GetAvailability(out string reason)
            ? "AUTOLAND AVAILABLE - PRESS U"
            : reason;

        private float rolloutTimer;
        private Rigidbody body;

        private void Awake() => ResolveReferences();

        private void Update()
        {
            if (!ExperimentalFeatureFlags.AutoLandEnabled)
            {
                if (input != null && input.AutomationOverrideActive)
                    input.ClearAutomationControls();
                Phase = AutoLandPhase.Off;
                return;
            }
            if (navigation == null || input == null || flightPhysics == null)
                ResolveReferences();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            {
                if (Active) Disengage("AUTOLAND DISCONNECTED");
                else TryEngage();
            }

            if (!Active) return;
            if (navigation == null ||
                (!navigation.IsActive &&
                 Phase != AutoLandPhase.Flare &&
                 Phase != AutoLandPhase.Touchdown &&
                 Phase != AutoLandPhase.Rollout &&
                 CurrentDistanceToThreshold() > 150f))
            {
                Disengage("AUTOLAND LOST RUNWAY GUIDANCE");
                return;
            }

            UpdatePhase();
            ApplyCommands();
        }

        private void TryEngage()
        {
            if (!GetAvailability(out string reason))
            {
                StatusMessage = reason;
                return;
            }

            holdAssist?.ResetAssists();
            brakes?.SetParkingBrake(false);
            rolloutTimer = 0f;
            Phase = AutoLandPhase.Capture;
            StatusMessage = "AUTOLAND ACTIVE - CAPTURE";
        }

        public void Disengage(string message = "AUTOLAND OFF")
        {
            input?.ClearAutomationControls();
            Phase = AutoLandPhase.Off;
            StatusMessage = message;
        }

        private bool GetAvailability(out string reason)
        {
            if (navigation == null || !navigation.IsActive ||
                navigation.Phase != NavigationPhase.FinalApproach)
            {
                reason = "AUTOLAND UNAVAILABLE - NO FINAL APPROACH";
                return false;
            }
            if (!navigation.ApproachClear)
            {
                reason = "AUTOLAND UNAVAILABLE - APPROACH BLOCKED";
                return false;
            }
            if (flightPhysics == null || flightPhysics.State !=
                FlightPhysicsController.AircraftState.Airborne)
            {
                reason = "AUTOLAND UNAVAILABLE - AIRCRAFT NOT AIRBORNE";
                return false;
            }
            if (navigation.DistanceToThreshold > maximumEngagementDistance ||
                navigation.DistanceToThreshold < 120f)
            {
                reason = "AUTOLAND UNAVAILABLE - OUTSIDE CAPTURE RANGE";
                return false;
            }
            if (Mathf.Abs(navigation.LateralOffset) > maximumLateralOffset ||
                Mathf.Abs(navigation.AlignmentError) > maximumAlignmentError ||
                Mathf.Abs(navigation.GlideSlopeAltitudeError) > maximumGlideError)
            {
                reason = "AUTOLAND UNAVAILABLE - APPROACH NOT RECOVERABLE";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private void UpdatePhase()
        {
            float height = landing == null
                ? navigation.CurrentAltitudeAGL
                : landing.HeightAboveRunway;
            if (landing != null && landing.MainWheelsTouching)
            {
                Phase = landing.AllWheelsTouching
                    ? AutoLandPhase.Rollout
                    : AutoLandPhase.Touchdown;
            }
            else if (height <= flareHeight)
                Phase = AutoLandPhase.Flare;
            else if (CurrentDistanceToThreshold() <= finalPhaseDistance)
                Phase = AutoLandPhase.Final;
            else
                Phase = AutoLandPhase.Capture;
        }

        private void ApplyCommands()
        {
            if (Phase == AutoLandPhase.Rollout ||
                Phase == AutoLandPhase.Touchdown)
            {
                ApplyRolloutCommands();
                return;
            }

            Vector3 runwayRight = RunwayRight();
            float lateralOffset = CurrentLateralOffset();
            float lateralVelocity = body == null
                ? 0f
                : Vector3.Dot(body.linearVelocity, runwayRight);
            float distance = CurrentDistanceToThreshold();
            float closeFinalBlend = 1f - Mathf.Clamp01(distance / 1200f);
            float desiredLateralVelocity = Mathf.Clamp(
                -lateralOffset * Mathf.Lerp(0.12f, 0.28f, closeFinalBlend) -
                lateralVelocity * 0.45f,
                -9f, 9f);
            float windLateralVelocity = wind == null
                ? 0f
                : Vector3.Dot(wind.CurrentWindVelocity, runwayRight);
            float desiredAirLateralVelocity =
                desiredLateralVelocity - windLateralVelocity;
            float forwardSpeed = Mathf.Max(18f,
                flightPhysics.ForwardAirflowSpeed);
            float headingCorrection = Mathf.Clamp(
                Mathf.Atan2(
                    desiredAirLateralVelocity,
                    forwardSpeed) * Mathf.Rad2Deg,
                -38f, 38f);
            float desiredHeading = navigation.SelectedRunwayHeading +
                                   headingCorrection;
            float headingError = Mathf.DeltaAngle(
                flightPhysics.Heading, desiredHeading);
            // FlightPhysics uses positive BankAngle for left bank, while
            // positive aileron input rolls right. Keep those sign conventions
            // explicit so the feedback reduces, rather than amplifies, error.
            float height = landing == null
                ? navigation.CurrentAltitudeAGL
                : landing.HeightAboveRunway;
            float flareProgress = Phase == AutoLandPhase.Flare
                ? Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(flareHeight, 0.8f, height))
                : 0f;
            float allowedBank = Mathf.Lerp(
                maximumBankAngle, 4f, flareProgress);
            float desiredBank = Mathf.Clamp(
                -headingError * 0.8f, -allowedBank, allowedBank);
            float aileron = Mathf.Clamp(
                (flightPhysics.BankAngle - desiredBank) * 0.060f +
                flightPhysics.RollRate * 0.09f, -0.42f, 0.42f);
            float rudder = Mathf.Clamp(
                headingError * 0.045f - flightPhysics.YawRate * 0.03f,
                -0.70f, 0.70f);

            Vector3 runwayForward = Quaternion.Euler(
                0f, navigation.SelectedRunwayHeading, 0f) * Vector3.forward;
            float alongGroundSpeed = body == null
                ? flightPhysics.HorizontalGroundSpeed
                : Mathf.Max(8f, Vector3.Dot(body.linearVelocity, runwayForward));
            float pathVerticalSpeed = -alongGroundSpeed * Mathf.Tan(
                navigation.SelectedGlideSlopeDegrees * Mathf.Deg2Rad);
            float glideCaptureGain = Mathf.Lerp(
                0.075f, 0.14f,
                Mathf.Clamp01(Mathf.Abs(
                    navigation.GlideSlopeAltitudeError) / 35f));
            float targetVerticalSpeed = Mathf.Clamp(
                pathVerticalSpeed -
                navigation.GlideSlopeAltitudeError * glideCaptureGain,
                -3.8f, 2.5f);
            float elevator;
            if (Phase == AutoLandPhase.Flare)
            {
                targetVerticalSpeed = Mathf.Lerp(-0.65f, -0.05f, flareProgress);
                float targetPitch = Mathf.Lerp(4f, 8.5f, flareProgress);
                elevator = Mathf.Clamp(
                    (targetVerticalSpeed - flightPhysics.VerticalSpeed) * 0.28f +
                    (targetPitch - flightPhysics.PitchAngle) * 0.090f -
                    flightPhysics.PitchRateDegrees * 0.055f,
                    -0.20f, 0.76f);
            }
            else
            {
                elevator = Mathf.Clamp(
                    (targetVerticalSpeed - flightPhysics.VerticalSpeed) * 0.22f -
                    flightPhysics.PitchRateDegrees * 0.025f, -0.60f, 0.70f);
            }

            float landingSpeedBlend = Phase == AutoLandPhase.Flare
                ? 1f
                : 1f - Mathf.Clamp01(
                    (CurrentDistanceToThreshold() - 120f) / 780f);
            float commandedSpeedKnots = Mathf.Lerp(
                targetApproachSpeedKnots,
                targetTouchdownSpeedKnots,
                Mathf.SmoothStep(0f, 1f, landingSpeedBlend));
            float speedError = commandedSpeedKnots - flightPhysics.AirspeedKnots;
            float flareThrottleFade = Mathf.SmoothStep(
                0f, 1f, Mathf.InverseLerp(0.25f, 1f, flareProgress));
            float desiredThrottle = Phase == AutoLandPhase.Flare
                ? Mathf.Lerp(0.40f, 0f, flareThrottleFade)
                : Mathf.Clamp01(0.43f + speedError * 0.025f +
                                Mathf.Max(0f, targetVerticalSpeed) * 0.18f +
                                Mathf.Max(0f, -targetVerticalSpeed - 1.8f) * 0.03f);
            float throttleCommand = Mathf.Clamp(
                (desiredThrottle - (throttle == null ? 0f : throttle.Throttle)) * 5f,
                -1f, 1f);

            bool glideCaptured = navigation.GlideSlopeAltitudeError > -5f;
            flaps?.SetTargetAngle(
                CurrentDistanceToThreshold() < 900f && glideCaptured ? 30f : 20f);
            StatusMessage = $"AUTOLAND {Phase.ToString().ToUpper()}  " +
                            $"{CurrentDistanceToThreshold() / 1852f:0.0} NM  " +
                            $"{commandedSpeedKnots:0} KT";
            input.SetAutomationControls(
                throttleCommand, aileron, elevator, false, rudder);
        }

        private void ApplyRolloutCommands()
        {
            float headingError = Mathf.DeltaAngle(
                flightPhysics.Heading, navigation.SelectedRunwayHeading);
            float steering = Mathf.Clamp(
                headingError * 0.045f - CurrentLateralOffset() * 0.025f,
                -0.55f, 0.55f);
            float speed = flightPhysics.HorizontalGroundSpeed * 1.943844f;
            bool brake = Phase == AutoLandPhase.Rollout && speed < 55f;
            input.SetAutomationControls(-1f, steering, -0.10f, brake, steering);
            flaps?.SetTargetAngle(speed < 25f ? 0f : 30f);
            StatusMessage = $"AUTOLAND ROLLOUT  {speed:0} KT";

            if (speed < 1.2f && landing != null && landing.AllWheelsTouching)
            {
                rolloutTimer += Time.deltaTime;
                if (rolloutTimer < 1f) return;
                brakes?.SetParkingBrake(true);
                input.ClearAutomationControls();
                Phase = AutoLandPhase.Complete;
                StatusMessage = "AUTOLAND COMPLETE - PARKING BRAKE SET";
            }
            else rolloutTimer = 0f;
        }

        private void ResolveReferences()
        {
            input ??= GetComponent<AircraftInput>();
            flightPhysics ??= GetComponent<FlightPhysicsController>();
            throttle ??= GetComponent<ThrottleController>();
            flaps ??= GetComponent<FlapController>();
            brakes ??= GetComponent<BrakeController>();
            landing ??= GetComponent<LandingController>();
            holdAssist ??= GetComponent<FlightHoldAssistController>();
            wind ??= FindAnyObjectByType<WindController>();
            body ??= GetComponent<Rigidbody>();
            navigation ??= FindAnyObjectByType<NavigationManager>();
        }

        private float CurrentDistanceToThreshold()
        {
            if (navigation == null) return float.PositiveInfinity;
            return Vector3.ProjectOnPlane(
                transform.position - navigation.SelectedRunwayThreshold,
                Vector3.up).magnitude;
        }

        private float CurrentLateralOffset()
        {
            if (navigation == null) return 0f;
            return Vector3.Dot(
                transform.position - navigation.SelectedRunwayThreshold,
                RunwayRight());
        }

        private Vector3 RunwayRight()
        {
            return navigation == null
                ? transform.right
                : Quaternion.Euler(
                    0f, navigation.SelectedRunwayHeading, 0f) * Vector3.right;
        }

        private void OnDisable() => input?.ClearAutomationControls();
    }
}
