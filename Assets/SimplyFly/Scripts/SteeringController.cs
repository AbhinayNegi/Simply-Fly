using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class SteeringController : MonoBehaviour
    {
        [SerializeField] private AircraftInput input;
        [SerializeField] private WheelCollider noseWheel;
        [SerializeField] private Rigidbody aircraftBody;
        [SerializeField] private GroundMovementController movement;
        [SerializeField] private Vector3 localForwardAxis = Vector3.right;
        [Header("Steering")]
        [Range(1f, 60f)] [SerializeField] private float maximumSteeringAngle = 32f;
        [Min(1f)] [SerializeField] private float steeringSpeed = 75f;
        [Tooltip("X is normalized speed, Y is retained steering sensitivity.")]
        [SerializeField] private AnimationCurve steeringSensitivityCurve =
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.45f, 0.65f), new Keyframe(1f, 0.25f));
        [Min(1f)] [SerializeField] private float speedForMinimumSensitivity = 18f;

        public float SteeringAngle { get; private set; }

        private void Awake()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (aircraftBody == null) aircraftBody = GetComponent<Rigidbody>();
            if (movement == null) movement = GetComponent<GroundMovementController>();
        }

        private void FixedUpdate()
        {
            float speed = movement != null
                ? movement.GroundSpeed
                : aircraftBody == null ? 0f : Vector3.ProjectOnPlane(aircraftBody.linearVelocity, Vector3.up).magnitude;
            float sensitivity = steeringSensitivityCurve.Evaluate(Mathf.Clamp01(speed / speedForMinimumSensitivity));
            float target = (input == null ? 0f : input.SteeringInput) * maximumSteeringAngle * sensitivity;
            SteeringAngle = Mathf.MoveTowards(SteeringAngle, target, steeringSpeed * Time.fixedDeltaTime);
            if (noseWheel != null) noseWheel.steerAngle = SteeringAngle;

            // Ground-only yaw follows the nose-wheel angle. No air yaw is implemented.
            if (aircraftBody != null && movement != null && movement.IsGrounded && speed > 0.15f)
            {
                float turnRate = SteeringAngle * Mathf.Clamp01(speed / 3f) * 0.55f;
                Quaternion yaw = Quaternion.AngleAxis(turnRate * Time.fixedDeltaTime, transform.up);
                aircraftBody.MoveRotation(yaw * aircraftBody.rotation);
            }
        }
    }
}
