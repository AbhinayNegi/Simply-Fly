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
        [SerializeField] private PropellerController propeller;
        [SerializeField] private CameraController followCamera;

        [Header("Reset")]
        [SerializeField] private Key resetKey = Key.Backspace;
        [SerializeField] private bool stopEngineOnReset = true;

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
            if (propeller == null) propeller = GetComponent<PropellerController>();
            if (followCamera == null) followCamera = FindFirstObjectByType<CameraController>();

            spawnPosition = body.position;
            spawnRotation = body.rotation;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[resetKey].wasPressedThisFrame)
                ResetAircraft();
        }

        public void ResetAircraft()
        {
            if (stopEngineOnReset && engine != null) engine.ResetEngine();
            if (throttle != null) throttle.ResetThrottle();
            if (brakes != null) brakes.ResetBrakes();
            if (steering != null) steering.ResetSteering();
            if (groundMovement != null) groundMovement.ResetGroundMovement();
            if (flightPhysics != null) flightPhysics.ResetFlight();
            if (propeller != null) propeller.ResetPropeller();

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = spawnPosition;
            body.rotation = spawnRotation;
            body.WakeUp();

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            Physics.SyncTransforms();
            if (followCamera != null) followCamera.SnapToTarget();
        }
    }
}
