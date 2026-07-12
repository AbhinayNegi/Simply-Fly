using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace SimplyFly.GroundOps
{
    public sealed class CameraController : MonoBehaviour
    {
        public enum CameraMode
        {
            Chase,
            Landing,
            NoseCockpit,
            FreeOrbit,
            TopDownMap
        }

        [SerializeField] private Transform target;
        [SerializeField] private AircraftInput input;

        [Header("Chase framing")]
        [FormerlySerializedAs("followDistance")]
        [Min(1f)] [SerializeField] private float chaseDistance = 14.5f;
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

        [Header("Look ahead")]
        [Min(0f)] [SerializeField] private float baseLookAheadDistance = 5f;
        [Min(0f)] [SerializeField] private float speedLookAheadMultiplier = 0.08f;
        [Min(0f)] [SerializeField] private float maxLookAheadDistance = 11f;

        [Header("Landing camera")]
        [Min(1f)] [SerializeField] private float landingDistance = 11f;
        [SerializeField] private float landingHeight = 1.6f;
        [SerializeField] private float landingPitchAngle = 3f;
        [Min(0f)] [SerializeField] private float landingLookAhead = 9f;

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
        [SerializeField] private Key cycleModeKey = Key.C;
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

        public CameraMode CurrentMode => cameraMode;

        private float yaw;
        private float pitch;
        private float lastCameraInputTime;
        private bool quickLookWasActive;
        private bool returningFromQuickLook;
        private Vector3 previousTargetPosition;
        private Vector3 headingForward = Vector3.forward;
        private bool targetPositionInitialized;
        private Rigidbody targetBody;

        private void Start()
        {
            ResolveReferences();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            ResolveReferences();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard[cycleModeKey].wasPressedThisFrame)
                CycleCameraMode();

            bool orbitAllowed =
                cameraMode == CameraMode.Chase ||
                cameraMode == CameraMode.FreeOrbit;
            bool manualOrbit = false;
            if (orbitAllowed && input != null)
            {
                Vector2 look = input.CameraLookDelta * mouseSensitivity;
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

            bool explicitRecenter =
                (input != null && input.ResetCameraPressed) ||
                (keyboard != null && keyboard[recenterKey].wasPressedThisFrame) ||
                (mouse != null && mouse.middleButton.wasPressedThisFrame);
            if (explicitRecenter)
            {
                ResetOrbit();
                returningFromQuickLook = false;
                lastCameraInputTime = Time.unscaledTime;
            }

            UpdateQuickLookAndRecenter(
                keyboard, orbitAllowed, manualOrbit, explicitRecenter);
            UpdateTargetTranslation();
            UpdateHeading();

            switch (cameraMode)
            {
                case CameraMode.NoseCockpit:
                    UpdateNoseCamera();
                    break;
                case CameraMode.TopDownMap:
                    UpdateTopDownCamera();
                    break;
                default:
                    UpdateExternalCamera();
                    break;
            }
        }

        private void UpdateExternalCamera()
        {
            bool landing = cameraMode == CameraMode.Landing;
            float modePitch = landing ? landingPitchAngle : pitch;
            float distance = landing ? landingDistance : chaseDistance;
            float height = landing ? landingHeight : chaseHeight;

            Quaternion heading = Quaternion.LookRotation(headingForward, Vector3.up);
            Quaternion orbit = heading * Quaternion.Euler(modePitch, yaw, 0f);
            float speed = targetBody == null ? 0f : targetBody.linearVelocity.magnitude;
            float dynamicLookAhead = Mathf.Min(
                maxLookAheadDistance,
                baseLookAheadDistance +
                chaseLookAhead +
                speed * speedLookAheadMultiplier);
            if (landing) dynamicLookAhead = Mathf.Max(dynamicLookAhead, landingLookAhead);

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
            bool explicitRecenter)
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

            if (!explicitRecenter && !quickLookActive && !manualOrbit &&
                enableAutoCameraRecenter &&
                Time.unscaledTime - lastCameraInputTime >= autoRecenterDelay)
            {
                yaw = DampAngle(yaw, defaultYaw, autoRecenterStrength);
                pitch = DampAngle(
                    pitch, defaultPitchAngle, autoRecenterStrength);
            }
        }

        private Vector3 ResolveCollision(Vector3 focus, Vector3 desired)
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
                        Mathf.Max(0.1f, hit.distance - collisionPadding));
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
            float shake = awarenessAssist == null
                ? 0f
                : awarenessAssist.CameraShake;
            if (shake <= 0.0001f) return Vector3.zero;
            float time = Time.unscaledTime;
            return
                transform.right *
                ((Mathf.PerlinNoise(time * 19f, 0.13f) * 2f - 1f) * shake) +
                transform.up *
                ((Mathf.PerlinNoise(0.37f, time * 23f) * 2f - 1f) * shake);
        }

        private void ResolveReferences()
        {
            if (targetBody == null && target != null)
                targetBody = target.GetComponent<Rigidbody>();
            if (awarenessAssist == null && target != null)
                awarenessAssist = target.GetComponent<PilotAwarenessAssist>();
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
                          cameraMode == CameraMode.Landing
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

        public void CycleCameraMode()
        {
            cameraMode = (CameraMode)(((int)cameraMode + 1) %
                                      System.Enum.GetValues(typeof(CameraMode)).Length);
            if (cameraMode != CameraMode.FreeOrbit) ResetOrbit();
            PlayerPrefs.SetInt("settings.cameraMode", (int)cameraMode);
            PlayerPrefs.Save();
        }

        public void SetCameraMode(CameraMode mode, bool snap = false)
        {
            cameraMode = mode;
            ResetOrbit();
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
            previousTargetPosition = target.position;
            targetPositionInitialized = true;
            UpdateHeading();
            Vector3 cameraAnchor = target.position + targetOffset;
            Vector3 focus =
                cameraAnchor + headingForward * baseLookAheadDistance;
            Quaternion heading = Quaternion.LookRotation(headingForward, Vector3.up);
            Quaternion orbit = heading * Quaternion.Euler(pitch, yaw, 0f);
            transform.position =
                cameraAnchor + orbit * Vector3.back * chaseDistance +
                Vector3.up * chaseHeight;
            transform.rotation = Quaternion.LookRotation(
                focus - transform.position, Vector3.up);
        }

        public void SetAutoRecenterEnabled(bool enabled) =>
            enableAutoCameraRecenter = enabled;

        public void SetMouseSensitivity(float value) =>
            mouseSensitivity = Mathf.Clamp(value, 0.02f, 1f);

        public void SetInvertY(bool value) => invertY = value;
    }
}
