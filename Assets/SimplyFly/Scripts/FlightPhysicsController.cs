using UnityEngine;
using UnityEngine.Serialization;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FlightPhysicsController : MonoBehaviour
    {
        public enum AircraftState
        {
            GroundRoll,
            Rotating,
            Airborne
        }

        [Header("References")]
        [SerializeField] private AircraftInput input;
        [SerializeField] private EngineController engine;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private GroundMovementController groundMovement;
        [SerializeField] private Transform[] groundCheckPoints;

        [Header("Model axes")]
        [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
        [Tooltip("Negative local X makes positive elevator input pitch the +Z nose upward.")]
        [SerializeField] private Vector3 localPitchAxis = Vector3.left;

        [Header("Lift")]
        [SerializeField] private AnimationCurve liftCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.45f, 0.04f),
            new Keyframe(0.7f, 0.25f),
            new Keyframe(0.9f, 0.68f),
            new Keyframe(1f, 0.86f),
            new Keyframe(1.25f, 1.12f));
        [Min(0f)] [SerializeField] private float liftMultiplier = 1f;
        [Tooltip("Metres per second. 19.5 m/s is about 38 knots.")]
        [Min(1f)] [SerializeField] private float takeoffSpeed = 19.5f;
        [Min(0f)] [SerializeField] private float maxLiftForce = 16500f;
        [Range(0f, 1f)] [SerializeField] private float pitchLiftInfluence = 0.38f;
        [Tooltip("Fraction of lift retained at zero angle of attack.")]
        [Range(0.1f, 0.95f)] [SerializeField] private float levelLiftFactor = 0.68f;
        [Tooltip("Angle of attack that produces the full positive lift modifier.")]
        [Range(2f, 15f)] [SerializeField] private float referenceAngleOfAttack = 8f;

        [Header("Rotation")]
        [Tooltip("Metres per second. 17.5 m/s is about 34 knots.")]
        [Min(1f)] [SerializeField] private float rotationSpeed = 17.5f;
        [Min(0f)] [SerializeField] private float elevatorAuthority = 2.2f;
        [Min(0.01f)] [SerializeField] private float pitchResponse = 2.4f;
        [Min(0.01f)] [SerializeField] private float elevatorReturnSpeed = 3f;
        [Min(0f)] [SerializeField] private float pitchDamping = 3.2f;
        [Range(1f, 20f)] [SerializeField] private float maxRotationPitchAngle = 9f;
        [Tooltip("Elevator authority versus airspeed divided by takeoff speed.")]
        [SerializeField] private AnimationCurve elevatorAuthorityCurve = new AnimationCurve(
            new Keyframe(0f, 0.04f),
            new Keyframe(0.5f, 0.2f),
            new Keyframe(0.85f, 0.72f),
            new Keyframe(1f, 1f),
            new Keyframe(1.5f, 1.18f));
        [FormerlySerializedAs("stableAngleOfAttack")]
        [Tooltip("Gentle equilibrium angle of attack with neutral elevator.")]
        [Range(-2f, 5f)] [SerializeField] private float neutralAoA = 0.75f;
        [Tooltip("AoA error ignored by aerodynamic pitch stability.")]
        [Range(0f, 3f)] [SerializeField] private float aoADeadband = 0.5f;
        [Tooltip("Strength of the restoring moment toward stable angle of attack.")]
        [Min(0f)] [SerializeField] private float aerodynamicPitchStability = 0.085f;
        [Tooltip("Maximum aerodynamic stability moment, preserving elevator authority.")]
        [Min(0f)] [SerializeField] private float maxStabilityMoment = 0.45f;
        [Header("Virtual horizontal tail")]
        [Tooltip("Distance behind the center of mass where tail force is applied.")]
        [Min(0.1f)] [SerializeField] private float virtualTailArm = 2.7f;
        [Tooltip("Tail force per degree of effective AoA and speed squared.")]
        [Min(0f)] [SerializeField] private float tailStability = 0.42f;
        [Tooltip("Pitch-rate contribution to effective tail AoA, in seconds.")]
        [Min(0f)] [SerializeField] private float tailPitchRateInfluence = 0.32f;
        [Tooltip("Maximum corrective tail force.")]
        [Min(0f)] [SerializeField] private float maxTailForce = 1700f;
        [Tooltip("Additional neutral AoA requested per degree of descending flight path.")]
        [Range(0f, 1f)] [SerializeField] private float descentRecoveryAoAInfluence = 0.32f;
        [Tooltip("Maximum neutral AoA used for descent recovery.")]
        [Range(1f, 12f)] [SerializeField] private float maxDescentRecoveryAoA = 6f;
        [Tooltip("Soft restoring strength above the runway rotation limit.")]
        [Min(0f)] [SerializeField] private float runwayPitchLimitStrength = 0.65f;
        [Tooltip("Broad airborne pitch safety limit; not a target attitude.")]
        [Range(20f, 80f)] [SerializeField] private float maxAirbornePitchAngle = 45f;
        [Min(0f)] [SerializeField] private float airborneSafetyStrength = 0.18f;

        [Header("Ground transition")]
        [Min(0.02f)] [SerializeField] private float liftoffConfirmationTime = 0.22f;
        [Min(0.02f)] [SerializeField] private float groundedCheckDistance = 0.14f;

        [Header("Airborne forces")]
        [Tooltip("Quadratic drag coefficient: force = coefficient × speed².")]
        [Min(0f)] [SerializeField] private float aerodynamicDrag = 2.2f;
        [Min(0f)] [SerializeField] private float airborneThrustMultiplier = 1f;
        [Min(0f)] [SerializeField] private float rollStabilization = 0.055f;
        [Min(0f)] [SerializeField] private float rollDamping = 2.4f;
        [Min(0f)] [SerializeField] private float yawDamping = 0.8f;

        public AircraftState State { get; private set; } = AircraftState.GroundRoll;
        public float Airspeed { get; private set; }
        public float AirspeedKnots => Airspeed * 1.943844f;
        public float LiftForce { get; private set; }
        public float LiftPercentOfWeight { get; private set; }
        public float PitchAngle { get; private set; }
        public float AngleOfAttack { get; private set; }
        public float ElevatorInput { get; private set; }
        public float ElevatorDeflection => smoothedElevator;
        public float PitchMoment { get; private set; }
        public float TailForce { get; private set; }
        public float FlightPathAngle { get; private set; }
        public float DynamicNeutralAoA { get; private set; }
        public bool IsPhysicallyGrounded { get; private set; }
        public float VerticalSpeed { get; private set; }
        public float DragForce { get; private set; }

        private Rigidbody body;
        private float smoothedElevator;
        private float ungroundedTimer;
        private float groundedTimer;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (input == null) input = GetComponent<AircraftInput>();
            if (engine == null) engine = GetComponent<EngineController>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            if (groundMovement == null) groundMovement = GetComponent<GroundMovementController>();
            RepairLegacyTuning();
            ApplyStateConstraints();
        }

        private void FixedUpdate()
        {
            Vector3 velocity = body.linearVelocity;
            Vector3 forward = transform.TransformDirection(localForwardAxis.normalized);
            Vector3 pitchAxis = transform.TransformDirection(localPitchAxis.normalized);

            Airspeed = velocity.magnitude;
            VerticalSpeed = Vector3.Dot(velocity, Vector3.up);
            FlightPathAngle = Airspeed > 0.1f
                ? Mathf.Asin(Mathf.Clamp(
                    Vector3.Dot(velocity.normalized, Vector3.up), -1f, 1f)) *
                  Mathf.Rad2Deg
                : 0f;
            PitchAngle = Mathf.Asin(Mathf.Clamp(Vector3.Dot(forward, Vector3.up), -1f, 1f)) * Mathf.Rad2Deg;
            AngleOfAttack = CalculateAngleOfAttack(velocity, forward);
            ElevatorInput = input == null ? 0f : input.ElevatorInput;
            float elevatorSmoothing = Mathf.Abs(ElevatorInput) > Mathf.Abs(smoothedElevator)
                ? pitchResponse
                : elevatorReturnSpeed;
            smoothedElevator = Mathf.MoveTowards(
                smoothedElevator, ElevatorInput, elevatorSmoothing * Time.fixedDeltaTime);
            IsPhysicallyGrounded = CheckPhysicalGrounding();

            UpdateState();
            ApplyStateConstraints();
            if (groundMovement != null)
                groundMovement.SetFlightTransition(
                    State != AircraftState.GroundRoll, IsPhysicallyGrounded);

            ApplyLift(velocity, pitchAxis);
            ApplyDrag(velocity);
            ApplyPitch(pitchAxis);

            if (!IsPhysicallyGrounded)
                ApplyAirborneThrust(forward);

            if (State == AircraftState.Airborne)
            {
                ApplyVirtualTail(forward, pitchAxis);
                ApplyAirborneStability(forward, pitchAxis);
            }
            else
            {
                TailForce = 0f;
            }
        }

        private void ApplyLift(Vector3 velocity, Vector3 pitchAxis)
        {
            float normalizedSpeed = takeoffSpeed <= 0f ? 0f : Airspeed / takeoffSpeed;
            float baseLiftRatio = Mathf.Max(0f, liftCurve.Evaluate(normalizedSpeed)) * liftMultiplier;
            float normalizedAoA = Mathf.Clamp(
                AngleOfAttack / referenceAngleOfAttack, -1f, 1f);
            float aoaModifier = normalizedAoA >= 0f
                ? Mathf.Lerp(levelLiftFactor, 1f + pitchLiftInfluence, normalizedAoA)
                : Mathf.Lerp(levelLiftFactor, 0.12f, -normalizedAoA);
            float weight = body.mass * Physics.gravity.magnitude;
            LiftForce = Mathf.Min(maxLiftForce, weight * baseLiftRatio * aoaModifier);
            LiftPercentOfWeight = weight <= 0f ? 0f : LiftForce / weight * 100f;

            Vector3 liftDirection = Airspeed > 0.1f
                ? Vector3.Cross(pitchAxis, velocity.normalized).normalized
                : transform.up;
            if (Vector3.Dot(liftDirection, transform.up) < 0f)
                liftDirection = -liftDirection;
            body.AddForce(liftDirection * LiftForce, ForceMode.Force);
        }

        private float CalculateAngleOfAttack(Vector3 velocity, Vector3 forward)
        {
            if (velocity.sqrMagnitude < 0.25f) return 0f;

            float forwardVelocity = Vector3.Dot(velocity, forward);
            float aircraftUpVelocity = Vector3.Dot(velocity, transform.up);
            return Mathf.Atan2(-aircraftUpVelocity, Mathf.Max(0.1f, forwardVelocity)) *
                   Mathf.Rad2Deg;
        }

        private void ApplyDrag(Vector3 velocity)
        {
            if (Airspeed < 0.01f)
            {
                DragForce = 0f;
                return;
            }

            DragForce = aerodynamicDrag * Airspeed * Airspeed;
            body.AddForce(-velocity.normalized * DragForce, ForceMode.Force);
        }

        private void ApplyPitch(Vector3 pitchAxis)
        {
            float normalizedSpeed = takeoffSpeed <= 0f ? 0f : Airspeed / takeoffSpeed;
            float speedAuthority = Mathf.Max(0f, elevatorAuthorityCurve.Evaluate(normalizedSpeed));
            bool pitchAllowed = State != AircraftState.GroundRoll;
            float pitchRate = Vector3.Dot(body.angularVelocity, pitchAxis);
            float commandMoment = pitchAllowed
                ? smoothedElevator * elevatorAuthority * speedAuthority
                : 0f;
            float dampingMoment = -pitchRate * pitchDamping;
            float stabilityTargetAoA = 0f;
            float aoaError = AngleOfAttack - stabilityTargetAoA;
            float stabilityMoment = 0f;
            if (pitchAllowed && State != AircraftState.Airborne &&
                Mathf.Abs(aoaError) >= aoADeadband)
            {
                float rawStabilityMoment =
                    -aoaError * aerodynamicPitchStability * speedAuthority;
                stabilityMoment = Mathf.Clamp(
                    rawStabilityMoment, -maxStabilityMoment, maxStabilityMoment);

                float elevatorFadeProgress = Mathf.InverseLerp(
                    0.05f, 0.45f, Mathf.Abs(smoothedElevator));
                float elevatorFade = 1f - Mathf.SmoothStep(
                    0f, 1f, elevatorFadeProgress);
                stabilityMoment *= elevatorFade;
            }
            float limitMoment = 0f;

            if (State != AircraftState.Airborne)
            {
                // Soften nose-up input before the tail-strike attitude, then push back
                // progressively if momentum carries the aircraft beyond it.
                float runwayFade = Mathf.SmoothStep(
                    0f, 1f,
                    Mathf.InverseLerp(
                        maxRotationPitchAngle - 2f,
                        maxRotationPitchAngle + 1f,
                        PitchAngle));
                if (commandMoment > 0f)
                    commandMoment *= 1f - runwayFade;
                if (PitchAngle > maxRotationPitchAngle)
                    limitMoment -=
                        (PitchAngle - maxRotationPitchAngle) * runwayPitchLimitStrength;
            }
            else
            {
                // Broad recovery only. This does not select or hold an attitude.
                if (PitchAngle > maxAirbornePitchAngle)
                    limitMoment -=
                        (PitchAngle - maxAirbornePitchAngle) * airborneSafetyStrength;
                else if (PitchAngle < -maxAirbornePitchAngle)
                    limitMoment +=
                        (-maxAirbornePitchAngle - PitchAngle) * airborneSafetyStrength;
            }

            PitchMoment = commandMoment + dampingMoment + stabilityMoment + limitMoment;
            body.AddTorque(pitchAxis * PitchMoment, ForceMode.Acceleration);
        }

        private void ApplyVirtualTail(Vector3 forward, Vector3 pitchAxis)
        {
            float pitchRateDegrees =
                Vector3.Dot(body.angularVelocity, pitchAxis) * Mathf.Rad2Deg;
            DynamicNeutralAoA = Mathf.Clamp(
                neutralAoA +
                Mathf.Max(0f, -FlightPathAngle) * descentRecoveryAoAInfluence,
                neutralAoA,
                maxDescentRecoveryAoA);
            float effectiveAoAError =
                AngleOfAttack - DynamicNeutralAoA +
                pitchRateDegrees * tailPitchRateInfluence;

            float errorMagnitude = Mathf.Max(
                0f, Mathf.Abs(effectiveAoAError) - aoADeadband);
            float softenedError = Mathf.Sign(effectiveAoAError) * errorMagnitude;
            float elevatorDominance = Mathf.SmoothStep(
                0f, 1f,
                Mathf.InverseLerp(0.1f, 0.75f, Mathf.Abs(smoothedElevator)));
            float stabilityBlend = Mathf.Lerp(1f, 0.3f, elevatorDominance);

            TailForce = Mathf.Clamp(
                softenedError * tailStability * Airspeed * Airspeed * stabilityBlend,
                -maxTailForce,
                maxTailForce);

            Vector3 tailPosition = body.worldCenterOfMass - forward * virtualTailArm;
            body.AddForceAtPosition(
                transform.up * TailForce,
                tailPosition,
                ForceMode.Force);
        }

        private void ApplyAirborneThrust(Vector3 forward)
        {
            if (engine == null || engine.State != EngineController.EngineState.Running) return;

            float engineThrust = groundMovement != null
                ? groundMovement.CurrentEngineThrust
                : Mathf.Lerp(420f, 5220f, throttle == null ? 0f : throttle.Throttle);
            body.AddForce(forward * engineThrust * airborneThrustMultiplier, ForceMode.Force);
        }

        private void ApplyAirborneStability(Vector3 forward, Vector3 pitchAxis)
        {
            float rollAngle = Vector3.SignedAngle(Vector3.up, transform.up, forward);
            float rollRate = Vector3.Dot(body.angularVelocity, forward);
            body.AddTorque(
                forward * (-rollAngle * rollStabilization - rollRate * rollDamping),
                ForceMode.Acceleration);

            float yawRate = Vector3.Dot(body.angularVelocity, Vector3.up);
            body.AddTorque(Vector3.up * (-yawRate * yawDamping), ForceMode.Acceleration);

        }

        private void UpdateState()
        {
            if (IsPhysicallyGrounded)
            {
                groundedTimer += Time.fixedDeltaTime;
                ungroundedTimer = 0f;
            }
            else
            {
                groundedTimer = 0f;
                ungroundedTimer += Time.fixedDeltaTime;
            }

            switch (State)
            {
                case AircraftState.GroundRoll:
                    if (Airspeed >= rotationSpeed * 0.9f)
                        State = AircraftState.Rotating;
                    break;

                case AircraftState.Rotating:
                    if (!IsPhysicallyGrounded &&
                        ungroundedTimer >= liftoffConfirmationTime &&
                        VerticalSpeed > 0f)
                    {
                        State = AircraftState.Airborne;
                    }
                    else if (IsPhysicallyGrounded && Airspeed < rotationSpeed * 0.72f)
                    {
                        State = AircraftState.GroundRoll;
                    }
                    break;

                case AircraftState.Airborne:
                    if (IsPhysicallyGrounded && groundedTimer >= liftoffConfirmationTime)
                        State = Airspeed >= rotationSpeed * 0.72f
                            ? AircraftState.Rotating
                            : AircraftState.GroundRoll;
                    break;
            }
        }

        private bool CheckPhysicalGrounding()
        {
            if (groundCheckPoints == null || groundCheckPoints.Length == 0)
                return groundMovement != null && groundMovement.IsGrounded;

            foreach (Transform point in groundCheckPoints)
            {
                if (point == null) continue;
                SphereCollider support = point.GetComponent<SphereCollider>();
                Vector3 origin = support != null
                    ? point.TransformPoint(support.center)
                    : point.position;
                float supportRadius = support != null
                    ? support.radius * Mathf.Max(
                        Mathf.Abs(point.lossyScale.x),
                        Mathf.Abs(point.lossyScale.y),
                        Mathf.Abs(point.lossyScale.z))
                    : 0.4f;
                RaycastHit[] hits = Physics.RaycastAll(
                    origin + transform.up * 0.02f, -transform.up,
                    supportRadius + groundedCheckDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider != null && hit.collider.attachedRigidbody != body)
                        return true;
                }
            }
            return false;
        }

        private void ApplyStateConstraints()
        {
            body.constraints = State switch
            {
                AircraftState.GroundRoll =>
                    RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ,
                AircraftState.Rotating =>
                    RigidbodyConstraints.FreezeRotationZ,
                _ => RigidbodyConstraints.None
            };
        }

        private void RepairLegacyTuning()
        {
            if (takeoffSpeed <= 0f) takeoffSpeed = 19.5f;
            if (rotationSpeed <= 0f) rotationSpeed = 17.5f;
            if (maxLiftForce <= 0f) maxLiftForce = 16500f;
            if (elevatorAuthority <= 0f) elevatorAuthority = 2.2f;
            if (pitchResponse <= 0f) pitchResponse = 2.4f;
            if (elevatorReturnSpeed <= 0f) elevatorReturnSpeed = 3f;
            if (pitchDamping <= 0f) pitchDamping = 3.2f;
            if (maxRotationPitchAngle <= 0f) maxRotationPitchAngle = 9f;
            if (liftoffConfirmationTime <= 0f) liftoffConfirmationTime = 0.22f;
            if (groundedCheckDistance <= 0f) groundedCheckDistance = 0.14f;
            if (levelLiftFactor <= 0f) levelLiftFactor = 0.68f;
            if (referenceAngleOfAttack <= 0f) referenceAngleOfAttack = 8f;
            if (elevatorAuthorityCurve == null || elevatorAuthorityCurve.length == 0)
            {
                elevatorAuthorityCurve = new AnimationCurve(
                    new Keyframe(0f, 0.04f),
                    new Keyframe(0.5f, 0.2f),
                    new Keyframe(0.85f, 0.72f),
                    new Keyframe(1f, 1f),
                    new Keyframe(1.5f, 1.18f));
            }
            if (aerodynamicPitchStability <= 0f) aerodynamicPitchStability = 0.085f;
            if (neutralAoA >= 2.5f) neutralAoA = 0.75f;
            if (aoADeadband <= 0f) aoADeadband = 0.5f;
            if (maxStabilityMoment <= 0f) maxStabilityMoment = 0.45f;
            if (virtualTailArm <= 0f) virtualTailArm = 2.7f;
            if (tailStability <= 0f) tailStability = 0.42f;
            if (tailPitchRateInfluence <= 0f) tailPitchRateInfluence = 0.32f;
            if (maxTailForce <= 0f) maxTailForce = 1700f;
            if (descentRecoveryAoAInfluence <= 0f)
                descentRecoveryAoAInfluence = 0.32f;
            if (maxDescentRecoveryAoA <= neutralAoA)
                maxDescentRecoveryAoA = 6f;
            if (runwayPitchLimitStrength <= 0f) runwayPitchLimitStrength = 0.65f;
            if (maxAirbornePitchAngle <= 0f) maxAirbornePitchAngle = 45f;
            if (airborneSafetyStrength <= 0f) airborneSafetyStrength = 0.18f;
        }

        public void ResetFlight()
        {
            State = AircraftState.GroundRoll;
            Airspeed = 0f;
            LiftForce = 0f;
            LiftPercentOfWeight = 0f;
            PitchAngle = 0f;
            AngleOfAttack = 0f;
            ElevatorInput = 0f;
            PitchMoment = 0f;
            TailForce = 0f;
            FlightPathAngle = 0f;
            DynamicNeutralAoA = neutralAoA;
            VerticalSpeed = 0f;
            DragForce = 0f;
            smoothedElevator = 0f;
            ungroundedTimer = 0f;
            groundedTimer = 0f;
            IsPhysicallyGrounded = true;
            ApplyStateConstraints();
        }
    }
}
