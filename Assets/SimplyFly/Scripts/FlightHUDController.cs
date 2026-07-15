using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    public sealed class FlightHUDController : MonoBehaviour
    {
        [Header("Flight systems")]
        [SerializeField] private EngineController engine;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private ApproachGuidanceController approach;
        [SerializeField] private StallController stall;
        [SerializeField] private FlapController flaps;
        [SerializeField] private AssistedTrimController trim;
        [SerializeField] private FlightHoldAssistController holdAssist;
        [SerializeField] private AirportLocationManager locationManager;
        [SerializeField] private LandingController landing;
        [SerializeField] private PilotAwarenessAssist awarenessAssist;
        [SerializeField] private WindController wind;
        [SerializeField] private BrakeController brakes;
        [SerializeField] private UIController debugUI;

        [Header("HUD views")]
        [SerializeField] private CanvasGroup hudGroup;
        [SerializeField] private FlightHUDInstrument airspeedInstrument;
        [SerializeField] private AirspeedTapeHUD airspeedTape;
        [SerializeField] private FlightHUDInstrument altitudeInstrument;
        [SerializeField] private AltitudeTapeHUD altitudeTape;
        [SerializeField] private FlightHUDInstrument verticalSpeedInstrument;
        [SerializeField] private VerticalSpeedIndicatorHUD verticalSpeedIndicator;
        [SerializeField] private FlightHUDInstrument headingInstrument;
        [SerializeField] private HeadingRibbonController headingRibbon;
        [SerializeField] private WindInstrumentHUD windInstrument;
        [SerializeField] private FlapsInstrumentHUD flapsInstrument;
        [SerializeField] private BrakeStatusHUD brakeStatus;
        [SerializeField] private FlightPathMarkerHUD flightPathMarker;
        [SerializeField] private RunwayAlignmentHUD runwayAlignment;
        [SerializeField] private ApproachGuidanceHUD approachGuidanceHUD;
        [SerializeField] private FlightHUDInstrument powerInstrument;
        [SerializeField] private EngineInstrumentController engineInstrument;
        [SerializeField] private FlightHUDInstrument attitudeInstrument;
        [SerializeField] private MiniAttitudeController miniAttitude;
        [SerializeField] private TMP_Text warningLabel;
        [SerializeField] private TMP_Text awarenessLabel;
        [SerializeField] private TMP_Text landingFeedbackLabel;
        [SerializeField] private CanvasGroup landingFeedbackGroup;
        [SerializeField] private FlightTrimHUDView trimView;
        [SerializeField] private RudderController rudder;
        [SerializeField] private SlipSkidHUDView slipSkidView;
        [SerializeField] private TMP_Text holdAssistLabel;

        [Header("Display tuning")]
        [SerializeField] private Key hudToggleKey = Key.H;
        [SerializeField] private Key debugToggleKey = Key.F10;
        [Min(1f)] [SerializeField] private float safeApproachSpeedMin = 65f;
        [Min(1f)] [SerializeField] private float safeApproachSpeedMax = 75f;
        [Min(0.5f)] [SerializeField] private float landingFeedbackDuration = 5f;
        [Min(0.1f)] [SerializeField] private float landingFeedbackFadeDuration = 1.25f;
        [Min(0f)] [SerializeField] private float touchdownBankWarningAngle = 5f;

        private readonly Color normalColor = new Color(0.25f, 0.95f, 1f);
        private readonly Color safeColor = new Color(0.25f, 1f, 0.48f);
        private readonly Color cautionColor = new Color(1f, 0.75f, 0.18f);
        private readonly Color dangerColor = new Color(1f, 0.2f, 0.12f);
        private bool hudVisible = true;
        private bool navigationMapSuppressed;
        private LandingController.LandingQuality lastLandingQuality;
        private float landingFeedbackTimer;

        private void Awake()
        {
            if (hudGroup == null) hudGroup = GetComponent<CanvasGroup>();
            EnsureAirspeedTape();
            EnsureAltitudeTape();
            EnsureVerticalSpeedIndicator();
            EnsureHeadingRibbon();
            EnsureWindInstrument();
            EnsureFlapsInstrument();
            EnsureBrakeStatus();
            EnsureFlightPathMarker();
            EnsureApproachGuidance();
            EnsureEngineInstrument();
            EnsureMiniAttitude();
            EnsureHoldAssistStyle();
            if (wind == null) wind = FindAnyObjectByType<WindController>();
            if (slipSkidView != null)
                slipSkidView.gameObject.SetActive(false);
            ApplyHUDVisibility();
            if (landingFeedbackGroup != null) landingFeedbackGroup.alpha = 0f;
        }

        private void EnsureHeadingRibbon()
        {
            if (headingRibbon == null && headingInstrument != null)
                headingRibbon = headingInstrument.GetComponent<HeadingRibbonController>();
            if (headingRibbon == null && headingInstrument != null)
                headingRibbon = headingInstrument.gameObject.AddComponent<HeadingRibbonController>();
            headingRibbon?.Bind(flightPhysics);
        }

        private void EnsureWindInstrument()
        {
            if (wind == null) wind = FindAnyObjectByType<WindController>();
            if (windInstrument == null)
                windInstrument = GetComponentInChildren<WindInstrumentHUD>(true);
            if (windInstrument == null)
            {
                GameObject go = new("Wind Instrument", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                windInstrument = go.AddComponent<WindInstrumentHUD>();
            }
            windInstrument.Bind(wind, flightPhysics);
        }

        private void EnsureFlapsInstrument()
        {
            if (flapsInstrument == null)
                flapsInstrument = GetComponentInChildren<FlapsInstrumentHUD>(true);
            if (flapsInstrument == null)
            {
                GameObject go = new("Flaps Instrument", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                flapsInstrument = go.AddComponent<FlapsInstrumentHUD>();
            }
            flapsInstrument.Bind(flaps);
        }

        private void EnsureBrakeStatus()
        {
            if (brakes == null) brakes = FindAnyObjectByType<BrakeController>();
            if (brakeStatus == null)
                brakeStatus = GetComponentInChildren<BrakeStatusHUD>(true);
            if (brakeStatus == null)
            {
                GameObject go = new("Brake Status", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                brakeStatus = go.AddComponent<BrakeStatusHUD>();
            }
            brakeStatus.Bind(brakes);
        }

        private void EnsureFlightPathMarker()
        {
            if (flightPathMarker == null)
                flightPathMarker = GetComponent<FlightPathMarkerHUD>();
            if (flightPathMarker == null)
                flightPathMarker = gameObject.AddComponent<FlightPathMarkerHUD>();
            flightPathMarker.Bind(flightPhysics);
        }

        private void EnsureRunwayAlignment()
        {
            if (runwayAlignment == null)
                runwayAlignment = GetComponent<RunwayAlignmentHUD>();
            if (runwayAlignment == null)
                runwayAlignment = gameObject.AddComponent<RunwayAlignmentHUD>();
            runwayAlignment.Bind(
                FindAnyObjectByType<NavigationManager>(), flightPhysics);
        }

        private void EnsureApproachGuidance()
        {
            NavigationManager navigation = FindAnyObjectByType<NavigationManager>();
            if (approachGuidanceHUD == null)
                approachGuidanceHUD =
                    GetComponentInChildren<ApproachGuidanceHUD>(true);
            if (approachGuidanceHUD == null)
            {
                GameObject go = new("Approach Guidance HUD",
                    typeof(UnityEngine.UIElements.UIDocument));
                go.transform.SetParent(transform, false);
                approachGuidanceHUD = go.AddComponent<ApproachGuidanceHUD>();
            }
            approachGuidanceHUD.Bind(navigation, flightPhysics);

            // Retire the previous orange runway-perspective guidance.
            if (runwayAlignment == null)
                runwayAlignment = GetComponent<RunwayAlignmentHUD>();
            if (runwayAlignment != null)
            {
                runwayAlignment.SetSettingEnabled(false);
                runwayAlignment.enabled = false;
            }
        }

        private void EnsureEngineInstrument()
        {
            if (engineInstrument == null && powerInstrument != null)
                engineInstrument =
                    powerInstrument.GetComponent<EngineInstrumentController>();
            if (engineInstrument == null && powerInstrument != null)
                engineInstrument =
                    powerInstrument.gameObject.AddComponent<EngineInstrumentController>();
            engineInstrument?.Bind(engine, throttle);
        }

        private void EnsureMiniAttitude()
        {
            if (miniAttitude == null && attitudeInstrument != null)
                miniAttitude =
                    attitudeInstrument.GetComponent<MiniAttitudeController>();
            if (miniAttitude == null && attitudeInstrument != null)
                miniAttitude =
                    attitudeInstrument.gameObject.AddComponent<MiniAttitudeController>();
            miniAttitude?.Bind(flightPhysics);
        }

        private void EnsureHoldAssistStyle()
        {
            if (holdAssistLabel == null) return;
            RectTransform labelRect = holdAssistLabel.rectTransform;
            RectTransform container = labelRect.parent as RectTransform;
            if (container == null) return;

            container.anchorMin = container.anchorMax = Vector2.zero;
            container.pivot = Vector2.zero;
            // Continue the lower-left instrument row after the flaps panel.
            container.anchoredPosition = new Vector2(
                860f, FlightHUDStyle.BottomSafeMargin);
            container.sizeDelta = new Vector2(220f, 58f);

            Image background = container.GetComponent<Image>();
            if (background != null)
                background.color = FlightHUDStyle.SecondaryPanel;
            Outline outline = container.GetComponent<Outline>();
            if (outline == null) outline = container.gameObject.AddComponent<Outline>();
            outline.effectColor = FlightHUDStyle.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 5f);
            labelRect.offsetMax = new Vector2(-10f, -5f);
            holdAssistLabel.fontSize = 13f;
            holdAssistLabel.enableAutoSizing = true;
            holdAssistLabel.fontSizeMin = 11f;
            holdAssistLabel.fontSizeMax = 13f;
            holdAssistLabel.alignment = TextAlignmentOptions.Center;
            holdAssistLabel.textWrappingMode = TextWrappingModes.NoWrap;
        }

        public void SetFlightPathMarkerEnabled(bool enabled) =>
            flightPathMarker?.SetSettingEnabled(enabled);

        public void SetRunwayAlignmentEnabled(bool enabled)
        {
            approachGuidanceHUD?.SetSettingEnabled(enabled);
            runwayAlignment?.SetSettingEnabled(false);
        }

        private void EnsureVerticalSpeedIndicator()
        {
            if (verticalSpeedIndicator == null && verticalSpeedInstrument != null)
                verticalSpeedIndicator = verticalSpeedInstrument.GetComponent<VerticalSpeedIndicatorHUD>();
            if (verticalSpeedIndicator == null && verticalSpeedInstrument != null)
                verticalSpeedIndicator = verticalSpeedInstrument.gameObject.AddComponent<VerticalSpeedIndicatorHUD>();
            verticalSpeedIndicator?.Bind(flightPhysics);
        }

        private void EnsureAltitudeTape()
        {
            if (altitudeTape == null && altitudeInstrument != null)
                altitudeTape = altitudeInstrument.GetComponent<AltitudeTapeHUD>();
            if (altitudeTape == null && altitudeInstrument != null)
                altitudeTape = altitudeInstrument.gameObject.AddComponent<AltitudeTapeHUD>();
            altitudeTape?.Bind(flightPhysics, approach);
        }

        private void EnsureAirspeedTape()
        {
            if (airspeedTape == null && airspeedInstrument != null)
                airspeedTape = airspeedInstrument.GetComponent<AirspeedTapeHUD>();
            if (airspeedTape == null && airspeedInstrument != null)
                airspeedTape = airspeedInstrument.gameObject.AddComponent<AirspeedTapeHUD>();
            AircraftSpeedPerformance performance = flightPhysics == null
                ? null
                : flightPhysics.GetComponent<AircraftSpeedPerformance>();
            if (performance == null && flightPhysics != null)
                performance = flightPhysics.gameObject.AddComponent<AircraftSpeedPerformance>();
            airspeedTape?.Bind(flightPhysics, performance);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard[hudToggleKey].wasPressedThisFrame)
                {
                    hudVisible = !hudVisible;
                    ApplyHUDVisibility();
                }
                if (keyboard[debugToggleKey].wasPressedThisFrame)
                    debugUI?.ToggleDebugVisibility();
            }

            UpdateInstruments();
            UpdateStallWarning();
            UpdateAwarenessCue();
            UpdateLandingFeedback();
            trimView?.UpdateView(trim);
            UpdateHoldAssist();
        }

        private void ApplyHUDVisibility()
        {
            if (hudGroup == null) return;
            hudGroup.alpha = hudVisible ? 1f : 0f;
            if (navigationMapSuppressed) hudGroup.alpha = 0f;
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;
            verticalSpeedIndicator?.SetVisible(
                hudVisible && !navigationMapSuppressed);
        }

        public void SetHUDVisible(bool visible)
        {
            hudVisible = visible;
            ApplyHUDVisibility();
        }

        public void SetNavigationMapSuppressed(bool suppressed)
        {
            navigationMapSuppressed = suppressed;
            ApplyHUDVisibility();
        }

        private void UpdateInstruments()
        {
            float airspeed = flightPhysics == null ? 0f : flightPhysics.AirspeedKnots;
            float groundSpeed = flightPhysics == null
                ? 0f
                : flightPhysics.HorizontalGroundSpeed * 1.943844f;
            bool parkedWindAirflow = flightPhysics != null &&
                flightPhysics.IsPhysicallyGrounded && groundSpeed < 1f;
            float stallSpeed = stall == null ? 42f : stall.EffectiveStallSpeed;
            float flapRatio = flaps == null
                ? 0f
                : Mathf.Clamp01(flaps.ActualAngle / 30f);
            float approachMin = safeApproachSpeedMin - flapRatio * 15f;
            float approachMax = safeApproachSpeedMax - flapRatio * 15f;
            Color speedColor = parkedWindAirflow
                ? normalColor
                : stall != null && stall.StallWarning
                ? dangerColor
                : airspeed < stallSpeed
                    ? dangerColor
                    : airspeed < approachMin
                        ? cautionColor
                        : airspeed <= approachMax
                            ? safeColor
                            : normalColor;
            string compactEnergyCue = awarenessAssist == null
                ? "STEADY"
                : awarenessAssist.EnergyCue.Contains("ENERGY LOW")
                    ? "ENERGY LOW ↓"
                    : awarenessAssist.AirspeedTrend > 0.45f
                        ? "ACCEL ↑"
                        : awarenessAssist.AirspeedTrend < -0.45f
                            ? "SLOWING ↓"
                            : "STEADY";
            airspeedInstrument?.SetDisplay(
                $"{airspeed:000}",
                parkedWindAirflow
                    ? $"KT\nGROUND SPEED {groundSpeed:0} KT\nWIND AIRFLOW"
                    : $"KT  {compactEnergyCue}" +
                      $"\nSTALL < {stallSpeed:0}   APP {approachMin:0}-{approachMax:0}",
                speedColor);

            float altitude = approach != null
                ? approach.AltitudeAboveRunway
                : flightPhysics == null ? 0f : flightPhysics.Altitude;
            altitudeInstrument?.SetDisplay(
                $"{Mathf.Max(0f, altitude):0000}",
                "METERS AGL\nRUNWAY RELATIVE",
                altitude < 15f && flightPhysics != null && flightPhysics.VerticalSpeed < -1f
                    ? cautionColor
                    : normalColor);

            float verticalSpeed =
                flightPhysics == null ? 0f : flightPhysics.VerticalSpeed;
            verticalSpeedInstrument?.SetDisplay(
                $"{verticalSpeed:+0.0;-0.0;0.0}",
                "M/S\nCLIMB +   DESCENT -",
                verticalSpeed > 0.25f
                    ? safeColor
                    : verticalSpeed < -2.5f
                        ? cautionColor
                        : normalColor);

            float heading = flightPhysics == null ? 0f : flightPhysics.Heading;
            string headingDetail = approach != null && approach.HasRunwayTarget
                ? $"{(locationManager?.CurrentAirport?.airportName ?? "AIRPORT")}\n" +
                  $"RWY {approach.RunwayHeading:000}°   ERR {approach.AlignmentError:+0.0;-0.0;0.0}°"
                : "MAGNETIC HEADING";
            headingDetail = BuildHeadingDetail();
            headingInstrument?.SetDisplay(
                $"{heading:000}°",
                headingDetail,
                approach != null && Mathf.Abs(approach.AlignmentError) < 5f
                    ? safeColor
                    : normalColor);

        }

        private void UpdateStallWarning()
        {
            if (warningLabel == null) return;
            bool stallActive = stall != null && stall.StallWarning &&
                (flightPhysics == null || !flightPhysics.IsPhysicallyGrounded);
            bool flapWarning = flaps != null && flaps.SpeedWarning;
            bool warning = stallActive || flapWarning;
            warningLabel.gameObject.SetActive(warning);
            if (!warning) return;

            warningLabel.text = stallActive
                ? stall.State == StallController.StallState.Stalled
                    ? "STALL"
                    : "STALL WARNING"
                : "TOO FAST FOR FLAPS";
            float blink = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
            Color warningColor = stallActive ? dangerColor : cautionColor;
            warningLabel.color = Color.Lerp(
                warningColor, Color.white, blink * 0.45f);
        }

        private string BuildHeadingDetail()
        {
            return approach != null && approach.HasRunwayTarget
                ? $"{(locationManager?.CurrentAirport?.airportName ?? "AIRPORT")}\n" +
                  $"RWY {approach.RunwayHeading:000}°"
                : "MAGNETIC HEADING";
        }

        private void UpdateAwarenessCue()
        {
            if (awarenessLabel == null) return;
            string cue = awarenessAssist != null &&
                         awarenessAssist.HUDCuesEnabled
                ? awarenessAssist.AwarenessCue
                : string.Empty;
            bool visible = !string.IsNullOrWhiteSpace(cue);
            awarenessLabel.gameObject.SetActive(visible);
            if (!visible) return;
            awarenessLabel.text = cue;
            awarenessLabel.color =
                cue.Contains("STALL") || cue.Contains("SINK")
                    ? dangerColor
                    : cue.Contains("LOW SPEED") ||
                      cue.Contains("NOT ALIGNED")
                        ? cautionColor
                        : normalColor;
        }

        private void UpdateLandingFeedback()
        {
            if (landing == null || landingFeedbackGroup == null ||
                landingFeedbackLabel == null)
                return;

            if (landing.Quality != LandingController.LandingQuality.None &&
                landing.Quality != lastLandingQuality)
            {
                lastLandingQuality = landing.Quality;
                landingFeedbackTimer = landingFeedbackDuration;
                string wheelMessage = landing.NoseWheelFirstWarning
                    ? "\nNOSE WHEEL FIRST"
                    : "\nMAIN WHEELS FIRST";
                string bankMessage =
                    Mathf.Abs(landing.TouchdownBank) >
                    Mathf.Max(0.1f, touchdownBankWarningAngle)
                        ? $"\nBANK HIGH  {Mathf.Abs(landing.TouchdownBank):0.0}°"
                        : string.Empty;
                landingFeedbackLabel.text =
                    $"{landing.Quality.ToString().ToUpperInvariant()} LANDING" +
                    $"\n{landing.TouchdownVerticalSpeed:0.00} m/s" +
                    wheelMessage +
                    bankMessage;
            }
            else if (landing.Quality == LandingController.LandingQuality.None)
            {
                lastLandingQuality = LandingController.LandingQuality.None;
            }

            landingFeedbackTimer = Mathf.Max(
                0f, landingFeedbackTimer - Time.unscaledDeltaTime);
            landingFeedbackGroup.alpha = landingFeedbackTimer <= 0f
                ? 0f
                : Mathf.Clamp01(landingFeedbackTimer / landingFeedbackFadeDuration);
        }

        private void UpdateHoldAssist()
        {
            if (holdAssistLabel == null) return;
            GameObject holdPanel = holdAssistLabel.transform.parent == null
                ? holdAssistLabel.gameObject
                : holdAssistLabel.transform.parent.gameObject;
            if (holdAssist == null ||
                (!holdAssist.PitchHoldActive && !holdAssist.VerticalSpeedHoldActive))
            {
                holdPanel.SetActive(false);
                return;
            }

            holdPanel.SetActive(true);
            // Older HUD behavior disabled only the label, so explicitly restore
            // it when a hold mode becomes active.
            holdAssistLabel.gameObject.SetActive(true);

            const float FeetPerMinutePerMeterPerSecond = 196.8504f;
            float targetVerticalSpeedFeetPerMinute = Mathf.Round(
                holdAssist.TargetVerticalSpeed *
                FeetPerMinutePerMeterPerSecond);

            string text = holdAssist.PitchHoldActive
                ? $"<color=#E0F2FF>PITCH HOLD</color>\n" +
                  $"<color=#26F2FF>TARGET {holdAssist.TargetPitch:+0.0;-0.0;0.0}°</color>"
                : $"<color=#E0F2FF>VS HOLD</color>\n" +
                  $"<color=#26F2FF>TARGET {targetVerticalSpeedFeetPerMinute:+0;-0;0} FT/MIN</color>";
            if (holdAssist.VerticalSpeedHoldActive &&
                holdAssist.VerticalSpeedEnergyLimited)
                text += "\n<color=#FFA811>ADD POWER</color>";

            bool visible = !string.IsNullOrEmpty(text);
            holdPanel.SetActive(visible);
            if (!visible) return;
            holdAssistLabel.text = text;
            holdAssistLabel.color = FlightHUDStyle.White;
        }
    }
}
