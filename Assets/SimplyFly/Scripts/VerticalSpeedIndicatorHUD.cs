using UnityEngine;

namespace SimplyFly.GroundOps
{
    [DisallowMultipleComponent]
    public sealed class VerticalSpeedIndicatorHUD : MonoBehaviour
    {
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private VerticalScaleHUD scale;

        public void Bind(FlightPhysicsController source) => flightPhysics = source;

        private void Awake()
        {
            if (scale == null) scale = GetComponent<VerticalScaleHUD>();
            if (scale == null) scale = gameObject.AddComponent<VerticalScaleHUD>();
            scale.Configure(-10f, 10f, 1f, 2, 15f,
                "VERTICAL SPEED", "M/S", "+0.0;-0.0;0.0");
            if (flightPhysics == null) flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
        }

        private void Update() =>
            scale?.SetValue(flightPhysics == null ? 0f : flightPhysics.VerticalSpeed);
    }
}
