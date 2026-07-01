using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class BrakeController : MonoBehaviour
    {
        [SerializeField] private AircraftInput input;
        [Min(0f)] [SerializeField] private float brakingEffectiveness = 2600f;
        [Min(0.01f)] [SerializeField] private float brakeBuildSpeed = 2.5f;
        [Min(0.01f)] [SerializeField] private float brakeReleaseSpeed = 4f;

        public float BrakeAmount { get; private set; }
        public bool IsBraking => BrakeAmount > 0.05f;
        public float BrakeTorque => brakingEffectiveness * BrakeAmount;

        private void Awake()
        {
            if (input == null) input = GetComponent<AircraftInput>();
        }

        private void Update()
        {
            float target = input != null && input.BrakeHeld ? 1f : 0f;
            float speed = target > BrakeAmount ? brakeBuildSpeed : brakeReleaseSpeed;
            BrakeAmount = Mathf.MoveTowards(BrakeAmount, target, speed * Time.deltaTime);
        }
    }
}
