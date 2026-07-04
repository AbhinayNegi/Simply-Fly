using System;

namespace SimplyFly.GroundOps
{
    [Serializable]
    public sealed class FlightTestMetric
    {
        public string name;
        public float value;
        public string unit;
    }

    [Serializable]
    public sealed class FlightTestSummary
    {
        public string scenarioName;
        public string startTime;
        public float duration;
        public string result;
        public int sampleCount;
        public FlightTestMetric[] keyMetrics;
        public string[] warnings;
        public float minimumAirspeed;
        public float maximumAirspeed;
        public float averageAirspeed;
        public float minimumGroundSpeed;
        public float maximumGroundSpeed;
        public float averageGroundSpeed;
        public float minimumVerticalSpeed;
        public float maximumVerticalSpeed;
        public float averageVerticalSpeed;
        public float minimumAltitude;
        public float maximumAltitude;
        public string landingQuality;
        public bool mainWheelsFirst;
        public bool noseFirstWarning;
    }
}
