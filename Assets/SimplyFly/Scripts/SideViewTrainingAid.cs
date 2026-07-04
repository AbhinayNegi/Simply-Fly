using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    public sealed class SideViewTrainingAid : MonoBehaviour
    {
        [Header("Flight references")]
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private AircraftInput input;
        [SerializeField] private AssistedTrimController trim;
        [SerializeField] private FlapController flaps;
        [SerializeField] private LandingController landing;
        [SerializeField] private StallController stall;
        [SerializeField] private ThrottleController throttle;

        [Header("View references")]
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private CanvasGroup advancedGroup;
        [SerializeField] private RectTransform aircraftSilhouette;
        [SerializeField] private RectTransform flightPathArrow;
        [SerializeField] private RectTransform elevatorMarker;
        [SerializeField] private RectTransform trimMarker;
        [SerializeField] private RectTransform liftArrow;
        [SerializeField] private RectTransform weightArrow;
        [SerializeField] private RectTransform thrustArrow;
        [SerializeField] private RectTransform dragArrow;
        [SerializeField] private TMP_Text attitudeLabel;
        [SerializeField] private TMP_Text motionLabel;
        [SerializeField] private TMP_Text configurationLabel;
        [SerializeField] private TMP_Text guidanceLabel;
        [SerializeField] private TMP_Text modeLabel;

        [Header("Controls")]
        [SerializeField] private Key toggleKey = Key.I;
        [SerializeField] private Key advancedToggleKey = Key.K;
        [SerializeField] private bool visibleByDefault = true;
        [SerializeField] private bool advancedForcesByDefault;

        [Header("Display tuning")]
        [Range(0.5f, 3f)] [SerializeField] private float angleDisplayScale = 1.35f;
        [Min(0.1f)] [SerializeField] private float trendResponse = 2.5f;
        [Min(0.1f)] [SerializeField] private float speedTrendDeadband = 0.35f;
        [Min(0.1f)] [SerializeField] private float verticalTrendDeadband = 0.25f;
        [Min(1f)] [SerializeField] private float maximumArrowLength = 68f;

        private bool visible;
        private bool advancedVisible;
        private bool settingEnabled = true;
        private float previousAirspeed;
        private float smoothedSpeedTrend;
        private bool speedInitialized;

        private void Awake()
        {
            visible = visibleByDefault;
            advancedVisible = advancedForcesByDefault;
            ApplyVisibility();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard[toggleKey].wasPressedThisFrame)
                {
                    visible = !visible;
                    ApplyVisibility();
                }
                if (keyboard[advancedToggleKey].wasPressedThisFrame)
                {
                    advancedVisible = !advancedVisible;
                    ApplyVisibility();
                }
            }

            if (!visible || flightPhysics == null) return;
            UpdateTrends();
            UpdateAttitudeView();
            UpdateLabels();
            UpdateForceVectors();
        }

        private void UpdateTrends()
        {
            float airspeed = flightPhysics.AirspeedKnots;
            if (!speedInitialized)
            {
                previousAirspeed = airspeed;
                speedInitialized = true;
            }

            float rawTrend = (airspeed - previousAirspeed) /
                             Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            previousAirspeed = airspeed;
            float blend = 1f - Mathf.Exp(-trendResponse * Time.unscaledDeltaTime);
            smoothedSpeedTrend = Mathf.Lerp(smoothedSpeedTrend, rawTrend, blend);
        }

        private void UpdateAttitudeView()
        {
            float pitch = flightPhysics.PitchAngle;
            float flightPath = flightPhysics.FlightPathAngle;
            if (aircraftSilhouette != null)
                aircraftSilhouette.localRotation = Quaternion.Euler(
                    0f, 0f, pitch * angleDisplayScale);
            if (flightPathArrow != null)
                flightPathArrow.localRotation = Quaternion.Euler(
                    0f, 0f, flightPath * angleDisplayScale);

            if (elevatorMarker != null)
            {
                Vector2 position = elevatorMarker.anchoredPosition;
                position.y = flightPhysics.ElevatorDeflection * 30f;
                elevatorMarker.anchoredPosition = position;
            }

            if (trimMarker != null)
            {
                float trimRange = trim == null
                    ? 1f
                    : Mathf.Max(
                        trim.MaximumNoseUpTrim,
                        Mathf.Abs(trim.MaximumNoseDownTrim));
                float normalizedTrim = trim == null
                    ? 0f
                    : Mathf.Clamp(trim.CurrentTrimOffset /
                                  Mathf.Max(0.01f, trimRange), -1f, 1f);
                Vector2 position = trimMarker.anchoredPosition;
                position.y = normalizedTrim * 30f;
                trimMarker.anchoredPosition = position;
            }
        }

        private void UpdateLabels()
        {
            float pitch = flightPhysics.PitchAngle;
            float flightPath = flightPhysics.FlightPathAngle;
            float aoa = flightPhysics.AngleOfAttack;
            if (attitudeLabel != null)
            {
                attitudeLabel.text =
                    $"PITCH {pitch:+0.0;-0.0;0.0}°   " +
                    $"PATH {flightPath:+0.0;-0.0;0.0}°   " +
                    $"AOA {aoa:+0.0;-0.0;0.0}°";
            }

            string speedTrend = smoothedSpeedTrend > speedTrendDeadband
                ? "ACCELERATING ↑"
                : smoothedSpeedTrend < -speedTrendDeadband
                    ? "SLOWING ↓"
                    : "SPEED STEADY";
            string verticalTrend =
                flightPhysics.VerticalSpeed > verticalTrendDeadband
                    ? "CLIMBING ↑"
                    : flightPhysics.VerticalSpeed < -verticalTrendDeadband
                        ? "DESCENDING ↓"
                        : "LEVEL";
            if (motionLabel != null)
            {
                motionLabel.text =
                    $"{flightPhysics.AirspeedKnots:0} KT  {speedTrend}\n" +
                    $"{flightPhysics.VerticalSpeed:+0.0;-0.0;0.0} m/s  " +
                    verticalTrend;
            }

            string wheelState = landing == null
                ? "WHEELS —"
                : landing.AllWheelsTouching
                    ? "ALL WHEELS"
                    : landing.MainWheelsTouching
                        ? "MAINS TOUCHING"
                        : landing.NoseWheelTouching
                            ? "NOSE WHEEL"
                            : "AIRBORNE";
            if (configurationLabel != null)
            {
                configurationLabel.text =
                    $"ELEV {flightPhysics.ElevatorDeflection:+0.00;-0.00;0.00}  " +
                    $"TRIM {(trim == null ? 0f : trim.CurrentTrimOffset):+0.00;-0.00;0.00}\n" +
                    $"FLAPS {(flaps == null ? 0f : flaps.ActualAngle):0}°   " +
                    wheelState;
            }

            if (guidanceLabel != null)
                guidanceLabel.text = BuildGuidance(pitch, flightPath, aoa);
            if (modeLabel != null)
                modeLabel.text = advancedVisible
                    ? "I HIDE   K SIMPLE VIEW"
                    : "I HIDE   K FORCE VECTORS";
        }

        private string BuildGuidance(float pitch, float flightPath, float aoa)
        {
            if (stall != null && stall.StallWarning)
                return "STALL RISK — LOWER NOSE, REGAIN SPEED";
            if (pitch > 3f && flightPath < -1f)
                return "NOSE UP, BUT AIRCRAFT IS DESCENDING";
            if (landing != null && landing.FlareActive)
                return "FLARE — REDUCE DESCENT, WATCH AIRSPEED";
            if (trim != null && trim.Recommendation != "TRIMMED")
                return trim.Recommendation;
            if (Mathf.Abs(aoa) > 8f)
                return "HIGH ANGLE OF ATTACK";
            return "ATTITUDE AND FLIGHT PATH";
        }

        private void UpdateForceVectors()
        {
            if (!advancedVisible) return;
            float weight = Mathf.Max(
                1f,
                flightPhysics.LiftForce /
                Mathf.Max(0.01f, flightPhysics.LiftPercentOfWeight * 0.01f));
            SetArrowLength(
                liftArrow,
                flightPhysics.LiftForce / weight * maximumArrowLength);
            SetArrowLength(weightArrow, maximumArrowLength);
            SetArrowLength(
                dragArrow,
                Mathf.Clamp01(flightPhysics.DragForce / weight) *
                maximumArrowLength);
            SetArrowLength(
                thrustArrow,
                (throttle == null ? 0f : throttle.Throttle) *
                maximumArrowLength);
        }

        private static void SetArrowLength(RectTransform arrow, float length)
        {
            if (arrow == null) return;
            float safeLength = Mathf.Max(4f, length);
            Vector2 size = arrow.sizeDelta;
            size.x = safeLength;
            arrow.sizeDelta = size;
            SetArrowHeadPosition(arrow.Find("Head Upper") as RectTransform, safeLength);
            SetArrowHeadPosition(arrow.Find("Head Lower") as RectTransform, safeLength);
        }

        private static void SetArrowHeadPosition(
            RectTransform head,
            float arrowLength)
        {
            if (head == null) return;
            Vector2 position = head.anchoredPosition;
            position.x = arrowLength - 2f;
            head.anchoredPosition = position;
        }

        private void ApplyVisibility()
        {
            if (panelGroup != null)
            {
                panelGroup.alpha = visible && settingEnabled ? 1f : 0f;
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
            }
            if (advancedGroup != null)
            {
                advancedGroup.alpha =
                    visible && settingEnabled && advancedVisible ? 1f : 0f;
                advancedGroup.interactable = false;
                advancedGroup.blocksRaycasts = false;
            }
        }

        public void ShowForLesson()
        {
            visible = true;
            ApplyVisibility();
        }

        public void SetSettingEnabled(bool enabled)
        {
            settingEnabled = enabled;
            ApplyVisibility();
        }
    }
}
