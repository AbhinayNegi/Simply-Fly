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
        [Header("Navigation")]
        public Vector3 airportPosition;
        public Vector3 climbOutWaypoint;
        public Vector3 arrivalEntryWaypoint;
        public Vector3 finalApproachFix;
        public Vector3 runwayThresholdTargetPoint;
        public float patternAltitude = 300f;
        public float recommendedApproachSpeed = 60f;
        public GameObject environmentRoot;

        public Quaternion RunwayRotation =>
            Quaternion.Euler(0f, runwayHeading, 0f);

        public Vector3 Forward => RunwayRotation * Vector3.forward;
        public Vector3 ThresholdPoint =>
            runwayThresholdTargetPoint == Vector3.zero
                ? runwayThresholdPosition
                : runwayThresholdTargetPoint;

        public Vector3 ClimbOutPoint =>
            climbOutWaypoint == Vector3.zero
                ? ThresholdPoint + Forward * 1400f + Vector3.up * patternAltitude
                : climbOutWaypoint;

        public Vector3 ArrivalEntryPoint =>
            arrivalEntryWaypoint == Vector3.zero
                ? ThresholdPoint - Forward * 5000f +
                  RunwayRotation * Vector3.right * 1800f +
                  Vector3.up * patternAltitude
                : arrivalEntryWaypoint;

        public Vector3 FinalApproachFixPoint =>
            finalApproachFix == Vector3.zero
                ? ThresholdPoint - Forward * 3000f +
                  Vector3.up * Mathf.Tan(3f * Mathf.Deg2Rad) * 3000f
                : finalApproachFix;
    }

    public sealed class AirportLocationManager : MonoBehaviour
    {
        [SerializeField] private AirportDefinition[] airports;
        [SerializeField] private AircraftResetController resetController;
        [SerializeField] private ApproachGuidanceController approachGuidance;
        [SerializeField] private int selectedAirportIndex;
        [SerializeField] private bool activeRunwayReciprocal;
        private Transform reciprocalRunwayReference;

        public event Action AirportChanged;

        public AirportDefinition[] Airports => airports;
        public int SelectedAirportIndex => selectedAirportIndex;
        public AirportDefinition CurrentAirport =>
            airports == null || airports.Length == 0
                ? null
                : airports[Mathf.Clamp(selectedAirportIndex, 0, airports.Length - 1)];
        public bool ActiveRunwayReciprocal => activeRunwayReciprocal;
        public float ActiveRunwayHeading => CurrentAirport == null
            ? 0f
            : Mathf.Repeat(
                CurrentAirport.runwayHeading +
                (activeRunwayReciprocal ? 180f : 0f), 360f);
        public string ActiveRunwayName => FormatRunway(ActiveRunwayHeading);

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

        public void SetRouteAirports(int departureIndex, int arrivalIndex)
        {
            if (airports == null) return;
            for (int i = 0; i < airports.Length; i++)
            {
                AirportDefinition definition = airports[i];
                if (definition?.environmentRoot != null)
                    definition.environmentRoot.SetActive(
                        i == departureIndex || i == arrivalIndex);
            }
        }

        public void SpawnAtRunway()
        {
            AirportDefinition airport = CurrentAirport;
            if (airport == null || resetController == null) return;
            if (activeRunwayReciprocal)
            {
                Vector3 primaryForward = airport.Forward;
                Vector3 position = airport.aircraftSpawnPosition +
                                   primaryForward * Mathf.Max(100f, airport.runwayLength - 90f);
                resetController.SpawnAtRunway(
                    position,
                    Quaternion.Euler(0f, ActiveRunwayHeading, 0f));
                return;
            }
            resetController.SpawnAtRunway(
                airport.aircraftSpawnPosition,
                airport.RunwayRotation);
        }

        public void SetActiveRunway(bool reciprocal)
        {
            activeRunwayReciprocal = reciprocal;
            UpdateApproachRunwayReference();
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
            Quaternion runwayRotation =
                Quaternion.Euler(0f, ActiveRunwayHeading, 0f);
            Vector3 forward = runwayRotation * Vector3.forward;
            Vector3 position;
            if (activeRunwayReciprocal)
            {
                Vector3 reciprocalThreshold =
                    airport.ThresholdPoint + airport.Forward * airport.runwayLength;
                position = reciprocalThreshold - forward * 1600f +
                           Vector3.up * airport.finalApproachAltitude;
            }
            else
            {
                position = airport.finalApproachSpawnPosition +
                           Vector3.up * airport.finalApproachAltitude;
            }
            Vector3 velocity =
                forward *
                airport.finalApproachSpeed +
                Vector3.up * airport.finalApproachVerticalSpeed;
            resetController.SpawnAirborneAt(
                position,
                runwayRotation,
                velocity,
                airport.finalApproachThrottle);
        }

        public void SpawnAirborne()
        {
            AirportDefinition airport = CurrentAirport;
            if (airport == null || resetController == null) return;
            Quaternion runwayRotation =
                Quaternion.Euler(0f, ActiveRunwayHeading, 0f);
            Vector3 forward = runwayRotation * Vector3.forward;
            Vector3 right = runwayRotation * Vector3.right;
            Vector3 position =
                GetActiveThreshold(airport) -
                forward * 900f +
                right * 650f +
                Vector3.up * Mathf.Max(180f, airport.patternAltitude);
            Vector3 velocity = forward * 42f;
            resetController.SpawnAirborneAt(
                position,
                runwayRotation,
                velocity,
                0.72f);
        }

        public void SpawnAirborneAt(
            Vector3 position,
            Quaternion rotation,
            Vector3 velocity,
            float throttle)
        {
            resetController?.SpawnAirborneAt(
                position, rotation, velocity, throttle);
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

            UpdateApproachRunwayReference();
            AirportChanged?.Invoke();
            if (spawnAtRunway) SpawnAtRunway();
        }

        public Vector3 GetActiveThreshold(AirportDefinition airport = null)
        {
            airport ??= CurrentAirport;
            if (airport == null) return Vector3.zero;
            return activeRunwayReciprocal
                ? airport.ThresholdPoint + airport.Forward * airport.runwayLength
                : airport.ThresholdPoint;
        }

        public static string FormatRunway(float heading)
        {
            int number = Mathf.RoundToInt(Mathf.Repeat(heading, 360f) / 10f);
            if (number <= 0) number = 36;
            return $"RWY {number:00}";
        }

        private void UpdateApproachRunwayReference()
        {
            AirportDefinition airport = CurrentAirport;
            if (airport == null || approachGuidance == null) return;
            if (!activeRunwayReciprocal)
            {
                approachGuidance.SetRunwayThreshold(airport.runwayThreshold);
                return;
            }
            if (reciprocalRunwayReference == null)
            {
                GameObject reference = new("Active Reciprocal Runway Threshold");
                reciprocalRunwayReference = reference.transform;
                reciprocalRunwayReference.SetParent(transform, false);
            }
            reciprocalRunwayReference.SetPositionAndRotation(
                airport.ThresholdPoint + airport.Forward * airport.runwayLength,
                Quaternion.Euler(0f, ActiveRunwayHeading, 0f));
            approachGuidance.SetRunwayThreshold(reciprocalRunwayReference);
        }
    }
}
