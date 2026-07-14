using UnityEngine;

namespace SimplyFly.GroundOps
{
    /// <summary>UI-independent aircraft-relative wind calculation.</summary>
    public readonly struct AircraftRelativeWind
    {
        public readonly float FromHeading;
        public readonly float SpeedKnots;
        public readonly float HeadwindKnots;
        public readonly float CrosswindKnots;
        public readonly float RelativeFromAngle;

        public bool IsTailwind => HeadwindKnots < 0f;
        public bool IsFromRight => CrosswindKnots > 0f;

        public AircraftRelativeWind(float fromHeading, float speedKnots,
            float headwindKnots, float crosswindKnots, float relativeFromAngle)
        {
            FromHeading = fromHeading;
            SpeedKnots = speedKnots;
            HeadwindKnots = headwindKnots;
            CrosswindKnots = crosswindKnots;
            RelativeFromAngle = relativeFromAngle;
        }

        public static AircraftRelativeWind Calculate(
            Vector3 windVelocityMetersPerSecond, float aircraftHeading)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(
                windVelocityMetersPerSecond, Vector3.up);
            float speedKnots = horizontal.magnitude * 1.943844f;
            if (speedKnots < 0.001f)
                return new AircraftRelativeWind(0f, 0f, 0f, 0f, 0f);

            // WindController stores the direction air travels; aviation wind
            // direction is the reciprocal (the direction it comes from).
            Vector3 from = -horizontal.normalized;
            float fromHeading = Mathf.Repeat(
                Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg, 360f);
            float relative = Mathf.DeltaAngle(aircraftHeading, fromHeading);
            float radians = relative * Mathf.Deg2Rad;
            return new AircraftRelativeWind(
                fromHeading,
                speedKnots,
                speedKnots * Mathf.Cos(radians),
                speedKnots * Mathf.Sin(radians),
                relative);
        }
    }
}
