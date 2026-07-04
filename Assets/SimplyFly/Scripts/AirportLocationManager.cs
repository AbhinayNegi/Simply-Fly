using System;
using UnityEngine;

namespace SimplyFly.GroundOps
{
    [Serializable]
    public sealed class AirportDefinition
    {
        public string airportName;
        public float runwayHeading;
        public float runwayLength;
        public float runwayWidth;
        public Vector3 aircraftSpawnPosition;
        public Vector3 parkingSpawnPosition;
        public Vector3 parkingSpawnEuler;
        public Vector3 finalApproachSpawnPosition;
        public float finalApproachAltitude = 100f;
        public float finalApproachSpeed = 32f;
        public float finalApproachVerticalSpeed = -1.5f;
        public float finalApproachThrottle = 0.45f;
        public Vector3 runwayThresholdPosition;
        public Transform runwayThreshold;
        public Transform runwayCenterline;
        public float patternAltitude = 300f;
        public float recommendedApproachSpeed = 60f;
        public GameObject environmentRoot;

        public Quaternion RunwayRotation =>
            Quaternion.Euler(0f, runwayHeading, 0f);
    }

    public sealed class AirportLocationManager : MonoBehaviour
    {
        [SerializeField] private AirportDefinition[] airports;
        [SerializeField] private AircraftResetController resetController;
        [SerializeField] private ApproachGuidanceController approachGuidance;
        [SerializeField] private int selectedAirportIndex;

        public event Action AirportChanged;

        public AirportDefinition[] Airports => airports;
        public int SelectedAirportIndex => selectedAirportIndex;
        public AirportDefinition CurrentAirport =>
            airports == null || airports.Length == 0
                ? null
                : airports[Mathf.Clamp(selectedAirportIndex, 0, airports.Length - 1)];

        private void Awake()
        {
            if (resetController == null)
                resetController = FindAnyObjectByType<AircraftResetController>();
            if (approachGuidance == null && resetController != null)
                approachGuidance =
                    resetController.GetComponent<ApproachGuidanceController>();
            ApplySelectedAirport(false);
        }

        public void SelectAirport(int index, bool spawnAtRunway = true)
        {
            if (airports == null || airports.Length == 0) return;
            selectedAirportIndex = Mathf.Clamp(index, 0, airports.Length - 1);
            ApplySelectedAirport(spawnAtRunway);
        }

        public void SpawnAtRunway()
        {
            AirportDefinition airport = CurrentAirport;
            if (airport == null || resetController == null) return;
            resetController.SpawnAtRunway(
                airport.aircraftSpawnPosition,
                airport.RunwayRotation);
        }

        public void SpawnAtParking()
        {
            AirportDefinition airport = CurrentAirport;
            if (airport == null || resetController == null) return;
            resetController.SpawnAtRunway(
                airport.parkingSpawnPosition,
                Quaternion.Euler(airport.parkingSpawnEuler));
        }

        public void SpawnOnFinalApproach()
        {
            AirportDefinition airport = CurrentAirport;
            if (airport == null || resetController == null) return;
            Vector3 position =
                airport.finalApproachSpawnPosition +
                Vector3.up * airport.finalApproachAltitude;
            Vector3 velocity =
                airport.RunwayRotation * Vector3.forward *
                airport.finalApproachSpeed +
                Vector3.up * airport.finalApproachVerticalSpeed;
            resetController.SpawnAirborneAt(
                position,
                airport.RunwayRotation,
                velocity,
                airport.finalApproachThrottle);
        }

        public void SpawnAirborne()
        {
            AirportDefinition airport = CurrentAirport;
            if (airport == null || resetController == null) return;
            Vector3 forward = airport.RunwayRotation * Vector3.forward;
            Vector3 right = airport.RunwayRotation * Vector3.right;
            Vector3 position =
                airport.runwayThresholdPosition -
                forward * 900f +
                right * 650f +
                Vector3.up * Mathf.Max(180f, airport.patternAltitude);
            Vector3 velocity = forward * 42f;
            resetController.SpawnAirborneAt(
                position,
                airport.RunwayRotation,
                velocity,
                0.72f);
        }

        private void ApplySelectedAirport(bool spawnAtRunway)
        {
            if (airports == null || airports.Length == 0) return;
            selectedAirportIndex =
                Mathf.Clamp(selectedAirportIndex, 0, airports.Length - 1);
            for (int i = 0; i < airports.Length; i++)
            {
                AirportDefinition definition = airports[i];
                if (definition?.environmentRoot != null)
                    definition.environmentRoot.SetActive(i == selectedAirportIndex);
            }

            AirportDefinition airport = CurrentAirport;
            if (airport != null && approachGuidance != null)
                approachGuidance.SetRunwayThreshold(airport.runwayThreshold);
            AirportChanged?.Invoke();
            if (spawnAtRunway) SpawnAtRunway();
        }
    }
}
