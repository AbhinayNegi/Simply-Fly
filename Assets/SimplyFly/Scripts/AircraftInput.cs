using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    public sealed class AircraftInput : MonoBehaviour
    {
        [SerializeField] private MouseYokeController mouseYoke;
        [Header("Bindings")]
        [SerializeField] private Key startEngineKey = Key.E;
        [SerializeField] private Key brakeKey = Key.Space;
        [SerializeField] private Key parkingBrakeKey = Key.J;
        [SerializeField] private Key resetCameraKey = Key.R;
        [SerializeField] private Key extendFlapsKey = Key.F;
        [SerializeField] private Key retractFlapsKey = Key.V;

        public float ThrottleInput { get; private set; }
        public float SteeringInput { get; private set; }
        public float AileronInput { get; private set; }
        public float RudderInput { get; private set; }
        public float ElevatorInput { get; private set; }
        public bool BrakeHeld { get; private set; }
        public bool ParkingBrakeTogglePressed { get; private set; }
        public bool StartEnginePressed { get; private set; }
        public bool ResetCameraPressed { get; private set; }
        public bool FlapsExtendPressed { get; private set; }
        public bool FlapsRetractPressed { get; private set; }
        public bool TrimCapturePressed { get; private set; }
        public bool TrimResetPressed { get; private set; }
        public Vector2 CameraLookDelta { get; private set; }
        public bool TestOverrideActive { get; private set; }
        public bool AutomationOverrideActive { get; private set; }

        private float testThrottleInput;
        private float testSteeringInput;
        private float testRudderInput;
        private float testElevatorInput;
        private bool testBrakeHeld;
        private float automationThrottleInput;
        private float automationSteeringInput;
        private float automationRudderInput;
        private float automationElevatorInput;
        private bool automationBrakeHeld;

        private void Update()
        {
            if (mouseYoke == null)
                mouseYoke = GetComponent<MouseYokeController>();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard == null)
            {
                ClearInput();
                ApplyTestOverride();
                ApplyAutomationOverride();
                return;
            }

            ThrottleInput = ReadAxis(
                keyboard.leftShiftKey.isPressed || keyboard.pageUpKey.isPressed,
                keyboard.leftCtrlKey.isPressed || keyboard.pageDownKey.isPressed);
            RudderInput = ReadAxis(
                keyboard.dKey.isPressed,
                keyboard.aKey.isPressed);
            // Ground steering follows the rudder/nose-wheel keys. Airborne
            // bank is intentionally separate on the arrow keys.
            SteeringInput = RudderInput;
            float keyboardAileron = ReadAxis(
                keyboard.rightArrowKey.isPressed,
                keyboard.leftArrowKey.isPressed);
            AileronInput = Mathf.Clamp(
                keyboardAileron +
                (mouseYoke != null && mouseYoke.IsControlling
                    ? mouseYoke.RollInput
                    : 0f),
                -1f,
                1f);
            ElevatorInput = ReadAxis(
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
            if (mouseYoke != null && mouseYoke.IsControlling)
                ElevatorInput = Mathf.Clamp(
                    ElevatorInput + mouseYoke.PitchInput, -1f, 1f);
            BrakeHeld = keyboard[brakeKey].isPressed;
            ParkingBrakeTogglePressed =
                keyboard[parkingBrakeKey].wasPressedThisFrame;
            StartEnginePressed = keyboard[startEngineKey].wasPressedThisFrame;
            ResetCameraPressed = keyboard[resetCameraKey].wasPressedThisFrame;
            FlapsExtendPressed = keyboard[extendFlapsKey].wasPressedThisFrame;
            FlapsRetractPressed = keyboard[retractFlapsKey].wasPressedThisFrame;
            bool trimPressed = keyboard.tKey.wasPressedThisFrame;
            bool shiftHeld =
                keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            TrimCapturePressed = trimPressed && !shiftHeld;
            TrimResetPressed = trimPressed && shiftHeld;
            CameraLookDelta = mouse != null && mouse.rightButton.isPressed
                ? mouse.delta.ReadValue()
                : Vector2.zero;

            ApplyTestOverride();
            ApplyAutomationOverride();
        }

        private static float ReadAxis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private void ClearInput()
        {
            ThrottleInput = 0f;
            SteeringInput = 0f;
            AileronInput = 0f;
            RudderInput = 0f;
            ElevatorInput = 0f;
            BrakeHeld = false;
            ParkingBrakeTogglePressed = false;
            StartEnginePressed = false;
            ResetCameraPressed = false;
            FlapsExtendPressed = false;
            FlapsRetractPressed = false;
            TrimCapturePressed = false;
            TrimResetPressed = false;
            CameraLookDelta = Vector2.zero;
        }

        public void SetTestControls(
            float throttleInput,
            float steeringInput,
            float elevatorInput,
            bool brakeHeld,
            float rudderInput = 0f)
        {
            TestOverrideActive = true;
            testThrottleInput = Mathf.Clamp(throttleInput, -1f, 1f);
            testSteeringInput = Mathf.Clamp(steeringInput, -1f, 1f);
            testRudderInput = Mathf.Clamp(rudderInput, -1f, 1f);
            testElevatorInput = Mathf.Clamp(elevatorInput, -1f, 1f);
            testBrakeHeld = brakeHeld;
        }

        public void ClearTestControls()
        {
            TestOverrideActive = false;
            testThrottleInput = 0f;
            testSteeringInput = 0f;
            testRudderInput = 0f;
            testElevatorInput = 0f;
            testBrakeHeld = false;
        }

        public void SetAutomationControls(
            float throttleInput,
            float steeringInput,
            float elevatorInput,
            bool brakeHeld,
            float rudderInput = 0f)
        {
            AutomationOverrideActive = true;
            automationThrottleInput = Mathf.Clamp(throttleInput, -1f, 1f);
            automationSteeringInput = Mathf.Clamp(steeringInput, -1f, 1f);
            automationRudderInput = Mathf.Clamp(rudderInput, -1f, 1f);
            automationElevatorInput = Mathf.Clamp(elevatorInput, -1f, 1f);
            automationBrakeHeld = brakeHeld;
        }

        public void ClearAutomationControls()
        {
            AutomationOverrideActive = false;
            automationThrottleInput = 0f;
            automationSteeringInput = 0f;
            automationRudderInput = 0f;
            automationElevatorInput = 0f;
            automationBrakeHeld = false;
        }

        private void ApplyTestOverride()
        {
            if (!TestOverrideActive) return;
            ThrottleInput = testThrottleInput;
            SteeringInput = testSteeringInput;
            AileronInput = testSteeringInput;
            RudderInput = testRudderInput;
            ElevatorInput = testElevatorInput;
            BrakeHeld = testBrakeHeld;
        }

        private void ApplyAutomationOverride()
        {
            if (!AutomationOverrideActive) return;
            ThrottleInput = automationThrottleInput;
            SteeringInput = automationSteeringInput;
            AileronInput = automationSteeringInput;
            RudderInput = automationRudderInput;
            ElevatorInput = automationElevatorInput;
            BrakeHeld = automationBrakeHeld;
            ParkingBrakeTogglePressed = false;
            StartEnginePressed = false;
            FlapsExtendPressed = false;
            FlapsRetractPressed = false;
            TrimCapturePressed = false;
            TrimResetPressed = false;
        }
    }
}
