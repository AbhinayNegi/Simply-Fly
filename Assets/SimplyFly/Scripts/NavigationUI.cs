using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class NavigationUI : MonoBehaviour
    {
        [SerializeField] private NavigationManager navigation;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private GameFlowController gameFlow;
        [SerializeField] private SideViewTrainingAid pitchCoach;
        [SerializeField] private FlightHUDController flightHUD;
        [SerializeField] private FlightHoldAssistController holdAssist;
        [SerializeField] private ApproachTrajectoryVisualizer trajectoryVisualizer;
        [SerializeField] private bool navigationHUDEnabled = true;
        [SerializeField] private bool navigationMapEnabled = true;
        [SerializeField] private bool headingBugEnabled = true;
        [SerializeField] private bool approachHelpersEnabled = true;
        [SerializeField] private bool sideViewApproachHelperEnabled = true;
        [SerializeField] private bool topDownApproachHelperEnabled = true;
        [SerializeField] private bool trajectoryVisualizerEnabled = true;
        [SerializeField] private ApproachGuidanceDisplayMode guidanceMode =
            ApproachGuidanceDisplayMode.TwoDHelpers;
        [SerializeField] private bool autoShowOnFinal = true;
        [SerializeField] private float sideViewShowDistanceKm = 4f;
        [SerializeField] private float topDownShowDistanceKm = 4f;
        [SerializeField] private float approachCorridorWidthFar = 250f;
        [SerializeField] private float approachCorridorWidthNear = 35f;
        [SerializeField] private float lateralOffsetToleranceMeters = 25f;
        [SerializeField] private float alignmentToleranceDegrees = 4f;
        [SerializeField] private float mapZoom = 1f;
        [SerializeField] private bool verticalProfileEnabled = true;
        [Header("Side-view auto zoom")]
        [SerializeField] private bool sideViewAutoZoomEnabled = true;
        [SerializeField] private float farViewDistanceMeters = 4000f;
        [SerializeField] private float midViewDistanceMeters = 2000f;
        [SerializeField] private float closeViewDistanceMeters = 800f;
        [SerializeField] private float flareViewDistanceMeters = 300f;
        [SerializeField] private float minHorizontalScaleMeters = 350f;
        [SerializeField] private float maxHorizontalScaleMeters = 4200f;
        [SerializeField] private float minVerticalScaleMeters = 35f;
        [SerializeField] private float maxVerticalScaleMeters = 280f;
        [SerializeField] private float zoomSmoothing = 3.5f;
        [SerializeField] private float glideSlopeOnPathToleranceMeters = 8f;
        [SerializeField] private float glideSlopeHighLowWarningMeters = 20f;

        public bool ApproachHelperActive { get; private set; }
        public string SideViewStatus { get; private set; } = "HIDDEN";
        public string TopDownStatus { get; private set; } = "HIDDEN";
        public ApproachGuidanceDisplayMode GuidanceMode => guidanceMode;
        public bool TrajectoryVisualizerActive =>
            trajectoryVisualizer != null && trajectoryVisualizer.Active;
        public string TrajectoryStatus =>
            trajectoryVisualizer == null
                ? "HIDDEN"
                : trajectoryVisualizer.Status.ToString();
        public ApproachTrajectoryVisualizer TrajectoryVisualizer =>
            trajectoryVisualizer;
        public float SideViewZoomLevel { get; private set; } = 1f;
        public string SideViewMode { get; private set; } = "FAR";
        public bool MapVisible =>
            mapVisible && navigation != null &&
            (navigation.IsActive ||
             navigation.Phase == NavigationPhase.Completed);

        private VisualElement navPanel;
        private Label routeLabel;
        private Label phaseLabel;
        private Label nextLabel;
        private Label guidanceLabel;
        private VisualElement headingBug;
        private VisualElement mapPanel;
        private NavigationMapElement map;
        private Label mapAltitudeLegend;
        private VerticalProfileElement verticalProfile;
        private VisualElement sidePanel;
        private GlideProfileElement glideProfile;
        private Label sideStatus;
        private VisualElement topPanel;
        private LateralApproachElement lateralProfile;
        private Label topStatus;
        private VisualElement trajectoryHudPanel;
        private Label trajectoryHudStatus;
        private Label modeToast;
        private bool mapVisible;
        private bool plannerSuppressed;
        private bool verticalProfileVisible = true;
        private float smoothedHorizontalScale = 4000f;
        private float smoothedVerticalScale = 260f;
        private float modeToastTimer;

        private void Start()
        {
            ResolveReferences();
            BuildUI();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[Key.M].wasPressedThisFrame)
            {
                if (keyboard.leftShiftKey.isPressed ||
                    keyboard.rightShiftKey.isPressed)
                    verticalProfileVisible = !verticalProfileVisible;
                else if (navigationMapEnabled)
                    mapVisible = !mapVisible;
            }
            if (keyboard != null && keyboard[Key.G].wasPressedThisFrame)
                CycleApproachGuidanceMode();
            if (modeToastTimer > 0f)
                modeToastTimer = Mathf.Max(
                    0f, modeToastTimer - Time.unscaledDeltaTime);
            Refresh();
        }

        private void OnDisable()
        {
            pitchCoach?.SetNavigationOverlaySuppressed(false);
            flightHUD?.SetNavigationMapSuppressed(false);
            trajectoryVisualizer?.SetActiveVisual(false);
        }

        public void ApplySettings(
            bool navHud, bool navMap, bool headingBugValue,
            bool helpers, bool side, bool top, bool trajectoryEnabled,
            ApproachGuidanceDisplayMode mode, bool autoShow)
        {
            navigationHUDEnabled = navHud;
            navigationMapEnabled = navMap;
            headingBugEnabled = headingBugValue;
            approachHelpersEnabled = helpers;
            sideViewApproachHelperEnabled = side;
            topDownApproachHelperEnabled = top;
            trajectoryVisualizerEnabled = false;
            guidanceMode =
                mode == ApproachGuidanceDisplayMode.Off
                    ? ApproachGuidanceDisplayMode.Off
                    : ApproachGuidanceDisplayMode.TwoDHelpers;
            autoShowOnFinal = autoShow;
            trajectoryVisualizer?.SetActiveVisual(false);
        }

        public void ShowApproachHelpersForLesson()
        {
            approachHelpersEnabled = true;
            sideViewApproachHelperEnabled = true;
            topDownApproachHelperEnabled = true;
            trajectoryVisualizerEnabled = false;
            guidanceMode = ApproachGuidanceDisplayMode.TwoDHelpers;
            autoShowOnFinal = true;
            mapVisible = false;
        }

        public void SetLegacyHeadingBugVisible(bool visible)
        {
            headingBugEnabled = visible;
            if (!visible && headingBug != null)
                headingBug.style.display = DisplayStyle.None;
        }

        public void SetTrajectorySettings(
            bool enabled,
            float predictionSeconds,
            float visualIntensity,
            bool showMarker,
            bool showLabels)
        {
            trajectoryVisualizerEnabled = false;
            trajectoryVisualizer?.ApplySettings(
                false,
                predictionSeconds,
                visualIntensity,
                showMarker,
                showLabels);
        }

        public void SetApproachGuidanceMode(ApproachGuidanceDisplayMode mode)
        {
            guidanceMode =
                mode == ApproachGuidanceDisplayMode.Off
                    ? ApproachGuidanceDisplayMode.Off
                    : ApproachGuidanceDisplayMode.TwoDHelpers;
            ShowModeToast();
        }

        public void SetVerticalProfileEnabled(bool enabled)
        {
            verticalProfileEnabled = enabled;
        }

        public void SetPlannerSuppressed(bool suppressed)
        {
            plannerSuppressed = suppressed;
            if (suppressed) mapVisible = false;
        }

        private void BuildUI()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;

            navPanel = MakePanel(root, 18f, 18f, 350f, 228f);
            routeLabel = AddLabel(navPanel, "NO ACTIVE FLIGHT PLAN", 15, true);
            phaseLabel = AddLabel(navPanel, "NAV INACTIVE", 12, false);
            nextLabel = AddLabel(navPanel, "NEXT: --", 14, true);
            guidanceLabel = AddLabel(navPanel, "M: MAP", 12, false);

            headingBug = new VisualElement();
            headingBug.style.position = Position.Absolute;
            headingBug.style.top = 8f;
            headingBug.style.left = Length.Percent(50);
            headingBug.style.width = 14f;
            headingBug.style.height = 14f;
            headingBug.style.rotate = new Rotate(45f);
            headingBug.style.backgroundColor = new Color(1f, 0.75f, 0.1f);
            root.Add(headingBug);

            mapPanel = MakePanel(root, null, 18f, 560f, 540f);
            mapPanel.style.right = 18f;
            AddLabel(mapPanel, "NAVIGATION MAP — NORTH UP", 14, true);
            map = new NavigationMapElement
            {
                navigation = navigation,
                zoom = mapZoom
            };
            map.style.flexGrow = 1f;
            map.style.marginTop = 6f;
            mapPanel.Add(map);
            mapAltitudeLegend = AddLabel(
                mapPanel, "ALTITUDE PROFILE", 11, false);
            verticalProfile =
                new VerticalProfileElement { navigation = navigation };
            verticalProfile.style.height = 120f;
            verticalProfile.style.marginTop = 3f;
            mapPanel.Add(verticalProfile);

            sidePanel = MakePanel(root, null, 18f, 430f, 200f);
            sidePanel.style.right = 18f;
            AddLabel(sidePanel, "GLIDE PROFILE — 3° PATH", 14, true);
            glideProfile = new GlideProfileElement { navigation = navigation };
            glideProfile.style.height = 112f;
            glideProfile.style.marginTop = 3f;
            sidePanel.Add(glideProfile);
            sideStatus = AddLabel(sidePanel, "ON GLIDE", 13, true);

            topPanel = MakePanel(root, null, 228f, 390f, 200f);
            topPanel.style.right = 18f;
            AddLabel(topPanel, "FINAL ALIGNMENT", 14, true);
            lateralProfile =
                new LateralApproachElement { navigation = navigation };
            lateralProfile.style.height = 108f;
            lateralProfile.style.marginTop = 3f;
            topPanel.Add(lateralProfile);
            topStatus = AddLabel(topPanel, "ALIGNED", 13, true);

            trajectoryHudPanel = MakePanel(root, null, 446f, 390f, 88f);
            trajectoryHudPanel.style.right = 18f;
            trajectoryHudStatus =
                AddLabel(trajectoryHudPanel, "PROJECTED TOUCHDOWN", 13, true);

            modeToast = AddLabel(root, string.Empty, 16, true);
            modeToast.style.position = Position.Absolute;
            modeToast.style.top = 286f;
            modeToast.style.left = Length.Percent(50f);
            modeToast.style.translate =
                new Translate(Length.Percent(-50f), 0f);
            modeToast.style.paddingLeft = 16f;
            modeToast.style.paddingRight = 16f;
            modeToast.style.paddingTop = 7f;
            modeToast.style.paddingBottom = 7f;
            modeToast.style.backgroundColor =
                new Color(0.01f, 0.06f, 0.075f, 0.86f);
            modeToast.style.unityTextAlign = TextAnchor.MiddleCenter;
            modeToast.style.display = DisplayStyle.None;
        }

        private void Refresh()
        {
            if (navPanel == null || navigation == null) return;
            bool active = navigation.IsActive ||
                          navigation.Phase == NavigationPhase.Completed;
            bool flightSchool =
                gameFlow != null &&
                gameFlow.Mode == SimplyFlyGameMode.FlightSchool;
            bool freeFlight =
                gameFlow != null &&
                gameFlow.Mode == SimplyFlyGameMode.FreeFlight;
            navPanel.style.display =
                navigationHUDEnabled && (active || freeFlight) &&
                !flightSchool && !mapVisible && !plannerSuppressed
                    ? DisplayStyle.Flex : DisplayStyle.None;
            mapPanel.style.display =
                navigationMapEnabled && mapVisible && active && !plannerSuppressed
                    ? DisplayStyle.Flex : DisplayStyle.None;
            headingBug.style.display =
                headingBugEnabled && active && !mapVisible && !plannerSuppressed
                    ? DisplayStyle.Flex : DisplayStyle.None;

            if (active)
            {
                routeLabel.text =
                    navigation.RouteName.ToUpperInvariant() + "\n" +
                    $"{navigation.SelectedDepartureRunway} → " +
                    $"{navigation.SelectedArrivalRunway}   " +
                    $"CRUISE: {navigation.CruiseAltitudeBand}";
                phaseLabel.text =
                    $"{navigation.Phase.ToString().ToUpperInvariant()}   " +
                    $"{navigation.RouteProgress * 100f:0}%";
                nextLabel.text =
                    $"NEXT: {navigation.CurrentWaypoint?.name ?? "COMPLETE"}";
                if (!navigation.ApproachClear)
                {
                    phaseLabel.text = "APPROACH BLOCKED";
                    nextLabel.text =
                        $"OBSTACLE ON FINAL: {navigation.BlockingObjectName}";
                    guidanceLabel.text =
                        "TRY OPPOSITE RUNWAY OR ANOTHER AIRPORT\n" +
                        "NAVIGATION CANNOT CLEAR THIS APPROACH";
                    phaseLabel.style.color = new Color(1f, 0.25f, 0.15f);
                    nextLabel.style.color = new Color(1f, 0.65f, 0.18f);
                }
                else
                {
                    phaseLabel.style.color = new Color(0.82f, 0.96f, 1f);
                    nextLabel.style.color = new Color(0.82f, 0.96f, 1f);
                    if (navigation.FinalApproachActive)
                    {
                        phaseLabel.text =
                            $"FINAL APPROACH — {navigation.SelectedArrivalRunway}";
                        nextLabel.text = "RUNWAY AHEAD — GLIDE PATH ACTIVE";
                    }
                    guidanceLabel.text =
                        $"DIST {FormatDistance(navigation.DistanceToWaypoint)}   " +
                        $"TARGET HDG {navigation.DesiredHeading:000}°\n" +
                        $"TARGET ALT {navigation.CurrentTargetAltitude:0} m\n" +
                        $"{navigation.VerticalInstruction}\n" +
                        $"SUGGESTED VS {navigation.SuggestedVerticalSpeed:+0.0;-0.0;0.0} m/s" +
                        (holdAssist != null &&
                         holdAssist.VerticalSpeedHoldActive
                            ? $"\nVS HOLD {holdAssist.TargetVerticalSpeed:+0.0;-0.0;0.0} m/s"
                            : string.Empty) +
                        "\nG: AIDS   M: MAP   SHIFT+M: PROFILE";
                }
                float bugOffset = Mathf.Clamp(
                    navigation.HeadingError / 60f, -1f, 1f);
                headingBug.style.left = Length.Percent(50f + bugOffset * 22f);
                map?.MarkDirtyRepaint();
                if (mapAltitudeLegend != null)
                {
                    mapAltitudeLegend.text = BuildAltitudeLegend();
                    mapAltitudeLegend.style.display =
                        verticalProfileEnabled && verticalProfileVisible
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                }
                if (verticalProfile != null)
                {
                    verticalProfile.style.display =
                        verticalProfileEnabled && verticalProfileVisible
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    verticalProfile.MarkDirtyRepaint();
                }
            }
            else if (freeFlight)
            {
                routeLabel.text = "NO FLIGHT PLAN";
                phaseLabel.text = "PRESS L TO OPEN FLIGHT PLANNER";
                nextLabel.text = string.Empty;
                guidanceLabel.text = string.Empty;
                phaseLabel.style.color = new Color(0.82f, 0.96f, 1f);
                nextLabel.style.color = new Color(0.82f, 0.96f, 1f);
            }

            bool notPaused = gameFlow == null ||
                             gameFlow.Mode != SimplyFlyGameMode.Paused;
            bool inRange =
                navigation.DistanceToThreshold <=
                Mathf.Max(sideViewShowDistanceKm, topDownShowDistanceKm) * 1000f;
            bool generallyFacingRunway =
                Mathf.Abs(navigation.AlignmentError) < 100f;
            ApproachHelperActive =
                approachHelpersEnabled && active && notPaused && !mapVisible &&
                inRange &&
                generallyFacingRunway &&
                (!autoShowOnFinal || navigation.FinalApproachActive);

            bool wants2D =
                guidanceMode == ApproachGuidanceDisplayMode.TwoDHelpers;
            bool wants3D = false;
            bool sideVisible = ApproachHelperActive &&
                               wants2D &&
                               sideViewApproachHelperEnabled &&
                               navigation.DistanceToThreshold <=
                               sideViewShowDistanceKm * 1000f;
            bool topVisible = ApproachHelperActive &&
                              wants2D &&
                              topDownApproachHelperEnabled &&
                              navigation.DistanceToThreshold <=
                              topDownShowDistanceKm * 1000f;
            bool trajectoryVisible = ApproachHelperActive &&
                                     wants3D &&
                                     trajectoryVisualizerEnabled;
            sidePanel.style.display =
                sideVisible ? DisplayStyle.Flex : DisplayStyle.None;
            topPanel.style.display =
                topVisible ? DisplayStyle.Flex : DisplayStyle.None;
            trajectoryVisualizer?.SetActiveVisual(trajectoryVisible);
            trajectoryHudPanel.style.display =
                trajectoryVisible && trajectoryVisualizer != null
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (trajectoryVisible && trajectoryVisualizer != null)
                trajectoryHudStatus.text = trajectoryVisualizer.HudMessage;
            UpdateApproachHelpers(sideVisible, topVisible);
            pitchCoach?.SetNavigationOverlaySuppressed(
                mapVisible && active || sideVisible || topVisible);
            flightHUD?.SetNavigationMapSuppressed(mapVisible && active);
            modeToast.style.display =
                modeToastTimer > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void CycleApproachGuidanceMode()
        {
            guidanceMode = guidanceMode == ApproachGuidanceDisplayMode.Off
                ? ApproachGuidanceDisplayMode.TwoDHelpers
                : ApproachGuidanceDisplayMode.Off;
            ShowModeToast();
        }

        private void ShowModeToast()
        {
            modeToastTimer = 1.8f;
            if (modeToast != null)
                modeToast.text =
                    $"APPROACH AIDS: {FormatGuidanceMode(guidanceMode)}";
        }

        private static string FormatGuidanceMode(
            ApproachGuidanceDisplayMode mode) => mode switch
        {
            ApproachGuidanceDisplayMode.Off => "OFF",
            ApproachGuidanceDisplayMode.TwoDHelpers => "2D HELPERS",
            ApproachGuidanceDisplayMode.ThreeDTrajectory => "2D HELPERS",
            ApproachGuidanceDisplayMode.Both => "2D HELPERS",
            _ => mode.ToString().ToUpperInvariant()
        };

        private void UpdateApproachHelpers(bool sideVisible, bool topVisible)
        {
            if (sideVisible)
            {
                float error = navigation.GlideSlopeAltitudeError;
                float tolerance = navigation.DistanceToTouchdownTarget <
                                  closeViewDistanceMeters
                    ? Mathf.Min(
                        glideSlopeOnPathToleranceMeters,
                        navigation.ThresholdCrossingHeightTolerance)
                    : glideSlopeOnPathToleranceMeters;
                UpdateSideViewZoom();
                SideViewStatus =
                    Mathf.Abs(error) <= tolerance
                        ? "ON GLIDE"
                        : error > 0f ? "HIGH — REDUCE ALTITUDE"
                        : "LOW — REDUCE DESCENT / ADD POWER";
                sideStatus.text =
                    $"{SideViewStatus}  {error:+0.0;-0.0;0.0} m   " +
                    $"{FormatDistance(navigation.DistanceToTouchdownTarget)} TO TOUCHDOWN\n" +
                    $"THRESHOLD {navigation.ThresholdCrossingHeight:0} m   " +
                    $"TOUCHDOWN ZONE +{navigation.TouchdownTargetDistanceFromThreshold:0} m";
                sideStatus.style.color =
                    Mathf.Abs(error) <= tolerance
                        ? new Color(0.25f, 1f, 0.48f)
                        : Mathf.Abs(error) >= glideSlopeHighLowWarningMeters
                            ? new Color(1f, 0.42f, 0.15f)
                            : new Color(1f, 0.75f, 0.18f);
                if (glideProfile != null)
                {
                    glideProfile.horizontalScale =
                        smoothedHorizontalScale;
                    glideProfile.verticalScale = smoothedVerticalScale;
                    glideProfile.flareMode = SideViewMode == "FLARE";
                    glideProfile.MarkDirtyRepaint();
                }
            }
            else SideViewStatus = "HIDDEN";

            if (topVisible)
            {
                float offset = navigation.LateralOffset;
                bool aligned =
                    Mathf.Abs(offset) <= lateralOffsetToleranceMeters &&
                    Mathf.Abs(navigation.AlignmentError) <=
                    alignmentToleranceDegrees;
                TopDownStatus = aligned
                    ? "ALIGNED"
                    : offset < 0f
                        ? "LEFT OF FINAL — TURN RIGHT TO INTERCEPT"
                        : "RIGHT OF FINAL — TURN LEFT TO INTERCEPT";
                topStatus.text =
                    $"{TopDownStatus}\nOFFSET {Mathf.Abs(offset):0} m   " +
                    $"ALIGN {navigation.AlignmentError:+0;-0;0}°";
                topStatus.style.color = aligned
                    ? new Color(0.25f, 1f, 0.48f)
                    : new Color(1f, 0.75f, 0.18f);
                if (lateralProfile != null)
                {
                    lateralProfile.corridorFar = approachCorridorWidthFar;
                    lateralProfile.corridorNear = approachCorridorWidthNear;
                    lateralProfile.showDistance =
                        topDownShowDistanceKm * 1000f;
                    lateralProfile.MarkDirtyRepaint();
                }
            }
            else TopDownStatus = "HIDDEN";
        }

        private void UpdateSideViewZoom()
        {
            float distance = navigation.DistanceToTouchdownTarget;
            float desiredHorizontal;
            float desiredVertical;
            if (!sideViewAutoZoomEnabled || distance > farViewDistanceMeters)
            {
                SideViewMode = "FAR";
                desiredHorizontal = maxHorizontalScaleMeters;
                desiredVertical = maxVerticalScaleMeters;
            }
            else if (distance > midViewDistanceMeters)
            {
                SideViewMode = "FAR";
                desiredHorizontal = Mathf.Max(
                    farViewDistanceMeters, distance * 1.12f);
                desiredVertical = maxVerticalScaleMeters;
            }
            else if (distance > closeViewDistanceMeters)
            {
                SideViewMode = "MID";
                desiredHorizontal = Mathf.Max(
                    midViewDistanceMeters, distance * 1.3f);
                desiredVertical = Mathf.Lerp(
                    110f, maxVerticalScaleMeters,
                    Mathf.InverseLerp(
                        closeViewDistanceMeters,
                        midViewDistanceMeters,
                        distance));
            }
            else if (distance > flareViewDistanceMeters)
            {
                SideViewMode = "CLOSE";
                desiredHorizontal = Mathf.Max(900f, distance * 1.45f);
                desiredVertical = Mathf.Lerp(
                    55f, 110f,
                    Mathf.InverseLerp(
                        flareViewDistanceMeters,
                        closeViewDistanceMeters,
                        distance));
            }
            else
            {
                SideViewMode = "FLARE";
                desiredHorizontal = Mathf.Max(
                    minHorizontalScaleMeters, distance + 280f);
                desiredVertical = minVerticalScaleMeters;
            }

            desiredHorizontal = Mathf.Clamp(
                desiredHorizontal,
                minHorizontalScaleMeters,
                maxHorizontalScaleMeters);
            desiredVertical = Mathf.Clamp(
                desiredVertical,
                minVerticalScaleMeters,
                maxVerticalScaleMeters);
            float blend = 1f - Mathf.Exp(
                -Mathf.Max(0.1f, zoomSmoothing) *
                Time.unscaledDeltaTime);
            smoothedHorizontalScale = Mathf.Lerp(
                smoothedHorizontalScale, desiredHorizontal, blend);
            smoothedVerticalScale = Mathf.Lerp(
                smoothedVerticalScale, desiredVertical, blend);
            SideViewZoomLevel =
                maxHorizontalScaleMeters /
                Mathf.Max(1f, smoothedHorizontalScale);
        }

        private static VisualElement MakePanel(
            VisualElement root, float? left, float? top,
            float width, float height)
        {
            VisualElement panel = new VisualElement();
            panel.style.position = Position.Absolute;
            if (left.HasValue) panel.style.left = left.Value;
            if (top.HasValue) panel.style.top = top.Value;
            panel.style.width = width;
            panel.style.height = height;
            panel.style.paddingLeft = 14f;
            panel.style.paddingRight = 14f;
            panel.style.paddingTop = 10f;
            panel.style.paddingBottom = 10f;
            panel.style.backgroundColor =
                new Color(0.01f, 0.06f, 0.075f, 0.82f);
            panel.style.borderTopLeftRadius = 7f;
            panel.style.borderTopRightRadius = 7f;
            panel.style.borderBottomLeftRadius = 7f;
            panel.style.borderBottomRightRadius = 7f;
            root.Add(panel);
            return panel;
        }

        private static Label AddLabel(
            VisualElement parent, string text, int size, bool bold)
        {
            Label label = new Label(text);
            label.style.fontSize = size;
            label.style.color = new Color(0.82f, 0.96f, 1f);
            label.style.unityFontStyleAndWeight =
                bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 3f;
            parent.Add(label);
            return label;
        }

        private static string FormatDistance(float meters) =>
            meters >= 1000f ? $"{meters / 1000f:0.0} km" : $"{meters:0} m";

        private void ResolveReferences()
        {
            if (navigation == null)
                navigation = FindAnyObjectByType<NavigationManager>();
            if (flightPhysics == null)
                flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
            if (gameFlow == null)
                gameFlow = FindAnyObjectByType<GameFlowController>();
            if (pitchCoach == null)
                pitchCoach = FindAnyObjectByType<SideViewTrainingAid>();
            if (flightHUD == null)
                flightHUD = FindAnyObjectByType<FlightHUDController>();
            if (holdAssist == null)
                holdAssist = FindAnyObjectByType<FlightHoldAssistController>();
            if (trajectoryVisualizer == null)
                trajectoryVisualizer =
                    FindAnyObjectByType<ApproachTrajectoryVisualizer>();
            trajectoryVisualizer?.SetActiveVisual(false);
        }

        private string BuildAltitudeLegend()
        {
            if (navigation == null) return string.Empty;
            System.Text.StringBuilder text = new("VERTICAL PROFILE  ");
            for (int i = 0; i < navigation.Waypoints.Count; i++)
            {
                NavigationWaypoint waypoint = navigation.Waypoints[i];
                if (i > 0) text.Append("  •  ");
                text.Append(waypoint.name);
                text.Append(' ');
                text.Append(waypoint.targetAltitude.ToString("0"));
                text.Append('m');
            }
            return text.ToString();
        }

        private sealed class VerticalProfileElement : VisualElement
        {
            public NavigationManager navigation;

            public VerticalProfileElement() => generateVisualContent += Draw;

            private void Draw(MeshGenerationContext context)
            {
                if (navigation == null || navigation.Waypoints.Count < 2)
                    return;
                Painter2D p = context.painter2D;
                Rect r = contentRect;
                float left = r.xMin + 12f;
                float right = r.xMax - 12f;
                float top = r.yMin + 8f;
                float bottom = r.yMax - 12f;
                float minAltitude = float.MaxValue;
                float maxAltitude = float.MinValue;
                foreach (NavigationWaypoint waypoint in navigation.Waypoints)
                {
                    minAltitude = Mathf.Min(
                        minAltitude, waypoint.targetAltitude);
                    maxAltitude = Mathf.Max(
                        maxAltitude, waypoint.targetAltitude);
                }
                minAltitude = Mathf.Max(0f, minAltitude - 30f);
                maxAltitude += 50f;
                Vector2 Point(int index, float altitude) => new(
                    Mathf.Lerp(
                        left, right,
                        index / (navigation.Waypoints.Count - 1f)),
                    Mathf.Lerp(
                        bottom, top,
                        Mathf.InverseLerp(
                            minAltitude, maxAltitude, altitude)));

                p.lineWidth = 1f;
                p.strokeColor = new Color(0.35f, 0.5f, 0.55f, 0.7f);
                for (int i = 0; i <= 3; i++)
                {
                    float y = Mathf.Lerp(top, bottom, i / 3f);
                    p.BeginPath(); p.MoveTo(new Vector2(left, y));
                    p.LineTo(new Vector2(right, y)); p.Stroke();
                }
                p.lineWidth = 2.5f;
                p.strokeColor = new Color(0.3f, 1f, 0.55f);
                p.BeginPath();
                p.MoveTo(Point(
                    0, navigation.Waypoints[0].targetAltitude));
                for (int i = 1; i < navigation.Waypoints.Count; i++)
                    p.LineTo(Point(
                        i, navigation.Waypoints[i].targetAltitude));
                p.Stroke();

                float routeX = Mathf.Lerp(
                    left, right, navigation.RouteProgress);
                float aircraftY = Mathf.Lerp(
                    bottom, top,
                    Mathf.InverseLerp(
                        minAltitude,
                        maxAltitude,
                        navigation.CurrentAltitude));
                p.fillColor = new Color(0.25f, 0.95f, 1f);
                p.BeginPath();
                p.Arc(new Vector2(routeX, aircraftY), 5f, 0f, 360f);
                p.Fill();
                p.lineWidth = 1.5f;
                p.strokeColor = Mathf.Abs(navigation.AltitudeError) < 30f
                    ? new Color(0.25f, 1f, 0.48f)
                    : new Color(1f, 0.7f, 0.18f);
                float targetY = Mathf.Lerp(
                    bottom, top,
                    Mathf.InverseLerp(
                        minAltitude,
                        maxAltitude,
                        navigation.RouteProfileTargetAltitude));
                p.BeginPath();
                p.MoveTo(new Vector2(routeX, aircraftY));
                p.LineTo(new Vector2(routeX, targetY));
                p.Stroke();
            }
        }

        private sealed class GlideProfileElement : VisualElement
        {
            public NavigationManager navigation;
            public float horizontalScale = 4000f;
            public float verticalScale = 280f;
            public bool flareMode;

            public GlideProfileElement() => generateVisualContent += Draw;

            private void Draw(MeshGenerationContext context)
            {
                if (navigation == null) return;
                Painter2D p = context.painter2D;
                Rect r = contentRect;
                float left = r.xMin + 34f;
                float right = r.xMax - 10f;
                float top = r.yMin + 8f;
                float bottom = r.yMax - 22f;
                float width = Mathf.Max(1f, right - left);
                float height = Mathf.Max(1f, bottom - top);
                float maxDistance = Mathf.Max(300f, horizontalScale);
                float maxAltitude = Mathf.Max(25f, verticalScale);

                p.lineWidth = 1f;
                p.strokeColor = new Color(0.38f, 0.52f, 0.56f, 0.7f);
                for (int i = 0; i <= 4; i++)
                {
                    float x = Mathf.Lerp(left, right, i / 4f);
                    p.BeginPath(); p.MoveTo(new Vector2(x, top));
                    p.LineTo(new Vector2(x, bottom)); p.Stroke();
                }
                for (int i = 0; i <= 3; i++)
                {
                    float y = Mathf.Lerp(bottom, top, i / 3f);
                    p.BeginPath(); p.MoveTo(new Vector2(left, y));
                    p.LineTo(new Vector2(right, y)); p.Stroke();
                }

                float touchdownInset = 25f;
                float touchdownX = right - touchdownInset;
                float thresholdX = touchdownX -
                    navigation.TouchdownTargetDistanceFromThreshold /
                    maxDistance * width;
                Vector2 touchdown = new(touchdownX, bottom);
                Vector2 threshold = new(
                    thresholdX,
                    bottom -
                    navigation.ThresholdCrossingHeight /
                    maxAltitude * height);
                float farHeight =
                    navigation.ThresholdCrossingHeight +
                    Mathf.Tan(
                        navigation.GlideSlopeAngle * Mathf.Deg2Rad) *
                    Mathf.Max(
                        0f,
                        maxDistance -
                        navigation.TouchdownTargetDistanceFromThreshold);
                Vector2 farIdeal = new(
                    left,
                    bottom - farHeight / maxAltitude * height);
                Vector2 farHigh = new(
                    left,
                    bottom - (farHeight + maxAltitude * 0.12f) /
                    maxAltitude * height);
                Vector2 farLow = new(
                    left,
                    bottom - Mathf.Max(
                        0f, farHeight - maxAltitude * 0.12f) /
                    maxAltitude * height);
                p.fillColor = new Color(0.25f, 0.95f, 0.55f, 0.13f);
                p.BeginPath();
                p.MoveTo(touchdown); p.LineTo(farHigh);
                p.LineTo(farLow); p.ClosePath(); p.Fill();
                p.lineWidth = 2f;
                p.strokeColor = new Color(0.3f, 1f, 0.55f);
                p.BeginPath(); p.MoveTo(touchdown);
                p.LineTo(threshold); p.LineTo(farIdeal); p.Stroke();

                p.lineWidth = 3f;
                p.strokeColor = Color.white;
                p.BeginPath(); p.MoveTo(new Vector2(thresholdX, bottom - 8f));
                p.LineTo(new Vector2(thresholdX, bottom + 3f)); p.Stroke();
                p.lineWidth = 5f;
                p.strokeColor = new Color(0.3f, 1f, 0.55f);
                p.BeginPath(); p.MoveTo(new Vector2(touchdownX, bottom - 8f));
                p.LineTo(new Vector2(touchdownX, bottom + 3f)); p.Stroke();
                p.lineWidth = 1.5f;
                p.strokeColor = new Color(0.65f, 0.88f, 0.92f);
                p.BeginPath(); p.MoveTo(new Vector2(left, bottom));
                p.LineTo(new Vector2(right, bottom)); p.Stroke();
                p.lineWidth = 4f;
                p.strokeColor = Color.white;
                p.BeginPath(); p.MoveTo(new Vector2(thresholdX, bottom));
                p.LineTo(new Vector2(right, bottom)); p.Stroke();

                if (flareMode)
                {
                    p.fillColor = new Color(1f, 0.75f, 0.15f, 0.16f);
                    p.BeginPath();
                    p.MoveTo(new Vector2(thresholdX, bottom - 8f));
                    p.LineTo(new Vector2(touchdownX, bottom - 8f));
                    p.LineTo(touchdown);
                    p.LineTo(new Vector2(thresholdX, bottom));
                    p.ClosePath(); p.Fill();
                }

                float distance = Mathf.Clamp(
                    navigation.DistanceToTouchdownTarget, 0f, maxDistance);
                float actualAltitude =
                    navigation.TargetGlideSlopeAltitude +
                    navigation.GlideSlopeAltitudeError -
                    (navigation.ArrivalAirport?.ThresholdPoint.y ?? 0f);
                float planeX =
                    touchdownX - distance / maxDistance * width;
                float planeY = bottom -
                    Mathf.Clamp(actualAltitude, 0f, maxAltitude) /
                    maxAltitude * height;
                DrawSideAircraft(
                    p,
                    new Vector2(planeX, planeY),
                    Mathf.Clamp(navigation.AlignmentError * 0.05f, -5f, 5f));
            }

            private static void DrawSideAircraft(
                Painter2D p, Vector2 center, float angle)
            {
                float rad = angle * Mathf.Deg2Rad;
                Vector2 Rotate(float x, float y) => center + new Vector2(
                    x * Mathf.Cos(rad) - y * Mathf.Sin(rad),
                    x * Mathf.Sin(rad) + y * Mathf.Cos(rad));
                p.lineWidth = 2.2f;
                p.strokeColor = new Color(0.25f, 0.95f, 1f);
                p.BeginPath();
                p.MoveTo(Rotate(-25f, 2f));
                p.LineTo(Rotate(20f, 2f));
                p.LineTo(Rotate(27f, 0f));
                p.LineTo(Rotate(19f, -3f));
                p.LineTo(Rotate(-18f, -3f));
                p.LineTo(Rotate(-25f, 2f));
                p.LineTo(Rotate(-20f, -11f));
                p.LineTo(Rotate(-15f, -11f));
                p.LineTo(Rotate(-12f, 2f));
                p.Stroke();
                p.BeginPath();
                p.MoveTo(Rotate(-2f, 2f));
                p.LineTo(Rotate(8f, 11f));
                p.LineTo(Rotate(12f, 11f));
                p.LineTo(Rotate(7f, 1f));
                p.Stroke();
            }
        }

        private sealed class LateralApproachElement : VisualElement
        {
            public NavigationManager navigation;
            public float corridorFar = 250f;
            public float corridorNear = 35f;
            public float showDistance = 4000f;

            public LateralApproachElement() => generateVisualContent += Draw;

            private void Draw(MeshGenerationContext context)
            {
                if (navigation == null) return;
                Painter2D p = context.painter2D;
                Rect r = contentRect;
                Vector2 threshold = new(r.center.x, r.yMax - 38f);
                Vector2 touchdown = new(r.center.x, r.yMax - 14f);
                float top = r.yMin + 10f;
                float halfFar = r.width * 0.42f;
                float halfNear = r.width * 0.08f;
                p.fillColor = new Color(0.25f, 0.95f, 0.55f, 0.10f);
                p.BeginPath();
                p.MoveTo(new Vector2(r.center.x - halfFar, top));
                p.LineTo(new Vector2(r.center.x + halfFar, top));
                p.LineTo(threshold + new Vector2(halfNear, 0f));
                p.LineTo(threshold - new Vector2(halfNear, 0f));
                p.ClosePath(); p.Fill();
                p.lineWidth = 1.5f;
                p.strokeColor = new Color(0.3f, 1f, 0.55f);
                p.BeginPath(); p.MoveTo(new Vector2(r.center.x, top));
                p.LineTo(threshold); p.Stroke();
                p.BeginPath();
                p.MoveTo(new Vector2(r.center.x - halfFar, top));
                p.LineTo(threshold - new Vector2(halfNear, 0f)); p.Stroke();
                p.BeginPath();
                p.MoveTo(new Vector2(r.center.x + halfFar, top));
                p.LineTo(threshold + new Vector2(halfNear, 0f)); p.Stroke();
                p.lineWidth = 5f; p.strokeColor = Color.white;
                p.BeginPath(); p.MoveTo(threshold - new Vector2(22f, 0f));
                p.LineTo(threshold + new Vector2(22f, 0f)); p.Stroke();
                p.lineWidth = 2f;
                p.strokeColor = new Color(0.3f, 1f, 0.55f);
                p.BeginPath(); p.MoveTo(threshold);
                p.LineTo(touchdown); p.Stroke();
                p.lineWidth = 5f;
                p.BeginPath(); p.MoveTo(touchdown - new Vector2(12f, 0f));
                p.LineTo(touchdown + new Vector2(12f, 0f)); p.Stroke();

                float distanceRatio = Mathf.Clamp01(
                    navigation.DistanceToTouchdownTarget /
                    Mathf.Max(1f, showDistance));
                float planeY = Mathf.Lerp(threshold.y - 8f, top + 10f, distanceRatio);
                float corridor = Mathf.Lerp(
                    corridorNear, corridorFar, distanceRatio);
                float planeX = r.center.x +
                    Mathf.Clamp(
                        navigation.LateralOffset / Mathf.Max(1f, corridor),
                        -1.2f, 1.2f) * halfFar;
                DrawAircraft(p, new Vector2(planeX, planeY));
            }

            private static void DrawAircraft(Painter2D p, Vector2 c)
            {
                p.fillColor = new Color(0.25f, 0.95f, 1f);
                p.BeginPath();
                p.MoveTo(c + new Vector2(0f, -9f));
                p.LineTo(c + new Vector2(-5f, 7f));
                p.LineTo(c + new Vector2(0f, 4f));
                p.LineTo(c + new Vector2(5f, 7f));
                p.ClosePath(); p.Fill();
                p.lineWidth = 2f;
                p.strokeColor = new Color(0.25f, 0.95f, 1f);
                p.BeginPath(); p.MoveTo(c + new Vector2(-13f, 0f));
                p.LineTo(c + new Vector2(13f, 0f)); p.Stroke();
            }
        }

        private sealed class NavigationMapElement : VisualElement
        {
            public NavigationManager navigation;
            public float zoom = 1f;

            public NavigationMapElement()
            {
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                if (navigation == null || navigation.Waypoints.Count == 0)
                    return;
                Painter2D painter = context.painter2D;
                Rect bounds = contentRect;
                Vector3 center = navigation.CurrentWaypoint?.position ??
                                 navigation.Waypoints[0].position;
                float extent = 1000f;
                foreach (NavigationWaypoint waypoint in navigation.Waypoints)
                    extent = Mathf.Max(
                        extent,
                        Vector2.Distance(
                            new Vector2(center.x, center.z),
                            new Vector2(waypoint.position.x, waypoint.position.z)));
                extent *= 1.15f / Mathf.Max(0.25f, zoom);

                Vector2 Map(Vector3 world) => new(
                    bounds.center.x + (world.x - center.x) / extent *
                    bounds.width * 0.45f,
                    bounds.center.y - (world.z - center.z) / extent *
                    bounds.height * 0.45f);

                painter.lineWidth = 2f;
                painter.strokeColor = new Color(0.2f, 0.8f, 0.9f);
                painter.BeginPath();
                painter.MoveTo(Map(navigation.Waypoints[0].position));
                for (int i = 1; i < navigation.Waypoints.Count; i++)
                    painter.LineTo(Map(navigation.Waypoints[i].position));
                painter.Stroke();

                for (int i = 0; i < navigation.Waypoints.Count; i++)
                {
                    Vector2 point = Map(navigation.Waypoints[i].position);
                    painter.fillColor = i == navigation.WaypointIndex
                        ? new Color(1f, 0.75f, 0.1f)
                        : new Color(0.35f, 1f, 0.55f);
                    painter.BeginPath();
                    painter.Arc(point, i == navigation.WaypointIndex ? 6f : 4f,
                        0f, 360f);
                    painter.Fill();
                }

                Vector2 aircraftPoint = Map(navigation.AircraftPosition);
                painter.fillColor = Color.white;
                painter.BeginPath();
                painter.MoveTo(aircraftPoint + new Vector2(0f, -8f));
                painter.LineTo(aircraftPoint + new Vector2(-6f, 7f));
                painter.LineTo(aircraftPoint + new Vector2(6f, 7f));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}
