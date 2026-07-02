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
        [SerializeField] private StallController stall;
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
        [Min(0f)] [SerializeField] private float liftMultiplier = 1.04f;
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
        [Tooltip("Neutral AoA used at ordinary cruise power.")]
        [Range(-2f, 6f)] [SerializeField] private float cruiseNeutralAoA = 1.2f;
        [Tooltip("Neutral AoA used near full throttle for a shallow climb.")]
        [Range(-2f, 8f)] [SerializeField] private float climbNeutralAoA = 2f;
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
        [Tooltip("Neutral-input vertical-speed correction, in AoA degrees per m/s of error.")]
        [Min(0f)] [SerializeField] private float verticalSpeedStability = 0.38f;
        [Tooltip("Desired neutral-input vertical speed near full throttle.")]
        [Range(-2f, 4f)] [SerializeField] private float targetNeutralVerticalSpeed = 1.2f;
        [Tooltip("Neutral-input descent target at idle throttle.")]
        [Range(-6f, 0f)] [SerializeField] private float idleTargetVerticalSpeed = -2.5f;
        [Tooltip("Throttle setting that targets approximately level flight.")]
        [Range(0.2f, 0.8f)] [SerializeField] private float levelFlightThrottle = 0.52f;
        [Tooltip("Maximum AoA adjustment made by vertical-speed stability.")]
        [Range(0f, 8f)] [SerializeField] private float maxVerticalSpeedAoACorrection = 3.5f;
        [Tooltip("Soft restoring strength above the runway rotation limit.")]
        [Min(0f)] [SerializeField] private float runwayPitchLimitStrength = 0.65f;
        [Tooltip("Broad airborne pitch safety limit; not a target attitude.")]
        [Range(20f, 80f)] [SerializeField] private float maxAirbornePitchAngle = 45f;
        [Min(0f)] [SerializeField] private float airborneSafetyStrength = 0.18f;

        [Header("Ground transition")]
        [Min(0.02f)] [SerializeField] private float liftoffConfirmationTime = 0.22f;
        [Min(0.02f)] [SerializeField] private float groundedCheckDistance = 0.14f;
        [Tooltip("Pitch angle below which the nose is considered settled after landing.")]
        [Range(0.25f, 4f)] [SerializeField] private float noseSettledPitchAngle = 1.25f;
        [Tooltip("Gentle nose-down moment after the main wheels touch at low speed.")]
        [Min(0f)] [SerializeField] private float groundNoseSettlingStrength = 0.2f;
        [Tooltip("Maximum low-speed nose settling acceleration.")]
        [Min(0f)] [SerializeField] private float maxGroundNoseSettlingMoment = 1.4f;

        [Header("Airborne forces")]
        [Tooltip("Quadratic drag coefficient: force = coefficient × speed².")]
        [Min(0f)] [SerializeField] private float aerodynamicDrag = 2.2f;
        [Min(0f)] [SerializeField] private float dragMultiplier = 1f;
        [Min(0f)] [SerializeField] private float airborneThrustMultiplier = 1f;

        [Header("Airborne roll")]
        [Min(0f)] [SerializeField] private float aileronAuthority = 2.2f;
        [Min(0.01f)] [SerializeField] private float rollResponse = 3f;
        [Min(0.01f)] [SerializeField] private float aileronReturnSpeed = 4f;
        [Tooltip("Aileron authority versus airspeed divided by takeoff speed.")]
        [SerializeField] private AnimationCurve rollAuthorityCurve = new AnimationCurve(
            new Keyframe(0f, 0.02f),
            new Keyframe(0.6f, 0.22f),
            new Keyframe(0.9f, 0.75f),
            new Keyframe(1f, 1f),
            new Keyframe(1.6f, 1.15f));
        [Min(0f)] [SerializeField] private float rollDamping = 2.4f;
        [FormerlySerializedAs("rollStabilization")]
        [Tooltip("Optional automatic wings-level tendency. Zero preserves the current bank.")]
        [Min(0f)] [SerializeField] private float bankStability = 0f;
        [Range(30f, 85f)] [SerializeField] private float maximumBankAngle = 70f;
        [Min(0f)] [SerializeField] private float bankLimitStrength = 0.12f;
        [Tooltip("Acceleration opposing velocity across the aircraft body.")]
        [Min(0f)] [SerializeField] private float lateralSlipDamping = 0.7f;
        [Tooltip("Response gain used to follow the bank-derived yaw rate.")]
        [Min(0f)] [SerializeField] private float coordinatedTurnStrength = 3f;
        [Tooltip("Multiplier for the natural bank-derived turn rate.")]
        [Min(0f)] [SerializeField] private float bankTurnStrength = 1f;
        [Tooltip("Maximum yaw rate requested by coordinated-turn assistance.")]
        [Range(1f, 45f)] [SerializeField] private float maxAssistedYawRate = 18f;
        [Min(0f)] [SerializeField] private float yawDamping = 0.7f;
        [Tooltip("Safety clamp for sideways damping acceleration.")]
        [Min(0f)] [SerializeField] private float maxLateralSlipAcceleration = 14f;

        public AircraftState State { get; private set; } = AircraftState.GroundRoll;
        public string DebugFlightState =>
            State == AircraftState.GroundRoll ? "Grounded" : State.ToString();
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
        public float AileronInput { get; private set; }
        public float AileronDeflection => smoothedAileron;
        public float BankAngle { get; private set; }
        public float RollRate { get; private set; }
        public float RollMoment { get; private set; }
        public float CoordinatedYawMoment { get; private set; }
        public float Altitude => body == null ? transform.position.y : body.position.y;
        public float Heading { get; private set; }
        public float TurnRate { get; private set; }
        public string TurnDirection =>
            TurnRate > 0.15f ? "Right" : TurnRate < -0.15f ? "Left" : "None";
        public float YawRate { get; private set; }
        public float YawAssistMoment { get; private set; }
        public float YawDampingMoment { get; private set; }
        public float MaximumBankAngle => maximumBankAngle;
        public float CoordinatedTurnStrength => coordinatedTurnStrength;
        public float BankTurnStrengthSetting => bankTurnStrength;
        public float LateralSlipDamping => lateralSlipDamping;
        public float MaxAssistedYawRate => maxAssistedYawRate;
        public float VerticalLiftForce { get; private set; }
        public float VerticalLiftPercentOfWeight { get; private set; }
        public float SideLiftForce { get; private set; }
        public float BankTurnForcePercent { get; private set; }
        public float LateralSlipDampingForce { get; private set; }
        public float LateralVelocity { get; private set; }
        public float ForwardVelocity { get; private set; }
        public float HorizontalGroundSpeed { get; private set; }

        private Rigidbody body;
        private float smoothedElevator;
        private float smoothedAileron;
        private float ungroundedTimer;
        private float groundedTimer;
        private float previousHeading;
        private bool headingInitialized;
        private float landingLiftMultiplier = 1f;
        private bool landingRollActive;
        private bool landingTransitionActive;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (input == null) input = GetComponent<AircraftInput>();
            if (engine == null) engine = GetComponent<EngineController>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            if (groundMovement == null) groundMovement = GetComponent<GroundMovementController>();
            if (stall == null) stall = GetComponent<StallController>();
            RepairLegacyTuning();
            ApplyStateConstraints();
        }

        private void FixedUpdate()
        {
            Vector3 velocity = body.linearVelocity;
            Vector3 forward = transform.TransformDirection(localForwardAxis.normalized);
            Vector3 pitchAxis = transform.TransformDirection(localPitchAxis.normalized);

            Airspeed = velocity.magnitude;
            ForwardVelocity = Vector3.Dot(velocity, forward);
            LateralVelocity = Vector3.Dot(velocity, transform.right);
            HorizontalGroundSpeed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
            UpdateHeading(forward);
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
            AileronInput = State == AircraftState.Airborne && input != null
                ? input.SteeringInput
                : 0f;
            float aileronSmoothing = Mathf.Abs(AileronInput) > Mathf.Abs(smoothedAileron)
                ? rollResponse
                : aileronReturnSpeed;
            smoothedAileron = Mathf.MoveTowards(
                smoothedAileron,
                AileronInput,
                aileronSmoothing * Time.fixedDeltaTime);
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
                ApplyAirborneRollAndYaw(forward);
            }
            else
            {
                TailForce = 0f;
                BankAngle = 0f;
                RollRate = 0f;
                RollMoment = 0f;
                YawRate = Vector3.Dot(body.angularVelocity, Vector3.up);
                YawAssistMoment = 0f;
                YawDampingMoment = 0f;
                CoordinatedYawMoment = 0f;
                LateralSlipDampingForce = 0f;
            }
        }

        private void ApplyLift(Vector3 velocity, Vector3 pitchAxis)
        {
            float normalizedSpeed = takeoffSpeed <= 0f ? 0f : Airspeed / takeoffSpeed;
            float baseLiftRatio =
                Mathf.Max(0f, liftCurve.Evaluate(normalizedSpeed)) *
                liftMultiplier *
                landingLiftMultiplier *
                (stall == null ? 1f : stall.LiftMultiplier);
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
            Vector3 liftVector = liftDirection * LiftForce;
            body.AddForce(liftVector, ForceMode.Force);

            VerticalLiftForce = Vector3.Dot(liftVector, Vector3.up);
            VerticalLiftPercentOfWeight =
                weight <= 0f ? 0f : VerticalLiftForce / weight * 100f;
            Vector3 horizontalForward = Vector3.ProjectOnPlane(
                transform.TransformDirection(localForwardAxis.normalized),
                Vector3.up).normalized;
            Vector3 horizontalRight = Vector3.Cross(Vector3.up, horizontalForward).normalized;
            SideLiftForce = horizontalRight.sqrMagnitude > 0.001f
                ? Vector3.Dot(liftVector, horizontalRight)
                : 0f;
            BankTurnForcePercent = weight <= 0f
                ? 0f
                : Mathf.Abs(SideLiftForce) / weight * 100f;
        }

        private void UpdateHeading(Vector3 forward)
        {
            Vector3 horizontalForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (horizontalForward.sqrMagnitude < 0.001f)
            {
                TurnRate = 0f;
                return;
            }

            horizontalForward.Normalize();
            Heading = Mathf.Repeat(
                Mathf.Atan2(horizontalForward.x, horizontalForward.z) * Mathf.Rad2Deg,
                360f);
            if (!headingInitialized)
            {
                previousHeading = Heading;
                headingInitialized = true;
                TurnRate = 0f;
                return;
            }

            TurnRate = Mathf.DeltaAngle(previousHeading, Heading) / Time.fixedDeltaTime;
            previousHeading = Heading;
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

            DragForce = aerodynamicDrag * dragMultiplier * Airspeed * Airspeed;
            body.AddForce(-velocity.normalized * DragForce, ForceMode.Force);
        }

        private void ApplyPitch(Vector3 pitchAxis)
        {
            float normalizedSpeed = takeoffSpeed <= 0f ? 0f : Airspeed / takeoffSpeed;
            float speedAuthority = Mathf.Max(0f, elevatorAuthorityCurve.Evaluate(normalizedSpeed));
            bool pitchAllowed = State != AircraftState.GroundRoll;
            float pitchRate = Vector3.Dot(body.angularVelocity, pitchAxis);
            float commandMoment = pitchAllowed
                ? smoothedElevator *
                  elevatorAuthority *
                  speedAuthority *
                  (stall == null ? 1f : stall.ControlAuthorityMultiplier)
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

                // On landing, keep pitch free until the nose wheel can settle. GroundRoll
                // freezes pitch, so entering it while the mains alone are supporting the
                // aircraft would otherwise lock the nose at the touchdown attitude.
                if (State == AircraftState.Rotating &&
                    IsPhysicallyGrounded &&
                    PitchAngle > noseSettledPitchAngle)
                {
                    float lowSpeedBlend = 1f - Mathf.SmoothStep(
                        0f, 1f, Mathf.InverseLerp(rotationSpeed * 0.35f, rotationSpeed, Airspeed));
                    float settlingMoment = Mathf.Clamp(
                        -PitchAngle * groundNoseSettlingStrength,
                        -maxGroundNoseSettlingMoment,
                        0f);
                    // At taxi speed there is not enough airflow for full elevator
                    // deflection to balance the aircraft on its main wheels. Do not
                    // let held W/S cancel the mechanical nose-settling tendency.
                    limitMoment += settlingMoment * lowSpeedBlend;
                }
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

            float power = engine != null &&
                          engine.State == EngineController.EngineState.Running &&
                          throttle != null
                ? throttle.Throttle
                : 0f;
            float powerNeutralAoA = power < 0.45f
                ? Mathf.Lerp(neutralAoA, cruiseNeutralAoA, power / 0.45f)
                : Mathf.Lerp(
                    cruiseNeutralAoA,
                    climbNeutralAoA,
                    Mathf.InverseLerp(0.45f, 1f, power));

            float safeSpeedBlend = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(takeoffSpeed * 0.65f, takeoffSpeed, Airspeed));
            float desiredVerticalSpeed;
            if (power <= levelFlightThrottle)
            {
                float levelProgress = levelFlightThrottle <= 0f
                    ? 1f
                    : Mathf.Clamp01(power / levelFlightThrottle);
                desiredVerticalSpeed = Mathf.Lerp(
                    idleTargetVerticalSpeed,
                    0f,
                    Mathf.SmoothStep(0f, 1f, levelProgress));
            }
            else
            {
                float climbProgress = Mathf.InverseLerp(
                    levelFlightThrottle,
                    1f,
                    power);
                desiredVerticalSpeed = Mathf.Lerp(
                    0f,
                    targetNeutralVerticalSpeed,
                    Mathf.SmoothStep(0f, 1f, climbProgress));
            }
            float verticalSpeedAoACorrection = Mathf.Clamp(
                (desiredVerticalSpeed - VerticalSpeed) * verticalSpeedStability,
                -maxVerticalSpeedAoACorrection,
                maxVerticalSpeedAoACorrection) * safeSpeedBlend;
            DynamicNeutralAoA = powerNeutralAoA + verticalSpeedAoACorrection;

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

        private void ApplyAirborneRollAndYaw(Vector3 forward)
        {
            Vector3 levelUp = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
            Vector3 aircraftUp = Vector3.ProjectOnPlane(transform.up, forward).normalized;
            BankAngle = levelUp.sqrMagnitude > 0.001f && aircraftUp.sqrMagnitude > 0.001f
                ? Vector3.SignedAngle(levelUp, aircraftUp, forward)
                : 0f;
            RollRate = Vector3.Dot(body.angularVelocity, forward);

            float normalizedSpeed = takeoffSpeed <= 0f ? 0f : Airspeed / takeoffSpeed;
            float speedAuthority = Mathf.Max(
                0f, rollAuthorityCurve.Evaluate(normalizedSpeed));

            // Positive steering input means right roll. With local +Z forward,
            // right roll is negative torque around the forward axis.
            float commandMoment =
                -smoothedAileron *
                aileronAuthority *
                speedAuthority *
                (stall == null ? 1f : stall.ControlAuthorityMultiplier);
            float dampingMoment = -RollRate * rollDamping;
            float levelMoment = -BankAngle * bankStability;
            float bankLimitMoment = 0f;
            float bankExcess = Mathf.Abs(BankAngle) - maximumBankAngle;
            if (bankExcess > 0f)
                bankLimitMoment =
                    -Mathf.Sign(BankAngle) * bankExcess * bankLimitStrength;

            RollMoment =
                commandMoment + dampingMoment + levelMoment + bankLimitMoment;
            body.AddTorque(forward * RollMoment, ForceMode.Acceleration);

            float bankRadians = BankAngle * Mathf.Deg2Rad;
            float turnSpeed = Mathf.Max(Airspeed, takeoffSpeed * 0.5f);
            float desiredYawRate =
                -Physics.gravity.magnitude * Mathf.Tan(bankRadians) /
                turnSpeed * bankTurnStrength;
            float maxYawRateRadians = maxAssistedYawRate * Mathf.Deg2Rad;
            desiredYawRate = Mathf.Clamp(
                desiredYawRate,
                -maxYawRateRadians,
                maxYawRateRadians);
            float yawRate = Vector3.Dot(body.angularVelocity, Vector3.up);
            YawRate = yawRate;
            YawAssistMoment =
                (desiredYawRate - yawRate) * coordinatedTurnStrength * speedAuthority;
            YawDampingMoment = -yawRate * yawDamping;
            CoordinatedYawMoment = YawAssistMoment + YawDampingMoment;
            body.AddTorque(
                transform.up * CoordinatedYawMoment,
                ForceMode.Acceleration);

            float slipSpeedBlend = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(takeoffSpeed * 0.45f, takeoffSpeed, Airspeed));
            float lateralAcceleration = Mathf.Clamp(
                -LateralVelocity * lateralSlipDamping * slipSpeedBlend,
                -maxLateralSlipAcceleration,
                maxLateralSlipAcceleration);
            LateralSlipDampingForce = lateralAcceleration * body.mass;
            body.AddForce(
                transform.right * lateralAcceleration,
                ForceMode.Acceleration);
        }

        private void UpdateState()
        {
            if (landingTransitionActive &&
                IsPhysicallyGrounded &&
                State == AircraftState.GroundRoll)
            {
                State = AircraftState.Rotating;
            }

            if (landingRollActive && IsPhysicallyGrounded)
            {
                if (Airspeed >= rotationSpeed * 0.9f)
                {
                    // Permit a touch-and-go or another takeoff after rollout.
                    // Rotation remains pilot-controlled; this only releases the
                    // landing-roll pitch constraint at the normal rotation speed.
                    landingRollActive = false;
                    State = AircraftState.Rotating;
                }
                else
                {
                    State = AircraftState.GroundRoll;
                    groundedTimer = liftoffConfirmationTime;
                    ungroundedTimer = 0f;
                    return;
                }
            }

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
                    if (IsPhysicallyGrounded &&
                        PitchAngle > noseSettledPitchAngle + 0.5f)
                    {
                        State = AircraftState.Rotating;
                    }
                    else if (Airspeed >= rotationSpeed * 0.9f)
                        State = AircraftState.Rotating;
                    break;

                case AircraftState.Rotating:
                    if (!IsPhysicallyGrounded &&
                        ungroundedTimer >= liftoffConfirmationTime &&
                        VerticalSpeed > 0f)
                    {
                        State = AircraftState.Airborne;
                    }
                    else if (!landingTransitionActive &&
                             IsPhysicallyGrounded &&
                             Airspeed < rotationSpeed * 0.72f &&
                             PitchAngle <= noseSettledPitchAngle)
                    {
                        State = AircraftState.GroundRoll;
                    }
                    break;

                case AircraftState.Airborne:
                    if (IsPhysicallyGrounded && groundedTimer >= liftoffConfirmationTime)
                        State = Airspeed >= rotationSpeed * 0.72f ||
                                PitchAngle > noseSettledPitchAngle
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
            if (liftMultiplier <= 0f) liftMultiplier = 1.04f;
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
            if (cruiseNeutralAoA <= 0f) cruiseNeutralAoA = 1.2f;
            if (climbNeutralAoA <= 0f) climbNeutralAoA = 2f;
            if (aoADeadband <= 0f) aoADeadband = 0.5f;
            if (maxStabilityMoment <= 0f) maxStabilityMoment = 0.45f;
            if (virtualTailArm <= 0f) virtualTailArm = 2.7f;
            if (tailStability <= 0f) tailStability = 0.42f;
            if (tailPitchRateInfluence <= 0f) tailPitchRateInfluence = 0.32f;
            if (maxTailForce <= 0f) maxTailForce = 1700f;
            if (verticalSpeedStability <= 0f) verticalSpeedStability = 0.38f;
            if (idleTargetVerticalSpeed >= 0f) idleTargetVerticalSpeed = -2.5f;
            if (levelFlightThrottle <= 0f) levelFlightThrottle = 0.52f;
            if (maxVerticalSpeedAoACorrection <= 0f)
                maxVerticalSpeedAoACorrection = 3.5f;
            if (dragMultiplier <= 0f) dragMultiplier = 1f;
            if (airborneThrustMultiplier <= 0f) airborneThrustMultiplier = 1f;
            if (aileronAuthority <= 0f) aileronAuthority = 2.2f;
            if (rollResponse <= 0f) rollResponse = 3f;
            if (aileronReturnSpeed <= 0f) aileronReturnSpeed = 4f;
            if (rollAuthorityCurve == null || rollAuthorityCurve.length == 0)
            {
                rollAuthorityCurve = new AnimationCurve(
                    new Keyframe(0f, 0.02f),
                    new Keyframe(0.6f, 0.22f),
                    new Keyframe(0.9f, 0.75f),
                    new Keyframe(1f, 1f),
                    new Keyframe(1.6f, 1.15f));
            }
            if (rollDamping <= 0f) rollDamping = 2.4f;
            if (maximumBankAngle <= 0f) maximumBankAngle = 70f;
            if (bankLimitStrength <= 0f) bankLimitStrength = 0.12f;
            if (lateralSlipDamping <= 0f) lateralSlipDamping = 0.7f;
            if (coordinatedTurnStrength <= 0f) coordinatedTurnStrength = 3f;
            if (bankTurnStrength <= 0f) bankTurnStrength = 1f;
            if (maxAssistedYawRate <= 0f) maxAssistedYawRate = 18f;
            if (yawDamping <= 0f) yawDamping = 0.7f;
            if (maxLateralSlipAcceleration <= 0f)
                maxLateralSlipAcceleration = 14f;
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
            AileronInput = 0f;
            BankAngle = 0f;
            RollRate = 0f;
            RollMoment = 0f;
            CoordinatedYawMoment = 0f;
            Heading = 0f;
            TurnRate = 0f;
            YawRate = 0f;
            YawAssistMoment = 0f;
            YawDampingMoment = 0f;
            VerticalLiftForce = 0f;
            VerticalLiftPercentOfWeight = 0f;
            SideLiftForce = 0f;
            BankTurnForcePercent = 0f;
            LateralSlipDampingForce = 0f;
            LateralVelocity = 0f;
            ForwardVelocity = 0f;
            HorizontalGroundSpeed = 0f;
            smoothedElevator = 0f;
            smoothedAileron = 0f;
            ungroundedTimer = 0f;
            groundedTimer = 0f;
            IsPhysicallyGrounded = true;
            headingInitialized = false;
            landingLiftMultiplier = 1f;
            landingRollActive = false;
            landingTransitionActive = false;
            ApplyStateConstraints();
        }

        public void SetAirborneForTest()
        {
            State = AircraftState.Airborne;
            landingRollActive = false;
            landingTransitionActive = false;
            IsPhysicallyGrounded = false;
            groundedTimer = 0f;
            ungroundedTimer = liftoffConfirmationTime;
            headingInitialized = false;
            ApplyStateConstraints();
        }

        public void SetLandingLiftMultiplier(float multiplier)
        {
            landingLiftMultiplier = Mathf.Max(0f, multiplier);
        }

        public void EnterLandingRoll()
        {
            State = AircraftState.GroundRoll;
            landingRollActive = true;
            landingTransitionActive = false;
            groundedTimer = liftoffConfirmationTime;
            ungroundedTimer = 0f;
            smoothedAileron = 0f;
            ApplyStateConstraints();
        }

        public void SetLandingTransitionActive(bool active)
        {
            landingTransitionActive = active;
            if (active)
            {
                landingRollActive = false;
                if (State == AircraftState.GroundRoll)
                    State = AircraftState.Rotating;
                ApplyStateConstraints();
            }
        }
    }
}
