using System;
using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class WheelVisualController : MonoBehaviour
    {
        [Serializable]
        private struct WheelBinding
        {
            public WheelCollider collider;
            public Transform visual;
            [Tooltip("Wheel axle in the visual's authored local space.")]
            public Vector3 visualAxle;
        }

        [SerializeField] private WheelBinding[] wheels;

        private Quaternion[] authoredRotations;
        private Vector3[] authoredPositions;
        private float[] spinAngles;

        private void Awake()
        {
            if (wheels == null) return;
            authoredRotations = new Quaternion[wheels.Length];
            authoredPositions = new Vector3[wheels.Length];
            spinAngles = new float[wheels.Length];

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i].visual == null) continue;
                authoredRotations[i] = wheels[i].visual.localRotation;
                authoredPositions[i] = wheels[i].visual.localPosition;
            }
        }

        private void LateUpdate()
        {
            if (wheels == null || authoredRotations == null) return;
            for (int i = 0; i < wheels.Length; i++)
            {
                WheelBinding wheel = wheels[i];
                if (wheel.collider == null || wheel.visual == null) continue;

                // Keep the imported mesh orientation. WheelCollider.GetWorldPose returns an
                // orientation in WheelCollider space, which turned this +X-forward model's
                // tire meshes sideways.
                spinAngles[i] = Mathf.Repeat(spinAngles[i] + wheel.collider.rpm * 6f * Time.deltaTime, 360f);
                Vector3 axle = wheel.visualAxle.sqrMagnitude > 0.01f
                    ? wheel.visualAxle.normalized
                    : Vector3.forward;
                Quaternion steering = Quaternion.AngleAxis(wheel.collider.steerAngle, Vector3.up);
                wheel.visual.localRotation =
                    steering * authoredRotations[i] * Quaternion.AngleAxis(spinAngles[i], axle);

                // Follow suspension vertically without allowing the collider to rewrite the
                // tire's lateral/longitudinal authored placement.
                wheel.collider.GetWorldPose(out Vector3 worldPosition, out _);
                Vector3 localPose = wheel.visual.parent.InverseTransformPoint(worldPosition);
                Vector3 localPosition = authoredPositions[i];
                localPosition.y = localPose.y;
                wheel.visual.localPosition = localPosition;
            }
        }
    }
}
