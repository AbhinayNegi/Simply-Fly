using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AircraftResetController : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private EngineController engine;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private BrakeController brakes;
        [SerializeField] private SteeringController steering;
        [SerializeField] private GroundMovementController groundMovement;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private LandingController landing;
        [SerializeField] private StallController stall;
        [SerializeField] private PropellerController propeller;
        [SerializeField] private CameraController followCamera;

        [Header("Reset")]
        [SerializeField] private Key resetKey = Key.Backspace;
        [SerializeField] private bool stopEngineOnReset = true;
        [Header("Development spawn points")]
        [SerializeField] private Vector3 airborneTestPosition = new Vector3(-400f, 300f, -900f);
        [SerializeField] private Vector3 airborneTestEuler = new Vector3(0f, 45f, 0f);
        [Min(10f)] [SerializeField] private float airborneTestSpeed = 42f;
        [Range(0f, 1f)] [SerializeField] private float airborneTestThrottle = 0.72f;
        [SerializeField] private Vector3 finalApproachPosition = new Vector3(-2600f, 95f, 0f);
        [SerializeField] private Vector3 finalApproachEuler = new Vector3(0f, 90f, 0f);
        [Min(10f)] [SerializeField] private float finalApproachSpeed = 33f;
        [SerializeField] private float finalApproachVerticalSpeed = -1.5f;
        [Range(0f, 1f)] [SerializeField] private float finalApproachThrottle = 0.45f;

        private Rigidbody body;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (engine == null) engine = GetComponent<EngineController>();
            if (throttle == null) throttle = GetComponent<ThrottleController>();
            if (brakes == null) brakes = GetComponent<BrakeController>();
            if (steering == null) steering = GetComponent<SteeringController>();
            if (groundMovement == null) groundMovement = GetComponent<GroundMovementController>();
            if (flightPhysics == null) flightPhysics = GetComponent<FlightPhysicsController>();
            if (landing == null) landing = GetComponent<LandingController>();
            if (stall == null) stall = GetComponent<StallController>();
            if (propeller == null) propeller = GetComponent<PropellerController>();
            if (followCamera == null) followCamera = FindAnyObjectByType<CameraController>();

            spawnPosition = body.position;
            spawnRotation = body.rotation;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame ||
                keyboard[resetKey].wasPressedThisFrame)
                ResetAircraft();
            else if (keyboard.f2Key.wasPressedThisFrame)
                SpawnAirborneTest();
            else if (keyboard.f3Key.wasPressedThisFrame)
                SpawnFinalApproach();
        }

        public void ResetAircraft()
        {
            ResetSystems(stopEngineOnReset);
            PlaceAircraft(
                spawnPosition,
                spawnRotation,
                Vector3.zero,
                false,
                0f);
        }

        public void SpawnAirborneTest()
        {
            Quaternion rotation = Quaternion.Euler(airborneTestEuler);
            Vector3 velocity = rotation * Vector3.forward * airborneTestSpeed;
            ResetSystems(true);
            PlaceAircraft(
                airborneTestPosition,
                rotation,
                velocity,
                true,
                airborneTestThrottle);
        }

        public void SpawnFinalApproach()
        {
            Quaternion rotation = Quaternion.Euler(finalApproachEuler);
            Vector3 velocity =
                rotation * Vector3.forward * finalApproachSpeed +
                Vector3.up * finalApproachVerticalSpeed;
            ResetSystems(true);
            PlaceAircraft(
                finalApproachPosition,
                rotation,
                velocity,
                true,
                finalApproachThrottle);
        }

        private void ResetSystems(bool resetEngine)
        {
            if (resetEngine && engine != null) engine.ResetEngine();
            if (throttle != null) throttle.ResetThrottle();
            if (brakes != null) brakes.ResetBrakes();
            if (steering != null) steering.ResetSteering();
            if (groundMovement != null) groundMovement.ResetGroundMovement();
            if (flightPhysics != null) flightPhysics.ResetFlight();
            if (landing != null) landing.ResetLanding();
            if (stall != null) stall.ResetStall();
            if (propeller != null) propeller.ResetPropeller();
        }

        private void PlaceAircraft(
            Vector3 position,
            Quaternion rotation,
            Vector3 velocity,
            bool airborne,
            float throttleValue)
        {
            if (airborne)
            {
                throttle?.SetThrottleForTest(throttleValue);
                engine?.SetRunningForTest(throttleValue);
                flightPhysics?.SetAirborneForTest();
                landing?.ResetLanding(true);
            }

            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            body.WakeUp();

            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            if (followCamera != null) followCamera.SnapToTarget();
        }
    }
}
