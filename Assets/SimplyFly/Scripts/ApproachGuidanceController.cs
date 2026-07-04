using UnityEngine;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ApproachGuidanceController : MonoBehaviour
    {
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private Transform runwayThreshold;
        [SerializeField] private Vector3 localForwardAxis = Vector3.forward;

        public float DistanceToThreshold { get; private set; }
        public float AltitudeAboveRunway { get; private set; }
        public float VerticalSpeed =>
            flightPhysics == null ? 0f : flightPhysics.VerticalSpeed;
        public float AirspeedKnots =>
            flightPhysics == null ? 0f : flightPhysics.AirspeedKnots;
        public float GroundSpeedKnots =>
            flightPhysics == null ? 0f : flightPhysics.HorizontalGroundSpeed * 1.943844f;
        public float Heading =>
            flightPhysics == null ? 0f : flightPhysics.Heading;
        public float RunwayHeading { get; private set; }
        public float AlignmentError { get; private set; }
        public float LateralOffset { get; private set; }
        public float GlideSlopeAngle { get; private set; }
        public bool HasRunwayTarget => runwayThreshold != null;

        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
        }

        private void FixedUpdate()
        {
            if (runwayThreshold == null)
            {
                DistanceToThreshold = 0f;
                AltitudeAboveRunway = 0f;
                RunwayHeading = 0f;
                AlignmentError = 0f;
                LateralOffset = 0f;
                GlideSlopeAngle = 0f;
                return;
            }

            Vector3 runwayForward = runwayThreshold.TransformDirection(
                localForwardAxis.normalized);
            Vector3 horizontalRunwayForward =
                Vector3.ProjectOnPlane(runwayForward, Vector3.up).normalized;
            Vector3 toAircraft = body.position - runwayThreshold.position;
            Vector3 horizontalToAircraft =
                Vector3.ProjectOnPlane(toAircraft, Vector3.up);

            DistanceToThreshold = horizontalToAircraft.magnitude;
            AltitudeAboveRunway = body.position.y - runwayThreshold.position.y;
            RunwayHeading = Mathf.Repeat(
                Mathf.Atan2(horizontalRunwayForward.x, horizontalRunwayForward.z) *
                Mathf.Rad2Deg,
                360f);
            AlignmentError = Mathf.DeltaAngle(Heading, RunwayHeading);

            Vector3 runwayRight =
                Vector3.Cross(Vector3.up, horizontalRunwayForward).normalized;
            LateralOffset = Vector3.Dot(horizontalToAircraft, runwayRight);
            GlideSlopeAngle = Mathf.Atan2(
                AltitudeAboveRunway,
                Mathf.Max(1f, DistanceToThreshold)) * Mathf.Rad2Deg;
        }

        public void SetRunwayThreshold(Transform threshold)
        {
            runwayThreshold = threshold;
        }
    }
}
