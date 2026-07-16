using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace SimplyFly.GroundOps
{
    public sealed class CameraController : MonoBehaviour
    {
        [System.Serializable]
        private sealed class MotionMultipliers
        {
            [Range(0f, 2f)] public float engine = 1f;
            [Range(0f, 2f)] public float runway = 1f;
            [Range(0f, 2f)] public float wind = 1f;
            [Range(0f, 2f)] public float landing = 1f;
            [Range(0f, 2f)] public float acceleration = 1f;
            [Range(0f, 2f)] public float turbulence = 1f;
        }

        public enum CameraMode
        {
            Chase,
            CloseChase,
            Cockpit,
            LeftWing,
            RightWing
        }

        public enum CinematicMode
        {
            FlyBy,
            RunwayObserver,
            OrbitShowcase
        }

        [SerializeField] private Transform target;
        [Tooltip("Dedicated point near the upper fuselage/wing root used for all chase orbit framing.")]
        [SerializeField] private Transform focusTarget;
        [SerializeField] private Transform cockpitCameraAnchor;
        [SerializeField] private Transform leftWingCameraAnchor;
        [SerializeField] private Transform rightWingCameraAnchor;
        [SerializeField] private AircraftInput input;
        [SerializeField] private GameFlowController gameFlow;

        [Header("Phase 1 chase camera")]
        [Tooltip("Uses the smooth, roll-reduced Phase 1 chase behaviour. Disable to restore the legacy chase camera.")]
        [SerializeField] private bool usePhase1ChaseCamera = true;
        [Tooltip("How much aircraft bank rotates the camera horizon (0 = level, 1 = full aircraft roll).")]
        [Range(0f, 1f)] [SerializeField] private float rollInfluence = 0.18f;
        [Tooltip("SmoothDamp time in seconds for chase position movement.")]
        [Min(0.01f)] [SerializeField] private float positionSmoothing = 0.28f;
        [Tooltip("Exponential smoothing speed for chase rotation.")]
        [Min(0.01f)] [SerializeField] private float phase1RotationSmoothing = 18f;
        [Tooltip("Seconds after RMB orbit is released before the camera recenters.")]
        [Min(0f)] [SerializeField] private float recenterDelay = 0.65f;
        [Tooltip("Speed used to smoothly return orbit yaw and pitch behind the aircraft.")]
        [Min(0.1f)] [SerializeField] private float recenterSpeed = 2.5f;
        [Tooltip("Extra space around renderer bounds when calculating a safe camera distance.")]
        [Min(1f)] [SerializeField] private float framingPadding = 1.25f;
        [Tooltip("Closest normal orbit distance, before renderer-bounds framing is applied.")]
        [Min(1f)] [SerializeField] private float minimumDistance = 16f;
        [Tooltip("Viewport-style framing offset. Positive Y places the aircraft below screen centre.")]
        [SerializeField] private Vector2 screenFramingOffset = new Vector2(0f, 0.07f);
        [Tooltip("Additional local adjustment applied when CameraFocusTarget is created automatically.")]
        [SerializeField] private Vector3 focusTargetOffset = Vector3.zero;

        [Header("Close chase")]
        [Min(1f)] [SerializeField] private float closeChaseDistance = 12.5f;
        [Min(1f)] [SerializeField] private float closeChaseMinimumDistance = 11.5f;
        [SerializeField] private float closeChaseHeight = 1.5f;
        [Min(0.01f)] [SerializeField] private float closeChasePositionSmoothing = 0.18f;

        [Header("Attached camera look")]
        [SerializeField] private Vector2 cockpitLookLimits = new Vector2(75f, 45f);
        [SerializeField] private Vector2 wingLookLimits = new Vector2(18f, 12f);
        [Min(0.01f)] [SerializeField] private float attachedLookSensitivity = 0.12f;
        [Min(0.1f)] [SerializeField] private float attachedLookRecenterSpeed = 5f;
        [Min(0.01f)] [SerializeField] private float anchorPositionSmoothing = 0.04f;
        [Min(0.01f)] [SerializeField] private float anchorRotationSmoothing = 20f;

        [Header("Mode transitions")]
        [Min(0.05f)] [SerializeField] private float modeTransitionDuration = 0.35f;
        [Min(0.1f)] [SerializeField] private float fovTransitionSpeed = 10f;
        [Min(0.1f)] [SerializeField] private float modeLabelDuration = 1.5f;

        [Header("Camera motion comfort")]
        [SerializeField] private bool cameraShakeEnabled = true;
        [Range(0f, 1f)] [SerializeField] private float cameraMotionIntensity = 0.65f;
        [Min(0.1f)] [SerializeField] private float motionBlendSpeed = 5f;

        [Header("Camera motion tuning")]
        [Min(0f)] [SerializeField] private float engineVibrationPosition = 0.018f;
        [Min(0f)] [SerializeField] private float engineVibrationRotation = 0.08f;
        [Min(0f)] [SerializeField] private float runwayRumblePosition = 0.025f;
        [Min(0f)] [SerializeField] private float windMovementPosition = 0.018f;
        [Min(0f)] [SerializeField] private float maximumAccelerationOffset = 0.08f;
        [Min(0f)] [SerializeField] private float maximumLandingOffset = 0.16f;
        [Range(0f, 0.5f)] [SerializeField] private float chaseBankInfluence = 0.08f;
        [Min(0.1f)] [SerializeField] private float bankInfluenceSmoothing = 3.5f;

        [Header("Per-mode motion multipliers")]
        [SerializeField] private MotionMultipliers chaseMotion = new()
        { engine = 0.25f, runway = 0.35f, wind = 0.15f, landing = 0.35f,
          acceleration = 0.3f, turbulence = 0.25f };
        [SerializeField] private MotionMultipliers closeChaseMotion = new()
        { engine = 0.35f, runway = 0.5f, wind = 0.25f, landing = 0.5f,
          acceleration = 0.45f, turbulence = 0.4f };
        [SerializeField] private MotionMultipliers cockpitMotion = new()
        { engine = 1f, runway = 1f, wind = 0.85f, landing = 1f,
          acceleration = 1f, turbulence = 1f };
        [SerializeField] private MotionMultipliers leftWingMotion = new()
        { engine = 0.85f, runway = 0.8f, wind = 1f, landing = 0.8f,
          acceleration = 0.7f, turbulence = 1f };
        [SerializeField] private MotionMultipliers rightWingMotion = new()
        { engine = 0.85f, runway = 0.8f, wind = 1f, landing = 0.8f,
          acceleration = 0.7f, turbulence = 1f };

        [Header("Chase framing")]
        [FormerlySerializedAs("followDistance")]
        [Min(1f)] [SerializeField] private float chaseDistance = 18f;
        [FormerlySerializedAs("followHeight")]
        [SerializeField] private float chaseHeight = 2.2f;
        [Min(0f)] [SerializeField] private float chaseLookAhead = 1f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);

        [Header("Orbit limits")]
        [SerializeField] private float minPitchAngle = -10f;
        [SerializeField] private float maxPitchAngle = 40f;
        [SerializeField] private float defaultPitchAngle = 8f;
        [SerializeField] private float defaultYaw;
        [FormerlySerializedAs("mouseSensitivity")]
        [Min(0.01f)] [SerializeField] private float mouseSensitivity = 0.18f;
        [SerializeField] private bool invertY;

        [Header("Legacy external camera look ahead")]
        [Min(0f)] [SerializeField] private float baseLookAheadDistance = 5f;
        [Min(0f)] [SerializeField] private float speedLookAheadMultiplier = 0.08f;
        [Min(0f)] [SerializeField] private float maxLookAheadDistance = 11f;

        [Header("Nose and map camera")]
        [SerializeField] private Vector3 noseCameraOffset = new Vector3(0f, 1.25f, 2.1f);
        [Min(20f)] [SerializeField] private float topDownHeight = 120f;

        [Header("Follow feel")]
        [FormerlySerializedAs("movementSmoothing")]
        [Min(0.01f)] [SerializeField] private float cameraSmoothness = 5.5f;
        [FormerlySerializedAs("rotationSmoothing")]
        [Min(0.01f)] [SerializeField] private float rotationSmoothness = 8f;
        [Min(0f)] [SerializeField] private float cameraLag = 0.2f;

        [Header("Collision avoidance")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [Min(0.05f)] [SerializeField] private float collisionRadius = 0.45f;
        [Min(0.05f)] [SerializeField] private float collisionPadding = 0.3f;
        [Min(0f)] [SerializeField] private float minimumGroundClearance = 0.8f;
        [Min(0.1f)] [SerializeField] private float collisionReturnSpeed = 10f;

        [Header("Controls")]
        [SerializeField] private Key recenterKey = Key.R;
        [SerializeField] private Key quickLookLeftKey = Key.Z;
        [SerializeField] private Key quickLookRightKey = Key.X;
        [Range(20f, 150f)] [SerializeField] private float quickLookAngle = 80f;
        [Min(0.1f)] [SerializeField] private float quickLookSpeed = 10f;
        [Min(0.1f)] [SerializeField] private float cameraReturnSpeed = 7f;
        [Min(0f)] [SerializeField] private float cameraRecenterDelay = 0.15f;
        [SerializeField] private bool enableAutoCameraRecenter = true;
        [Min(0f)] [SerializeField] private float autoRecenterDelay = 2.5f;
        [Min(0.1f)] [SerializeField] private float autoRecenterStrength = 1.8f;

        [Header("Mode")]
        [SerializeField] private CameraMode cameraMode = CameraMode.Chase;
        [SerializeField] private PilotAwarenessAssist awarenessAssist;
        [SerializeField] private EngineController engine;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private LandingController landingController;
        [SerializeField] private StallController stallController;
        [SerializeField] private WindController windController;
        [SerializeField] private AirportLocationManager airportLocation;
        [SerializeField] private Transform runwayObserverAnchor;
        [SerializeField] private FlightHUDController flightHUD;
        [SerializeField] private GameSettingsManager settings;

        [Header("Cinematic controls")]
        [SerializeField] private Key cinematicCycleKey = Key.C;
        [SerializeField] private bool showHUDInCinematicCameras;

        [Header("Fly-by camera")]
        [Min(10f)] [SerializeField] private float flyByLeadDistance = 90f;
        [SerializeField] private Vector2 flyBySideOffsetRange = new Vector2(-18f, 18f);
        [SerializeField] private Vector2 flyByHeightOffsetRange = new Vector2(5f, 16f);
        [Min(10f)] [SerializeField] private float flyByPassResetDistance = 110f;
        [Min(0f)] [SerializeField] private float flyByMinimumActivationSpeed = 12f;

        [Header("Runway observer camera")]
        [Min(2f)] [SerializeField] private float runwaySideDistance = 28f;
        [SerializeField] private float runwayDistanceFromThreshold = 450f;
        [Min(1f)] [SerializeField] private float runwayCameraHeight = 5f;
        [Min(50f)] [SerializeField] private float runwayMaximumTrackingDistance = 2500f;

        [Header("Orbit showcase camera")]
        [Min(0f)] [SerializeField] private float showcaseOrbitSpeed = 8f;
        [Min(1f)] [SerializeField] private float showcaseHeight = 4f;
        [Min(5f)] [SerializeField] private float showcaseDistance = 20f;
        [SerializeField] private int showcaseDirection = 1;

        public CameraMode CurrentMode => cameraMode;
        public bool IsCinematic => cinematicActive;
        public CinematicMode CurrentCinematicMode => cinematicMode;

        private float yaw;
        private float pitch;
        private float lastCameraInputTime;
        private bool quickLookWasActive;
        private bool returningFromQuickLook;
        private Vector3 previousTargetPosition;
        private Vector3 headingForward = Vector3.forward;
        private bool targetPositionInitialized;
        private Rigidbody targetBody;
        private float orbitDistanceVelocity;
        private Vector3 currentOrbitOffset;
        private bool orbitOffsetInitialized;
        private float aircraftBoundsRadius = 5f;
        private Camera controlledCamera;
        private Vector3 smoothedCameraUp = Vector3.up;
        private bool cameraUpInitialized;
        private float attachedLookYaw;
        private float attachedLookPitch;
        private bool modeTransitionActive;
        private float modeTransitionStarted;
        private Vector3 transitionStartLocalPosition;
        private Quaternion transitionStartLocalRotation;
        private float modeLabelShownAt = float.NegativeInfinity;
        private Vector3 anchorPositionVelocity;
        private bool motionContactInitialized;
        private bool wasGrounded;
        private Vector3 previousMotionVelocity;
        private float smoothedAcceleration;
        private float bankLean;
        private float bankLeanVelocity;
        private float landingSpringPosition;
        private float landingSpringVelocity;
        private float turbulenceInput;
        private float stallBuffetInput;
        private float motionBlend;
        private Vector3 lastMotionWorldOffset;
        private Quaternion lastMotionRotation = Quaternion.identity;
        private bool cinematicActive;
        private CinematicMode cinematicMode;
        private CameraMode previousGameplayMode;
        private Vector3 cinematicCameraPosition;
        private Vector3 flyByDirection = Vector3.forward;
        private float showcaseAngle;
        private float playerExternalFov = 60f;
        private float playerCockpitFov = 70f;
        private float playerCinematicFov = 50f;
        private float externalSensitivityMultiplier = 1f;
        private float cockpitSensitivityMultiplier = 1f;
        private bool invertCockpitY;
        private bool cinematicTransitionsEnabled = true;
        private bool includeWingViewsInCycle = true;
        private bool includeCloseChaseInCycle = true;
        private bool cinematicTransitionActive;
        private float cinematicTransitionStarted;
        private Vector3 cinematicTransitionStartPosition;
        private Quaternion cinematicTransitionStartRotation;
        private bool orbitWasHeld;
        private float orbitReleasedTime = float.NegativeInfinity;
        private float lastOrbitHeldTime = float.NegativeInfinity;

        private void Start()
        {
            ResolveReferences();
            ResolveFocusTarget();
            ResolveCameraAnchors();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            RemovePreviousSecondaryMotion();
            ResolveReferences();
            ResolveFocusTarget();
            ResolveCameraAnchors();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool cameraInputAllowed = gameFlow == null || gameFlow.IsInFlight;

            if (cameraInputAllowed && keyboard != null &&
                keyboard[cinematicCycleKey].wasPressedThisFrame)
            {
                bool shiftHeld = keyboard.leftShiftKey.isPressed ||
                                 keyboard.rightShiftKey.isPressed;
                if (shiftHeld)
                {
                    if (cinematicActive) ExitCinematicMode();
                    return;
                }
                if (!cinematicActive) EnterCinematicMode(CinematicMode.FlyBy);
                else CycleCinematicMode();
            }
            if (cinematicActive && keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame)
            {
                ExitCinematicMode();
                return;
            }
            if (cinematicActive)
            {
                UpdateCinematicCamera(cameraInputAllowed);
                return;
            }

            if (cameraInputAllowed && keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                    SetCameraMode(CameraMode.Chase);
                else if (keyboard.digit2Key.wasPressedThisFrame)
                    SetCameraMode(CameraMode.CloseChase);
                else if (keyboard.digit3Key.wasPressedThisFrame)
                    SetCameraMode(CameraMode.Cockpit);
                else if (keyboard.digit4Key.wasPressedThisFrame)
                    SetCameraMode(CameraMode.LeftWing);
                else if (keyboard.digit5Key.wasPressedThisFrame)
                    SetCameraMode(CameraMode.RightWing);
            }

            bool orbitAllowed =
                cameraMode == CameraMode.Chase ||
                cameraMode == CameraMode.CloseChase;
            bool manualOrbit = false;
            bool mouseLookHeld = cameraInputAllowed && mouse != null &&
                                 mouse.rightButton.isPressed;
            bool orbitHeld = orbitAllowed && mouseLookHeld;
            if (orbitHeld) lastOrbitHeldTime = Time.unscaledTime;
            if (orbitHeld && input != null)
            {
                Vector2 look = input.CameraLookDelta * mouseSensitivity *
                               externalSensitivityMultiplier;
                manualOrbit = look.sqrMagnitude > 0.0001f;
                if (manualOrbit)
                {
                    yaw += look.x;
                    float verticalSign = invertY ? 1f : -1f;
                    pitch = Mathf.Clamp(
                        pitch + look.y * verticalSign,
                        minPitchAngle,
                        maxPitchAngle);
                    lastCameraInputTime = Time.unscaledTime;
                }
            }

            bool attachedMode = cameraMode == CameraMode.Cockpit ||
                                cameraMode == CameraMode.LeftWing ||
                                cameraMode == CameraMode.RightWing;
            if (attachedMode && mouseLookHeld && input != null)
            {
                Vector2 limits = cameraMode == CameraMode.Cockpit
                    ? cockpitLookLimits
                    : wingLookLimits;
                Vector2 look = input.CameraLookDelta * attachedLookSensitivity *
                               cockpitSensitivityMultiplier;
                attachedLookYaw = Mathf.Clamp(
                    attachedLookYaw + look.x, -limits.x, limits.x);
                float verticalSign = invertCockpitY ? 1f : -1f;
                attachedLookPitch = Mathf.Clamp(
                    attachedLookPitch + look.y * verticalSign,
                    -limits.y, limits.y);
            }
            else if (attachedMode && !mouseLookHeld)
            {
                attachedLookYaw = DampAngle(
                    attachedLookYaw, 0f, attachedLookRecenterSpeed);
                attachedLookPitch = DampAngle(
                    attachedLookPitch, 0f, attachedLookRecenterSpeed);
            }

            if (orbitWasHeld && !orbitHeld)
                orbitReleasedTime = Time.unscaledTime;
            orbitWasHeld = orbitHeld;

            bool explicitRecenter =
                cameraInputAllowed && ((input != null && input.ResetCameraPressed) ||
                (keyboard != null && keyboard[recenterKey].wasPressedThisFrame) ||
                (mouse != null && mouse.middleButton.wasPressedThisFrame));
            if (explicitRecenter)
            {
                ResetOrbit();
                returningFromQuickLook = false;
                lastCameraInputTime = Time.unscaledTime;
            }

            if (cameraInputAllowed)
                UpdateQuickLookAndRecenter(
                    keyboard, orbitAllowed, manualOrbit, explicitRecenter,
                    orbitHeld);
            // Legacy modes compensate target translation directly. Phase 1
            // deliberately leaves translation to SmoothDamp.
            bool phase1External = usePhase1ChaseCamera &&
                                  (cameraMode == CameraMode.Chase ||
                                   cameraMode == CameraMode.CloseChase);
            if (!phase1External)
                UpdateTargetTranslation();
            else
            {
                previousTargetPosition = target.position;
                targetPositionInitialized = true;
            }
            UpdateHeading();

            switch (cameraMode)
            {
                case CameraMode.Cockpit:
                    UpdateAttachedCamera(cockpitCameraAnchor, playerCockpitFov);
                    break;
                case CameraMode.LeftWing:
                    UpdateAttachedCamera(leftWingCameraAnchor, playerExternalFov);
                    break;
                case CameraMode.RightWing:
                    UpdateAttachedCamera(rightWingCameraAnchor, playerExternalFov);
                    break;
                default:
                    UpdateExternalCamera();
                    break;
            }
            ApplySecondaryMotion();
        }

        private void UpdateExternalCamera()
        {
            if (usePhase1ChaseCamera &&
                (cameraMode == CameraMode.Chase ||
                 cameraMode == CameraMode.CloseChase))
            {
                UpdatePhase1ChaseCamera(cameraMode == CameraMode.CloseChase);
                return;
            }

            float modePitch = pitch;
            float distance = chaseDistance;
            float height = chaseHeight;

            Quaternion heading = Quaternion.LookRotation(headingForward, Vector3.up);
            Quaternion orbit = heading * Quaternion.Euler(modePitch, yaw, 0f);
            float speed = targetBody == null ? 0f : targetBody.linearVelocity.magnitude;
            float dynamicLookAhead = Mathf.Min(
                maxLookAheadDistance,
                baseLookAheadDistance +
                chaseLookAhead +
                speed * speedLookAheadMultiplier);
            Vector3 lagDirection = targetBody != null &&
                                   targetBody.linearVelocity.sqrMagnitude > 0.1f
                ? -targetBody.linearVelocity.normalized
                : -headingForward;
            Vector3 cameraAnchor =
                target.position +
                targetOffset +
                lagDirection * cameraLag;
            Vector3 focus =
                cameraAnchor + headingForward * dynamicLookAhead;
            Vector3 desired =
                cameraAnchor +
                orbit * Vector3.back * distance +
                Vector3.up * height;

            if (awarenessAssist != null)
                desired -= headingForward * awarenessAssist.LongitudinalCameraCue;
            desired = ResolveCollision(focus, desired);
            desired += CalculateShake();
            SmoothPosition(desired);

            float cameraLean = awarenessAssist == null
                ? 0f
                : awarenessAssist.CameraLeanDegrees;
            Quaternion rotation =
                Quaternion.LookRotation(focus - transform.position, Vector3.up) *
                Quaternion.AngleAxis(cameraLean, Vector3.forward);
            SmoothRotation(rotation);
        }

        public void EnterCinematicMode(CinematicMode mode)
        {
            if (!cinematicActive) previousGameplayMode = cameraMode;
            cinematicTransitionStartPosition = target.InverseTransformPoint(
                transform.position);
            cinematicTransitionStartRotation = Quaternion.Inverse(target.rotation) *
                                                transform.rotation;
            cinematicTransitionActive = cinematicTransitionsEnabled;
            cinematicTransitionStarted = Time.unscaledTime;
            cinematicActive = true;
            cinematicMode = mode;
            modeLabelShownAt = Time.unscaledTime;
            if (!showHUDInCinematicCameras) flightHUD?.SetHUDVisible(false);

            if (mode == CinematicMode.FlyBy && GetAircraftSpeed() <
                flyByMinimumActivationSpeed)
                cinematicMode = CinematicMode.OrbitShowcase;
            if (cinematicMode == CinematicMode.FlyBy)
                RepositionFlyBy();
            else if (cinematicMode == CinematicMode.RunwayObserver &&
                     !RepositionRunwayObserver())
            {
                cinematicMode = GetAircraftSpeed() >= flyByMinimumActivationSpeed
                    ? CinematicMode.FlyBy
                    : CinematicMode.OrbitShowcase;
                if (cinematicMode == CinematicMode.FlyBy) RepositionFlyBy();
            }
            else if (cinematicMode == CinematicMode.OrbitShowcase)
                showcaseAngle = 0f;
        }

        public void ExitCinematicMode()
        {
            if (!cinematicActive) return;
            cinematicActive = false;
            SetCameraMode(previousGameplayMode, true);
            bool shouldShowHUD = settings == null || settings.HUDEnabled;
            if (gameFlow == null || gameFlow.IsInFlight)
                flightHUD?.SetHUDVisible(shouldShowHUD);
        }

        private void CycleCinematicMode()
        {
            CinematicMode next = (CinematicMode)(((int)cinematicMode + 1) % 3);
            EnterCinematicMode(next);
        }

        private void UpdateCinematicCamera(bool inputAllowed)
        {
            Vector3 focus = focusTarget == null ? target.position : focusTarget.position;
            switch (cinematicMode)
            {
                case CinematicMode.FlyBy:
                    Vector3 fromCamera = target.position - cinematicCameraPosition;
                    bool passed = Vector3.Dot(fromCamera, flyByDirection) > 0f &&
                                  fromCamera.magnitude > flyByPassResetDistance;
                    if (passed) RepositionFlyBy();
                    controlledCamera.fieldOfView = playerCinematicFov;
                    break;
                case CinematicMode.RunwayObserver:
                    if (Vector3.Distance(target.position, cinematicCameraPosition) >
                        runwayMaximumTrackingDistance && !RepositionRunwayObserver())
                    {
                        EnterCinematicMode(CinematicMode.FlyBy);
                        return;
                    }
                    controlledCamera.fieldOfView = playerCinematicFov;
                    break;
                case CinematicMode.OrbitShowcase:
                    if (inputAllowed)
                        showcaseAngle += showcaseOrbitSpeed *
                                         Mathf.Sign(showcaseDirection == 0 ? 1 : showcaseDirection) *
                                         Time.unscaledDeltaTime;
                    float distance = Mathf.Max(
                        showcaseDistance,
                        CalculateSafeOrbitDistance(showcaseDistance, minimumDistance));
                    Quaternion orbit = Quaternion.AngleAxis(showcaseAngle, Vector3.up);
                    Vector3 candidate = focus + orbit *
                        new Vector3(0f, showcaseHeight, -distance);
                    if (TryFindSafeCinematicPosition(candidate, out Vector3 safeOrbit))
                        cinematicCameraPosition = safeOrbit;
                    controlledCamera.fieldOfView = playerCinematicFov;
                    break;
            }

            Vector3 finalPosition = cinematicCameraPosition;
            Vector3 direction = focus - finalPosition;
            if (direction.sqrMagnitude < 0.01f) direction = target.forward;
            Quaternion finalRotation = Quaternion.LookRotation(direction, Vector3.up);
            if (cinematicTransitionActive)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                    (Time.unscaledTime - cinematicTransitionStarted) / 0.22f));
                Vector3 trackedStartPosition = target.TransformPoint(
                    cinematicTransitionStartPosition);
                Quaternion trackedStartRotation = target.rotation *
                                                  cinematicTransitionStartRotation;
                transform.position = Vector3.Lerp(
                    trackedStartPosition, finalPosition, t);
                transform.rotation = Quaternion.Slerp(
                    trackedStartRotation, finalRotation, t);
                if (t >= 0.999f) cinematicTransitionActive = false;
            }
            else
            {
                transform.position = finalPosition;
                transform.rotation = finalRotation;
            }
        }

        private void RepositionFlyBy()
        {
            Vector3 velocity = targetBody == null
                ? Vector3.zero
                : targetBody.linearVelocity;
            flyByDirection = velocity.magnitude >= flyByMinimumActivationSpeed
                ? velocity.normalized
                : target.forward.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, flyByDirection).normalized;
            if (side.sqrMagnitude < 0.01f) side = target.right;
            float sample = Mathf.PerlinNoise(
                target.position.x * 0.013f + Time.unscaledTime * 0.17f,
                target.position.z * 0.011f + 7.3f);
            float sideOffset = Mathf.Lerp(
                flyBySideOffsetRange.x, flyBySideOffsetRange.y, sample);
            float heightOffset = Mathf.Lerp(
                flyByHeightOffsetRange.x, flyByHeightOffsetRange.y, 1f - sample);
            Vector3 candidate = target.position +
                                flyByDirection * flyByLeadDistance +
                                side * sideOffset + Vector3.up * heightOffset;
            if (!TryFindSafeCinematicPosition(candidate, out cinematicCameraPosition))
                cinematicCameraPosition = candidate + Vector3.up * 12f;
        }

        private bool RepositionRunwayObserver()
        {
            Vector3 candidate;
            if (airportLocation != null && airportLocation.CurrentAirport != null)
            {
                AirportDefinition airport = airportLocation.CurrentAirport;
                Quaternion runwayRotation = Quaternion.Euler(
                    0f, airportLocation.ActiveRunwayHeading, 0f);
                Vector3 forward = runwayRotation * Vector3.forward;
                Vector3 right = runwayRotation * Vector3.right;
                Vector3 threshold = airportLocation.GetActiveThreshold(airport);
                float safeSide = airport.runwayWidth * 0.5f + runwaySideDistance;
                float aircraftSide = Vector3.Dot(target.position - threshold, right);
                float sideSign = aircraftSide >= 0f ? -1f : 1f;
                candidate = threshold + forward * runwayDistanceFromThreshold +
                            right * safeSide * sideSign +
                            Vector3.up * runwayCameraHeight;
            }
            else if (runwayObserverAnchor != null)
                candidate = runwayObserverAnchor.position;
            else return false;

            return TryFindSafeCinematicPosition(
                candidate, out cinematicCameraPosition);
        }

        private bool TryFindSafeCinematicPosition(
            Vector3 candidate,
            out Vector3 safePosition)
        {
            Vector3[] offsets =
            {
                Vector3.zero, Vector3.up * 4f, Vector3.up * 9f,
                Vector3.right * 6f + Vector3.up * 3f,
                Vector3.left * 6f + Vector3.up * 3f
            };
            foreach (Vector3 offset in offsets)
            {
                Vector3 test = candidate + offset;
                if (Physics.Raycast(test + Vector3.up * 150f, Vector3.down,
                    out RaycastHit ground, 300f, collisionMask,
                    QueryTriggerInteraction.Ignore))
                    test.y = Mathf.Max(test.y, ground.point.y + 1.2f);
                if (Vector3.Distance(test, target.position) < minimumDistance)
                    continue;
                Collider[] overlaps = Physics.OverlapSphere(
                    test, 0.65f, collisionMask, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                foreach (Collider overlap in overlaps)
                {
                    if (overlap == null || overlap.transform.IsChildOf(target) ||
                        overlap.attachedRigidbody == targetBody) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;
                safePosition = test;
                return true;
            }
            safePosition = candidate;
            return false;
        }

        private float GetAircraftSpeed() => targetBody == null
            ? 0f
            : targetBody.linearVelocity.magnitude;

        private void UpdatePhase1ChaseCamera(bool closeChase)
        {
            Vector3 aircraftForward = target.forward.normalized;
            Vector3 aircraftUp = target.up.normalized;
            Vector3 desiredReducedRollUp = Vector3.Slerp(
                Vector3.up, aircraftUp, rollInfluence).normalized;
            if (Mathf.Abs(Vector3.Dot(aircraftForward, desiredReducedRollUp)) > 0.98f)
                desiredReducedRollUp = aircraftUp;
            if (!cameraUpInitialized)
            {
                smoothedCameraUp = desiredReducedRollUp;
                cameraUpInitialized = true;
            }
            smoothedCameraUp = Vector3.Slerp(
                smoothedCameraUp,
                desiredReducedRollUp,
                Damp(phase1RotationSmoothing)).normalized;
            Vector3 reducedRollUp = smoothedCameraUp;
            // Building the frame from direction vectors avoids Euler-angle
            // discontinuities during rapid pitch/heading changes.
            Quaternion reducedRollFrame = Quaternion.LookRotation(
                aircraftForward, reducedRollUp);
            Quaternion orbitRotation = reducedRollFrame *
                                       Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = focusTarget.position;
            float requestedDistance = closeChase
                ? closeChaseDistance
                : chaseDistance;
            float requestedHeight = closeChase
                ? closeChaseHeight
                : chaseHeight;
            float modeMinimumDistance = closeChase
                ? closeChaseMinimumDistance
                : minimumDistance;
            float safeDistance = CalculateSafeOrbitDistance(
                requestedDistance, modeMinimumDistance);
            Vector3 desiredOrbitOffset = orbitRotation *
                new Vector3(0f, requestedHeight, -safeDistance);
            if (!orbitOffsetInitialized)
            {
                currentOrbitOffset = desiredOrbitOffset;
                orbitOffsetInitialized = true;
            }
            if (orbitWasHeld)
            {
                // Mouse input controls target angles directly. Do not add a
                // second angular lag that can sweep the camera through the model.
                currentOrbitOffset = desiredOrbitOffset;
            }
            else
            {
                float smoothing = closeChase
                    ? closeChasePositionSmoothing
                    : positionSmoothing;
                float directionT = Damp(1f / smoothing);
                Vector3 smoothDirection = Vector3.Slerp(
                    currentOrbitOffset.normalized,
                    desiredOrbitOffset.normalized,
                    directionT).normalized;
                float smoothDistance = Mathf.SmoothDamp(
                    currentOrbitOffset.magnitude,
                    desiredOrbitOffset.magnitude,
                    ref orbitDistanceVelocity,
                    smoothing,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
                currentOrbitOffset = smoothDirection * smoothDistance;
            }
            // The smoothed value is an offset, never an old world position, so
            // aircraft translation cannot leave the camera or aim behind.
            Vector3 desired = focus + currentOrbitOffset;
            desired = ResolveCollision(focus, desired, modeMinimumDistance);
            desired += CalculateShake();
            ApplyModeTransitionPosition(desired);

            float finalDistance = Vector3.Distance(transform.position, focus);
            float verticalHalfSize = finalDistance * Mathf.Tan(
                0.5f * controlledCamera.fieldOfView * Mathf.Deg2Rad);
            float horizontalHalfSize = verticalHalfSize * controlledCamera.aspect;
            Vector3 framingAim = focus +
                                 reducedRollUp *
                                 (screenFramingOffset.y * verticalHalfSize * 2f) +
                                 (orbitRotation * Vector3.right) *
                                 (screenFramingOffset.x * horizontalHalfSize * 2f);
            Vector3 lookDirection = framingAim - transform.position;
            if (lookDirection.sqrMagnitude < 0.0001f)
                lookDirection = aircraftForward;
            Quaternion desiredRotation = Quaternion.LookRotation(
                lookDirection, reducedRollUp);
            // Aim is deliberately independent of position damping. Recompute it
            // after the final position every LateUpdate, including recentering.
            ApplyModeTransitionRotation(desiredRotation);
            UpdateFov(playerExternalFov);
        }

        private float CalculateSafeOrbitDistance(
            float requestedDistance,
            float modeMinimumDistance)
        {
            float verticalFov = controlledCamera == null
                ? 60f
                : controlledCamera.fieldOfView;
            float aspect = controlledCamera == null
                ? 16f / 9f
                : Mathf.Max(0.1f, controlledCamera.aspect);
            float horizontalFov = 2f * Mathf.Atan(
                Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * aspect) *
                Mathf.Rad2Deg;
            float limitingHalfFov = Mathf.Min(verticalFov, horizontalFov) *
                                    0.5f * Mathf.Deg2Rad;
            float boundsDistance = aircraftBoundsRadius /
                                   Mathf.Max(0.1f, Mathf.Tan(limitingHalfFov));
            return Mathf.Max(requestedDistance, modeMinimumDistance,
                boundsDistance * framingPadding);
        }

        private void UpdateAttachedCamera(Transform anchor, float modeFov)
        {
            if (anchor == null) return;
            Quaternion lookOffset = Quaternion.Euler(
                attachedLookPitch, attachedLookYaw, 0f);

            Vector3 currentLocalPosition = anchor.InverseTransformPoint(
                transform.position);
            Vector3 smoothLocalPosition = Vector3.SmoothDamp(
                currentLocalPosition,
                Vector3.zero,
                ref anchorPositionVelocity,
                anchorPositionSmoothing,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            Vector3 desiredPosition = anchor.TransformPoint(smoothLocalPosition);

            Quaternion currentLocalRotation = Quaternion.Inverse(anchor.rotation) *
                                              transform.rotation;
            Quaternion smoothLocalRotation = Quaternion.Slerp(
                currentLocalRotation,
                lookOffset,
                Damp(anchorRotationSmoothing));
            Quaternion desiredRotation = anchor.rotation * smoothLocalRotation;

            ApplyModeTransitionPosition(desiredPosition);
            ApplyModeTransitionRotation(desiredRotation);
            UpdateFov(modeFov);
        }

        private float ModeTransitionT => modeTransitionActive
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                (Time.unscaledTime - modeTransitionStarted) /
                modeTransitionDuration))
            : 1f;

        private void ApplyModeTransitionPosition(Vector3 desiredPosition)
        {
            if (!modeTransitionActive)
            {
                transform.position = desiredPosition;
                return;
            }
            Vector3 trackedStart = target.TransformPoint(
                transitionStartLocalPosition);
            transform.position = Vector3.Lerp(
                trackedStart, desiredPosition, ModeTransitionT);
        }

        private void ApplyModeTransitionRotation(Quaternion desiredRotation)
        {
            if (!modeTransitionActive)
            {
                transform.rotation = desiredRotation;
                return;
            }
            Quaternion trackedStart = target.rotation *
                                      transitionStartLocalRotation;
            float t = ModeTransitionT;
            transform.rotation = Quaternion.Slerp(
                trackedStart, desiredRotation, t);
            if (t >= 0.999f) modeTransitionActive = false;
        }

        private void UpdateFov(float desiredFov)
        {
            if (controlledCamera == null) return;
            controlledCamera.fieldOfView = Mathf.Lerp(
                controlledCamera.fieldOfView,
                desiredFov,
                Damp(fovTransitionSpeed));
        }

        private void UpdateNoseCamera()
        {
            Vector3 desired = target.TransformPoint(noseCameraOffset);
            transform.position = Vector3.Lerp(
                transform.position,
                desired + CalculateShake() * 0.35f,
                Damp(cameraSmoothness * 1.5f));
            Quaternion rotation = Quaternion.LookRotation(
                target.forward,
                target.up);
            SmoothRotation(rotation);
        }

        private void UpdateTopDownCamera()
        {
            Vector3 desired = target.position + Vector3.up * topDownHeight;
            SmoothPosition(desired);
            Quaternion rotation = Quaternion.LookRotation(
                Vector3.down,
                headingForward);
            SmoothRotation(rotation);
        }

        private void UpdateQuickLookAndRecenter(
            Keyboard keyboard,
            bool orbitAllowed,
            bool manualOrbit,
            bool explicitRecenter,
            bool orbitHeld)
        {
            if (!orbitAllowed) return;
            bool quickLeft =
                keyboard != null && keyboard[quickLookLeftKey].isPressed;
            bool quickRight =
                keyboard != null && keyboard[quickLookRightKey].isPressed;
            bool quickLookActive = quickLeft ^ quickRight;
            if (quickLookActive)
            {
                returningFromQuickLook = false;
                float targetYaw =
                    defaultYaw + (quickRight ? quickLookAngle : -quickLookAngle);
                yaw = DampAngle(yaw, targetYaw, quickLookSpeed);
                lastCameraInputTime = Time.unscaledTime;
            }
            else
            {
                if (quickLookWasActive) returningFromQuickLook = true;
                if (returningFromQuickLook &&
                    Time.unscaledTime - lastCameraInputTime >= cameraRecenterDelay)
                {
                    yaw = DampAngle(yaw, defaultYaw, cameraReturnSpeed);
                    pitch = DampAngle(pitch, defaultPitchAngle, cameraReturnSpeed);
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, defaultYaw)) < 0.25f &&
                        Mathf.Abs(pitch - defaultPitchAngle) < 0.25f)
                        returningFromQuickLook = false;
                }
            }
            quickLookWasActive = quickLookActive;

            if (!explicitRecenter && !quickLookActive && !orbitHeld &&
                enableAutoCameraRecenter &&
                usePhase1ChaseCamera &&
                (cameraMode == CameraMode.Chase ||
                 cameraMode == CameraMode.CloseChase) &&
                Time.unscaledTime - orbitReleasedTime >= recenterDelay)
            {
                yaw = DampAngle(yaw, defaultYaw, recenterSpeed);
                pitch = DampAngle(pitch, defaultPitchAngle, recenterSpeed);
            }

            if (!usePhase1ChaseCamera && !explicitRecenter &&
                !quickLookActive && !manualOrbit &&
                enableAutoCameraRecenter &&
                Time.unscaledTime - lastCameraInputTime >= autoRecenterDelay)
            {
                yaw = DampAngle(yaw, defaultYaw, autoRecenterStrength);
                pitch = DampAngle(
                    pitch, defaultPitchAngle, autoRecenterStrength);
            }
        }

        private Vector3 ResolveCollision(
            Vector3 focus,
            Vector3 desired,
            float minimumAllowedDistance = 0.1f)
        {
            Vector3 offset = desired - focus;
            float distance = offset.magnitude;
            if (distance > 0.01f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(
                    focus,
                    collisionRadius,
                    offset / distance,
                    distance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore);
                float nearest = distance;
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider == null ||
                        hit.collider.attachedRigidbody == targetBody ||
                        hit.collider.transform.IsChildOf(target))
                        continue;
                    nearest = Mathf.Min(
                        nearest,
                        Mathf.Max(minimumAllowedDistance,
                            hit.distance - collisionPadding));
                }
                if (nearest < distance)
                    desired = focus + offset.normalized * nearest;
            }

            RaycastHit[] groundHits = Physics.RaycastAll(
                desired + Vector3.up * 20f,
                Vector3.down,
                40f,
                collisionMask,
                QueryTriggerInteraction.Ignore);
            float groundHeight = float.NegativeInfinity;
            foreach (RaycastHit groundHit in groundHits)
            {
                if (groundHit.collider == null ||
                    groundHit.collider.attachedRigidbody == targetBody ||
                    groundHit.collider.transform.IsChildOf(target))
                    continue;
                groundHeight = Mathf.Max(groundHeight, groundHit.point.y);
            }
            if (!float.IsNegativeInfinity(groundHeight))
            {
                desired.y = Mathf.Max(
                    desired.y,
                    groundHeight + minimumGroundClearance);
            }
            return desired;
        }

        private Vector3 CalculateShake()
        {
            if (!cameraShakeEnabled || cameraMotionIntensity <= 0.0001f)
                return Vector3.zero;
            float shake = awarenessAssist == null
                ? 0f
                : awarenessAssist.CameraShake;
            if (shake <= 0.0001f) return Vector3.zero;
            float time = Time.unscaledTime;
            return cameraMotionIntensity *
                (
                transform.right *
                ((Mathf.PerlinNoise(time * 19f, 0.13f) * 2f - 1f) * shake) +
                transform.up *
                ((Mathf.PerlinNoise(0.37f, time * 23f) * 2f - 1f) * shake));
        }

        private void RemovePreviousSecondaryMotion()
        {
            transform.position -= lastMotionWorldOffset;
            transform.rotation *= Quaternion.Inverse(lastMotionRotation);
            lastMotionWorldOffset = Vector3.zero;
            lastMotionRotation = Quaternion.identity;
        }

        private void ApplySecondaryMotion()
        {
            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            bool enabled = cameraShakeEnabled && cameraMotionIntensity > 0.0001f;
            motionBlend = Mathf.MoveTowards(
                motionBlend, enabled ? cameraMotionIntensity : 0f,
                motionBlendSpeed * dt);
            if (motionBlend <= 0.0001f)
            {
                ResetMotionInputs(dt);
                return;
            }

            MotionMultipliers mode = GetMotionMultipliers();
            bool grounded = flightPhysics != null &&
                            flightPhysics.IsPhysicallyGrounded;
            if (!motionContactInitialized)
            {
                wasGrounded = grounded;
                previousMotionVelocity = targetBody == null
                    ? Vector3.zero
                    : targetBody.linearVelocity;
                motionContactInitialized = true;
            }
            if (wasGrounded && !grounded)
                landingSpringVelocity -= 0.22f;
            else if (!wasGrounded && grounded)
            {
                float descent = landingController == null
                    ? Mathf.Max(0f, -previousMotionVelocity.y)
                    : Mathf.Max(0f, -landingController.TouchdownVerticalSpeed);
                ApplyLandingImpact(Mathf.InverseLerp(0.8f, 6f, descent));
            }
            wasGrounded = grounded;

            Vector3 velocity = targetBody == null
                ? Vector3.zero
                : targetBody.linearVelocity;
            float forwardAcceleration = dt <= 0f
                ? 0f
                : Vector3.Dot((velocity - previousMotionVelocity) / dt,
                    target.forward);
            previousMotionVelocity = velocity;
            smoothedAcceleration = Mathf.Lerp(
                smoothedAcceleration,
                Mathf.Clamp(forwardAcceleration, -10f, 10f),
                Damp(3.5f));

            float rpm = engine != null && engine.State != EngineController.EngineState.Off
                ? engine.NormalizedRPM
                : 0f;
            float airspeed = flightPhysics == null ? velocity.magnitude : flightPhysics.Airspeed;
            float groundSpeed = flightPhysics == null
                ? Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude
                : flightPhysics.HorizontalGroundSpeed;
            float runwayAmount = grounded
                ? Mathf.InverseLerp(3f, 32f, groundSpeed)
                : 0f;
            float windAmount = Mathf.InverseLerp(20f, 75f, airspeed);
            float windGust = windController != null && windController.GustsEnabled
                ? windController.GustStrengthKnots / 12f * 0.25f
                : 0f;
            float stallAmount = stallController != null && stallController.StallWarning
                ? (stallController.State == StallController.StallState.Stalled ? 1f : 0.55f)
                : 0f;
            stallBuffetInput = Mathf.MoveTowards(
                stallBuffetInput, Mathf.Max(stallBuffetInput, stallAmount), dt * 3f);

            turbulenceInput = Mathf.Max(turbulenceInput, windGust);
            turbulenceInput = Mathf.MoveTowards(turbulenceInput, 0f, dt * 0.45f);
            stallBuffetInput = Mathf.MoveTowards(stallBuffetInput, 0f, dt * 0.7f);

            UpdateLandingSpring(dt);
            float bankTarget = IsChaseMode() && flightPhysics != null
                ? -flightPhysics.BankAngle * chaseBankInfluence
                : 0f;
            bankLean = Mathf.SmoothDampAngle(
                bankLean, bankTarget, ref bankLeanVelocity,
                1f / bankInfluenceSmoothing, Mathf.Infinity, dt);

            float time = Time.unscaledTime;
            float engineNoiseX = SmoothNoise(time * (8f + rpm * 10f), 11.3f);
            float engineNoiseY = SmoothNoise(time * (9f + rpm * 11f), 29.7f);
            float runwayNoise = SmoothNoise(time * (3f + groundSpeed * 0.18f), 47.1f);
            float windNoiseX = SmoothNoise(time * 1.35f, 71.9f);
            float windNoiseY = SmoothNoise(time * 1.7f, 93.2f);
            float buffetNoise = SmoothNoise(time * 7.5f, 121.4f);

            Vector3 localPosition = Vector3.zero;
            localPosition.x += engineNoiseX * engineVibrationPosition * rpm * mode.engine;
            localPosition.y += engineNoiseY * engineVibrationPosition * rpm * mode.engine;
            localPosition.y += runwayNoise * runwayRumblePosition * runwayAmount * mode.runway;
            localPosition.x += windNoiseX * windMovementPosition * windAmount * mode.wind;
            localPosition.y += windNoiseY * windMovementPosition * windAmount * 0.6f * mode.wind;
            localPosition.z += Mathf.Clamp(
                -smoothedAcceleration * 0.008f,
                -maximumAccelerationOffset,
                maximumAccelerationOffset) * mode.acceleration;
            localPosition.y += landingSpringPosition * maximumLandingOffset * mode.landing;
            float combinedTurbulence = Mathf.Clamp01(
                turbulenceInput + stallBuffetInput);
            localPosition += new Vector3(
                buffetNoise, windNoiseY, 0f) *
                (0.025f * combinedTurbulence * mode.turbulence);
            localPosition *= motionBlend;

            Vector3 localEuler = new Vector3(
                engineNoiseY * engineVibrationRotation * rpm * mode.engine,
                windNoiseX * 0.12f * combinedTurbulence * mode.turbulence,
                bankLean + buffetNoise * 0.16f * combinedTurbulence * mode.turbulence);
            localEuler *= motionBlend;

            lastMotionWorldOffset = transform.TransformVector(localPosition);
            lastMotionRotation = Quaternion.Euler(localEuler);
            transform.position += lastMotionWorldOffset;
            transform.rotation *= lastMotionRotation;
        }

        private void ResetMotionInputs(float dt)
        {
            turbulenceInput = Mathf.MoveTowards(turbulenceInput, 0f, dt);
            stallBuffetInput = Mathf.MoveTowards(stallBuffetInput, 0f, dt);
            UpdateLandingSpring(dt);
        }

        private void UpdateLandingSpring(float dt)
        {
            const float spring = 42f;
            const float damping = 10f;
            landingSpringVelocity +=
                (-landingSpringPosition * spring - landingSpringVelocity * damping) * dt;
            landingSpringPosition += landingSpringVelocity * dt;
            landingSpringPosition = Mathf.Clamp(landingSpringPosition, -1f, 1f);
        }

        private MotionMultipliers GetMotionMultipliers() => cameraMode switch
        {
            CameraMode.CloseChase => closeChaseMotion,
            CameraMode.Cockpit => cockpitMotion,
            CameraMode.LeftWing => leftWingMotion,
            CameraMode.RightWing => rightWingMotion,
            _ => chaseMotion
        };

        private bool IsChaseMode() =>
            cameraMode == CameraMode.Chase || cameraMode == CameraMode.CloseChase;

        private static float SmoothNoise(float time, float seed) =>
            Mathf.PerlinNoise(time, seed) * 2f - 1f;

        public void ApplyTurbulence(float intensity) =>
            turbulenceInput = Mathf.Max(turbulenceInput, Mathf.Clamp01(intensity));

        public void ApplyStallBuffet(float intensity) =>
            stallBuffetInput = Mathf.Max(stallBuffetInput, Mathf.Clamp01(intensity));

        public void ApplyLandingImpact(float severity)
        {
            float controlledSeverity = Mathf.Clamp01(severity);
            landingSpringVelocity -= Mathf.Lerp(0.08f, 1.1f, controlledSeverity);
        }

        public void SetCameraMotionIntensity(float intensity) =>
            cameraMotionIntensity = Mathf.Clamp01(intensity);

        public void SetCameraShakeEnabled(bool enabled) =>
            cameraShakeEnabled = enabled;

        public void SetShowHUDInCinematicCameras(bool show)
        {
            showHUDInCinematicCameras = show;
            if (!cinematicActive) return;
            flightHUD?.SetHUDVisible(show &&
                (settings == null || settings.HUDEnabled));
        }

        private void ResolveReferences()
        {
            if (controlledCamera == null)
                controlledCamera = GetComponent<Camera>();
            if (targetBody == null && target != null)
                targetBody = target.GetComponent<Rigidbody>();
            if (awarenessAssist == null && target != null)
                awarenessAssist = target.GetComponent<PilotAwarenessAssist>();
            if (engine == null && target != null)
                engine = target.GetComponent<EngineController>();
            if (flightPhysics == null && target != null)
                flightPhysics = target.GetComponent<FlightPhysicsController>();
            if (landingController == null && target != null)
                landingController = target.GetComponent<LandingController>();
            if (stallController == null && target != null)
                stallController = target.GetComponent<StallController>();
            if (windController == null)
                windController = FindAnyObjectByType<WindController>();
            if (airportLocation == null)
                airportLocation = FindAnyObjectByType<AirportLocationManager>();
            if (flightHUD == null)
                flightHUD = FindAnyObjectByType<FlightHUDController>();
            if (settings == null)
                settings = FindAnyObjectByType<GameSettingsManager>();
            if (gameFlow == null)
                gameFlow = FindAnyObjectByType<GameFlowController>();
        }

        private void ResolveFocusTarget()
        {
            if (focusTarget != null || target == null) return;
            Transform existing = target.Find("CameraFocusTarget");
            if (existing != null)
            {
                focusTarget = existing;
                CacheAircraftBounds();
                return;
            }

            CacheAircraftBounds(out Bounds bounds, out bool hasBounds);
            GameObject focusObject = new GameObject("CameraFocusTarget");
            focusTarget = focusObject.transform;
            focusTarget.SetParent(target, false);
            Vector3 worldPosition = hasBounds
                ? bounds.center + Vector3.up * bounds.extents.y * 0.18f
                : target.position + target.TransformVector(targetOffset);
            focusTarget.position = worldPosition;
            focusTarget.localPosition += focusTargetOffset;
        }

        private void ResolveCameraAnchors()
        {
            if (target == null) return;
            cockpitCameraAnchor = ResolveOrCreateAnchor(
                cockpitCameraAnchor,
                "CockpitCameraAnchor",
                new Vector3(0f, 0.78f, 0.9f));
            leftWingCameraAnchor = ResolveOrCreateAnchor(
                leftWingCameraAnchor,
                "LeftWingCameraAnchor",
                new Vector3(-4.25f, 0.2f, -0.15f));
            rightWingCameraAnchor = ResolveOrCreateAnchor(
                rightWingCameraAnchor,
                "RightWingCameraAnchor",
                new Vector3(4.25f, 0.2f, -0.15f));
        }

        private Transform ResolveOrCreateAnchor(
            Transform anchor,
            string anchorName,
            Vector3 localPosition)
        {
            if (anchor != null) return anchor;
            Transform existing = target.Find(anchorName);
            if (existing != null) return existing;
            GameObject anchorObject = new GameObject(anchorName);
            Transform created = anchorObject.transform;
            created.SetParent(target, false);
            created.localPosition = localPosition;
            created.localRotation = Quaternion.identity;
            return created;
        }

        private void CacheAircraftBounds()
        {
            CacheAircraftBounds(out _, out _);
        }

        private void CacheAircraftBounds(out Bounds bounds, out bool hasBounds)
        {
            bounds = default;
            hasBounds = false;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer)
                    continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (hasBounds)
                aircraftBoundsRadius = Mathf.Max(1f, bounds.extents.magnitude);
        }

        private void UpdateTargetTranslation()
        {
            Vector3 position = target.position;
            if (!targetPositionInitialized)
            {
                previousTargetPosition = position;
                targetPositionInitialized = true;
            }
            transform.position += position - previousTargetPosition;
            previousTargetPosition = position;
        }

        private void UpdateHeading()
        {
            Vector3 planar = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (planar.sqrMagnitude > 0.001f) headingForward = planar.normalized;
        }

        private void SmoothPosition(Vector3 desired)
        {
            float speed = cameraMode == CameraMode.Chase ||
                          cameraMode == CameraMode.CloseChase
                ? cameraSmoothness
                : collisionReturnSpeed;
            transform.position = Vector3.Lerp(
                transform.position, desired, Damp(speed));
        }

        private void SmoothRotation(Quaternion desired)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desired, Damp(rotationSmoothness));
        }

        private static float Damp(float speed) =>
            1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);

        private float DampAngle(float current, float destination, float speed) =>
            Mathf.LerpAngle(current, destination, Damp(speed));

        public void CycleCameraMode(int direction = 1)
        {
            int count = System.Enum.GetValues(typeof(CameraMode)).Length;
            int next = (int)cameraMode;
            for (int i = 0; i < count; i++)
            {
                next = (next + direction) % count;
                if (next < 0) next += count;
                CameraMode candidate = (CameraMode)next;
                if (!includeCloseChaseInCycle && candidate == CameraMode.CloseChase)
                    continue;
                if (!includeWingViewsInCycle &&
                    (candidate == CameraMode.LeftWing ||
                     candidate == CameraMode.RightWing))
                    continue;
                SetCameraMode(candidate);
                return;
            }
        }

        public void SetCameraMode(CameraMode mode, bool snap = false)
        {
            if (cameraMode == mode && !snap) return;
            transitionStartLocalPosition = target == null
                ? transform.position
                : target.InverseTransformPoint(transform.position);
            transitionStartLocalRotation = target == null
                ? transform.rotation
                : Quaternion.Inverse(target.rotation) * transform.rotation;
            cameraMode = mode;
            ResetOrbit();
            attachedLookYaw = 0f;
            attachedLookPitch = 0f;
            anchorPositionVelocity = Vector3.zero;
            landingSpringPosition = 0f;
            landingSpringVelocity = 0f;
            bankLeanVelocity = 0f;
            modeTransitionActive = !snap && target != null;
            modeTransitionStarted = Time.unscaledTime;
            modeLabelShownAt = Time.unscaledTime;
            settings?.NotifyCameraModeSelected(cameraMode);
            if (snap) SnapToTarget();
        }

        public void ResetOrbit()
        {
            yaw = defaultYaw;
            pitch = Mathf.Clamp(
                defaultPitchAngle, minPitchAngle, maxPitchAngle);
        }

        public void SnapToTarget()
        {
            ResetOrbit();
            if (target == null) return;
            ResolveReferences();
            ResolveFocusTarget();
            previousTargetPosition = target.position;
            targetPositionInitialized = true;
            UpdateHeading();
            Vector3 cameraAnchor = usePhase1ChaseCamera && focusTarget != null
                ? focusTarget.position
                : target.position + targetOffset;
            Vector3 focus = usePhase1ChaseCamera && focusTarget != null
                ? focusTarget.position
                : cameraAnchor + headingForward * baseLookAheadDistance;
            Quaternion heading = Quaternion.LookRotation(headingForward, Vector3.up);
            Quaternion orbit = heading * Quaternion.Euler(pitch, yaw, 0f);
            transform.position =
                cameraAnchor + orbit * Vector3.back * chaseDistance +
                Vector3.up * chaseHeight;
            transform.rotation = Quaternion.LookRotation(
                focus - transform.position, Vector3.up);
            orbitDistanceVelocity = 0f;
            currentOrbitOffset = transform.position - cameraAnchor;
            orbitOffsetInitialized = true;
        }

        public void SetAutoRecenterEnabled(bool enabled) =>
            enableAutoCameraRecenter = enabled;

        public void SetMouseSensitivity(float value) =>
            mouseSensitivity = Mathf.Clamp(value, 0.02f, 1f);

        public void SetInvertY(bool value) => invertY = value;

        public void ApplyPlayerSettings(CameraSettingsData data)
        {
            if (data == null) return;
            playerExternalFov = Mathf.Clamp(data.ExternalFov, 45f, 85f);
            playerCockpitFov = Mathf.Clamp(data.CockpitFov, 55f, 95f);
            playerCinematicFov = Mathf.Clamp(data.CinematicFov, 35f, 80f);
            externalSensitivityMultiplier = Mathf.Clamp(
                data.ExternalSensitivity, 0.25f, 2f);
            cockpitSensitivityMultiplier = Mathf.Clamp(
                data.CockpitSensitivity, 0.25f, 2f);
            cameraMotionIntensity = Mathf.Clamp01(data.MotionIntensity);
            cameraShakeEnabled = data.ShakeEnabled;
            enableAutoCameraRecenter = data.AutoRecenter;
            recenterDelay = Mathf.Clamp(data.RecenterDelay, 0f, 5f);
            recenterSpeed = Mathf.Clamp(data.RecenterSpeed, 0.25f, 8f);
            invertY = data.InvertExternalY;
            invertCockpitY = data.InvertCockpitY;
            showHUDInCinematicCameras = data.ShowCinematicHud;
            cinematicTransitionsEnabled = data.CinematicTransitions;
            includeWingViewsInCycle = data.IncludeWingViews;
            includeCloseChaseInCycle = data.IncludeCloseChase;
            float desiredFov = cinematicActive
                ? playerCinematicFov
                : cameraMode == CameraMode.Cockpit
                    ? playerCockpitFov
                    : playerExternalFov;
            if (controlledCamera != null) controlledCamera.fieldOfView = desiredFov;
        }

        private void OnGUI()
        {
            float elapsed = Time.unscaledTime - modeLabelShownAt;
            if (elapsed < 0f || elapsed > modeLabelDuration) return;
            float fadeStart = Mathf.Max(0f, modeLabelDuration - 0.35f);
            float alpha = elapsed <= fadeStart
                ? 1f
                : 1f - Mathf.InverseLerp(fadeStart, modeLabelDuration, elapsed);

            string label = cinematicActive
                ? cinematicMode switch
                {
                    CinematicMode.FlyBy => "FLY-BY CAMERA",
                    CinematicMode.RunwayObserver => "RUNWAY OBSERVER",
                    _ => "ORBIT SHOWCASE"
                }
                : cameraMode switch
                {
                    CameraMode.CloseChase => "CLOSE CHASE",
                    CameraMode.LeftWing => "LEFT WING",
                    CameraMode.RightWing => "RIGHT WING",
                    _ => cameraMode.ToString().ToUpperInvariant()
                };
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = new Color(0.55f, 0.95f, 1f, alpha);
            Texture2D oldBackground = style.normal.background;
            Color previousColor = GUI.color;
            GUI.color = new Color(0.03f, 0.12f, 0.16f, 0.86f * alpha);
            Rect rect = new Rect(Screen.width * 0.5f - 85f, 54f, 170f, 34f);
            GUI.Box(rect, GUIContent.none, style);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            style.normal.background = oldBackground;
            GUI.Label(rect, label, style);
            GUI.color = previousColor;
        }
    }
}
