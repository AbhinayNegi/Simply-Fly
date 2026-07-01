using UnityEngine;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GroundMovementController : MonoBehaviour
    {
        [SerializeField] private EngineController engine;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private BrakeController brakes;
        [SerializeField] private WheelCollider[] wheels;

        [Header("Ground movement")]
        [SerializeField] private Vector3 localForwardAxis = Vector3.right;
        [Min(0f)] [SerializeField] private float maximumDriveForce = 5200f;
        [Tooltip("Metres per second. 14 m/s is roughly 27 knots.")]
        [Min(1f)] [SerializeField] private float maximumTaxiSpeed = 14f;
        [Min(0f)] [SerializeField] private float rollingResistance = 180f;
        [Min(0f)] [SerializeField] private float lateralGrip = 2.5f;
        [Min(0f)] [SerializeField] private float groundHoldForce = 2500f;
        [Tooltip("Fallback ground probe below each wheel, used while suspension is fully compressed.")]
        [Min(0.1f)] [SerializeField] private float groundProbeDistance = 1.1f;

        public float GroundSpeed { get; private set; }
        public float GroundSpeedKnots => GroundSpeed * 1.943844f;
        public bool IsGrounded { get; private set; }

        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (engine == null) engine = GetComponent<EngineController>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            if (brakes == null) brakes = GetComponent<BrakeController>();
        }

        private void FixedUpdate()
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            GroundSpeed = planarVelocity.magnitude;
            IsGrounded = false;

            float brakeTorque = brakes == null ? 0f : brakes.BrakeTorque;
            if (wheels != null)
            {
                foreach (WheelCollider wheel in wheels)
                {
                    if (wheel == null) continue;
                    wheel.brakeTorque = brakeTorque;
                    IsGrounded |= wheel.isGrounded || HasGroundBelow(wheel);
                }
            }

            if (!IsGrounded) return;

            Vector3 forward = transform.TransformDirection(localForwardAxis.normalized);
            float forwardSpeed = Vector3.Dot(planarVelocity, forward);
            if (engine != null && engine.State == EngineController.EngineState.Running && throttle != null &&
                forwardSpeed < maximumTaxiSpeed)
            {
                float speedFade = 1f - Mathf.Clamp01(Mathf.Max(0f, forwardSpeed) / maximumTaxiSpeed);
                body.AddForce(forward * (maximumDriveForce * throttle.Throttle * speedFade), ForceMode.Force);
            }

            if (GroundSpeed > 0.02f)
                body.AddForce(-planarVelocity.normalized * rollingResistance, ForceMode.Force);

            Vector3 lateral = planarVelocity - Vector3.Project(planarVelocity, forward);
            body.AddForce(-lateral * lateralGrip * body.mass, ForceMode.Force);
            body.AddForce(Vector3.down * groundHoldForce, ForceMode.Force);

            if (GroundSpeed > maximumTaxiSpeed * 1.08f)
            {
                Vector3 clamped = planarVelocity.normalized * maximumTaxiSpeed * 1.08f;
                body.linearVelocity = new Vector3(clamped.x, Mathf.Min(body.linearVelocity.y, 0f), clamped.z);
            }
        }

        private bool HasGroundBelow(WheelCollider wheel)
        {
            Vector3 origin = wheel.transform.position + transform.up * 0.35f;
            RaycastHit[] hits = Physics.RaycastAll(origin, -transform.up, groundProbeDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null && hit.collider.attachedRigidbody != body)
                    return true;
            }
            return false;
        }
    }
}
