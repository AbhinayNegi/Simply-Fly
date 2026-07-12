using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    [DefaultExecutionOrder(-50)]
    public sealed class MouseYokeController : MonoBehaviour
    {
        [Header("Mouse yoke")]
        [SerializeField] private bool mouseYokeEnabled;
        [Range(0.2f, 3f)] [SerializeField] private float sensitivity = 1f;
        [Range(0f, 0.25f)] [SerializeField] private float deadZone = 0.04f;
        [Range(1f, 24f)] [SerializeField] private float smoothing = 8f;
        [Range(0.25f, 1f)] [SerializeField] private float maximumInput = 0.85f;
        [Min(80f)] [SerializeField] private float pixelsForFullDeflection = 320f;

        [Header("Indicator")]
        [SerializeField] private bool showIndicator = true;
        [SerializeField] private Vector2 indicatorSize = new(110f, 90f);
        [SerializeField] private Vector2 indicatorAnchor = new(0.5f, 0.5f);
        [SerializeField] private Key toggleKey = Key.U;

        public bool MouseYokeEnabled => mouseYokeEnabled;
        public bool IsControlling => mouseYokeEnabled || fadingOut;
        public float Sensitivity => sensitivity;
        public float DeadZone => deadZone;
        public float Smoothing => smoothing;
        public float MaximumInput => maximumInput;
        public float RollInput => output.x;
        public float PitchInput => output.y;
        public Vector2 ControlPosition => output;

        private Vector2 target;
        private Vector2 output;
        private bool fadingOut;
        private bool skipNextDelta;
        private GameFlowController gameFlow;
        private GameSettingsManager settings;

        private void Awake() => ResolveReferences();

        private void Update()
        {
            ResolveReferences();
            Keyboard keyboard = Keyboard.current;
            if (IsActiveFlight && keyboard != null &&
                keyboard[toggleKey].wasPressedThisFrame)
            {
                bool enabled = !mouseYokeEnabled;
                if (settings != null) settings.SetMouseYokeEnabled(enabled);
                else SetEnabled(enabled);
            }

            Mouse mouse = Mouse.current;
            if (mouseYokeEnabled && IsActiveFlight && mouse != null)
            {
                bool cameraLook = mouse.rightButton.isPressed;
                if (mouse.middleButton.wasPressedThisFrame)
                    Recenter();

                if (!cameraLook)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    if (skipNextDelta)
                    {
                        delta = Vector2.zero;
                        skipNextDelta = false;
                    }
                    float scale = sensitivity /
                                  Mathf.Max(80f, pixelsForFullDeflection);
                    target.x += delta.x * scale;
                    // Pulling the mouse down is equivalent to pulling the yoke:
                    // positive elevator / nose-up input.
                    target.y -= delta.y * scale;
                    target.x = Mathf.Clamp(target.x, -1f, 1f);
                    target.y = Mathf.Clamp(target.y, -1f, 1f);
                }
            }
            else
            {
                target = Vector2.zero;
            }

            Vector2 desired = ApplyDeadZone(target) * maximumInput;
            float blend = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            output = Vector2.Lerp(output, desired, blend);

            if (fadingOut && output.sqrMagnitude < 0.000025f)
            {
                output = Vector2.zero;
                fadingOut = false;
            }
        }

        public void SetEnabled(bool enabled)
        {
            if (mouseYokeEnabled == enabled) return;
            mouseYokeEnabled = enabled;
            target = Vector2.zero;
            skipNextDelta = enabled;
            fadingOut = !enabled && output.sqrMagnitude > 0.000025f;
            if (enabled)
            {
                output = Vector2.zero;
                fadingOut = false;
            }
        }

        private bool IsActiveFlight =>
            gameFlow != null && gameFlow.IsInFlight;

        private void ResolveReferences()
        {
            if (gameFlow == null)
                gameFlow = FindAnyObjectByType<GameFlowController>();
            if (settings == null)
                settings = FindAnyObjectByType<GameSettingsManager>();
        }

        public void SetSensitivity(float value) =>
            sensitivity = Mathf.Clamp(value, 0.2f, 3f);

        public void SetDeadZone(float value) =>
            deadZone = Mathf.Clamp(value, 0f, 0.25f);

        public void SetSmoothing(float value) =>
            smoothing = Mathf.Clamp(value, 1f, 24f);

        public void SetMaximumInput(float value) =>
            maximumInput = Mathf.Clamp(value, 0.25f, 1f);

        public void Recenter()
        {
            target = Vector2.zero;
        }

        private Vector2 ApplyDeadZone(Vector2 value)
        {
            return new Vector2(
                ApplyAxisDeadZone(value.x),
                ApplyAxisDeadZone(value.y));
        }

        private float ApplyAxisDeadZone(float value)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude <= deadZone) return 0f;
            return Mathf.Sign(value) * Mathf.InverseLerp(deadZone, 1f, magnitude);
        }

        private void OnGUI()
        {
            if (!IsActiveFlight) return;
            if (!showIndicator || !mouseYokeEnabled) return;
            float x = Screen.width * Mathf.Clamp01(indicatorAnchor.x) -
                      indicatorSize.x * 0.5f;
            float y = Screen.height * Mathf.Clamp01(indicatorAnchor.y) -
                      indicatorSize.y * 0.5f;
            Rect panel = new(x, y, indicatorSize.x, indicatorSize.y);
            Color oldColor = GUI.color;
            float centerX = panel.center.x;
            float centerY = panel.center.y;
            GUI.color = new Color(0.35f, 0.94f, 1f, 0.72f);
            GUI.DrawTexture(
                new Rect(panel.x, centerY - 1f, panel.width, 2f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(centerX - 1f, panel.y, 2f, panel.height),
                Texture2D.whiteTexture);

            Vector2 travel = new(panel.width * 0.36f, panel.height * 0.28f);
            Vector2 marker = new(
                centerX + output.x / Mathf.Max(0.01f, maximumInput) * travel.x,
                centerY + output.y / Mathf.Max(0.01f, maximumInput) * travel.y);
            GUI.color = new Color(0.25f, 1f, 0.55f, 0.95f);
            GUI.DrawTexture(
                new Rect(marker.x - 4f, marker.y - 4f, 8f, 8f),
                Texture2D.whiteTexture);
            GUI.color = oldColor;
        }
    }
}
