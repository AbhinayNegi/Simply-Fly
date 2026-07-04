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

        private void Awake()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (brakeStrength <= 0f) brakeStrength = 2800f;
            if (brakeResponseSpeed <= 0f) brakeResponseSpeed = 1.5f;
        }

        private void Update()
        {
            float target = input != null && input.BrakeHeld ? 1f : 0f;
            BrakeAmount = Mathf.MoveTowards(
                BrakeAmount, target, brakeResponseSpeed * Time.deltaTime);
        }

        public void ResetBrakes()
        {
            BrakeAmount = 0f;
        }
    }
}
