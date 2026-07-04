using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class SteeringController : MonoBehaviour
    {
        [SerializeField] private AircraftInput input;
        [SerializeField] private WheelCollider noseWheel;
        [SerializeField] private Rigidbody aircraftBody;
        [SerializeField] private GroundMovementController movement;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
        [Header("Steering")]
        [Range(1f, 60f)] [SerializeField] private float maximumSteeringAngle = 32f;
        [Min(1f)] [SerializeField] private float steeringSpeed = 75f;
        [Tooltip("X is normalized speed, Y is retained steering sensitivity.")]
        [SerializeField] private AnimationCurve steeringSensitivityCurve =
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.45f, 0.62f), new Keyframe(1f, 0.18f));
        [Min(1f)] [SerializeField] private float speedForMinimumSensitivity = 18f;
        [Range(0.02f, 1f)] [SerializeField] private float highSpeedSteeringMultiplier = 0.18f;
        [Tooltip("Fraction of J/L rudder input mechanically linked to nosewheel steering.")]
        [Range(0f, 1f)] [SerializeField] private float rudderNosewheelInfluence = 0.18f;
        [Tooltip("Distance between the main gear and nose wheel.")]
        [Min(0.5f)] [SerializeField] private float wheelbase = 2.8f;
        [Tooltip("How quickly yaw rate follows the bicycle-model target.")]
        [Min(0.1f)] [SerializeField] private float yawResponse = 5f;
        [Tooltip("Maximum ground yaw acceleration in radians per second squared.")]
        [Min(0.1f)] [SerializeField] private float maximumYawAcceleration = 1.6f;
        [Tooltip("Yaw rate considered stopped when steering is neutral.")]
        [Min(0.001f)] [SerializeField] private float neutralYawRateDeadband = 0.015f;

        public float SteeringAngle { get; private set; }
        public float DesiredYawRate { get; private set; }
        public float CurrentYawRate { get; private set; }

        private void Awake()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (aircraftBody == null) aircraftBody = GetComponent<Rigidbody>();
            if (movement == null) movement = GetComponent<GroundMovementController>();
            if (flightPhysics == null) flightPhysics = GetComponent<FlightPhysicsController>();
        }

        private void FixedUpdate()
        {
            float speed = movement != null
                ? movement.GroundSpeed
                : aircraftBody == null ? 0f : Vector3.ProjectOnPlane(aircraftBody.linearVelocity, Vector3.up).magnitude;
            float normalizedSpeed = Mathf.Clamp01(speed / speedForMinimumSensitivity);
            float sensitivity = steeringSensitivityCurve.Evaluate(normalizedSpeed);
            float highSpeedFade = Mathf.SmoothStep(0f, 1f, normalizedSpeed);
            sensitivity *= Mathf.Lerp(1f, highSpeedSteeringMultiplier, highSpeedFade);
            float groundSteeringCommand = input == null
                ? 0f
                : Mathf.Clamp(
                    input.SteeringInput +
                    input.RudderInput * rudderNosewheelInfluence,
                    -1f,
                    1f);
            float target =
                groundSteeringCommand * maximumSteeringAngle * sensitivity;
            SteeringAngle = Mathf.MoveTowards(SteeringAngle, target, steeringSpeed * Time.fixedDeltaTime);
            if (noseWheel != null) noseWheel.steerAngle = SteeringAngle;

            // Ground-only yaw follows the nose-wheel angle. No air yaw is implemented.
            bool groundSteeringAllowed =
                flightPhysics == null ||
                flightPhysics.State != FlightPhysicsController.AircraftState.Airborne;
            if (aircraftBody != null && movement != null && movement.IsGrounded &&
                groundSteeringAllowed)
            {
                Vector3 forward = transform.TransformDirection(localForwardAxis.normalized);
                float forwardSpeed = Vector3.Dot(
                    Vector3.ProjectOnPlane(aircraftBody.linearVelocity, transform.up),
                    forward);
                DesiredYawRate =
                    forwardSpeed / Mathf.Max(0.5f, wheelbase) *
                    Mathf.Tan(SteeringAngle * Mathf.Deg2Rad);
                CurrentYawRate = Vector3.Dot(aircraftBody.angularVelocity, transform.up);

                float yawAcceleration;
                if (Mathf.Abs(DesiredYawRate) < 0.001f &&
                    Mathf.Abs(CurrentYawRate) < neutralYawRateDeadband)
                {
                    // Finish at zero without crossing into a visible opposite turn.
                    yawAcceleration = -CurrentYawRate / Time.fixedDeltaTime;
                }
                else
                {
                    yawAcceleration =
                        (DesiredYawRate - CurrentYawRate) * yawResponse;
                }

                yawAcceleration = Mathf.Clamp(
                    yawAcceleration,
                    -maximumYawAcceleration,
                    maximumYawAcceleration);
                aircraftBody.AddTorque(
                    transform.up * yawAcceleration,
                    ForceMode.Acceleration);
            }
            else
            {
                DesiredYawRate = 0f;
                CurrentYawRate = aircraftBody == null
                    ? 0f
                    : Vector3.Dot(aircraftBody.angularVelocity, transform.up);
            }
        }

        public void ResetSteering()
        {
            SteeringAngle = 0f;
            DesiredYawRate = 0f;
            CurrentYawRate = 0f;
            if (noseWheel != null) noseWheel.steerAngle = 0f;
        }
    }
}
