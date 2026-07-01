using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class EngineController : MonoBehaviour
    {
        public enum EngineState { Off, Starting, Running }

        [SerializeField] private AircraftInput input;
        [SerializeField] private ThrottleController throttle;

        [Header("RPM")]
        [Min(0f)] [SerializeField] private float offRPM = 25f;
        [Min(1f)] [SerializeField] private float idleRPM = 750f;
        [Min(1f)] [SerializeField] private float maximumRPM = 2700f;
        [Tooltip("RPM gained per second.")]
        [Min(1f)] [SerializeField] private float engineAccelerationRate = 900f;
        [Tooltip("RPM lost per second.")]
        [Min(1f)] [SerializeField] private float engineDecelerationRate = 700f;
        [Tooltip("Additional smoothing applied while RPM follows throttle.")]
        [Min(0.01f)] [SerializeField] private float rpmResponseSpeed = 5f;
        [Min(0.1f)] [SerializeField] private float starterDuration = 1.6f;

        public EngineState State { get; private set; } = EngineState.Off;
        public float CurrentRPM { get; private set; }
        public float NormalizedRPM => maximumRPM <= 0f ? 0f : Mathf.Clamp01(CurrentRPM / maximumRPM);

        private float starterTimer;

        private void Awake()
        {
            if (input == null) input = GetComponent<AircraftInput>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            CurrentRPM = offRPM;
        }

        private void Update()
        {
            if (input != null && input.StartEnginePressed && State == EngineState.Off)
            {
                State = EngineState.Starting;
                starterTimer = 0f;
            }

            float targetRPM = offRPM;
            if (State == EngineState.Starting)
            {
                starterTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(starterTimer / starterDuration);
                targetRPM = Mathf.Lerp(offRPM, idleRPM, Mathf.SmoothStep(0f, 1f, progress));
                if (starterTimer >= starterDuration) State = EngineState.Running;
            }
            else if (State == EngineState.Running)
            {
                float throttleValue = throttle == null ? 0f : throttle.Throttle;
                targetRPM = Mathf.Lerp(idleRPM, maximumRPM, throttleValue);
            }

            float rate = targetRPM >= CurrentRPM ? engineAccelerationRate : engineDecelerationRate;
            float smoothedRate = rate * Mathf.Clamp(rpmResponseSpeed, 0.01f, 20f) / 5f;
            CurrentRPM = Mathf.MoveTowards(CurrentRPM, targetRPM, smoothedRate * Time.deltaTime);
        }
    }
}
