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
        [Range(0.5f, 3f)] [SerializeField] private float angleDisplayScale = 1f;
        [Range(5f, 90f)] [SerializeField] private float maximumDisplayAngle = 40f;
        [Min(0.1f)] [SerializeField] private float trendResponse = 2.5f;
        [Min(0.1f)] [SerializeField] private float speedTrendDeadband = 0.35f;
        [Min(0.1f)] [SerializeField] private float verticalTrendDeadband = 0.25f;
        [Min(1f)] [SerializeField] private float maximumArrowLength = 68f;

        private bool visible;
        private bool advancedVisible;
        private bool settingEnabled = true;
        private bool navigationOverlaySuppressed;
        private bool visibilityForcedByToggle;
        private float previousAirspeed;
        private float smoothedSpeedTrend;
        private bool speedInitialized;

        private void Awake()
        {
            ApplyPitchTrajectoryDesign();
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
                    bool currentlyShown = panelGroup != null &&
                                          panelGroup.alpha > 0.5f;
                    visible = !currentlyShown;
                    visibilityForcedByToggle = visible;
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
            float airspeed = GetMovementCueSpeed();
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
            float pitch = Mathf.Clamp(
                flightPhysics.PitchAngle, -maximumDisplayAngle, maximumDisplayAngle);
            float flightPath = Mathf.Clamp(
                flightPhysics.FlightPathAngle, -maximumDisplayAngle, maximumDisplayAngle);
            if (aircraftSilhouette != null)
                aircraftSilhouette.localRotation = Quaternion.Euler(
                    0f, 0f, -pitch * angleDisplayScale);
            if (flightPathArrow != null)
                flightPathArrow.localRotation = Quaternion.Euler(
                    0f, 0f, -flightPath * angleDisplayScale);

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

        private void ApplyPitchTrajectoryDesign()
        {
            RectTransform panel = transform as RectTransform;
            if (panel != null)
            {
                // Bottom-right instrument stack: sit immediately left of the VSI.
                panel.anchorMin = new Vector2(1f, 0f);
                panel.anchorMax = new Vector2(1f, 0f);
                panel.pivot = new Vector2(1f, 0f);
                // BrakeStatusHUD occupies right offsets 270..408. Keep a
                // deliberate 24 px gap to its left on the same bottom row.
                // Keep the low visual position while leaving enough clearance
                // for the 150x64 aircraft to rotate through the full +/-40°.
                panel.anchoredPosition = new Vector2(-432f, 24f);
                panel.sizeDelta = new Vector2(300f, 150f);
            }

            Image panelImage = GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = Color.clear;
                panelImage.raycastTarget = false;
            }

            Outline panelOutline = GetComponent<Outline>();
            if (panelOutline != null) panelOutline.enabled = false;

            RectTransform diagram = transform.Find("Diagram") as RectTransform;
            if (diagram != null)
            {
                diagram.anchorMin = new Vector2(0.5f, 0.5f);
                diagram.anchorMax = new Vector2(0.5f, 0.5f);
                diagram.pivot = new Vector2(0.5f, 0.5f);
                diagram.anchoredPosition = Vector2.zero;
                diagram.sizeDelta = new Vector2(300f, 150f);
            }

            RectTransform horizon = diagram == null
                ? null : diagram.Find("Horizon") as RectTransform;
            SetElementRect(horizon, Vector2.zero, new Vector2(270f, 2f),
                new Color(0.302f, 0.847f, 0.91f, 0.92f));
            AddContrastOutline(
                horizon == null ? null : horizon.GetComponent<Graphic>(), 1f);
            SetDiagramElementVisible(diagram, "Elevator Track", false);
            SetDiagramElementVisible(diagram, "Trim Track", false);

            if (flightPathArrow != null)
            {
                flightPathArrow.anchorMin = new Vector2(0.5f, 0.5f);
                flightPathArrow.anchorMax = new Vector2(0.5f, 0.5f);
                flightPathArrow.pivot = new Vector2(1f, 0.5f);
                flightPathArrow.anchoredPosition = Vector2.zero;
                // At the -40° display limit, 84 px keeps the marker inside the
                // viewport while preserving a clearly readable path vector.
                flightPathArrow.sizeDelta = new Vector2(112f, 1.5f);
                Image line = flightPathArrow.GetComponent<Image>();
                if (line != null)
                {
                    line.color = new Color(0.549f, 0.91f, 0.478f, 0.95f);
                    AddContrastOutline(line, 1f);
                }

                foreach (Transform child in flightPathArrow)
                    child.gameObject.SetActive(false);
                CreateTrajectoryDiamond(flightPathArrow);
            }

            if (aircraftSilhouette != null)
            {
                aircraftSilhouette.anchorMin = new Vector2(0.5f, 0.5f);
                aircraftSilhouette.anchorMax = new Vector2(0.5f, 0.5f);
                // The artwork's fuselage centerline is 37% up from its bottom.
                // Using that point as the pivot keeps the horizon out of the roof.
                aircraftSilhouette.pivot = new Vector2(0.5f, 0.37f);
                aircraftSilhouette.anchoredPosition = Vector2.zero;
                aircraftSilhouette.sizeDelta = new Vector2(150f, 64f);
                aircraftSilhouette.localScale = new Vector3(-1f, 1f, 1f);

                foreach (Transform child in aircraftSilhouette)
                    child.gameObject.SetActive(false);
                Image aircraftImage = aircraftSilhouette.GetComponent<Image>();
                if (aircraftImage != null)
                {
                    aircraftImage.enabled = true;
                    aircraftImage.color = Color.white;
                    aircraftImage.preserveAspect = true;
                    aircraftImage.raycastTarget = false;
                    aircraftImage.sprite = Resources.Load<Sprite>(
                        "PitchTrajectoryHUD/plane_icon");
                    AddContrastOutline(aircraftImage, 1.5f);
                }
            }

            SetLegacyDetailsVisible(false);
        }

        private static void SetElementRect(
            RectTransform rect, Vector2 position, Vector2 size, Color color)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        private static void SetDiagramElementVisible(
            RectTransform diagram, string elementName, bool visible)
        {
            Transform element = diagram == null ? null : diagram.Find(elementName);
            if (element != null) element.gameObject.SetActive(visible);
        }

        private static void AddContrastOutline(Graphic graphic, float distance)
        {
            if (graphic == null) return;
            Outline outline = graphic.GetComponent<Outline>();
            if (outline == null)
                outline = graphic.gameObject.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = new Color(0.015f, 0.035f, 0.055f, 0.92f);
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        private static void CreateTrajectoryDiamond(RectTransform line)
        {
            const string markerName = "Trajectory Marker";
            Transform existing = line.Find(markerName);
            GameObject marker = existing == null
                ? new GameObject(markerName, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image))
                : existing.gameObject;
            marker.SetActive(true);
            RectTransform rect = marker.GetComponent<RectTransform>();
            rect.SetParent(line, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(9f, 9f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image image = marker.GetComponent<Image>();
            image.color = new Color(0.549f, 0.91f, 0.478f, 1f);
            image.raycastTarget = false;
            Outline outline = marker.GetComponent<Outline>();
            if (outline == null) outline = marker.AddComponent<Outline>();
            outline.effectColor = new Color(0.043f, 0.071f, 0.125f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        private void SetLegacyDetailsVisible(bool show)
        {
            if (advancedGroup != null) advancedGroup.gameObject.SetActive(show);
            SetActive(elevatorMarker, show);
            SetActive(trimMarker, show);
            SetActive(attitudeLabel, show);
            SetActive(motionLabel, show);
            SetActive(configurationLabel, show);
            SetActive(guidanceLabel, show);
            SetActive(modeLabel, show);
            Transform title = transform.Find("Title");
            if (title != null) title.gameObject.SetActive(show);
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null) component.gameObject.SetActive(active);
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
                    $"{GetMovementCueSpeed():0} KT  {speedTrend}\n" +
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

        private float GetMovementCueSpeed()
        {
            if (flightPhysics == null) return 0f;
            float groundSpeed =
                flightPhysics.HorizontalGroundSpeed * 1.943844f;
            return flightPhysics.IsPhysicallyGrounded
                ? groundSpeed
                : flightPhysics.AirspeedKnots;
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
            bool showPanel = visible &&
                             (visibilityForcedByToggle || settingEnabled) &&
                             (visibilityForcedByToggle ||
                              !navigationOverlaySuppressed);
            if (panelGroup != null)
            {
                panelGroup.alpha = showPanel ? 1f : 0f;
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
            }
            if (advancedGroup != null)
            {
                advancedGroup.alpha =
                    showPanel && advancedVisible ? 1f : 0f;
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

        public void SetNavigationOverlaySuppressed(bool suppressed)
        {
            navigationOverlaySuppressed = suppressed;
            ApplyVisibility();
        }
    }
}
