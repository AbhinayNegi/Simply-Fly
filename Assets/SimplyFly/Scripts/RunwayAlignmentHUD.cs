using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    /// <summary>Read-only final-approach geometry display; never sends control input.</summary>
    [DisallowMultipleComponent]
    public sealed class RunwayAlignmentHUD : MonoBehaviour
    {
        private const float MaximumDistanceMeters = 18520f;
        private const float MaximumAltitudeMeters = 915f;
        [SerializeField] private NavigationManager navigation;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [Min(0.01f)] [SerializeField] private float smoothingTime = 0.18f;

        private RectTransform root;
        private RectTransform aircraftMarker;
        private TMP_Text runwayLabel;
        private TMP_Text offsetLabel;
        private TMP_Text turnLabel;
        private TMP_Text distanceLabel;
        private TMP_Text leftArrow;
        private TMP_Text rightArrow;
        private TMP_Text aircraftSymbol;
        private CanvasGroup group;
        private readonly List<Image> guidanceLines = new List<Image>();
        private float displayedOffset;
        private float offsetVelocity;
        private float displayedHeadingError;
        private float headingVelocity;
        private float displayedDistance;
        private float distanceVelocity;
        private bool settingEnabled = true;

        private static readonly Color Cyan = new Color(0.15f, 0.95f, 1f, 0.95f);
        private static readonly Color Amber = new Color(1f, 0.68f, 0.08f, 0.98f);
        private static readonly Color White = new Color(0.9f, 0.97f, 1f, 0.92f);

        public void Bind(NavigationManager source, FlightPhysicsController physics)
        {
            navigation = source;
            flightPhysics = physics;
        }

        public void SetSettingEnabled(bool enabled)
        {
            settingEnabled = enabled;
            if (group != null && !enabled) group.alpha = 0f;
        }

        private void Awake()
        {
            settingEnabled = PlayerPrefs.GetInt("settings.runwayAlignment", 1) != 0;
            if (navigation == null) navigation = FindAnyObjectByType<NavigationManager>();
            if (flightPhysics == null) flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
            Build();
        }

        private void LateUpdate()
        {
            if (root == null) Build();
            bool visible = IsValidApproach();
            group.alpha = visible ? 1f : 0f;
            if (!visible) return;

            displayedOffset = Mathf.SmoothDamp(
                displayedOffset, navigation.LateralOffset, ref offsetVelocity,
                smoothingTime, Mathf.Infinity, Time.unscaledDeltaTime);
            displayedHeadingError = Mathf.SmoothDampAngle(
                displayedHeadingError, navigation.AlignmentError,
                ref headingVelocity, smoothingTime, Mathf.Infinity,
                Time.unscaledDeltaTime);
            displayedDistance = Mathf.SmoothDamp(
                displayedDistance, navigation.DistanceToThreshold,
                ref distanceVelocity, smoothingTime, Mathf.Infinity,
                Time.unscaledDeltaTime);

            bool largeError = Mathf.Abs(displayedOffset) > 100f ||
                              Mathf.Abs(displayedHeadingError) > 10f;
            Color guidance = largeError ? Amber : Cyan;
            string side = Mathf.Abs(displayedOffset) < 2f
                ? "C" : displayedOffset < 0f ? "L" : "R";
            string turnSide = Mathf.Abs(displayedHeadingError) < 1f
                ? "C" : displayedHeadingError < 0f ? "L" : "R";

            runwayLabel.text = navigation.SelectedArrivalRunway;
            offsetLabel.text = $"{Mathf.Abs(displayedOffset) / 1852f:0.00}\n<size=15>{side}</size>";
            turnLabel.text = $"{Mathf.Abs(displayedHeadingError):0}°\n<size=15>{turnSide}</size>";
            distanceLabel.text = $"{displayedDistance / 1852f:0.0} NM";
            runwayLabel.color = guidance;
            offsetLabel.color = guidance;
            turnLabel.color = guidance;
            distanceLabel.color = White;
            leftArrow.color = guidance;
            rightArrow.color = guidance;
            aircraftSymbol.color = guidance;
            for (int i = 0; i < guidanceLines.Count; i++)
                guidanceLines[i].color = new Color(
                    guidance.r, guidance.g, guidance.b, 0.86f);
            aircraftMarker.anchoredPosition = new Vector2(
                Mathf.Clamp(displayedOffset / 300f, -1f, 1f) * 82f, -30f);
        }

        private bool IsValidApproach()
        {
            if (!settingEnabled || navigation == null || flightPhysics == null ||
                !navigation.IsActive || navigation.ArrivalAirport == null ||
                string.IsNullOrWhiteSpace(navigation.SelectedArrivalRunway) ||
                flightPhysics.IsPhysicallyGrounded ||
                navigation.DistanceToThreshold <= 5f ||
                navigation.DistanceToThreshold > MaximumDistanceMeters ||
                navigation.CurrentAltitudeAGL > MaximumAltitudeMeters)
                return false;

            NavigationPhase phase = navigation.Phase;
            if (phase != NavigationPhase.Arrival &&
                phase != NavigationPhase.FinalIntercept &&
                phase != NavigationPhase.FinalApproach)
                return false;

            Vector3 runwayForward = Quaternion.Euler(
                0f, navigation.SelectedRunwayHeading, 0f) * Vector3.forward;
            Vector3 fromThreshold = navigation.AircraftPosition -
                                    navigation.SelectedRunwayThreshold;
            return Vector3.Dot(fromThreshold, runwayForward) < 0f;
        }

        private void Build()
        {
            if (root != null) return;
            root = Rect("Runway Alignment Guidance", transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 72f);
            root.sizeDelta = new Vector2(440f, 126f);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.01f, 0.035f, 0.05f, 0.54f);
            Outline frame = root.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(0.25f, 0.7f, 0.72f, 0.42f);
            frame.effectDistance = new Vector2(1f, -1f);

            runwayLabel = Text("Runway", root, 24f, FontStyles.Normal,
                new Vector2(0f, 47f), new Vector2(150f, 30f));
            distanceLabel = Text("Distance", root, 13f, FontStyles.Normal,
                new Vector2(0f, 25f), new Vector2(120f, 19f));
            offsetLabel = Text("Offset", root, 27f, FontStyles.Normal,
                new Vector2(-145f, -3f), new Vector2(115f, 58f));
            turnLabel = Text("Turn", root, 27f, FontStyles.Normal,
                new Vector2(145f, -3f), new Vector2(115f, 58f));
            leftArrow = Text("Left Guidance Arrow", root, 31f, FontStyles.Bold,
                new Vector2(-205f, -2f), new Vector2(28f, 42f));
            leftArrow.text = "<<";
            rightArrow = Text("Right Guidance Arrow", root, 31f, FontStyles.Bold,
                new Vector2(205f, -2f), new Vector2(28f, 42f));
            rightArrow.text = ">>";

            AddGuidanceLine("Runway Left", new Vector2(-31f, -15f),
                new Vector2(2f, 59f), -36f);
            AddGuidanceLine("Runway Right", new Vector2(31f, -15f),
                new Vector2(2f, 59f), 36f);
            AddGuidanceLine("Runway Far", new Vector2(0f, 12f),
                new Vector2(57f, 2f), 0f);
            AddGuidanceLine("Runway Near", new Vector2(0f, -39f),
                new Vector2(105f, 2f), 0f);
            for (int i = 0; i < 3; i++)
                AddGuidanceLine($"Runway Center Dash {i}",
                    new Vector2(0f, -25f + i * 16f), new Vector2(2f, 9f), 0f);

            AddGuidanceLine("Deviation Scale", new Vector2(0f, -51f),
                new Vector2(300f, 2f), 0f);
            for (int i = -5; i <= 5; i++)
                AddGuidanceLine($"Deviation Tick {i}",
                    new Vector2(i * 27f, -47f),
                    new Vector2(2f, i == 0 ? 15f : 9f), 0f);

            aircraftMarker = Rect("Centerline Offset Marker", root);
            aircraftMarker.anchorMin = aircraftMarker.anchorMax = new Vector2(0.5f, 0.5f);
            aircraftMarker.anchoredPosition = new Vector2(0f, -30f);
            aircraftMarker.sizeDelta = new Vector2(36f, 38f);
            aircraftSymbol = aircraftMarker.gameObject.AddComponent<TextMeshProUGUI>();
            aircraftSymbol.text = "▲";
            aircraftSymbol.fontSize = 36f;
            aircraftSymbol.alignment = TextAlignmentOptions.Center;
            aircraftSymbol.raycastTarget = false;
            aircraftSymbol.color = Cyan;
        }

        private void AddGuidanceLine(string name, Vector2 position,
            Vector2 size, float rotation)
        {
            RectTransform line = Rect(name, root);
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = position;
            line.sizeDelta = size;
            line.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = line.gameObject.AddComponent<Image>();
            image.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.86f);
            guidanceLines.Add(image);
        }

        private static TMP_Text Text(string name, Transform parent, float size,
            FontStyles style, Vector2 position, Vector2 dimensions)
        {
            TMP_Text text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.color = White;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax =
                new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = dimensions;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
