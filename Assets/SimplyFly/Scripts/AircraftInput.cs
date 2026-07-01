using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    public sealed class AircraftInput : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private Key startEngineKey = Key.E;
        [SerializeField] private Key brakeKey = Key.Space;
        [SerializeField] private Key resetCameraKey = Key.R;

        public float ThrottleInput { get; private set; }
        public float SteeringInput { get; private set; }
        public bool BrakeHeld { get; private set; }
        public bool StartEnginePressed { get; private set; }
        public bool ResetCameraPressed { get; private set; }
        public Vector2 CameraLookDelta { get; private set; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard == null)
            {
                ClearInput();
                return;
            }

            ThrottleInput = ReadAxis(
                keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            SteeringInput = ReadAxis(
                keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
            BrakeHeld = keyboard[brakeKey].isPressed;
            StartEnginePressed = keyboard[startEngineKey].wasPressedThisFrame;
            ResetCameraPressed = keyboard[resetCameraKey].wasPressedThisFrame;
            CameraLookDelta = mouse != null && mouse.rightButton.isPressed
                ? mouse.delta.ReadValue()
                : Vector2.zero;
        }

        private static float ReadAxis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private void ClearInput()
        {
            ThrottleInput = 0f;
            SteeringInput = 0f;
            BrakeHeld = false;
            StartEnginePressed = false;
            ResetCameraPressed = false;
            CameraLookDelta = Vector2.zero;
        }
    }
}
