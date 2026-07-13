using UnityEngine;

namespace SimplyFly.GroundOps
{
    /// <summary>IAS data adapter for the reusable numeric tape presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class AirspeedTapeHUD : MonoBehaviour
    {
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private NumericTapeHUD tape;

        public void Bind(FlightPhysicsController source) => flightPhysics = source;

        private void Awake()
        {
            if (tape == null) tape = GetComponent<NumericTapeHUD>();
            if (tape == null) tape = gameObject.AddComponent<NumericTapeHUD>();
            if (flightPhysics == null) flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
        }

        private void Update()
        {
            tape?.SetValue(flightPhysics == null ? 0f : flightPhysics.AirspeedKnots);
        }
    }
}
