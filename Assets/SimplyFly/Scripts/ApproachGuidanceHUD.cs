using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimplyFly.GroundOps
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ApproachGuidanceHUD : MonoBehaviour
    {
        private const float GsOnPathThresholdDeg = 0.3f;
        // Match NavigationManager's existing 25 m centerline tolerance.
        private const float LatOnPathThresholdNm = 25f / 1852f;
        private const float GsClampDeg = 3f;
        private const float LatClampNm = 1f;
        private const float DisplayRangeMeters = 4000f;

        private static readonly Color OnTargetGreen =
            new(0.290f, 0.871f, 0.502f);
        private static readonly Color OffTargetAmber =
            new(0.961f, 0.647f, 0.141f);
        private static readonly Vector2 FarPoint = new(100f, 32f);
        private static readonly Vector2 RunwayPoint = new(0f, 60f);

        [SerializeField] private NavigationManager navigation;
        [SerializeField] private FlightPhysicsController flightPhysics;

        private UIDocument document;
        private VisualElement panel;
        private Label gsValue;
        private Label gsStatus;
        private Label latValue;
        private Label latStatus;
        private VisualElement aircraftDot;
        private VisualElement aircraftFuselage;
        private VisualElement aircraftRing;
        private VisualElement trackMarker;
        private SideViewTrainingAid pitchCoach;
        private bool initialized;
        private bool shown;
        private bool settingEnabled = true;

        public void Bind(NavigationManager source, FlightPhysicsController physics)
        {
            navigation = source;
            flightPhysics = physics;
        }

        private void Awake()
        {
            settingEnabled = PlayerPrefs.GetInt(
                "settings.runwayAlignment", 1) != 0;
            if (navigation == null)
                navigation = FindAnyObjectByType<NavigationManager>();
            if (flightPhysics == null)
                flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
            pitchCoach = FindAnyObjectByType<SideViewTrainingAid>();
            document = GetComponent<UIDocument>();
            ConfigureDocument();
        }

        public void SetSettingEnabled(bool enabled)
        {
            settingEnabled = enabled;
            if (!enabled && initialized) SetShown(false);
        }

        private IEnumerator Start()
        {
            yield return null;
            InitializeVisualTree();
        }

        private void ConfigureDocument()
        {
            if (document.panelSettings == null)
            {
                foreach (UIDocument candidate in FindObjectsByType<UIDocument>(
                             FindObjectsInactive.Include))
                {
                    if (candidate == document || candidate.panelSettings == null)
                        continue;
                    document.panelSettings = candidate.panelSettings;
                    break;
                }
            }

            document.visualTreeAsset = Resources.Load<VisualTreeAsset>(
                "ApproachGuidance/ApproachGuidanceHUD");
            document.sortingOrder = 70;
        }

        private void InitializeVisualTree()
        {
            VisualElement root = document.rootVisualElement;
            StyleSheet style = Resources.Load<StyleSheet>(
                "ApproachGuidance/ApproachGuidanceHUD");
            if (style != null && !root.styleSheets.Contains(style))
                root.styleSheets.Add(style);

            panel = root.Q<VisualElement>("approach-guidance-panel");
            gsValue = root.Q<Label>("glideslope-value");
            gsStatus = root.Q<Label>("glideslope-status");
            latValue = root.Q<Label>("centerline-value");
            latStatus = root.Q<Label>("centerline-status");
            aircraftDot = root.Q<VisualElement>("aircraft-dot");
            aircraftFuselage = root.Q<VisualElement>("aircraft-fuselage");
            aircraftRing = root.Q<VisualElement>("aircraft-ring");
            trackMarker = root.Q<VisualElement>("track-marker");
            initialized = panel != null && gsValue != null && gsStatus != null &&
                          latValue != null && latStatus != null &&
                          aircraftDot != null && aircraftFuselage != null &&
                          aircraftRing != null && trackMarker != null;
            if (!initialized)
            {
                Debug.LogError("Approach Guidance HUD assets failed to initialize.", this);
                return;
            }
            SetShown(false);
        }

        private void Update()
        {
            if (!initialized) return;
            bool shouldShow = ShouldShow();
            SetShown(shouldShow);
            if (!shouldShow) return;

            float horizontalDistance = Mathf.Max(1f, navigation.DistanceToThreshold);
            float glideslopeDeviation = Mathf.Atan2(
                navigation.GlideSlopeAltitudeError, horizontalDistance) *
                Mathf.Rad2Deg;
            float lateralNm = navigation.LateralOffset / 1852f;
            Refresh(glideslopeDeviation, lateralNm);
        }

        private bool ShouldShow()
        {
            if (!settingEnabled || navigation == null || flightPhysics == null ||
                !navigation.IsActive || !navigation.FinalApproachActive ||
                flightPhysics.IsPhysicallyGrounded ||
                navigation.DistanceToThreshold <= 5f ||
                navigation.DistanceToThreshold > DisplayRangeMeters)
                return false;

            Vector3 runwayForward = Quaternion.Euler(
                0f, navigation.SelectedRunwayHeading, 0f) * Vector3.forward;
            return Vector3.Dot(
                navigation.AircraftPosition - navigation.SelectedRunwayThreshold,
                runwayForward) < 0f;
        }

        private void SetShown(bool visible)
        {
            if (shown == visible && panel.resolvedStyle.display != DisplayStyle.None)
                return;
            shown = visible;
            panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            pitchCoach?.SetNavigationOverlaySuppressed(visible);
        }

        private void Refresh(float glideslopeDeg, float lateralOffsetNm)
        {
            bool gsOnPath = Mathf.Abs(glideslopeDeg) < GsOnPathThresholdDeg;
            Color gsColor = gsOnPath ? OnTargetGreen : OffTargetAmber;
            gsValue.text = $"{(glideslopeDeg >= 0f ? "+" : string.Empty)}{glideslopeDeg:0.0}°";
            gsStatus.text = gsOnPath ? "ON PATH" : glideslopeDeg > 0f ? "HIGH" : "LOW";
            gsStatus.style.color = gsColor;
            SetAircraftColor(gsColor);

            bool latOnPath = Mathf.Abs(lateralOffsetNm) < LatOnPathThresholdNm;
            Color latColor = latOnPath ? OnTargetGreen : OffTargetAmber;
            string side = latOnPath ? "C" : lateralOffsetNm > 0f ? "R" : "L";
            latValue.text = $"{side} {Mathf.Abs(lateralOffsetNm):0.00} NM";
            latStatus.text = latOnPath ? "ON CENTERLINE" :
                lateralOffsetNm > 0f ? "RIGHT" : "LEFT";
            latStatus.style.color = latColor;
            trackMarker.style.backgroundColor = latColor;
            float displayedLateralNm = latOnPath ? 0f : lateralOffsetNm;
            trackMarker.style.left = Length.Percent(Mathf.Clamp(
                50f + Mathf.Clamp(displayedLateralNm, -LatClampNm, LatClampNm) * 50f,
                2f, 98f));

            float progress = 1f - Mathf.Clamp01(
                navigation.DistanceToThreshold / DisplayRangeMeters);
            UpdateGlideslopeVisual(gsOnPath ? 0f : glideslopeDeg, progress);
            aircraftRing.style.opacity =
                0.35f + 0.65f * (0.5f + 0.5f *
                    Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.2f));
        }

        private void SetAircraftColor(Color color)
        {
            aircraftDot.style.backgroundColor = color;
            aircraftFuselage.style.backgroundColor = color;
            aircraftRing.style.borderTopColor = color;
            aircraftRing.style.borderBottomColor = color;
            aircraftRing.style.borderLeftColor = color;
            aircraftRing.style.borderRightColor = color;
        }

        private void UpdateGlideslopeVisual(float deviationDeg, float progress)
        {
            Vector2 direction = RunwayPoint - FarPoint;
            Vector2 normalized = direction.normalized;
            Vector2 perpendicular = new(-normalized.y, normalized.x);
            Vector2 onLine = Vector2.Lerp(FarPoint, RunwayPoint, progress);
            Vector2 dot = onLine + perpendicular *
                (Mathf.Clamp(deviationDeg, -GsClampDeg, GsClampDeg) * 6f);
            dot.x = Mathf.Clamp(dot.x, 6f, 94f);
            dot.y = Mathf.Clamp(dot.y, 8f, 56f);

            aircraftDot.style.left = Length.Percent(dot.x);
            aircraftDot.style.top = dot.y;
            aircraftRing.style.left = Length.Percent(dot.x);
            aircraftRing.style.top = dot.y;
            aircraftFuselage.style.left = Length.Percent(dot.x);
            aircraftFuselage.style.top = dot.y;
            aircraftFuselage.style.rotate = new Rotate(
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void OnDisable()
        {
            pitchCoach?.SetNavigationOverlaySuppressed(false);
        }
    }
}
