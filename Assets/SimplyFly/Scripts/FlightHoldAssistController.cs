using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    public sealed class FlightHoldAssistController : MonoBehaviour
    {
        private const float FeetPerMinutePerMeterPerSecond = 196.8504f;

        public enum AssistMode
        {
            None,
            PitchHold,
            VerticalSpeedHold
        }

        [Header("References")]
        [SerializeField] private AircraftInput input;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private StallController stall;

        [Header("Availability")]
        [SerializeField] private bool pitchHoldEnabled = true;
        [SerializeField] private bool verticalSpeedHoldEnabled = true;

        [Header("Pitch Hold Assist")]
        [Range(0f, 0.2f)] [SerializeField] private float pitchHoldAssistStrength = 0.08f;
        [Range(0f, 0.2f)] [SerializeField] private float pitchHoldIntegralStrength = 0.04f;
        [Range(0f, 1f)] [SerializeField] private float pitchHoldIntegralLimit = 0.32f;
        [Range(0f, 1f)] [SerializeField] private float pitchHoldMaxElevatorAuthority = 0.45f;
        [Min(0.01f)] [SerializeField] private float pitchHoldResponseSpeed = 1.1f;
        [Min(0f)] [SerializeField] private float pitchHoldDamping = 0.018f;
        [Range(0f, 30f)] [SerializeField] private float pitchHoldMaxPitchUp = 15f;
        [Range(-30f, 0f)] [SerializeField] private float pitchHoldMaxPitchDown = -10f;
        [SerializeField] private bool pitchHoldDisengageOnManualInput = true;
        [Range(0.05f, 1f)] [SerializeField] private float pitchHoldManualInputThreshold = 0.25f;
        [Min(1f)] [SerializeField] private float pitchHoldMinAirspeed = 50f;
        [Range(5f, 80f)] [SerializeField] private float pitchHoldMaxBankAngle = 35f;

        [Header("Vertical Speed Hold Assist")]
        [Range(0f, 0.5f)] [SerializeField] private float vsHoldAssistStrength = 0.18f;
        [Range(0f, 0.25f)] [SerializeField] private float vsHoldIntegralStrength = 0.06f;
        [Range(0f, 1f)] [SerializeField] private float vsHoldIntegralLimit = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float vsHoldMaxElevatorAuthority = 0.65f;
        [Min(0.01f)] [SerializeField] private float vsHoldResponseSpeed = 1.35f;
        [Min(0f)] [SerializeField] private float vsHoldDamping = 0.015f;
        [Range(1f, 2f)] [SerializeField] private float vsHoldLowSpeedAuthorityBoost = 1.45f;
        [Range(0f, 8f)] [SerializeField] private float vsHoldMaxClimbRate = 3f;
        [Range(-8f, 0f)] [SerializeField] private float vsHoldMaxDescentRate = -3f;
        [Min(100f)] [SerializeField] private float vsHoldTargetStepFeetPerMinute = 100f;
        [Min(1f)] [SerializeField] private float mouseScrollThreshold = 1f;
        [Tooltip("Optional low-speed cutoff. Disabled by default; stall protection still applies.")]
        [SerializeField] private bool vsHoldDisengageOnLowSpeed;
        [Tooltip("Allows VS Hold to engage and remain selected while the aircraft is grounded.")]
        [SerializeField] private bool vsHoldAllowedOnGround = true;
        [Min(1f)] [SerializeField] private float vsHoldMinAirspeed = 52f;
        [Range(5f, 80f)] [SerializeField] private float vsHoldMaxBankAngle = 35f;
        [SerializeField] private bool vsHoldDisengageOnManualInput = true;
        [Range(0.05f, 1f)] [SerializeField] private float vsHoldManualInputThreshold = 0.25f;

        [Header("Shared safety")]
        [Min(1f)] [SerializeField] private float maxSafeVerticalSpeed = 10f;
        [Range(10f, 45f)] [SerializeField] private float maxSafePitchAngle = 22f;
        [Min(0.1f)] [SerializeField] private float messageDuration = 2.5f;

        public AssistMode ActiveMode { get; private set; }
        public bool PitchHoldEnabled => pitchHoldEnabled;
        public bool VerticalSpeedHoldEnabled => verticalSpeedHoldEnabled;
        public bool PitchHoldActive => ActiveMode == AssistMode.PitchHold;
        public bool VerticalSpeedHoldActive =>
            ActiveMode == AssistMode.VerticalSpeedHold;
        public float TargetPitch { get; private set; }
        public float TargetVerticalSpeed { get; private set; }
        public float CapturedVerticalSpeed { get; private set; }
        public bool VerticalSpeedTargetClamped { get; private set; }
        public float PitchError { get; private set; }
        public float VerticalSpeedError { get; private set; }
        public float ElevatorCommand { get; private set; }
        public float PitchHoldElevatorCommand =>
            PitchHoldActive ? ElevatorCommand : 0f;
        public float VerticalSpeedHoldElevatorCommand =>
            VerticalSpeedHoldActive ? ElevatorCommand : 0f;
        public bool VerticalSpeedEnergyLimited { get; private set; }
        public string DisengageReason { get; private set; } = string.Empty;
        public string TransientMessage { get; private set; } = string.Empty;

        private float targetElevatorCommand;
        private float integralCommand;
        private float messageTimer;

        private void Awake()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (stall == null) stall = GetComponent<StallController>();
        }

        private void Update()
        {
            if (input != null && input.AutomationOverrideActive)
            {
                if (ActiveMode != AssistMode.None) ResetAssists();
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
            {
                bool altHeld =
                    keyboard.leftAltKey.isPressed ||
                    keyboard.rightAltKey.isPressed;
                if (altHeld) ToggleVerticalSpeedHold();
                else TogglePitchHold();
            }

            if (VerticalSpeedHoldActive)
            {
                Mouse mouse = Mouse.current;
                bool increase =
                    keyboard != null &&
                    (keyboard.equalsKey.wasPressedThisFrame ||
                     keyboard.numpadPlusKey.wasPressedThisFrame);
                bool decrease =
                    keyboard != null &&
                    (keyboard.minusKey.wasPressedThisFrame ||
                     keyboard.numpadMinusKey.wasPressedThisFrame);
                float scroll = mouse == null
                    ? 0f
                    : mouse.scroll.ReadValue().y;
                if (scroll >= mouseScrollThreshold) increase = true;
                else if (scroll <= -mouseScrollThreshold) decrease = true;

                if (increase ^ decrease)
                    AdjustVerticalSpeedTarget(
                        increase
                            ? vsHoldTargetStepFeetPerMinute
                            : -vsHoldTargetStepFeetPerMinute);
            }

            if (messageTimer > 0f)
            {
                messageTimer -= Time.unscaledDeltaTime;
                if (messageTimer <= 0f) TransientMessage = string.Empty;
            }
        }

        private void FixedUpdate()
        {
            if (ActiveMode == AssistMode.None)
            {
                VerticalSpeedEnergyLimited = false;
                targetElevatorCommand = 0f;
                ElevatorCommand = Mathf.MoveTowards(
                    ElevatorCommand, 0f, Time.fixedDeltaTime);
                PitchError = 0f;
                VerticalSpeedError = 0f;
                return;
            }

            if (ShouldDisengageForManualInput())
            {
                Disengage("MANUAL INPUT");
                return;
            }
            if (!IsSafeToUse(ActiveMode, out string unsafeReason))
            {
                Disengage(unsafeReason);
                return;
            }

            float pitchRate = flightPhysics.PitchRateDegrees;
            if (PitchHoldActive)
            {
                PitchError = TargetPitch - flightPhysics.PitchAngle;
                VerticalSpeedError = 0f;
                VerticalSpeedEnergyLimited = false;
                integralCommand = Mathf.Clamp(
                    integralCommand +
                    PitchError * pitchHoldIntegralStrength *
                    Time.fixedDeltaTime,
                    -pitchHoldIntegralLimit,
                    pitchHoldIntegralLimit);
                targetElevatorCommand = Mathf.Clamp(
                    PitchError * pitchHoldAssistStrength -
                    pitchRate * pitchHoldDamping +
                    integralCommand,
                    -pitchHoldMaxElevatorAuthority,
                    pitchHoldMaxElevatorAuthority);
                ElevatorCommand = Mathf.MoveTowards(
                    ElevatorCommand,
                    targetElevatorCommand,
                    pitchHoldResponseSpeed * Time.fixedDeltaTime);
            }
            else
            {
                PitchError = 0f;
                VerticalSpeedError =
                    TargetVerticalSpeed - flightPhysics.VerticalSpeed;
                integralCommand = Mathf.Clamp(
                    integralCommand +
                    VerticalSpeedError * vsHoldIntegralStrength *
                    Time.fixedDeltaTime,
                    -vsHoldIntegralLimit,
                    vsHoldIntegralLimit);
                float lowSpeedBlend = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        35f,
                        70f,
                        flightPhysics.AirspeedKnots));
                float lowSpeedBoost = Mathf.Lerp(
                    1f,
                    vsHoldLowSpeedAuthorityBoost,
                    lowSpeedBlend);
                targetElevatorCommand = Mathf.Clamp(
                    (VerticalSpeedError * vsHoldAssistStrength +
                     integralCommand) * lowSpeedBoost -
                    pitchRate * vsHoldDamping,
                    -vsHoldMaxElevatorAuthority,
                    vsHoldMaxElevatorAuthority);
                VerticalSpeedEnergyLimited =
                    VerticalSpeedError > 0.5f &&
                    targetElevatorCommand >=
                    vsHoldMaxElevatorAuthority * 0.92f &&
                    flightPhysics.AirspeedKnots < 60f;
                ElevatorCommand = Mathf.MoveTowards(
                    ElevatorCommand,
                    targetElevatorCommand,
                    vsHoldResponseSpeed * Time.fixedDeltaTime);
            }
        }

        public void TogglePitchHold()
        {
            if (PitchHoldActive)
            {
                Disengage("PILOT OFF", "PITCH HOLD OFF");
                return;
            }
            if (!pitchHoldEnabled)
            {
                ShowMessage("PITCH HOLD DISABLED IN SETTINGS");
                return;
            }
            if (!IsSafeToUse(AssistMode.PitchHold, out string reason))
            {
                ShowMessage($"PITCH HOLD UNAVAILABLE: {reason}");
                return;
            }

            bool switching = VerticalSpeedHoldActive;
            ActiveMode = AssistMode.PitchHold;
            TargetPitch = Mathf.Clamp(
                flightPhysics.PitchAngle,
                pitchHoldMaxPitchDown,
                pitchHoldMaxPitchUp);
            ElevatorCommand = 0f;
            targetElevatorCommand = 0f;
            integralCommand = Mathf.Clamp(
                flightPhysics.ElevatorDeflection,
                -pitchHoldIntegralLimit,
                pitchHoldIntegralLimit);
            DisengageReason = string.Empty;
            ShowMessage(switching
                ? "VS HOLD OFF - PITCH HOLD ON"
                : "PITCH HOLD ON");
        }

        public void ToggleVerticalSpeedHold()
        {
            if (VerticalSpeedHoldActive)
            {
                Disengage("PILOT OFF", "VS HOLD OFF");
                return;
            }
            if (!verticalSpeedHoldEnabled)
            {
                ShowMessage("VS HOLD DISABLED IN SETTINGS");
                return;
            }
            if (!IsSafeToUse(AssistMode.VerticalSpeedHold, out string reason))
            {
                ShowMessage($"VS HOLD UNAVAILABLE: {reason}");
                return;
            }

            bool switching = PitchHoldActive;
            ActiveMode = AssistMode.VerticalSpeedHold;
            CapturedVerticalSpeed = flightPhysics.VerticalSpeed;
            TargetVerticalSpeed = SnapVerticalSpeedTarget(
                CapturedVerticalSpeed);
            VerticalSpeedTargetClamped =
                !Mathf.Approximately(
                    CapturedVerticalSpeed,
                    TargetVerticalSpeed);
            ElevatorCommand = 0f;
            targetElevatorCommand = 0f;
            integralCommand = Mathf.Clamp(
                flightPhysics.ElevatorDeflection,
                -vsHoldIntegralLimit,
                vsHoldIntegralLimit);
            DisengageReason = string.Empty;
            float targetFeetPerMinute =
                TargetVerticalSpeed * FeetPerMinutePerMeterPerSecond;
            string captureMessage =
                $"VS HOLD ON - TARGET {targetFeetPerMinute:+0;-0;0} FT/MIN";
            ShowMessage(switching
                ? $"PITCH HOLD OFF - {captureMessage}"
                : captureMessage);
        }

        public void AdjustVerticalSpeedTarget(float deltaFeetPerMinute)
        {
            if (!VerticalSpeedHoldActive ||
                Mathf.Approximately(deltaFeetPerMinute, 0f))
                return;

            float step = Mathf.Max(100f, vsHoldTargetStepFeetPerMinute);
            float currentFeetPerMinute = Mathf.Round(
                TargetVerticalSpeed * FeetPerMinutePerMeterPerSecond / step) * step;
            float targetFeetPerMinute = currentFeetPerMinute +
                Mathf.Sign(deltaFeetPerMinute) * step;
            TargetVerticalSpeed = ClampFeetPerMinuteTarget(targetFeetPerMinute);
            VerticalSpeedTargetClamped = false;
            CapturedVerticalSpeed = TargetVerticalSpeed;
            ShowMessage(
                $"VS TARGET {TargetVerticalSpeed * FeetPerMinutePerMeterPerSecond:+0;-0;0} FT/MIN");
        }

        private float SnapVerticalSpeedTarget(float metersPerSecond)
        {
            float step = Mathf.Max(100f, vsHoldTargetStepFeetPerMinute);
            float feetPerMinute = Mathf.Round(
                metersPerSecond * FeetPerMinutePerMeterPerSecond / step) * step;
            return ClampFeetPerMinuteTarget(feetPerMinute);
        }

        private float ClampFeetPerMinuteTarget(float feetPerMinute)
        {
            float step = Mathf.Max(100f, vsHoldTargetStepFeetPerMinute);
            float minimumFeetPerMinute = Mathf.Ceil(
                vsHoldMaxDescentRate * FeetPerMinutePerMeterPerSecond / step) * step;
            float maximumFeetPerMinute = Mathf.Floor(
                vsHoldMaxClimbRate * FeetPerMinutePerMeterPerSecond / step) * step;
            return Mathf.Clamp(
                feetPerMinute, minimumFeetPerMinute, maximumFeetPerMinute) /
                FeetPerMinutePerMeterPerSecond;
        }

        private bool ShouldDisengageForManualInput()
        {
            float manual = input == null ? 0f : input.ElevatorInput;
            return PitchHoldActive
                ? pitchHoldDisengageOnManualInput &&
                  Mathf.Abs(manual) >= pitchHoldManualInputThreshold
                : vsHoldDisengageOnManualInput &&
                  Mathf.Abs(manual) >= vsHoldManualInputThreshold;
        }

        private bool IsSafeToUse(AssistMode mode, out string reason)
        {
            if (flightPhysics == null)
            {
                reason = "FLIGHT DATA";
                return false;
            }
            bool grounded =
                flightPhysics.State !=
                FlightPhysicsController.AircraftState.Airborne ||
                flightPhysics.IsPhysicallyGrounded;
            if (grounded &&
                (mode == AssistMode.PitchHold || !vsHoldAllowedOnGround))
            {
                reason = "GROUNDED";
                return false;
            }
            if (stall != null && stall.State != StallController.StallState.Normal)
            {
                reason = stall.State == StallController.StallState.Stalled
                    ? "STALL"
                    : "STALL WARNING";
                return false;
            }
            // Pitch Hold requires normal flying speed. VS Hold may continue
            // through low-speed flight, but the shared stall warning/stalled
            // checks above still disengage it before it can fight a stall.
            if (mode == AssistMode.PitchHold &&
                flightPhysics.AirspeedKnots < pitchHoldMinAirspeed)
            {
                reason = "LOW SPEED";
                return false;
            }
            if (mode == AssistMode.VerticalSpeedHold &&
                vsHoldDisengageOnLowSpeed &&
                flightPhysics.AirspeedKnots < vsHoldMinAirspeed)
            {
                reason = "LOW SPEED";
                return false;
            }
            float maxBank = mode == AssistMode.PitchHold
                ? pitchHoldMaxBankAngle
                : vsHoldMaxBankAngle;
            if (Mathf.Abs(flightPhysics.BankAngle) > maxBank)
            {
                reason = "BANK";
                return false;
            }
            if (Mathf.Abs(flightPhysics.VerticalSpeed) > maxSafeVerticalSpeed)
            {
                reason = "VERTICAL SPEED";
                return false;
            }
            if (Mathf.Abs(flightPhysics.PitchAngle) > maxSafePitchAngle)
            {
                reason = "PITCH";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void Disengage(string reason, string message = null)
        {
            AssistMode previous = ActiveMode;
            ActiveMode = AssistMode.None;
            targetElevatorCommand = 0f;
            integralCommand = 0f;
            DisengageReason = reason;
            VerticalSpeedEnergyLimited = false;
            string name = previous == AssistMode.PitchHold
                ? "PITCH HOLD"
                : "VS HOLD";
            ShowMessage(message ?? $"{name} DISENGAGED: {reason}");
        }

        private void ShowMessage(string message)
        {
            TransientMessage = message;
            messageTimer = messageDuration;
        }

        public void SetPitchHoldEnabled(bool enabled)
        {
            pitchHoldEnabled = enabled;
            if (!enabled && PitchHoldActive) Disengage("DISABLED IN SETTINGS");
        }

        public void SetVerticalSpeedHoldEnabled(bool enabled)
        {
            verticalSpeedHoldEnabled = enabled;
            if (!enabled && VerticalSpeedHoldActive)
                Disengage("DISABLED IN SETTINGS");
        }

        public void SetPitchHoldStrength(float value) =>
            pitchHoldAssistStrength = Mathf.Clamp(value, 0f, 0.2f);

        public void SetVerticalSpeedHoldStrength(float value) =>
            vsHoldAssistStrength = Mathf.Clamp(value, 0f, 0.5f);

        public void ResetAssists()
        {
            ActiveMode = AssistMode.None;
            TargetPitch = 0f;
            TargetVerticalSpeed = 0f;
            CapturedVerticalSpeed = 0f;
            VerticalSpeedTargetClamped = false;
            VerticalSpeedEnergyLimited = false;
            PitchError = 0f;
            VerticalSpeedError = 0f;
            ElevatorCommand = 0f;
            targetElevatorCommand = 0f;
            integralCommand = 0f;
            DisengageReason = string.Empty;
            TransientMessage = string.Empty;
            messageTimer = 0f;
        }
    }
}
