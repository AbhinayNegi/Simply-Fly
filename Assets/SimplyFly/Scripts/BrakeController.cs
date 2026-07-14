using UnityEngine;
using UnityEngine.Serialization;

namespace SimplyFly.GroundOps
{
    public sealed class BrakeController : MonoBehaviour
    {
        [SerializeField] private AircraftInput input;
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
        private bool brakeWasHeld;

        private void Awake()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (brakeStrength <= 0f) brakeStrength = 2800f;
            if (brakeResponseSpeed <= 0f) brakeResponseSpeed = 1.5f;
        }

        private void Update()
        {
            bool brakeHeld = input != null && input.BrakeHeld;
            BrakeInputActive = brakeHeld;
            if (ParkingBrakeEnabled && brakeHeld && !brakeWasHeld)
                ParkingBrakeEnabled = false;
            float target = ParkingBrakeEnabled || brakeHeld ? 1f : 0f;
            BrakeAmount = Mathf.MoveTowards(
                BrakeAmount, target, brakeResponseSpeed * Time.deltaTime);
            brakeWasHeld = brakeHeld;
        }

        public void ResetBrakes()
        {
            BrakeAmount = 0f;
            ParkingBrakeEnabled = false;
            BrakeInputActive = false;
            brakeWasHeld = input != null && input.BrakeHeld;
        }

        public void SetParkingBrake(bool enabled)
        {
            ParkingBrakeEnabled = enabled;
            BrakeAmount = enabled ? 1f : 0f;
            brakeWasHeld = input != null && input.BrakeHeld;
        }
    }
}
