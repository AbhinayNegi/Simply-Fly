using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class SteeringController : MonoBehaviour
    {
        [SerializeField] private AircraftInput input;
        [SerializeField] private WheelCollider noseWheel;
        [SerializeField] private Rigidbody aircraftBody;
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
        }

        private void FixedUpdate()
        {
            float speed = aircraftBody == null ? 0f : Vector3.ProjectOnPlane(aircraftBody.linearVelocity, Vector3.up).magnitude;
            float sensitivity = steeringSensitivityCurve.Evaluate(Mathf.Clamp01(speed / speedForMinimumSensitivity));
            float target = (input == null ? 0f : input.SteeringInput) * maximumSteeringAngle * sensitivity;
            SteeringAngle = Mathf.MoveTowards(SteeringAngle, target, steeringSpeed * Time.fixedDeltaTime);
            if (noseWheel != null) noseWheel.steerAngle = SteeringAngle;

            // The force is applied at the nose wheel contact area, so only nose-wheel steering initiates yaw.
            if (aircraftBody != null && noseWheel != null && noseWheel.isGrounded && speed > 0.15f)
            {
                Vector3 forward = transform.TransformDirection(localForwardAxis.normalized);
                float direction = Mathf.Sign(Vector3.Dot(aircraftBody.linearVelocity, forward));
                Vector3 lateral = Quaternion.AngleAxis(SteeringAngle, transform.up) * forward;
                aircraftBody.AddForceAtPosition(lateral * (speed * Mathf.Abs(SteeringAngle) * 1.6f * direction),
                    noseWheel.transform.position, ForceMode.Force);
            }
        }
    }
}
