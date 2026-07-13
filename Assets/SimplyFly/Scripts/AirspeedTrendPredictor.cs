using UnityEngine;

namespace SimplyFly.GroundOps
{
    /// <summary>Calculates a stable near-future IAS delta independently of tape rendering.</summary>
    [DisallowMultipleComponent]
    public sealed class AirspeedTrendPredictor : MonoBehaviour
    {
        [Min(0.1f)] [SerializeField] private float predictionSeconds = 6f;
        [Min(0.01f)] [SerializeField] private float accelerationSmoothingTime = 0.45f;
        [Min(0f)] [SerializeField] private float accelerationDeadband = 0.08f;
        [Min(1f)] [SerializeField] private float maximumTrendKnots = 35f;

        private FlightPhysicsController flightPhysics;
        private float previousAirspeed;
        private float smoothedAcceleration;
        private float accelerationVelocity;
        private bool hasSample;

        public float PredictedDeltaKnots { get; private set; }
        public bool TrendVisible => Mathf.Abs(smoothedAcceleration) >= accelerationDeadband;

        public void Bind(FlightPhysicsController source)
        {
            flightPhysics = source;
            hasSample = false;
            smoothedAcceleration = 0f;
            PredictedDeltaKnots = 0f;
        }

        private void Update()
        {
            if (flightPhysics == null || Time.unscaledDeltaTime <= 0.0001f)
            {
                PredictedDeltaKnots = 0f;
                return;
            }

            float airspeed = Mathf.Max(0f, flightPhysics.AirspeedKnots);
            if (!hasSample)
            {
                previousAirspeed = airspeed;
                hasSample = true;
                return;
            }

            float acceleration = (airspeed - previousAirspeed) / Time.unscaledDeltaTime;
            previousAirspeed = airspeed;
            smoothedAcceleration = Mathf.SmoothDamp(
                smoothedAcceleration, acceleration, ref accelerationVelocity,
                accelerationSmoothingTime, Mathf.Infinity, Time.unscaledDeltaTime);

            PredictedDeltaKnots = TrendVisible
                ? Mathf.Clamp(smoothedAcceleration * predictionSeconds,
                    -maximumTrendKnots, maximumTrendKnots)
                : 0f;
        }
    }
}
