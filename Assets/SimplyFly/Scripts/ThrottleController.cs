using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class ThrottleController : MonoBehaviour
    {
        [SerializeField] private AircraftInput input;
        [Tooltip("Fraction of the full throttle range moved per second.")]
        [Min(0.01f)] [SerializeField] private float throttleResponseSpeed = 0.35f;
        [Range(0f, 1f)] [SerializeField] private float initialThrottle;

        public float Throttle { get; private set; }

        private void Awake()
        {
            Throttle = initialThrottle;
            if (input == null) input = GetComponent<AircraftInput>();
        }

        private void Update()
        {
            if (input == null) return;
            Throttle = Mathf.Clamp01(Throttle + input.ThrottleInput * throttleResponseSpeed * Time.deltaTime);
        }

        public void ResetThrottle()
        {
            Throttle = 0f;
        }

        public void SetThrottleForTest(float value)
        {
            Throttle = Mathf.Clamp01(value);
        }
    }
}
