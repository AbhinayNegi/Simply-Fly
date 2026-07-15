using UnityEngine;

namespace SimplyFly.GroundOps
{
    /// <summary>IAS data adapter for the reusable numeric tape presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class AirspeedTapeHUD : MonoBehaviour
    {
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private AircraftSpeedPerformance performance;
        [SerializeField] private NumericTapeHUD tape;
        [SerializeField] private AirspeedTrendPredictor trendPredictor;
        private bool warnedMissingConfiguration;
        private int appliedPerformanceHash = int.MinValue;

        private static readonly Color GreenBand = new Color(0.2f, 0.95f, 0.35f, 0.9f);
        private static readonly Color AmberBand = new Color(1f, 0.66f, 0.08f, 0.95f);
        private static readonly Color RedBand = new Color(1f, 0.08f, 0.05f, 1f);

        public void Bind(FlightPhysicsController source, AircraftSpeedPerformance configuration = null)
        {
            flightPhysics = source;
            performance = configuration != null
                ? configuration
                : source == null ? null : source.GetComponent<AircraftSpeedPerformance>();
            if (trendPredictor == null) trendPredictor = GetComponent<AirspeedTrendPredictor>();
            if (trendPredictor == null) trendPredictor = gameObject.AddComponent<AirspeedTrendPredictor>();
            trendPredictor.Bind(source);
            ApplyPerformanceRanges();
        }

        private void Awake()
        {
            // The expanded Phase 2 engine panel occupies more vertical space.
            // Lift the airspeed tape into the free upper-left area so the two
            // instruments retain a clear visual gutter.
            RectTransform host = (RectTransform)transform;
            host.anchoredPosition = new Vector2(host.anchoredPosition.x, 40f);
            if (tape == null) tape = GetComponent<NumericTapeHUD>();
            if (tape == null) tape = gameObject.AddComponent<NumericTapeHUD>();
            tape.ConfigureAirspeedPresentation();
            tape.ConfigureScale(1f, 10, 5f, "AIRSPEED", "KT", "000", 0f);
            if (flightPhysics == null) flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
            if (performance == null && flightPhysics != null)
                performance = flightPhysics.GetComponent<AircraftSpeedPerformance>();
            ApplyPerformanceRanges();
        }

        private void Update()
        {
            float ias = flightPhysics == null ? 0f : Mathf.Max(0f, flightPhysics.AirspeedKnots);
            tape?.SetValue(ias);
            if (trendPredictor != null)
                tape?.SetTrend(trendPredictor.PredictedDeltaKnots, trendPredictor.TrendVisible);
            if (performance == null)
            {
                // Safe display-only fallback. Aircraft-specific values remain outside
                // the generic tape presentation.
                if (!warnedMissingConfiguration)
                {
                    Debug.LogWarning("AirspeedTapeHUD: active aircraft has no AircraftSpeedPerformance configuration; using temporary safe display defaults.", this);
                    warnedMissingConfiguration = true;
                }
                ApplyFallbackRanges();
                SetWarning(ias,
                    AircraftSpeedPerformance.DefaultStallWarningSpeed,
                    AircraftSpeedPerformance.DefaultRedLineSpeed);
            }
            else
            {
                ApplyPerformanceRanges();
                SetWarning(ias, performance.StallWarningSpeed, performance.RedLineSpeed);
            }
        }

        private void ApplyPerformanceRanges()
        {
            if (tape == null) tape = GetComponent<NumericTapeHUD>();
            if (tape == null || performance == null) return;
            int hash = BuildHash(
                performance.StallWarningSpeed,
                performance.GreenRangeStart, performance.GreenRangeEnd,
                performance.AmberRangeStart, performance.AmberRangeEnd,
                performance.RedLineSpeed);
            if (hash == appliedPerformanceHash) return;
            appliedPerformanceHash = hash;
            tape.SetRangeBands(
                new NumericTapeHUD.RangeBand(0f, performance.StallWarningSpeed, RedBand),
                new NumericTapeHUD.RangeBand(performance.GreenRangeStart, performance.GreenRangeEnd, GreenBand),
                new NumericTapeHUD.RangeBand(performance.AmberRangeStart, performance.AmberRangeEnd, AmberBand),
                new NumericTapeHUD.RangeBand(performance.RedLineSpeed, performance.RedLineSpeed, RedBand));
        }

        private void ApplyFallbackRanges()
        {
            if (tape == null) return;
            int hash = BuildHash(
                AircraftSpeedPerformance.DefaultStallWarningSpeed,
                AircraftSpeedPerformance.DefaultGreenRangeStart,
                AircraftSpeedPerformance.DefaultGreenRangeEnd,
                AircraftSpeedPerformance.DefaultAmberRangeStart,
                AircraftSpeedPerformance.DefaultAmberRangeEnd,
                AircraftSpeedPerformance.DefaultRedLineSpeed);
            if (hash == appliedPerformanceHash) return;
            appliedPerformanceHash = hash;
            tape.SetRangeBands(
                new NumericTapeHUD.RangeBand(
                    0f, AircraftSpeedPerformance.DefaultStallWarningSpeed, RedBand),
                new NumericTapeHUD.RangeBand(
                    AircraftSpeedPerformance.DefaultGreenRangeStart,
                    AircraftSpeedPerformance.DefaultGreenRangeEnd, GreenBand),
                new NumericTapeHUD.RangeBand(
                    AircraftSpeedPerformance.DefaultAmberRangeStart,
                    AircraftSpeedPerformance.DefaultAmberRangeEnd, AmberBand),
                new NumericTapeHUD.RangeBand(
                    AircraftSpeedPerformance.DefaultRedLineSpeed,
                    AircraftSpeedPerformance.DefaultRedLineSpeed, RedBand));
        }

        private static int BuildHash(params float[] values)
        {
            unchecked
            {
                int hash = 17;
                foreach (float value in values) hash = hash * 31 + value.GetHashCode();
                return hash;
            }
        }

        private void SetWarning(float ias, float stallSpeed, float redLineSpeed)
        {
            if (ias >= redLineSpeed) tape?.SetWarning("OVERSPEED", RedBand);
            else if (ias < stallSpeed) tape?.SetWarning("LOW SPEED", AmberBand);
            else tape?.SetWarning(string.Empty, Color.clear);
        }
    }
}
