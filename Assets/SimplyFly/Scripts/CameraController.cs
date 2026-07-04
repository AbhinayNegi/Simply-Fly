using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private AircraftInput input;
        [Header("Framing")]
        [Min(1f)] [SerializeField] private float followDistance = 12f;
        [SerializeField] private float followHeight = 4f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.7f, 0f);
        [Header("Feel")]
        [Min(0.01f)] [SerializeField] private float movementSmoothing = 4.5f;
        [Min(0.01f)] [SerializeField] private float rotationSmoothing = 7f;
        [Min(0f)] [SerializeField] private float cameraLag = 0.35f;
        [Min(0.01f)] [SerializeField] private float mouseSensitivity = 0.18f;
        [SerializeField] private float defaultYaw = -90f;
        [SerializeField] private float defaultPitch = 14f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-10f, 65f);
        [Header("Pilot awareness - quick look")]
        [SerializeField] private Key quickLookLeftKey = Key.Z;
        [SerializeField] private Key quickLookRightKey = Key.X;
        [SerializeField] private Key recenterKey = Key.C;
        [Range(20f, 150f)] [SerializeField] private float quickLookAngle = 80f;
        [Min(0.1f)] [SerializeField] private float quickLookSpeed = 10f;
        [Min(0.1f)] [SerializeField] private float cameraReturnSpeed = 7f;
        [Min(0f)] [SerializeField] private float cameraRecenterDelay = 0.15f;
        [SerializeField] private bool enableAutoCameraRecenter = true;
        [Header("Pilot awareness - auto recenter")]
        [SerializeField] private bool autoRecenterEnabled = true;
        [Min(0f)] [SerializeField] private float autoRecenterDelay = 2.5f;
        [Min(0.1f)] [SerializeField] private float autoRecenterStrength = 1.8f;
        [SerializeField] private PilotAwarenessAssist awarenessAssist;

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
            if (target != null) targetBody = target.GetComponent<Rigidbody>();
            if (awarenessAssist == null && target != null)
                awarenessAssist = target.GetComponent<PilotAwarenessAssist>();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool manualOrbit = false;
            if (input != null)
            {
                Vector2 look = input.CameraLookDelta * mouseSensitivity;
                manualOrbit = look.sqrMagnitude > 0.0001f;
                if (manualOrbit)
                {
                    yaw += look.x;
                    pitch = Mathf.Clamp(
                        pitch - look.y, pitchLimits.x, pitchLimits.y);
                    lastCameraInputTime = Time.unscaledTime;
                }
                if (input.ResetCameraPressed) ResetOrbit();
            }

            bool quickLeft =
                keyboard != null && keyboard[quickLookLeftKey].isPressed;
            bool quickRight =
                keyboard != null && keyboard[quickLookRightKey].isPressed;
            bool quickLookActive = quickLeft ^ quickRight;
            if (quickLookActive)
            {
                returningFromQuickLook = false;
                float quickTarget =
                    defaultYaw + (quickRight ? quickLookAngle : -quickLookAngle);
                yaw = DampAngle(
                    yaw, quickTarget, quickLookSpeed, Time.unscaledDeltaTime);
                lastCameraInputTime = Time.unscaledTime;
            }
            else
            {
                if (quickLookWasActive) returningFromQuickLook = true;
                if (returningFromQuickLook &&
                    Time.unscaledTime - lastCameraInputTime >=
                    cameraRecenterDelay)
                {
                    yaw = DampAngle(
                        yaw, defaultYaw, cameraReturnSpeed, Time.unscaledDeltaTime);
                    pitch = DampAngle(
                        pitch, defaultPitch, cameraReturnSpeed, Time.unscaledDeltaTime);
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, defaultYaw)) < 0.25f &&
                        Mathf.Abs(Mathf.DeltaAngle(pitch, defaultPitch)) < 0.25f)
                    {
                        yaw = defaultYaw;
                        pitch = defaultPitch;
                        returningFromQuickLook = false;
                    }
                }
            }
            quickLookWasActive = quickLookActive;

            bool explicitRecenter =
                keyboard != null && keyboard[recenterKey].wasPressedThisFrame ||
                mouse != null && mouse.middleButton.wasPressedThisFrame;
            if (explicitRecenter)
            {
                ResetOrbit();
                returningFromQuickLook = false;
                lastCameraInputTime = Time.unscaledTime;
            }
            else if (!quickLookActive && !manualOrbit &&
                     enableAutoCameraRecenter && autoRecenterEnabled &&
                     Time.unscaledTime - lastCameraInputTime >=
                     autoRecenterDelay)
            {
                yaw = DampAngle(
                    yaw,
                    defaultYaw,
                    autoRecenterStrength,
                    Time.unscaledDeltaTime);
                pitch = DampAngle(
                    pitch,
                    defaultPitch,
                    autoRecenterStrength,
                    Time.unscaledDeltaTime);
            }

            Vector3 targetPosition = target.position;
            if (!targetPositionInitialized)
            {
                previousTargetPosition = targetPosition;
                targetPositionInitialized = true;
            }

            // Inherit target translation immediately. Smoothing only the relative camera
            // offset prevents follow error from growing with aircraft speed.
            Vector3 targetDelta = targetPosition - previousTargetPosition;
            transform.position += targetDelta;
            previousTargetPosition = targetPosition;

            Vector3 planarForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (planarForward.sqrMagnitude > 0.001f)
                headingForward = planarForward.normalized;
            Quaternion heading = Quaternion.LookRotation(headingForward, Vector3.up);
            Quaternion orbit = heading * Quaternion.Euler(pitch, yaw, 0f);

            Vector3 lagDirection = targetBody != null &&
                                   targetBody.linearVelocity.sqrMagnitude > 0.1f
                ? -targetBody.linearVelocity.normalized
                : -headingForward;
            Vector3 focus = targetPosition + targetOffset + lagDirection * cameraLag;
            float shake = awarenessAssist == null
                ? 0f
                : awarenessAssist.CameraShake;
            Vector3 shakeOffset = Vector3.zero;
            if (shake > 0.0001f)
            {
                float time = Time.unscaledTime;
                shakeOffset =
                    transform.right *
                    ((Mathf.PerlinNoise(time * 19f, 0.13f) * 2f - 1f) * shake) +
                    transform.up *
                    ((Mathf.PerlinNoise(0.37f, time * 23f) * 2f - 1f) * shake);
            }
            Vector3 desiredPosition =
                focus + orbit * Vector3.back * followDistance +
                Vector3.up * followHeight + shakeOffset;
            if (awarenessAssist != null)
            {
                desiredPosition -=
                    headingForward *
                    awarenessAssist.LongitudinalCameraCue;
            }
            float targetBlend = 1f - Mathf.Exp(-movementSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, targetBlend);

            float cameraLean = awarenessAssist == null
                ? 0f
                : awarenessAssist.CameraLeanDegrees;
            Quaternion desiredRotation =
                Quaternion.LookRotation(focus - transform.position, Vector3.up) *
                Quaternion.AngleAxis(cameraLean, Vector3.forward);
            float rotationBlend = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
        }

        public void ResetOrbit()
        {
            yaw = defaultYaw;
            pitch = defaultPitch;
        }

        private static float DampAngle(
            float current,
            float target,
            float speed,
            float deltaTime)
        {
            float blend = 1f - Mathf.Exp(-speed * deltaTime);
            return Mathf.LerpAngle(current, target, blend);
        }

        public void SnapToTarget()
        {
            ResetOrbit();
            if (target == null) return;

            targetBody = target.GetComponent<Rigidbody>();
            previousTargetPosition = target.position;
            targetPositionInitialized = true;
            Vector3 planarForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            headingForward = planarForward.sqrMagnitude > 0.001f
                ? planarForward.normalized
                : Vector3.forward;
            Quaternion heading = Quaternion.LookRotation(headingForward, Vector3.up);
            Quaternion orbit = heading * Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + targetOffset;
            transform.position =
                focus + orbit * Vector3.back * followDistance + Vector3.up * followHeight;
            transform.rotation = Quaternion.LookRotation(
                focus - transform.position, Vector3.up);
        }

        public void SetAutoRecenterEnabled(bool enabled)
        {
            enableAutoCameraRecenter = enabled;
            autoRecenterEnabled = enabled;
        }

        public void SetMouseSensitivity(float value)
        {
            mouseSensitivity = Mathf.Clamp(value, 0.02f, 1f);
        }
    }
}
