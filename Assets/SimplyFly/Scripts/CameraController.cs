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
        private Vector3 smoothedTarget;

        private void Start()
        {
            ResetOrbit();
            if (target != null) smoothedTarget = target.position;
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

            float targetBlend = 1f - Mathf.Exp(-movementSmoothing * Time.deltaTime);
            smoothedTarget = Vector3.Lerp(smoothedTarget, target.position, targetBlend);
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = smoothedTarget + targetOffset - target.forward * cameraLag;
            Vector3 desiredPosition = focus + orbit * Vector3.back * followDistance + Vector3.up * followHeight;
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
    }
}
