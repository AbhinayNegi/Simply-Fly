using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class AssistedTrimController : MonoBehaviour
    {
        public enum TrimStatus
        {
            Neutral,
            Active,
            Trimmed
        }

        [Header("References")]
        [SerializeField] private AircraftInput input;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private StallController stall;
        [SerializeField] private FlapController flaps;
        [SerializeField] private ThrottleController throttle;

        [Header("Trim response")]
        [Tooltip("Fraction of the sustained elevator requirement captured per tap.")]
        [Range(0.05f, 0.5f)] [SerializeField] private float trimCaptureSpeed = 0.22f;
        [Min(0.01f)] [SerializeField] private float trimResponseSpeed = 0.16f;
        [Range(0.05f, 0.6f)] [SerializeField] private float maximumNoseUpTrim = 0.38f;
        [Range(0.05f, 0.6f)] [SerializeField] private float maximumNoseDownTrim = 0.32f;
        [Min(0.1f)] [SerializeField] private float trimRecommendationDelay = 1f;
        [Range(0.01f, 0.5f)] [SerializeField] private float trimDeadband = 0.12f;
        [Range(0.1f, 1.5f)] [SerializeField] private float trimStrength = 1f;
        [Range(0f, 1f)] [SerializeField] private float airspeedTrimInfluence = 0.18f;
        [Range(0f, 1f)] [SerializeField] private float flapTrimInfluence = 0.2f;

        [Header("Capture limits")]
        [Tooltip("Knots.")]
        [Min(1f)] [SerializeField] private float trimCaptureMinSpeed = 50f;
        [Range(5f, 80f)] [SerializeField] private float trimCaptureMaxBank = 30f;
        [Tooltip("Degrees per second.")]
        [Min(1f)] [SerializeField] private float trimCaptureMaxPitchRate = 8f;
        [Tooltip("Absolute metres per second.")]
        [Min(0.5f)] [SerializeField] private float trimCaptureMaxVerticalSpeed = 8f;

        public TrimStatus Status { get; private set; } = TrimStatus.Neutral;
        public bool TrimActive => Mathf.Abs(TargetTrimOffset) > trimDeadband;
        public float CurrentTrimOffset { get; private set; }
        public float TargetTrimOffset { get; private set; }
        public float PitchCommandOffset => CurrentTrimOffset * trimStrength;
        public float SustainedElevatorPressure { get; private set; }
        public string Recommendation { get; private set; } = "TRIMMED";
        public string RejectionReason { get; private set; } = string.Empty;
        public string TransientMessage { get; private set; } = string.Empty;
        public float CapturedAirspeed { get; private set; }
        public float CapturedVerticalSpeed { get; private set; }
        public float CapturedFlapSetting { get; private set; }
        public float CapturedThrottle => capturedThrottle;
        public float MaximumNoseUpTrim => maximumNoseUpTrim;
        public float MaximumNoseDownTrim => maximumNoseDownTrim;

        private float sustainedInputTimer;
        private float messageTimer;
        private float capturedThrottle;
        private bool hasCapture;

        private void Awake()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (stall == null) stall = GetComponent<StallController>();
            if (flaps == null) flaps = GetComponent<FlapController>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
        }

        private void Update()
        {
            if (input != null && input.TrimResetPressed)
            {
                TargetTrimOffset = 0f;
                RejectionReason = string.Empty;
                ShowMessage("TRIM RESET");
            }
            else if (input != null && input.TrimCapturePressed)
            {
                CaptureTrim();
            }

            CurrentTrimOffset = Mathf.MoveTowards(
                CurrentTrimOffset,
                TargetTrimOffset,
                trimResponseSpeed * Time.deltaTime);
            UpdateStatus();
            UpdateRecommendation();

            if (messageTimer > 0f)
            {
                messageTimer -= Time.unscaledDeltaTime;
                if (messageTimer <= 0f) TransientMessage = string.Empty;
            }
        }

        private void CaptureTrim()
        {
            if (!CanCaptureTrim(out string reason))
            {
                RejectionReason = reason;
                ShowMessage("CANNOT TRIM NOW");
                return;
            }

            float elevatorRequirement = flightPhysics.ElevatorDeflection;
            float speedAdjustment = !hasCapture
                ? 0f
                : Mathf.Clamp(
                    (CapturedAirspeed - flightPhysics.AirspeedKnots) /
                    Mathf.Max(1f, CapturedAirspeed) * airspeedTrimInfluence,
                    -0.08f,
                    0.08f);
            float flapChange = !hasCapture || flaps == null
                ? 0f
                : (flaps.ActualAngle - CapturedFlapSetting) / 30f;
            float flapAdjustment = flapChange * flapTrimInfluence;
            float captureDelta =
                elevatorRequirement * trimCaptureSpeed +
                speedAdjustment +
                flapAdjustment;

            TargetTrimOffset = Mathf.Clamp(
                CurrentTrimOffset + captureDelta,
                -maximumNoseDownTrim,
                maximumNoseUpTrim);
            CapturedAirspeed = flightPhysics.AirspeedKnots;
            CapturedVerticalSpeed = flightPhysics.VerticalSpeed;
            CapturedFlapSetting = flaps == null ? 0f : flaps.ActualAngle;
            capturedThrottle = throttle == null ? 0f : throttle.Throttle;
            hasCapture = true;
            RejectionReason = string.Empty;
            sustainedInputTimer = 0f;
            ShowMessage(Mathf.Abs(captureDelta) <= trimDeadband * 0.25f
                ? "TRIMMED"
                : "TRIM CAPTURED");
        }

        private bool CanCaptureTrim(out string reason)
        {
            if (flightPhysics == null ||
                flightPhysics.State != FlightPhysicsController.AircraftState.Airborne)
            {
                reason = "Aircraft not airborne";
                return false;
            }
            if (stall != null && stall.State != StallController.StallState.Normal)
            {
                reason = "Stall risk active";
                return false;
            }
            if (flightPhysics.AirspeedKnots < trimCaptureMinSpeed)
            {
                reason = "Airspeed too low";
                return false;
            }
            if (Mathf.Abs(flightPhysics.BankAngle) > trimCaptureMaxBank)
            {
                reason = "Bank angle too high";
                return false;
            }
            if (Mathf.Abs(flightPhysics.PitchRateDegrees) > trimCaptureMaxPitchRate)
            {
                reason = "Pitch rate too high";
                return false;
            }
            if (Mathf.Abs(flightPhysics.VerticalSpeed) > trimCaptureMaxVerticalSpeed)
            {
                reason = "Vertical speed too high";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void UpdateRecommendation()
        {
            float elevator = flightPhysics == null
                ? 0f
                : flightPhysics.ElevatorDeflection;
            if (Mathf.Abs(elevator) > trimDeadband)
            {
                if (Mathf.Sign(elevator) == Mathf.Sign(SustainedElevatorPressure) ||
                    Mathf.Abs(SustainedElevatorPressure) <= trimDeadband)
                {
                    sustainedInputTimer += Time.deltaTime;
                }
                else
                {
                    sustainedInputTimer = 0f;
                }
                SustainedElevatorPressure = elevator;
            }
            else
            {
                sustainedInputTimer = 0f;
                SustainedElevatorPressure = Mathf.MoveTowards(
                    SustainedElevatorPressure,
                    0f,
                    Time.deltaTime * 2f);
            }

            bool flapChanged =
                hasCapture &&
                flaps != null &&
                Mathf.Abs(flaps.ActualAngle - CapturedFlapSetting) >= 5f;
            if (sustainedInputTimer >= trimRecommendationDelay)
            {
                Recommendation = SustainedElevatorPressure > 0f
                    ? "NOSE-UP PRESSURE — TAP T"
                    : "NOSE-DOWN PRESSURE — TAP T";
            }
            else if (flapChanged)
            {
                Recommendation = "FLAPS CHANGED — RETRIM";
            }
            else
            {
                Recommendation = "TRIMMED";
            }
        }

        private void UpdateStatus()
        {
            if (Mathf.Abs(CurrentTrimOffset) <= trimDeadband &&
                Mathf.Abs(TargetTrimOffset) <= trimDeadband)
            {
                Status = TrimStatus.Neutral;
            }
            else if (Mathf.Abs(CurrentTrimOffset - TargetTrimOffset) >
                     trimDeadband * 0.2f)
            {
                Status = TrimStatus.Active;
            }
            else
            {
                Status = TrimStatus.Trimmed;
            }
        }

        private void ShowMessage(string message)
        {
            TransientMessage = message;
            messageTimer = 2.5f;
        }

        public void ResetTrim()
        {
            CurrentTrimOffset = 0f;
            TargetTrimOffset = 0f;
            SustainedElevatorPressure = 0f;
            sustainedInputTimer = 0f;
            messageTimer = 0f;
            CapturedAirspeed = 0f;
            CapturedVerticalSpeed = 0f;
            CapturedFlapSetting = 0f;
            capturedThrottle = 0f;
            hasCapture = false;
            Recommendation = "TRIMMED";
            RejectionReason = string.Empty;
            TransientMessage = string.Empty;
            Status = TrimStatus.Neutral;
        }
    }
}
