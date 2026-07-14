using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace SimplyFly.GroundOps
{
    public sealed class BrakeController : MonoBehaviour
    {
        [SerializeField] private AircraftInput input;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [FormerlySerializedAs("brakingEffectiveness")]
        [Tooltip("Maximum braking force in Newtons.")]
        [Min(0f)] [SerializeField] private float brakeStrength = 2800f;
        [FormerlySerializedAs("brakeBuildSpeed")]
        [Tooltip("How quickly brake pressure builds and releases.")]
        [Min(0.01f)] [SerializeField] private float brakeResponseSpeed = 1.5f;

        public float BrakeAmount { get; private set; }
        public bool IsBraking => BrakeAmount > 0.05f;
        public float BrakeForce => brakeStrength * BrakeAmount;
        public bool ParkingBrakeEnabled { get; private set; }
        public bool BrakeInputActive { get; private set; }
        public bool ParkingBrakeMoveWarning =>
            ParkingBrakeEnabled && throttle != null && throttle.Throttle > 0.05f;

        private void Awake()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (brakeStrength <= 0f) brakeStrength = 2800f;
            if (brakeResponseSpeed <= 0f) brakeResponseSpeed = 1.5f;
        }

        private IEnumerator Start()
        {
            // Allow grounding checks to settle before deciding whether an
            // initial scene/hangar spawn needs its parking brake set.
            yield return new WaitForFixedUpdate();
            if (flightPhysics != null && flightPhysics.IsPhysicallyGrounded)
                SetParkingBrake(true);
        }

        private void Update()
        {
            bool brakeHeld = input != null && input.BrakeHeld;
            BrakeInputActive = brakeHeld;
            if (input != null && input.ParkingBrakeTogglePressed)
                ParkingBrakeEnabled = !ParkingBrakeEnabled;
            float target = ParkingBrakeEnabled || brakeHeld ? 1f : 0f;
            BrakeAmount = Mathf.MoveTowards(
                BrakeAmount, target, brakeResponseSpeed * Time.deltaTime);
        }

        public void ResetBrakes()
        {
            BrakeAmount = 0f;
            ParkingBrakeEnabled = false;
            BrakeInputActive = false;
        }

        public void SetParkingBrake(bool enabled)
        {
            ParkingBrakeEnabled = enabled;
            BrakeAmount = enabled ? 1f : 0f;
        }
    }
}
