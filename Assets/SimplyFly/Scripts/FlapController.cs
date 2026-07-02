using UnityEngine;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FlapController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AircraftInput input;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private Vector3 localPitchAxis = Vector3.left;

        [Header("Positions")]
        [SerializeField] private float[] flapPositions = { 0f, 10f, 20f, 30f };
        [Min(0.1f)] [SerializeField] private float flapDeploySpeed = 10f;
        [Min(0.1f)] [SerializeField] private float flapRetractSpeed = 14f;

        [Header("Aerodynamic effects")]
        [Min(0f)] [SerializeField] private float flapLiftBonusPerStep = 0.08f;
        [Min(0f)] [SerializeField] private float maxFlapLiftBonus = 0.24f;
        [Min(0f)] [SerializeField] private float flapDragPerStep = 0.55f;
        [Min(0f)] [SerializeField] private float maxFlapDrag = 1.65f;
        [Tooltip("Maximum stall-speed reduction at full flap, in knots.")]
        [Min(0f)] [SerializeField] private float flapStallSpeedReduction = 7f;
        [Tooltip("Transient pitch moment while the flaps are moving.")]
        [SerializeField] private float flapPitchMoment = -0.06f;

        [Header("Safety")]
        [Tooltip("Knots.")]
        [Min(1f)] [SerializeField] private float maxFlapExtensionSpeed = 100f;

        public int SelectedIndex { get; private set; }
        public float SelectedAngle =>
            flapPositions == null || flapPositions.Length == 0
                ? 0f
                : flapPositions[Mathf.Clamp(SelectedIndex, 0, flapPositions.Length - 1)];
        public float ActualAngle { get; private set; }
        public float LiftBonus { get; private set; }
        public float DragBonus { get; private set; }
        public float StallSpeedReduction { get; private set; }
        public bool SpeedWarning { get; private set; }
        public float MaxExtensionSpeed => maxFlapExtensionSpeed;

        private Rigidbody body;
        private float previousAngle;
        private float transientPitchInput;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (input == null) input = GetComponent<AircraftInput>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            RepairPositions();
        }

        private void Update()
        {
            if (input != null && input.FlapsExtendPressed)
                SelectedIndex = Mathf.Min(SelectedIndex + 1, flapPositions.Length - 1);
            if (input != null && input.FlapsRetractPressed)
                SelectedIndex = Mathf.Max(SelectedIndex - 1, 0);

            float speed = SelectedAngle > ActualAngle
                ? flapDeploySpeed
                : flapRetractSpeed;
            previousAngle = ActualAngle;
            ActualAngle = Mathf.MoveTowards(
                ActualAngle,
                SelectedAngle,
                speed * Time.deltaTime);
            UpdateEffects();
            float movementRate = Time.deltaTime <= 0f
                ? 0f
                : (ActualAngle - previousAngle) / Time.deltaTime;
            transientPitchInput = Mathf.Clamp(movementRate / 10f, -1f, 1f);
        }

        private void UpdateEffects()
        {
            float stepAngle = flapPositions.Length > 1
                ? Mathf.Max(0.1f, flapPositions[1] - flapPositions[0])
                : 10f;
            float deployedSteps = ActualAngle / stepAngle;
            LiftBonus = Mathf.Min(
                maxFlapLiftBonus,
                deployedSteps * flapLiftBonusPerStep);
            DragBonus = Mathf.Min(
                maxFlapDrag,
                deployedSteps * flapDragPerStep);
            float maximumAngle = flapPositions.Length == 0
                ? 30f
                : Mathf.Max(0.1f, flapPositions[flapPositions.Length - 1]);
            StallSpeedReduction =
                flapStallSpeedReduction * Mathf.Clamp01(ActualAngle / maximumAngle);
            SpeedWarning =
                ActualAngle > 0.1f &&
                flightPhysics != null &&
                flightPhysics.AirspeedKnots > maxFlapExtensionSpeed;
        }

        private void FixedUpdate()
        {
            if (flightPhysics == null ||
                flightPhysics.State != FlightPhysicsController.AircraftState.Airborne)
                return;

            if (Mathf.Abs(transientPitchInput) < 0.001f) return;

            Vector3 pitchAxis =
                transform.TransformDirection(localPitchAxis.normalized);
            body.AddTorque(
                pitchAxis * flapPitchMoment * transientPitchInput,
                ForceMode.Acceleration);
        }

        private void RepairPositions()
        {
            if (flapPositions == null || flapPositions.Length < 2)
                flapPositions = new[] { 0f, 10f, 20f, 30f };
            SelectedIndex = Mathf.Clamp(SelectedIndex, 0, flapPositions.Length - 1);
        }

        public void ResetFlaps()
        {
            SelectedIndex = 0;
            ActualAngle = 0f;
            previousAngle = 0f;
            LiftBonus = 0f;
            DragBonus = 0f;
            StallSpeedReduction = 0f;
            SpeedWarning = false;
            transientPitchInput = 0f;
        }
    }
}
