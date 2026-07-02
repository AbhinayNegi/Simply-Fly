using UnityEngine;
using UnityEngine.Serialization;

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
        [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
        [Tooltip("Propeller thrust available at engine idle.")]
        [Min(0f)] [SerializeField] private float idleThrust = 420f;
        [FormerlySerializedAs("maximumDriveForce")]
        [Tooltip("Additional propeller thrust available at full throttle.")]
        [Min(0f)] [SerializeField] private float maxGroundThrust = 3000f;
        [Tooltip("How quickly movement throttle catches the commanded throttle.")]
        [Min(0.01f)] [SerializeField] private float throttleResponseSpeed = 0.4f;
        [Tooltip("Metres per second. 34 m/s is roughly 66 knots.")]
        [Min(1f)] [SerializeField] private float maximumTaxiSpeed = 34f;
        [Tooltip("Maps throttle to the fraction between idle-creep and maximum ground speed.")]
        [SerializeField] private AnimationCurve speedLimitThrottleCurve =
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.25f, 0.13f),
                new Keyframe(0.5f, 0.38f),
                new Keyframe(0.75f, 0.7f),
                new Keyframe(1f, 1f));
        [Range(0.5f, 0.98f)] [SerializeField] private float speedFadeStartRatio = 0.9f;
        [Tooltip("Gentle force opposing motion when brakes are released.")]
        [Min(0f)] [SerializeField] private float rollingResistance = 70f;
        [Tooltip("Speed over which normal rolling resistance builds to full strength.")]
        [Min(0.05f)] [SerializeField] private float rollingResistanceBuildSpeed = 0.75f;
        [Tooltip("Stable idle creep speed in metres per second.")]
        [Min(0.1f)] [SerializeField] private float idleCreepSpeed = 1.8f;
        [Tooltip("Fraction of thrust retained at a standstill for soft breakaway.")]
        [Range(0.05f, 1f)] [SerializeField] private float breakawayThrust = 0.18f;
        [Tooltip("Speed range over which static friction fades away.")]
        [Min(0.05f)] [SerializeField] private float staticFrictionSoftness = 2.5f;
        [Min(0f)] [SerializeField] private float lateralGrip = 2.5f;
        [Min(0f)] [SerializeField] private float groundHoldForce = 2500f;
        [Tooltip("Fallback ground probe below each wheel, used while suspension is fully compressed.")]
        [Min(0.1f)] [SerializeField] private float groundProbeDistance = 1.1f;
        [Tooltip("Ground-only safety probe from the aircraft body.")]
        [Min(0.5f)] [SerializeField] private float bodyGroundProbeDistance = 3f;

        public float GroundSpeed { get; private set; }
        public float GroundSpeedKnots => GroundSpeed * 1.943844f;
        public bool IsGrounded { get; private set; }
        public float SmoothedThrottle => smoothedThrottle;
        public float AppliedThrust { get; private set; }
        public float CurrentEngineThrust => engineThrustDiagnostic;
        public float GroundAcceleration { get; private set; }
        public bool FlightTransitionActive { get; private set; }
        public bool FlightWheelsGrounded { get; private set; } = true;

        private Rigidbody body;
        private float smoothedThrottle;
        private int fixedTickCount;
        private int groundedWheelCount;
        private bool bodyProbeGrounded;
        private bool engineRunningDiagnostic;
        private float engineThrustDiagnostic;
        private float breakawayFactorDiagnostic;
        private float speedFadeDiagnostic;
        private float resistanceForceDiagnostic;
        private float brakeForceDiagnostic;
        private float netForceDiagnostic;
        private float requestedMoveDiagnostic;
        private float actualMoveDiagnostic;
        private Vector3 previousPhysicsPosition;
        private bool hasPreviousPhysicsPosition;
        private int enabledWheelColliderCount;
        private float previousGroundSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (engine == null) engine = GetComponent<EngineController>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            if (brakes == null) brakes = GetComponent<BrakeController>();
            RepairLegacyTuning();
            ConfigureWheelsForSimplifiedGroundPhysics();
        }

        private void FixedUpdate()
        {
            fixedTickCount++;
            if (hasPreviousPhysicsPosition)
                actualMoveDiagnostic = Vector3.Distance(body.position, previousPhysicsPosition);
            previousPhysicsPosition = body.position;
            hasPreviousPhysicsPosition = true;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            GroundSpeed = planarVelocity.magnitude;
            GroundAcceleration =
                (GroundSpeed - previousGroundSpeed) / Time.fixedDeltaTime;
            previousGroundSpeed = GroundSpeed;
            IsGrounded = false;
            groundedWheelCount = 0;

            float brakeForce = brakes == null ? 0f : brakes.BrakeForce;
            if (wheels != null)
            {
                foreach (WheelCollider wheel in wheels)
                {
                    if (wheel == null) continue;
                    wheel.brakeTorque = brakeForce;
                    bool wheelGrounded = wheel.isGrounded || HasGroundBelow(wheel);
                    if (wheelGrounded) groundedWheelCount++;
                    IsGrounded |= wheelGrounded;
                }
            }

            bodyProbeGrounded = HasGroundBelowBody();
            IsGrounded |= bodyProbeGrounded;

            Vector3 forward = transform.TransformDirection(localForwardAxis.normalized);
            bool engineRunning = engine != null &&
                                 engine.State == EngineController.EngineState.Running;
            engineRunningDiagnostic = engineRunning;
            float commandedThrottle = engineRunning && throttle != null ? throttle.Throttle : 0f;
            smoothedThrottle = Mathf.MoveTowards(
                smoothedThrottle, commandedThrottle, throttleResponseSpeed * Time.fixedDeltaTime);
            float brakeAmount = brakes == null ? 0f : brakes.BrakeAmount;
            float engineThrust = engineRunning
                ? idleThrust + smoothedThrottle * maxGroundThrust
                : 0f;
            engineThrustDiagnostic = engineThrust;

            float forwardSpeed = Mathf.Max(0f, Vector3.Dot(planarVelocity, forward));
            float breakawayProgress = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(forwardSpeed / staticFrictionSoftness));
            float breakawayFactor = Mathf.Lerp(breakawayThrust, 1f, breakawayProgress);
            breakawayFactorDiagnostic = breakawayFactor;
            float speedLimitFraction = Mathf.Clamp01(
                speedLimitThrottleCurve.Evaluate(smoothedThrottle));
            float speedLimit = Mathf.Lerp(
                idleCreepSpeed,
                maximumTaxiSpeed,
                speedLimitFraction);
            float speedFadeProgress = Mathf.InverseLerp(
                speedLimit * speedFadeStartRatio, speedLimit, forwardSpeed);
            float speedFade = 1f - Mathf.SmoothStep(0f, 1f, speedFadeProgress);
            speedFade = Mathf.Clamp01(speedFade);
            speedFadeDiagnostic = speedFade;
            float appliedThrust = IsGrounded ? engineThrust * breakawayFactor * speedFade : 0f;
            AppliedThrust = appliedThrust;

            float resistanceMovingFactor = Mathf.Clamp01(
                GroundSpeed / rollingResistanceBuildSpeed);
            float brakeMovingFactor = Mathf.Clamp01(GroundSpeed / 0.15f);
            float resistanceForce = rollingResistance * resistanceMovingFactor;
            float appliedBrakeForce = brakeForce * brakeMovingFactor;
            float netForce = appliedThrust - resistanceForce - appliedBrakeForce;
            resistanceForceDiagnostic = resistanceForce;
            brakeForceDiagnostic = appliedBrakeForce;
            netForceDiagnostic = netForce;
            if (IsGrounded && (!FlightTransitionActive || FlightWheelsGrounded))
            {
                body.AddForce(forward * appliedThrust, ForceMode.Force);

                if (GroundSpeed > 0.001f)
                {
                    body.AddForce(
                        -planarVelocity.normalized * (resistanceForce + appliedBrakeForce),
                        ForceMode.Force);
                }

                Vector3 lateralVelocity =
                    planarVelocity - Vector3.Project(planarVelocity, forward);
                body.AddForce(
                    -lateralVelocity * lateralGrip * body.mass,
                    ForceMode.Force);
                if (!FlightTransitionActive)
                    body.AddForce(Vector3.down * groundHoldForce, ForceMode.Force);
            }

            requestedMoveDiagnostic = forwardSpeed * Time.fixedDeltaTime;
        }

        public void SetFlightTransition(bool active, bool wheelsGrounded)
        {
            FlightTransitionActive = active;
            FlightWheelsGrounded = wheelsGrounded;
        }

        public void ResetGroundMovement()
        {
            smoothedThrottle = 0f;
            AppliedThrust = 0f;
            engineThrustDiagnostic = 0f;
            GroundSpeed = 0f;
            FlightTransitionActive = false;
            FlightWheelsGrounded = true;
            requestedMoveDiagnostic = 0f;
            actualMoveDiagnostic = 0f;
            hasPreviousPhysicsPosition = false;
            previousGroundSpeed = 0f;
            GroundAcceleration = 0f;
        }

        public string GetDiagnostics()
        {
            if (body == null) return "GroundMovement: Rigidbody reference is NULL";

            return
                "GROUND MOVEMENT DIAGNOSTICS\n" +
                $"Component enabled : {enabled}\n" +
                $"Fixed ticks       : {fixedTickCount}\n" +
                $"Fixed delta       : {Time.fixedDeltaTime:0.0000} s\n" +
                $"Engine reference  : {(engine == null ? "NULL" : engine.State.ToString())}\n" +
                $"Engine running    : {engineRunningDiagnostic}\n" +
                $"Throttle reference: {(throttle == null ? "NULL" : $"{throttle.Throttle * 100f:0.0}%")}\n" +
                $"Smoothed throttle : {smoothedThrottle * 100f:0.0}%\n" +
                $"Idle / max thrust : {idleThrust:0} / {maxGroundThrust:0} N\n" +
                $"Throttle response : {throttleResponseSpeed:0.000}/s\n" +
                $"Engine thrust     : {engineThrustDiagnostic:0.0} N\n" +
                $"Breakaway factor  : {breakawayFactorDiagnostic:0.000}\n" +
                $"Speed fade        : {speedFadeDiagnostic:0.000}\n" +
                $"Applied thrust    : {AppliedThrust:0.0} N\n" +
                $"Rolling resistance: {resistanceForceDiagnostic:0.0} N\n" +
                $"Brake force       : {brakeForceDiagnostic:0.0} N\n" +
                $"NET FORCE         : {netForceDiagnostic:0.0} N\n" +
                $"Forward speed     : {Mathf.Max(0f, Vector3.Dot(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up), transform.TransformDirection(localForwardAxis.normalized))):0.000} m/s\n" +
                $"Requested move    : {requestedMoveDiagnostic:0.00000} m/tick\n" +
                $"Actual move       : {actualMoveDiagnostic:0.00000} m/tick\n" +
                $"Grounded          : {IsGrounded}\n" +
                $"Ground acceleration: {GroundAcceleration:+0.00;-0.00;0.00} m/s2\n" +
                $"Grounded wheels   : {groundedWheelCount}/{(wheels == null ? 0 : wheels.Length)}\n" +
                $"WheelColliders on : {enabledWheelColliderCount}/{(wheels == null ? 0 : wheels.Length)}\n" +
                $"Body ground probe : {bodyProbeGrounded}\n" +
                $"Body kinematic    : {body.isKinematic}\n" +
                $"Body sleeping     : {body.IsSleeping()}\n" +
                $"Body constraints  : {body.constraints}\n" +
                $"Body velocity     : {body.linearVelocity}\n" +
                $"Body position     : {body.position}";
        }

        private void RepairLegacyTuning()
        {
            // Components created before v4 do not contain these serialized fields.
            // Unity deserializes missing fields as zero rather than using field initializers.
            if (idleThrust <= 0f) idleThrust = 420f;
            if (maxGroundThrust <= 0f) maxGroundThrust = 3000f;
            if (throttleResponseSpeed <= 0f) throttleResponseSpeed = 0.4f;
            if (rollingResistance <= 0f) rollingResistance = 70f;
            if (rollingResistanceBuildSpeed <= 0f)
                rollingResistanceBuildSpeed = 0.75f;
            if (idleCreepSpeed <= 0f) idleCreepSpeed = 1.8f;
            if (breakawayThrust <= 0f) breakawayThrust = 0.18f;
            if (staticFrictionSoftness <= 0f) staticFrictionSoftness = 2.5f;
            if (maximumTaxiSpeed < 25f) maximumTaxiSpeed = 34f;
            if (speedLimitThrottleCurve == null || speedLimitThrottleCurve.length == 0)
            {
                speedLimitThrottleCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.25f, 0.13f),
                    new Keyframe(0.5f, 0.38f),
                    new Keyframe(0.75f, 0.7f),
                    new Keyframe(1f, 1f));
            }
            if (speedFadeStartRatio <= 0f) speedFadeStartRatio = 0.9f;
        }

        private void ConfigureWheelsForSimplifiedGroundPhysics()
        {
            if (wheels == null) return;

            PhysicsMaterial supportMaterial = new PhysicsMaterial("Aircraft Wheel Support")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            foreach (WheelCollider wheel in wheels)
            {
                if (wheel == null) continue;

                // Even at zero tire stiffness, Unity's vehicle solver constrained this
                // imported +X-forward model. A simple sphere provides stable wheel-height
                // support while our controller handles traction, braking and steering.
                SphereCollider support = wheel.GetComponent<SphereCollider>();
                if (support == null) support = wheel.gameObject.AddComponent<SphereCollider>();
                support.radius = wheel.radius;
                support.center = Vector3.down * 0.18f;
                support.material = supportMaterial;
                wheel.enabled = false;
            }

            enabledWheelColliderCount = 0;
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

        private bool HasGroundBelowBody()
        {
            Vector3 origin = body.worldCenterOfMass + transform.up * 0.25f;
            RaycastHit[] hits = Physics.RaycastAll(origin, -transform.up, bodyGroundProbeDistance,
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
