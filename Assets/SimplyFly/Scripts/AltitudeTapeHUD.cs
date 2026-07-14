using UnityEngine;

namespace SimplyFly.GroundOps
{
    /// <summary>Feet-based altitude adapter for the reusable numeric tape.</summary>
    [DisallowMultipleComponent]
    public sealed class AltitudeTapeHUD : MonoBehaviour
    {
        private const float FeetPerMeter = 3.28084f;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private ApproachGuidanceController approach;
        [SerializeField] private NumericTapeHUD tape;

        public void Bind(FlightPhysicsController physics, ApproachGuidanceController guidance)
        {
            flightPhysics = physics;
            approach = guidance;
        }

        private void Awake()
        {
            RectTransform host = (RectTransform)transform;
            host.anchorMin = host.anchorMax = new Vector2(1f, 0.5f);
            host.pivot = new Vector2(1f, 0.5f);
            // Reserve the top-right band for the wind card.
            host.anchoredPosition = new Vector2(-18f, 100f);
            if (tape == null) tape = GetComponent<NumericTapeHUD>();
            if (tape == null) tape = gameObject.AddComponent<NumericTapeHUD>();
            tape.ConfigureScale(100f, 1, 30f, "ALTITUDE", "FT", "00000", 0f, true);
            if (flightPhysics == null) flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
            if (approach == null) approach = FindAnyObjectByType<ApproachGuidanceController>();
        }

        private void Update()
        {
            float meters = approach != null
                ? approach.AltitudeAboveRunway
                : flightPhysics == null ? 0f : flightPhysics.Altitude;
            tape?.SetValue(Mathf.Max(0f, meters * FeetPerMeter));
        }
    }
}
