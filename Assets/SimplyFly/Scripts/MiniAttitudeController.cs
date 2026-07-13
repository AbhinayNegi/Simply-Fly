using UnityEngine;

namespace SimplyFly.GroundOps
{
    /// <summary>Adapts flight-model attitude values to the miniature HUD view.</summary>
    [DisallowMultipleComponent]
    public sealed class MiniAttitudeController : MonoBehaviour
    {
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private MiniAttitudeHUD view;

        public void Bind(FlightPhysicsController source) => flightPhysics = source;

        private void Awake()
        {
            if (view == null) view = GetComponent<MiniAttitudeHUD>();
            if (view == null) view = gameObject.AddComponent<MiniAttitudeHUD>();
            if (flightPhysics == null)
                flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
        }

        private void Update()
        {
            view?.SetAttitude(
                flightPhysics == null ? 0f : flightPhysics.PitchAngle,
                flightPhysics == null ? 0f : flightPhysics.BankAngle);
        }
    }
}
