using UnityEngine;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RudderController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AircraftInput input;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private GroundMovementController groundMovement;
        [SerializeField] private StallController stall;
        [SerializeField] private WindController wind;
        [SerializeField] private Vector3 localYawAxis = Vector3.up;

        [Header("Input response")]
        [Min(0.01f)] [SerializeField] private float rudderSmoothing = 2.5f;
        [Min(0.01f)] [SerializeField] private float rudderReturnSpeed = 4f;

        [Header("Yaw physics")]
        [Min(0f)] [SerializeField] private float rudderAuthority = 0.5f;
        [SerializeField] private AnimationCurve rudderAuthorityByAirspeedCurve =
            new AnimationCurve(
                new Keyframe(0f, 0.05f),
                new Keyframe(20f, 0.25f),
                new Keyframe(50f, 0.70f),
                new Keyframe(80f, 1f),
                new Keyframe(120f, 0.85f));
        [Min(0f)] [SerializeField] private float groundRudderAuthority = 0.2f;
        [Min(0f)] [SerializeField] private float yawDamping = 1.5f;
        [Min(1f)] [SerializeField] private float maxYawRate = 9f;
        [Min(0f)] [SerializeField] private float yawLimitStrength = 1f;

        [Header("Slip measurement")]
        [Range(1f, 45f)] [SerializeField] private float fullScaleSlipAngle = 15f;
        [Min(0.01f)] [SerializeField] private float slipBallSmoothing = 4f;

        public float RawRudderInput =>
            input == null ? 0f : input.RudderInput;
        public float RudderInput { get; private set; }
        public float LocalForwardVelocity { get; private set; }
        public float LocalLateralVelocity { get; private set; }
        public float SlipAmount { get; private set; }
        public float SlipBallPosition { get; private set; }
        public float YawRateDegrees { get; private set; }
        public float AuthorityMultiplier { get; private set; }
        public float CommandYawMoment { get; private set; }
        public float YawDampingMoment { get; private set; }
        public float YawLimitMoment { get; private set; }
        public float AppliedYawMoment { get; private set; }
        public string SlipDirection =>
            Mathf.Abs(SlipAmount) < 0.04f
                ? "Centered"
                : SlipAmount < 0f ? "Left" : "Right";

        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (input == null) input = GetComponent<AircraftInput>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (groundMovement == null)
                groundMovement = GetComponent<GroundMovementController>();
            if (stall == null) stall = GetComponent<StallController>();
            if (wind == null) wind = FindAnyObjectByType<WindController>();
        }

        private void FixedUpdate()
        {
            float target = RawRudderInput;
            float response = Mathf.Abs(target) > 0.001f
                ? rudderSmoothing
                : rudderReturnSpeed;
            RudderInput = Mathf.MoveTowards(
                RudderInput,
                target,
                response * Time.fixedDeltaTime);

            if (wind == null) wind = FindAnyObjectByType<WindController>();
            Vector3 airRelativeVelocity =
                body.linearVelocity -
                (wind == null ? Vector3.zero : wind.CurrentWindVelocity);
            Vector3 localVelocity =
                transform.InverseTransformDirection(airRelativeVelocity);
            LocalLateralVelocity = localVelocity.x;
            LocalForwardVelocity = localVelocity.z;
            float slipAngle = Mathf.Atan2(
                LocalLateralVelocity,
                Mathf.Max(1f, Mathf.Abs(LocalForwardVelocity))) *
                Mathf.Rad2Deg;
            SlipAmount = Mathf.Clamp(
                slipAngle / Mathf.Max(1f, fullScaleSlipAngle),
                -1f,
                1f);
            float slipBlend =
                1f - Mathf.Exp(-slipBallSmoothing * Time.fixedDeltaTime);
            SlipBallPosition = Mathf.Lerp(
                SlipBallPosition, SlipAmount, slipBlend);

            Vector3 yawAxis =
                transform.TransformDirection(localYawAxis.normalized);
            float yawRate = Vector3.Dot(body.angularVelocity, yawAxis);
            YawRateDegrees = yawRate * Mathf.Rad2Deg;

            float airspeed = flightPhysics == null
                ? body.linearVelocity.magnitude * 1.943844f
                : flightPhysics.AirspeedKnots;
            bool grounded = groundMovement != null &&
                            groundMovement.IsGrounded;
            float speedAuthority = Mathf.Max(
                0f,
                rudderAuthorityByAirspeedCurve == null
                    ? 1f
                    : rudderAuthorityByAirspeedCurve.Evaluate(airspeed));
            float controlAuthority =
                stall == null ? 1f : stall.ControlAuthorityMultiplier;
            AuthorityMultiplier = speedAuthority * controlAuthority;
            float commandMoment =
                RudderInput * rudderAuthority * speedAuthority *
                controlAuthority;
            if (grounded)
            {
                float groundSpeed = groundMovement.GroundSpeedKnots;
                float groundBlend = Mathf.SmoothStep(
                    0.15f, 1f, Mathf.InverseLerp(0f, 25f, groundSpeed));
                commandMoment +=
                    RudderInput * groundRudderAuthority * groundBlend;
            }

            CommandYawMoment = commandMoment;
            float dampingMoment = -yawRate * yawDamping;
            float maxYawRateRadians = maxYawRate * Mathf.Deg2Rad;
            float limitBlend = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    maxYawRateRadians * 0.6f,
                    maxYawRateRadians,
                    Mathf.Abs(yawRate)));
            float limitMoment =
                -Mathf.Sign(yawRate) *
                limitBlend * limitBlend *
                yawLimitStrength;
            YawDampingMoment = dampingMoment;
            YawLimitMoment = limitMoment;
            AppliedYawMoment =
                commandMoment + dampingMoment + limitMoment;
            body.AddTorque(
                yawAxis * AppliedYawMoment,
                ForceMode.Acceleration);
        }

        public void ResetRudder()
        {
            RudderInput = 0f;
            LocalForwardVelocity = 0f;
            LocalLateralVelocity = 0f;
            SlipAmount = 0f;
            SlipBallPosition = 0f;
            YawRateDegrees = 0f;
            AuthorityMultiplier = 0f;
            CommandYawMoment = 0f;
            YawDampingMoment = 0f;
            YawLimitMoment = 0f;
            AppliedYawMoment = 0f;
        }
    }
}
