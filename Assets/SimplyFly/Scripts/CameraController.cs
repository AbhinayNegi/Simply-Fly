using UnityEngine;

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

        private float yaw;
        private float pitch;
        private Vector3 previousTargetPosition;
        private Vector3 headingForward = Vector3.forward;
        private bool targetPositionInitialized;
        private Rigidbody targetBody;

        private void Start()
        {
            if (target != null) targetBody = target.GetComponent<Rigidbody>();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (input != null)
            {
                Vector2 look = input.CameraLookDelta * mouseSensitivity;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, pitchLimits.x, pitchLimits.y);
                if (input.ResetCameraPressed) ResetOrbit();
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
            Vector3 desiredPosition = focus + orbit * Vector3.back * followDistance + Vector3.up * followHeight;
            float targetBlend = 1f - Mathf.Exp(-movementSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, targetBlend);

            Quaternion desiredRotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
            float rotationBlend = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
        }

        public void ResetOrbit()
        {
            yaw = defaultYaw;
            pitch = defaultPitch;
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
    }
}
