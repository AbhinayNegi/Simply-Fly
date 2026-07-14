using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    [DisallowMultipleComponent]
    public sealed class WindInstrumentHUD : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private WindController wind;
        [SerializeField] private FlightPhysicsController aircraft;

        [Header("Display")]
        [Min(0f)] [SerializeField] private float calmThresholdKnots = 2f;
        [Min(0f)] [SerializeField] private float moderateCrosswindKnots = 8f;
        [Min(0f)] [SerializeField] private float strongCrosswindKnots = 15f;
        [Min(0.01f)] [SerializeField] private float valueSmoothing = 0.18f;

        private TMP_Text arrow;
        private TMP_Text directionSpeed;
        private TMP_Text longitudinal;
        private TMP_Text crosswind;
        private float shownSpeed;
        private float shownHeadwind;
        private float shownCrosswind;
        private float shownAngle;
        private float shownFromHeading;
        private float speedVelocity;
        private float headVelocity;
        private float crossVelocity;
        private float angleVelocity;
        private float fromHeadingVelocity;
        private bool angleInitialized;

        private static readonly Color Cyan = FlightHUDStyle.Cyan;
        private static readonly Color White = FlightHUDStyle.White;
        private static readonly Color Green = FlightHUDStyle.Green;
        private static readonly Color Amber = FlightHUDStyle.Amber;
        private static readonly Color Red = FlightHUDStyle.Red;

        public void Bind(WindController source, FlightPhysicsController plane)
        {
            wind = source;
            aircraft = plane;
        }

        private void Awake()
        {
            if (wind == null) wind = FindAnyObjectByType<WindController>();
            if (aircraft == null) aircraft = FindAnyObjectByType<FlightPhysicsController>();
            Build();
        }

        private void LateUpdate()
        {
            AircraftRelativeWind sample = AircraftRelativeWind.Calculate(
                wind == null || !wind.WindEnabled ? Vector3.zero : wind.CurrentWindVelocity,
                aircraft == null ? 0f : aircraft.Heading);
            float dt = Time.unscaledDeltaTime;
            shownSpeed = Mathf.SmoothDamp(shownSpeed, sample.SpeedKnots,
                ref speedVelocity, valueSmoothing, Mathf.Infinity, dt);
            shownHeadwind = Mathf.SmoothDamp(shownHeadwind, sample.HeadwindKnots,
                ref headVelocity, valueSmoothing, Mathf.Infinity, dt);
            shownCrosswind = Mathf.SmoothDamp(shownCrosswind, sample.CrosswindKnots,
                ref crossVelocity, valueSmoothing, Mathf.Infinity, dt);

            if (!angleInitialized)
            {
                shownAngle = sample.RelativeFromAngle;
                shownFromHeading = sample.FromHeading;
                angleInitialized = true;
            }
            float targetAngle = shownAngle + Mathf.DeltaAngle(shownAngle, sample.RelativeFromAngle);
            shownAngle = Mathf.SmoothDampAngle(shownAngle, targetAngle,
                ref angleVelocity, valueSmoothing, Mathf.Infinity, dt);
            shownFromHeading = Mathf.Repeat(Mathf.SmoothDampAngle(
                shownFromHeading, sample.FromHeading, ref fromHeadingVelocity,
                valueSmoothing, Mathf.Infinity, dt), 360f);
            arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -shownAngle);

            bool calm = shownSpeed < calmThresholdKnots;
            arrow.text = calm ? "~" : "↑";
            arrow.color = calm ? White : Cyan;
            directionSpeed.text = calm
                ? "CALM / 0 KT"
                : $"{shownFromHeading:000}° / {shownSpeed:0} KT";
            longitudinal.gameObject.SetActive(!calm);
            crosswind.gameObject.SetActive(!calm);
            if (calm) return;

            float headMagnitude = Mathf.Abs(shownHeadwind);
            longitudinal.text = shownHeadwind >= 0f
                ? $"↑ HEADWIND   {headMagnitude:0} KT"
                : $"↓ TAILWIND   {headMagnitude:0} KT";
            longitudinal.color = shownHeadwind >= 0f ? Green : Amber;

            float crossMagnitude = Mathf.Abs(shownCrosswind);
            string sideArrow = shownCrosswind >= 0f ? "→" : "←";
            crosswind.text = $"{sideArrow} CROSSWIND  {crossMagnitude:0} KT";
            crosswind.color = CrosswindColor(crossMagnitude);
        }

        public Color CrosswindColor(float magnitudeKnots)
        {
            if (magnitudeKnots >= strongCrosswindKnots) return Red;
            if (magnitudeKnots >= moderateCrosswindKnots) return Amber;
            return Green;
        }

        private void Build()
        {
            RectTransform host = (RectTransform)transform;
            host.anchorMin = host.anchorMax = new Vector2(1f, 1f);
            host.pivot = new Vector2(1f, 1f);
            // Altitude now occupies the middle-right band, so wind can use the
            // cleared top-right corner.
            host.anchoredPosition = new Vector2(-20f, -20f);
            host.sizeDelta = new Vector2(176f, 174f);

            Image panel = gameObject.AddComponent<Image>();
            panel.color = FlightHUDStyle.SecondaryPanel;
            FlightHUDStyle.AddOutline(gameObject);

            TMP_Text title = MakeText("Title", 14f, FontStyles.Bold,
                new Vector2(8f, -8f), new Vector2(160f, 20f));
            title.text = "WIND"; title.color = White;
            arrow = MakeText("Wind Arrow", 42f, FontStyles.Bold,
                new Vector2(58f, -32f), new Vector2(60f, 52f));
            directionSpeed = MakeText("Direction and Speed", 16f, FontStyles.Bold,
                new Vector2(8f, -82f), new Vector2(160f, 24f));
            directionSpeed.color = Cyan;

            RectTransform divider = Rect("Divider");
            Place(divider, new Vector2(12f, -108f), new Vector2(152f, 1f));
            divider.gameObject.AddComponent<Image>().color =
                new Color(0.75f, 0.9f, 0.94f, 0.3f);

            longitudinal = MakeText("Head or Tailwind", 13f, FontStyles.Bold,
                new Vector2(8f, -116f), new Vector2(160f, 22f));
            crosswind = MakeText("Crosswind", 13f, FontStyles.Bold,
                new Vector2(8f, -142f), new Vector2(160f, 22f));
        }

        private TMP_Text MakeText(string name, float size, FontStyles style,
            Vector2 position, Vector2 dimensions)
        {
            TMP_Text label = Rect(name).gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            Place(label.rectTransform, position, dimensions);
            return label;
        }

        private RectTransform Rect(string name)
        {
            GameObject child = new(name, typeof(RectTransform));
            child.transform.SetParent(transform, false);
            return (RectTransform)child.transform;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
