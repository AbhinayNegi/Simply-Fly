using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class WindController : MonoBehaviour
    {
        [Header("Wind")]
        [Tooltip("Enable simplified environmental wind.")]
        [SerializeField] private bool windEnabled = true;
        [Tooltip("Aviation-style direction the wind comes FROM, in degrees.")]
        [Range(0f, 360f)] [SerializeField] private float windFromHeading = 180f;
        [Tooltip("Steady wind speed in knots.")]
        [Range(0f, 40f)] [SerializeField] private float windSpeedKnots = 7f;

        [Header("Gusts")]
        [SerializeField] private bool gustsEnabled = true;
        [Range(0f, 12f)] [SerializeField] private float gustStrengthKnots = 2.5f;
        [Min(0.05f)] [SerializeField] private float gustFrequency = 0.12f;
        [Range(0f, 35f)] [SerializeField] private float gustDirectionVariation = 8f;

        [Header("Runway reference")]
        [SerializeField] private AirportLocationManager airportLocation;

        public bool WindEnabled => windEnabled;
        public float WindFromHeading => windFromHeading;
        public float WindSpeedKnots => windEnabled ? currentWindSpeedKnots : 0f;
        public float SteadyWindSpeedKnots => windEnabled ? windSpeedKnots : 0f;
        public bool GustsEnabled => gustsEnabled;
        public float GustStrengthKnots => gustStrengthKnots;
        public float CrosswindKnots { get; private set; }
        public float HeadwindKnots { get; private set; }
        public Vector3 CurrentWindVelocity { get; private set; }
        public string WindDescription =>
            windEnabled
                ? $"WIND {windFromHeading:000}°/{WindSpeedKnots:0} kt"
                : "WIND CALM";

        private float currentWindSpeedKnots;

        private void Awake()
        {
            LoadSavedSettings();
            if (airportLocation == null)
                airportLocation = FindAnyObjectByType<AirportLocationManager>();
            UpdateWind(true);
        }

        private void Start() => ConfigureSceneWindsocks();

        private void Update() => UpdateWind(false);

        public void SetWindEnabled(bool enabled)
        {
            windEnabled = enabled;
            UpdateWind(true);
        }

        public void SetWind(float fromHeading, float speedKnots)
        {
            windFromHeading = Mathf.Repeat(fromHeading, 360f);
            windSpeedKnots = Mathf.Clamp(speedKnots, 0f, 40f);
            UpdateWind(true);
        }

        public void SetGusts(bool enabled, float strengthKnots)
        {
            gustsEnabled = enabled;
            gustStrengthKnots = Mathf.Clamp(strengthKnots, 0f, 12f);
            UpdateWind(true);
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetInt("wind.enabled", windEnabled ? 1 : 0);
            PlayerPrefs.SetFloat("wind.direction", windFromHeading);
            PlayerPrefs.SetFloat("wind.speed", windSpeedKnots);
            PlayerPrefs.SetInt("wind.gustsEnabled", gustsEnabled ? 1 : 0);
            PlayerPrefs.SetFloat("wind.gustStrength", gustStrengthKnots);
            PlayerPrefs.Save();
        }

        public void ResetToCalm()
        {
            SetWind(180f, 0f);
            SetGusts(false, 0f);
            SetWindEnabled(false);
        }

        public void ResetToDefault()
        {
            SetWind(180f, 7f);
            SetGusts(true, 2.5f);
            SetWindEnabled(true);
        }

        private void LoadSavedSettings()
        {
            windEnabled = PlayerPrefs.GetInt(
                "wind.enabled", windEnabled ? 1 : 0) != 0;
            windFromHeading = PlayerPrefs.GetFloat(
                "wind.direction", windFromHeading);
            windSpeedKnots = PlayerPrefs.GetFloat(
                "wind.speed", windSpeedKnots);
            gustsEnabled = PlayerPrefs.GetInt(
                "wind.gustsEnabled", gustsEnabled ? 1 : 0) != 0;
            gustStrengthKnots = PlayerPrefs.GetFloat(
                "wind.gustStrength", gustStrengthKnots);
        }

        private void UpdateWind(bool instant)
        {
            if (!windEnabled)
            {
                currentWindSpeedKnots = 0f;
                CurrentWindVelocity = Vector3.zero;
                HeadwindKnots = 0f;
                CrosswindKnots = 0f;
                return;
            }

            float gust = 0f;
            float directionOffset = 0f;
            if (gustsEnabled)
            {
                float t = Time.time * gustFrequency;
                gust = (Mathf.PerlinNoise(t, 13.7f) - 0.5f) *
                       2f * gustStrengthKnots;
                directionOffset = (Mathf.PerlinNoise(41.3f, t) - 0.5f) *
                                  2f * gustDirectionVariation;
            }

            float targetSpeed = Mathf.Max(0f, windSpeedKnots + gust);
            currentWindSpeedKnots = instant
                ? targetSpeed
                : Mathf.Lerp(
                    currentWindSpeedKnots,
                    targetSpeed,
                    1f - Mathf.Exp(-2f * Time.deltaTime));

            float currentFrom = Mathf.Repeat(windFromHeading + directionOffset, 360f);
            float windToHeading = Mathf.Repeat(currentFrom + 180f, 360f);
            CurrentWindVelocity =
                HeadingToVector(windToHeading) *
                (currentWindSpeedKnots * 0.514444f);
            UpdateRunwayComponents();
        }

        private void UpdateRunwayComponents()
        {
            AirportDefinition airport = airportLocation == null
                ? null
                : airportLocation.CurrentAirport;
            Vector3 runwayForward = airport == null
                ? Vector3.forward
                : airport.Forward;
            Vector3 runwayRight = airport == null
                ? Vector3.right
                : airport.RunwayRotation * Vector3.right;
            Vector3 windKt = CurrentWindVelocity / 0.514444f;
            HeadwindKnots = -Vector3.Dot(windKt, runwayForward);
            CrosswindKnots = Vector3.Dot(windKt, runwayRight);
        }

        public static Vector3 HeadingToVector(float headingDegrees)
        {
            float radians = headingDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        private void ConfigureSceneWindsocks()
        {
            foreach (Transform candidate in FindObjectsByType<Transform>())
            {
                if (candidate == null) continue;
                bool looksLikeWindsock =
                    candidate.name.Contains("Windsock") ||
                    candidate.name.Contains("Wind Sock") ||
                    candidate.Find("Sock") != null ||
                    candidate.Find("Windsock") != null;
                if (!looksLikeWindsock) continue;

                Transform sock =
                    candidate.Find("Sock") ??
                    candidate.Find("Windsock") ??
                    candidate;
                WindsockController controller =
                    candidate.GetComponent<WindsockController>() ??
                    candidate.gameObject.AddComponent<WindsockController>();
                controller.Configure(this, sock);
            }
        }
    }
}
