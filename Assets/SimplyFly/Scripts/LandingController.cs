using UnityEngine;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LandingController : MonoBehaviour
    {
        public enum LandingPhase
        {
            Airborne,
            Flare,
            MainWheelTouchdown,
            NoseWheelLowering,
            LandingRoll
        }

        public enum LandingQuality
        {
            None,
            Smooth,
            Firm,
            Hard,
            VeryHard
        }

        [Header("References")]
        [SerializeField] private AircraftInput input;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private ApproachGuidanceController approachGuidance;
        [SerializeField] private WheelCollider leftMainWheel;
        [SerializeField] private WheelCollider rightMainWheel;
        [SerializeField] private WheelCollider noseWheel;
        [SerializeField] private Vector3 localPitchAxis = Vector3.left;

        [Header("Flare")]
        [Min(0f)] [SerializeField] private float flareAssistStrength = 0.15f;
        [Min(0.5f)] [SerializeField] private float flareHeight = 8f;
        [Range(1f, 1.3f)] [SerializeField] private float flareLiftMultiplier = 1.08f;
        [Range(3f, 20f)] [SerializeField] private float maxFlarePitchAngle = 11f;

        [Header("Touchdown quality")]
        [Min(0.1f)] [SerializeField] private float smoothThreshold = 1.5f;
        [Min(0.1f)] [SerializeField] private float firmThreshold = 3f;
        [Min(0.1f)] [SerializeField] private float hardThreshold = 5f;

        [Header("Bounce")]
        [Min(0f)] [SerializeField] private float bounceStrength = 0.28f;
        [Min(0f)] [SerializeField] private float touchdownDamping = 3f;
        [Min(0f)] [SerializeField] private float maxBounceVelocity = 2.2f;

        [Header("Nose lowering")]
        [Min(0f)] [SerializeField] private float noseLoweringDamping = 0.11f;
        [Min(0f)] [SerializeField] private float noseWheelTouchDelay = 0.35f;
        [Min(0f)] [SerializeField] private float postTouchdownPitchDamping = 3.5f;
        [Tooltip("Small nose-down allowance used only until the nose wheel contacts.")]
        [Range(-4f, 0f)] [SerializeField] private float noseLoweringPitchTarget = -1f;
        [Min(0.01f)] [SerializeField] private float wheelContactTolerance = 0.12f;

        public LandingPhase Phase { get; private set; } = LandingPhase.LandingRoll;
        public bool FlareActive => Phase == LandingPhase.Flare;
        public bool LeftMainTouching { get; private set; }
        public bool RightMainTouching { get; private set; }
        public bool MainWheelsTouching => LeftMainTouching || RightMainTouching;
        public bool BothMainWheelsTouching => LeftMainTouching && RightMainTouching;
        public bool NoseWheelTouching { get; private set; }
        public bool AllWheelsTouching =>
            LeftMainTouching && RightMainTouching && NoseWheelTouching;
        public bool Airborne => !MainWheelsTouching && !NoseWheelTouching;
        public float HeightAboveRunway =>
            approachGuidance == null ? transform.position.y : approachGuidance.AltitudeAboveRunway;
        public float TouchdownVerticalSpeed { get; private set; }
        public float TouchdownAirspeed { get; private set; }
        public float TouchdownPitch { get; private set; }
        public float TouchdownBank { get; private set; }
        public float TouchdownCenterlineOffset { get; private set; }
        public bool MainWheelsTouchedFirst { get; private set; }
        public bool NoseWheelFirstWarning { get; private set; }
        public LandingQuality Quality { get; private set; }
        public float BounceVelocity { get; private set; }
        public float LandingRollSpeed =>
            flightPhysics == null ? 0f : flightPhysics.HorizontalGroundSpeed;

        private Rigidbody body;
        private float previousVerticalSpeed;
        private float mainTouchdownTimer;
        private bool touchdownRecorded;
        private bool noseContactSeen;
        private bool landingArmed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (input == null) input = GetComponent<AircraftInput>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (approachGuidance == null)
                approachGuidance = GetComponent<ApproachGuidanceController>();
        }

        private void FixedUpdate()
        {
            float verticalSpeed = Vector3.Dot(body.linearVelocity, Vector3.up);
            UpdateWheelContacts();

            if (flightPhysics != null &&
                flightPhysics.State == FlightPhysicsController.AircraftState.Airborne &&
                Airborne)
            {
                if (!landingArmed)
                {
                    landingArmed = true;
                    touchdownRecorded = false;
                    noseContactSeen = false;
                    Quality = LandingQuality.None;
                    TouchdownVerticalSpeed = 0f;
                    TouchdownAirspeed = 0f;
                    TouchdownPitch = 0f;
                    TouchdownBank = 0f;
                    TouchdownCenterlineOffset = 0f;
                    NoseWheelFirstWarning = false;
                    MainWheelsTouchedFirst = false;
                }
                if (Phase == LandingPhase.LandingRoll)
                    Phase = LandingPhase.Airborne;
            }

            if (landingArmed)
                UpdateLandingPhase(verticalSpeed);
            ApplyFlareAssist();
            ApplyTouchdownTransition(verticalSpeed);

            if (landingArmed && !touchdownRecorded && Airborne)
                previousVerticalSpeed = verticalSpeed;
        }

        private void UpdateWheelContacts()
        {
            LeftMainTouching = HasWheelContact(leftMainWheel);
            RightMainTouching = HasWheelContact(rightMainWheel);
            NoseWheelTouching = HasWheelContact(noseWheel);
        }

        private bool HasWheelContact(WheelCollider wheel)
        {
            if (wheel == null) return false;
            SphereCollider support = wheel.GetComponent<SphereCollider>();
            Vector3 center = support != null
                ? support.transform.TransformPoint(support.center)
                : wheel.transform.position;
            float radius = support != null
                ? support.radius * Mathf.Max(
                    Mathf.Abs(support.transform.lossyScale.x),
                    Mathf.Abs(support.transform.lossyScale.y),
                    Mathf.Abs(support.transform.lossyScale.z))
                : wheel.radius;
            RaycastHit[] hits = Physics.RaycastAll(
                center + Vector3.up * 0.04f,
                Vector3.down,
                radius + wheelContactTolerance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null && hit.collider.attachedRigidbody != body)
                    return true;
            }
            return false;
        }

        private void UpdateLandingPhase(float verticalSpeed)
        {
            if (Phase == LandingPhase.Airborne &&
                Airborne &&
                HeightAboveRunway <= flareHeight &&
                HeightAboveRunway > 0f &&
                verticalSpeed < -0.1f)
            {
                Phase = LandingPhase.Flare;
            }
            else if (Phase == LandingPhase.Flare &&
                     Airborne &&
                     (HeightAboveRunway > flareHeight * 1.25f || verticalSpeed > 1f))
            {
                Phase = LandingPhase.Airborne;
            }

            if (NoseWheelTouching && !MainWheelsTouching && !touchdownRecorded)
            {
                noseContactSeen = true;
                NoseWheelFirstWarning = true;
            }

            if (MainWheelsTouching && !touchdownRecorded)
                RecordMainWheelTouchdown(verticalSpeed);

            if (Phase == LandingPhase.MainWheelTouchdown)
            {
                mainTouchdownTimer += Time.fixedDeltaTime;
                if (NoseWheelTouching)
                    EnterLandingRoll();
                else if (mainTouchdownTimer >= noseWheelTouchDelay)
                    Phase = LandingPhase.NoseWheelLowering;
            }
            else if (Phase == LandingPhase.NoseWheelLowering && NoseWheelTouching)
            {
                EnterLandingRoll();
            }
        }

        private void RecordMainWheelTouchdown(float verticalSpeed)
        {
            touchdownRecorded = true;
            flightPhysics?.SetLandingTransitionActive(true);
            mainTouchdownTimer = 0f;
            Phase = LandingPhase.MainWheelTouchdown;
            TouchdownVerticalSpeed = Mathf.Min(previousVerticalSpeed, verticalSpeed);
            TouchdownAirspeed = flightPhysics == null
                ? body.linearVelocity.magnitude
                : flightPhysics.Airspeed;
            TouchdownPitch = flightPhysics == null ? 0f : flightPhysics.PitchAngle;
            TouchdownBank = flightPhysics == null ? 0f : flightPhysics.BankAngle;
            TouchdownCenterlineOffset = approachGuidance == null
                ? 0f
                : approachGuidance.LateralOffset;
            MainWheelsTouchedFirst = !noseContactSeen && !NoseWheelTouching;
            NoseWheelFirstWarning = !MainWheelsTouchedFirst;

            float impactSpeed = Mathf.Max(0f, -TouchdownVerticalSpeed);
            Quality = impactSpeed <= smoothThreshold
                ? LandingQuality.Smooth
                : impactSpeed <= firmThreshold
                    ? LandingQuality.Firm
                    : impactSpeed <= hardThreshold
                        ? LandingQuality.Hard
                        : LandingQuality.VeryHard;

            float bounceStart = smoothThreshold;
            BounceVelocity = Mathf.Clamp(
                (impactSpeed - bounceStart) * bounceStrength,
                0f,
                maxBounceVelocity);
            if (BounceVelocity > 0.01f)
                body.AddForce(Vector3.up * BounceVelocity, ForceMode.VelocityChange);
        }

        private void ApplyFlareAssist()
        {
            float liftMultiplier = 1f;
            if (FlareActive)
            {
                float heightBlend = 1f - Mathf.Clamp01(HeightAboveRunway / flareHeight);
                liftMultiplier = Mathf.Lerp(1f, flareLiftMultiplier, heightBlend);

                float elevator = input == null ? 0f : Mathf.Max(0f, input.ElevatorInput);
                if (elevator > 0f && flightPhysics != null &&
                    flightPhysics.PitchAngle < maxFlarePitchAngle)
                {
                    Vector3 pitchAxis =
                        transform.TransformDirection(localPitchAxis.normalized);
                    body.AddTorque(
                        pitchAxis * elevator * flareAssistStrength * heightBlend,
                        ForceMode.Acceleration);
                }

                if (flightPhysics != null &&
                    flightPhysics.PitchAngle > maxFlarePitchAngle)
                {
                    Vector3 pitchAxis =
                        transform.TransformDirection(localPitchAxis.normalized);
                    float excessPitch =
                        flightPhysics.PitchAngle - maxFlarePitchAngle;
                    body.AddTorque(
                        pitchAxis * -excessPitch * flareAssistStrength * 2f,
                        ForceMode.Acceleration);
                }
            }

            flightPhysics?.SetLandingLiftMultiplier(liftMultiplier);
        }

        private void ApplyTouchdownTransition(float verticalSpeed)
        {
            bool touchdownTransition =
                Phase == LandingPhase.MainWheelTouchdown ||
                Phase == LandingPhase.NoseWheelLowering;
            if (!touchdownTransition) return;

            Vector3 pitchAxis =
                transform.TransformDirection(localPitchAxis.normalized);
            float pitchRate = Vector3.Dot(body.angularVelocity, pitchAxis);
            float dampingMoment = -pitchRate * postTouchdownPitchDamping;
            float loweringMoment = 0f;
            if (mainTouchdownTimer >= noseWheelTouchDelay &&
                flightPhysics != null &&
                !NoseWheelTouching &&
                flightPhysics.PitchAngle > noseLoweringPitchTarget)
            {
                loweringMoment =
                    -(flightPhysics.PitchAngle - noseLoweringPitchTarget) *
                    noseLoweringDamping;
            }
            body.AddTorque(
                pitchAxis * (dampingMoment + loweringMoment),
                ForceMode.Acceleration);

            if (!MainWheelsTouching && verticalSpeed > 0f)
            {
                body.AddForce(
                    Vector3.down * verticalSpeed * touchdownDamping,
                    ForceMode.Acceleration);
                BounceVelocity = verticalSpeed;
            }
            else if (MainWheelsTouching)
            {
                BounceVelocity = 0f;
            }
        }

        private void EnterLandingRoll()
        {
            Phase = LandingPhase.LandingRoll;
            landingArmed = false;
            flightPhysics?.EnterLandingRoll();
        }

        public void ResetLanding(bool airborne = false)
        {
            Phase = airborne ? LandingPhase.Airborne : LandingPhase.LandingRoll;
            landingArmed = airborne;
            touchdownRecorded = false;
            noseContactSeen = false;
            mainTouchdownTimer = 0f;
            TouchdownVerticalSpeed = 0f;
            TouchdownAirspeed = 0f;
            TouchdownPitch = 0f;
            TouchdownBank = 0f;
            TouchdownCenterlineOffset = 0f;
            MainWheelsTouchedFirst = false;
            NoseWheelFirstWarning = false;
            Quality = LandingQuality.None;
            BounceVelocity = 0f;
            previousVerticalSpeed = 0f;
            flightPhysics?.SetLandingLiftMultiplier(1f);
            flightPhysics?.SetLandingTransitionActive(false);
        }
    }
}
