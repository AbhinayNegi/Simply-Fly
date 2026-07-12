using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimplyFly.GroundOps
{
    public enum NavigationRouteType { DirectGPS, SimpleVFR }
    public enum AltitudeReferenceType { MSL, RunwayRelativeAGL }
    public enum NavigationPhase
    {
        Inactive, Departure, Enroute, Arrival, FinalIntercept,
        FinalApproach, Completed
    }

    [Serializable]
    public sealed class NavigationWaypoint
    {
        public string name;
        public Vector3 position;
        public float captureRadius = 400f;
        public NavigationPhase phase;
        public float targetAltitude;
        public AltitudeReferenceType altitudeReference;
        public float altitudeTolerance = 30f;
        public float recommendedSpeed;
        public string instruction;
        public bool altitudeRaisedForTerrain;
    }

    public sealed class NavigationManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AirportLocationManager airports;
        [SerializeField] private Transform aircraft;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private LandingController landing;

        [Header("Route")]
        [SerializeField] private bool navigationEnabled = true;
        [SerializeField] private float normalCaptureRadius = 400f;
        [SerializeField] private float arrivalCaptureRadius = 1200f;
        [SerializeField] private float finalCaptureRadius = 300f;
        [SerializeField] private float glideSlopeDegrees = 3f;
        [SerializeField] private float finalApproachDistance = 4000f;

        [Header("Vertical navigation")]
        [SerializeField] private float defaultCruiseAltitudeMeters = 300f;
        [SerializeField] private float defaultPatternAltitudeMeters = 280f;
        [SerializeField] private float defaultClimbOutAltitudeMeters = 150f;
        [SerializeField] private float defaultArrivalAltitudeMeters = 250f;
        [SerializeField] private float minimumSafeTerrainClearanceMeters = 100f;
        [SerializeField] private float altitudeToleranceMeters = 30f;
        [SerializeField] private float finalAltitudeToleranceMeters = 10f;

        [Header("Automatic cruise planning")]
        [SerializeField] private float cruiseTerrainClearanceMeters = 140f;
        [SerializeField] private float shortRouteCruiseAltitudeMeters = 180f;
        [SerializeField] private float normalRouteCruiseAltitudeMeters = 300f;
        [SerializeField] private float longRouteCruiseAltitudeMeters = 420f;
        [SerializeField] private float assumedClimbGradient = 0.075f;
        [SerializeField] private float assumedDescentGradient = 0.052f;
        [SerializeField] private float minimumCruiseSegmentMeters = 1200f;
        [Range(12, 80)] [SerializeField] private int cruiseTerrainSamples = 40;

        [Header("Approach validation")]
        [SerializeField] private bool approachValidationEnabled = true;
        [Range(2f, 4f)] [SerializeField] private float approachDistanceKm = 3f;
        [SerializeField] private float approachCorridorWidthFar = 300f;
        [SerializeField] private float approachCorridorWidthNear = 80f;
        [SerializeField] private float approachVerticalClearanceAbove = 60f;
        [SerializeField] private float approachVerticalClearanceBelow = 30f;
        [SerializeField] private float approachObstacleMargin = 20f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;
        [Range(12, 80)] [SerializeField] private int validationSamples = 36;
        [Min(0f)] [SerializeField] private float terrainMinimumClearance = 18f;
        [Min(0f)] [SerializeField] private float terrainCheckStartDistance = 500f;

        [Header("Touchdown guidance")]
        [SerializeField] private bool useTouchdownPointForGlideSlope = true;
        [Range(100f, 400f)]
        [SerializeField] private float touchdownTargetDistanceFromThresholdMeters =
            220f;
        [Range(5f, 25f)]
        [SerializeField] private float thresholdCrossingHeightMeters = 12f;
        [Range(1f, 15f)]
        [SerializeField] private float thresholdCrossingHeightToleranceMeters = 5f;

        public bool IsActive { get; private set; }
        public NavigationPhase Phase { get; private set; }
        public int DepartureAirportIndex { get; private set; }
        public int ArrivalAirportIndex { get; private set; }
        public NavigationRouteType RouteType { get; private set; }
        public int WaypointIndex { get; private set; }
        public IReadOnlyList<NavigationWaypoint> Waypoints => waypoints;
        public NavigationWaypoint CurrentWaypoint =>
            WaypointIndex >= 0 && WaypointIndex < waypoints.Count
                ? waypoints[WaypointIndex] : null;
        public AirportDefinition DepartureAirport => GetAirport(DepartureAirportIndex);
        public AirportDefinition ArrivalAirport => GetAirport(ArrivalAirportIndex);
        public string RouteName =>
            DepartureAirport == null || ArrivalAirport == null ? "NO ROUTE" :
            $"{DepartureAirport.airportName} > {ArrivalAirport.airportName}";
        public float DistanceToWaypoint { get; private set; }
        public float DesiredHeading { get; private set; }
        public float CurrentHeading { get; private set; }
        public float HeadingError { get; private set; }
        public float RouteProgress { get; private set; }
        public float DistanceToThreshold { get; private set; }
        public float RunwayHeading { get; private set; }
        public float LateralOffset { get; private set; }
        public float AlignmentError { get; private set; }
        public float TargetGlideSlopeAltitude { get; private set; }
        public float GlideSlopeAltitudeError { get; private set; }
        public float DistanceToTouchdownTarget { get; private set; }
        public float TouchdownTargetDistanceFromThreshold =>
            touchdownTargetDistanceFromThresholdMeters;
        public float ThresholdCrossingHeight => thresholdCrossingHeightMeters;
        public float ThresholdCrossingHeightTolerance =>
            thresholdCrossingHeightToleranceMeters;
        public Vector3 TouchdownTargetPoint { get; private set; }
        public float SelectedCruiseAltitude { get; private set; } = 300f;
        public float AutoCruiseAltitude { get; private set; } = 300f;
        public float HighestRouteObstacle { get; private set; }
        public float PlannedRouteDistance { get; private set; }
        public float CurrentTargetAltitude { get; private set; }
        public float CurrentAltitude => aircraft == null ? 0f : aircraft.position.y;
        public float CurrentAltitudeAGL =>
            aircraft == null || ArrivalAirport == null ? 0f :
            aircraft.position.y - ArrivalAirport.ThresholdPoint.y;
        public float AltitudeError { get; private set; }
        public float SuggestedVerticalSpeed { get; private set; }
        public float RouteProfileDistance { get; private set; }
        public float RouteProfileTargetAltitude { get; private set; }
        public float TerrainClearanceBelowAircraft { get; private set; }
        public float CurrentSegmentMinimumTerrainClearance { get; private set; }
        public string VerticalNavigationPhase { get; private set; } = "INACTIVE";
        public string VerticalInstruction { get; private set; } = "NAV INACTIVE";
        public bool RouteAltitudeRaisedForTerrain { get; private set; }
        public AltitudeReferenceType CurrentAltitudeReference =>
            CurrentWaypoint?.altitudeReference ?? AltitudeReferenceType.MSL;
        public float GlideSlopeAngle => glideSlopeDegrees;
        public bool FinalApproachActive =>
            IsActive && Phase == NavigationPhase.FinalApproach &&
            ApproachClear;
        public bool ApproachValidationEnabled => approachValidationEnabled;
        public bool ApproachClear { get; private set; } = true;
        public string ApproachBlockedReason { get; private set; } = string.Empty;
        public string BlockingObjectName { get; private set; } = string.Empty;
        public float BlockingObjectDistanceFromThreshold { get; private set; }
        public string BlockingObjectRelativePosition { get; private set; } =
            string.Empty;
        public float TerrainClearanceMin { get; private set; } = float.PositiveInfinity;
        public float SelectedApproachCorridorWidth =>
            approachCorridorWidthFar;
        public float FinalApproachFixDistance { get; private set; }
        public float SelectedGlideSlopeDegrees { get; private set; }
        public float SelectedRunwayHeading { get; private set; }
        public string SelectedArrivalRunway =>
            AirportLocationManager.FormatRunway(SelectedRunwayHeading);
        public float SelectedDepartureRunwayHeading { get; private set; }
        public string SelectedDepartureRunway =>
            AirportLocationManager.FormatRunway(SelectedDepartureRunwayHeading);
        public string CruiseAltitudeBand =>
            SelectedCruiseAltitude < 250f ? "LOW" :
            SelectedCruiseAltitude < 400f ? "MEDIUM" : "HIGH";
        public Vector3 SelectedRunwayThreshold { get; private set; }
        public Vector3 SelectedFinalApproachFix { get; private set; }
        public string TurnDirection =>
            Mathf.Abs(HeadingError) < 2f ? "STRAIGHT" :
            HeadingError < 0f ? "TURN LEFT" : "TURN RIGHT";
        public string GlideStatus =>
            Mathf.Abs(GlideSlopeAltitudeError) <= 12f ? "ON GLIDE" :
            GlideSlopeAltitudeError > 0f ? "HIGH" : "LOW";
        public string LateralStatus =>
            Mathf.Abs(LateralOffset) < 25f &&
            Mathf.Abs(AlignmentError) < 4f ? "ALIGNED" :
            LateralOffset < 0f ? "LEFT OF FINAL" : "RIGHT OF FINAL";
        public Vector3 AircraftPosition =>
            aircraft == null ? Vector3.zero : aircraft.position;

        private readonly List<NavigationWaypoint> waypoints = new();
        private bool requestedDepartureReciprocal;
        private bool requestedArrivalReciprocal;
        private bool useRequestedArrivalRunway;

        private void Awake()
        {
            ResolveReferences();
            SelectedCruiseAltitude = defaultCruiseAltitudeMeters;
        }

        private void Update()
        {
            if (!navigationEnabled || !IsActive || aircraft == null) return;
            UpdateGuidance();
        }

        public void SetNavigationEnabled(bool value)
        {
            navigationEnabled = value;
            if (!value) CancelFlightPlan();
        }

        public void SetCruiseAltitude(float altitudeMeters)
        {
            SelectedCruiseAltitude = Mathf.Clamp(
                altitudeMeters, 150f, 800f);
        }

        public void SetPlannerRunways(
            bool departureReciprocal,
            bool arrivalReciprocal)
        {
            requestedDepartureReciprocal = departureReciprocal;
            requestedArrivalReciprocal = arrivalReciprocal;
            useRequestedArrivalRunway = true;
        }

        public float CalculateSafeCruiseAltitude(
            int departureIndex,
            int arrivalIndex,
            bool departureReciprocal,
            bool arrivalReciprocal)
        {
            ResolveReferences();
            AirportDefinition departure = GetAirport(departureIndex);
            AirportDefinition arrival = GetAirport(arrivalIndex);
            if (departure == null || arrival == null)
                return defaultCruiseAltitudeMeters;

            Vector3 departureThreshold = GetRunwayThreshold(
                departure, departureReciprocal);
            Vector3 arrivalThreshold = GetRunwayThreshold(
                arrival, arrivalReciprocal);
            Vector3 route = Vector3.ProjectOnPlane(
                arrivalThreshold - departureThreshold, Vector3.up);
            PlannedRouteDistance = route.magnitude;
            float baseElevation = Mathf.Max(
                departureThreshold.y, arrivalThreshold.y);
            HighestRouteObstacle = baseElevation;

            for (int i = 0; i <= cruiseTerrainSamples; i++)
            {
                float t = i / (float)Mathf.Max(1, cruiseTerrainSamples);
                Vector3 point = Vector3.Lerp(
                    departureThreshold, arrivalThreshold, t);
                if (TryGetRouteSurfaceHeight(point, out float surface))
                    HighestRouteObstacle = Mathf.Max(
                        HighestRouteObstacle, surface);
            }

            float terrainRequired = Mathf.Max(
                shortRouteCruiseAltitudeMeters,
                HighestRouteObstacle - baseElevation +
                cruiseTerrainClearanceMeters);
            float practical = PlannedRouteDistance < 4500f
                ? shortRouteCruiseAltitudeMeters
                : PlannedRouteDistance < 10000f
                    ? normalRouteCruiseAltitudeMeters
                    : longRouteCruiseAltitudeMeters;

            float usableDistance = Mathf.Max(0f,
                PlannedRouteDistance - minimumCruiseSegmentMeters -
                finalApproachDistance * 0.35f);
            float combinedGradientDistance =
                1f / Mathf.Max(0.01f, assumedClimbGradient) +
                1f / Mathf.Max(0.01f, assumedDescentGradient);
            float feasibleAltitude = usableDistance / combinedGradientDistance;
            float desired = Mathf.Max(practical, terrainRequired);
            if (feasibleAltitude >= terrainRequired)
                desired = Mathf.Min(desired, feasibleAltitude);
            AutoCruiseAltitude = Mathf.Clamp(
                Mathf.Ceil(desired / 50f) * 50f, 150f, 800f);
            return AutoCruiseAltitude;
        }

        public void CreateFlightPlan(
            int departureIndex,
            int arrivalIndex,
            NavigationRouteType routeType)
        {
            ResolveReferences();
            AirportDefinition departure = GetAirport(departureIndex);
            AirportDefinition arrival = GetAirport(arrivalIndex);
            if (departure == null || arrival == null) return;

            DepartureAirportIndex = departureIndex;
            ArrivalAirportIndex = arrivalIndex;
            RouteType = routeType;
            waypoints.Clear();
            airports?.SetRouteAirports(departureIndex, arrivalIndex);
            Physics.SyncTransforms();
            SelectValidatedApproach(arrival, useRequestedArrivalRunway);
            RouteAltitudeRaisedForTerrain = false;
            float departureHeading = Mathf.Repeat(
                departure.runwayHeading +
                (requestedDepartureReciprocal ? 180f : 0f), 360f);
            Quaternion departureRotation =
                Quaternion.Euler(0f, departureHeading, 0f);
            Vector3 departureForward = departureRotation * Vector3.forward;
            Vector3 departureThreshold = requestedDepartureReciprocal
                ? departure.ThresholdPoint +
                  departure.Forward * departure.runwayLength
                : departure.ThresholdPoint;
            SelectedDepartureRunwayHeading = departureHeading;
            float departureElevation = departureThreshold.y;
            float arrivalElevation = SelectedRunwayThreshold.y;

            AddWaypoint(
                $"{departure.airportName} Departure",
                departureThreshold + departureForward * 300f,
                normalCaptureRadius, NavigationPhase.Departure,
                departureElevation, 20f, 60f,
                "TAKE OFF AND BEGIN CLIMB", false);
            AddWaypoint(
                $"{departure.airportName} Climb-out",
                departureThreshold + departureForward * 1400f +
                Vector3.up * defaultClimbOutAltitudeMeters,
                normalCaptureRadius, NavigationPhase.Departure,
                departureElevation + defaultClimbOutAltitudeMeters,
                altitudeToleranceMeters, 70f, "CLIMB");

            Quaternion selectedRotation =
                Quaternion.Euler(0f, SelectedRunwayHeading, 0f);
            Vector3 arrivalEntry =
                SelectedFinalApproachFix -
                selectedRotation * Vector3.forward * 1400f +
                selectedRotation * Vector3.right * 900f +
                Vector3.up * 80f;
            Vector3 cruiseStart =
                departureThreshold + departureForward * 1400f;
            Vector3 cruise = routeType == NavigationRouteType.DirectGPS
                ? Vector3.Lerp(cruiseStart, arrivalEntry, 0.38f)
                : BuildVfrCruisePoint(cruiseStart, arrivalEntry, 0.38f);
            float cruiseAltitude = Mathf.Max(
                departureElevation, arrivalElevation) +
                SelectedCruiseAltitude;
            cruise.y = cruiseAltitude;
            AddWaypoint(
                "Cruise Entry", cruise, normalCaptureRadius,
                NavigationPhase.Enroute, cruiseAltitude,
                altitudeToleranceMeters, 85f, "MAINTAIN CRUISE ALTITUDE");
            float cruiseRouteDistance = Vector3.Distance(
                Vector3.ProjectOnPlane(cruiseStart, Vector3.up),
                Vector3.ProjectOnPlane(arrivalEntry, Vector3.up));
            if (cruiseRouteDistance >= minimumCruiseSegmentMeters * 2f)
            {
                Vector3 cruiseExit = routeType == NavigationRouteType.DirectGPS
                    ? Vector3.Lerp(cruiseStart, arrivalEntry, 0.68f)
                    : BuildVfrCruisePoint(cruiseStart, arrivalEntry, 0.68f);
                cruiseExit.y = cruiseAltitude;
                AddWaypoint(
                    "Cruise Exit", cruiseExit, normalCaptureRadius,
                    NavigationPhase.Enroute, cruiseAltitude,
                    altitudeToleranceMeters, 85f,
                    "MAINTAIN CRUISE ALTITUDE");
            }
            AddWaypoint(
                $"{arrival.airportName} Entry",
                arrivalEntry,
                arrivalCaptureRadius, NavigationPhase.Arrival,
                arrivalElevation + Mathf.Max(
                    defaultArrivalAltitudeMeters,
                    Mathf.Min(
                        defaultPatternAltitudeMeters,
                        arrival.patternAltitude)),
                altitudeToleranceMeters,
                arrival.recommendedApproachSpeed + 15f,
                "DESCEND TO ARRIVAL ALTITUDE");
            AddWaypoint(
                $"{arrival.airportName} Final",
                SelectedFinalApproachFix,
                finalCaptureRadius, NavigationPhase.FinalIntercept,
                SelectedFinalApproachFix.y,
                finalAltitudeToleranceMeters,
                arrival.recommendedApproachSpeed,
                "INTERCEPT FINAL GLIDE PATH",
                false);
            AddWaypoint(
                SelectedArrivalRunway,
                SelectedRunwayThreshold,
                finalCaptureRadius, NavigationPhase.FinalApproach,
                arrivalElevation + thresholdCrossingHeightMeters,
                finalAltitudeToleranceMeters,
                arrival.recommendedApproachSpeed,
                "FOLLOW GLIDE PATH",
                false);

            WaypointIndex = 0;
            Phase = NavigationPhase.Departure;
            IsActive = navigationEnabled;
            UpdateGuidance();
        }

        public void CancelFlightPlan()
        {
            IsActive = false;
            Phase = NavigationPhase.Inactive;
            WaypointIndex = 0;
            waypoints.Clear();
        }

        public void StartApproachPractice(
            float distance = 3500f,
            float lateralOffset = 160f,
            float altitudeError = 18f,
            int airportIndex = 0)
        {
            airports?.SelectAirport(airportIndex, false);
            CreateFlightPlan(
                airportIndex, airportIndex,
                NavigationRouteType.DirectGPS);
            if (waypoints.Count == 0) return;
            WaypointIndex = waypoints.Count - 1;
            Phase = NavigationPhase.FinalApproach;
            IsActive = true;

            Quaternion rotation =
                Quaternion.Euler(0f, SelectedRunwayHeading, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            float height =
                thresholdCrossingHeightMeters +
                Mathf.Tan(
                    SelectedGlideSlopeDegrees * Mathf.Deg2Rad) *
                distance + altitudeError;
            Vector3 position =
                SelectedRunwayThreshold -
                forward * distance +
                right * lateralOffset +
                Vector3.up * height;
            Vector3 velocity = forward * 31f + Vector3.down * 1.5f;
            airports?.SpawnAirborneAt(
                position, rotation, velocity, 0.48f);
            UpdateGuidance();
        }

        private void UpdateGuidance()
        {
            NavigationWaypoint waypoint = CurrentWaypoint;
            AirportDefinition arrival = ArrivalAirport;
            if (waypoint == null || arrival == null) return;

            Vector3 delta = waypoint.position - aircraft.position;
            Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
            DistanceToWaypoint = horizontal.magnitude;
            DesiredHeading = HeadingFromDirection(horizontal);
            CurrentHeading = flightPhysics == null
                ? HeadingFromDirection(aircraft.forward)
                : flightPhysics.Heading;
            HeadingError = Mathf.DeltaAngle(CurrentHeading, DesiredHeading);
            RouteProgress = waypoints.Count <= 1 ? 1f :
                Mathf.Clamp01((WaypointIndex +
                    Mathf.InverseLerp(
                        Mathf.Max(waypoint.captureRadius * 4f, 1f),
                        waypoint.captureRadius,
                        DistanceToWaypoint)) / (waypoints.Count - 1f));

            UpdateApproachGeometry(arrival);
            UpdateVerticalGuidance();
            UpdateTerrainClearance();
            if (WaypointIndex == waypoints.Count - 1)
                Phase = NavigationPhase.FinalApproach;
            else if (DistanceToThreshold <= finalApproachDistance &&
                     WaypointIndex >= waypoints.Count - 2)
                Phase = NavigationPhase.FinalApproach;

            if (DistanceToWaypoint <= waypoint.captureRadius)
            {
                if (WaypointIndex < waypoints.Count - 1)
                {
                    WaypointIndex++;
                    Phase = waypoints[WaypointIndex].phase;
                }
                else if ((landing != null && landing.AllWheelsTouching) ||
                         DistanceToThreshold < 80f)
                {
                    Phase = NavigationPhase.Completed;
                    IsActive = false;
                }
            }
        }

        private void UpdateApproachGeometry(AirportDefinition arrival)
        {
            Vector3 threshold = SelectedRunwayThreshold;
            Quaternion runwayRotation =
                Quaternion.Euler(0f, SelectedRunwayHeading, 0f);
            Vector3 forward = runwayRotation * Vector3.forward;
            Vector3 right = runwayRotation * Vector3.right;
            Vector3 fromThreshold = aircraft.position - threshold;
            float along = -Vector3.Dot(fromThreshold, forward);
            DistanceToThreshold = Vector3.ProjectOnPlane(
                fromThreshold, Vector3.up).magnitude;
            DistanceToTouchdownTarget = Vector3.ProjectOnPlane(
                aircraft.position - TouchdownTargetPoint,
                Vector3.up).magnitude;
            RunwayHeading = SelectedRunwayHeading;
            LateralOffset = Vector3.Dot(fromThreshold, right);
            AlignmentError = Mathf.DeltaAngle(CurrentHeading, RunwayHeading);
            float forwardOfThreshold =
                Vector3.Dot(fromThreshold, forward);
            float targetHeight;
            if (!useTouchdownPointForGlideSlope)
            {
                targetHeight = Mathf.Max(0f, along) *
                               Mathf.Tan(glideSlopeDegrees * Mathf.Deg2Rad);
            }
            else if (forwardOfThreshold <= 0f)
            {
                targetHeight =
                    thresholdCrossingHeightMeters +
                    -forwardOfThreshold *
                    Mathf.Tan(glideSlopeDegrees * Mathf.Deg2Rad);
            }
            else
            {
                targetHeight = Mathf.Lerp(
                    thresholdCrossingHeightMeters,
                    0f,
                    Mathf.Clamp01(
                        forwardOfThreshold /
                        Mathf.Max(
                            1f,
                            touchdownTargetDistanceFromThresholdMeters)));
            }
            TargetGlideSlopeAltitude = threshold.y + targetHeight;
            GlideSlopeAltitudeError =
                aircraft.position.y - TargetGlideSlopeAltitude;
        }

        private void UpdateVerticalGuidance()
        {
            NavigationWaypoint waypoint = CurrentWaypoint;
            if (waypoint == null || aircraft == null) return;
            float segmentProgress = 1f;
            float previousAltitude = waypoint.targetAltitude;
            RouteProfileDistance = 0f;
            for (int i = 1; i <= WaypointIndex && i < waypoints.Count; i++)
            {
                RouteProfileDistance += Vector3.ProjectOnPlane(
                    waypoints[i].position - waypoints[i - 1].position,
                    Vector3.up).magnitude;
            }

            if (WaypointIndex > 0)
            {
                NavigationWaypoint previous = waypoints[WaypointIndex - 1];
                Vector3 segment = Vector3.ProjectOnPlane(
                    waypoint.position - previous.position, Vector3.up);
                Vector3 fromPrevious = Vector3.ProjectOnPlane(
                    aircraft.position - previous.position, Vector3.up);
                segmentProgress = segment.sqrMagnitude < 1f
                    ? 1f
                    : Mathf.Clamp01(
                        Vector3.Dot(fromPrevious, segment) /
                        segment.sqrMagnitude);
                previousAltitude = previous.targetAltitude;
                RouteProfileDistance -=
                    segment.magnitude * (1f - segmentProgress);
            }

            RouteProfileTargetAltitude = Mathf.Lerp(
                previousAltitude, waypoint.targetAltitude, segmentProgress);
            bool finalGuidance =
                Phase == NavigationPhase.FinalApproach ||
                Phase == NavigationPhase.FinalIntercept &&
                DistanceToThreshold < finalApproachDistance;
            CurrentTargetAltitude = finalGuidance
                ? TargetGlideSlopeAltitude
                : RouteProfileTargetAltitude;
            float tolerance = finalGuidance
                ? finalAltitudeToleranceMeters
                : Mathf.Max(5f, waypoint.altitudeTolerance);
            AltitudeError = CurrentAltitude - CurrentTargetAltitude;
            SuggestedVerticalSpeed = Mathf.Clamp(
                -AltitudeError / 45f, -2.5f, 2.5f);
            VerticalNavigationPhase = finalGuidance
                ? "FINAL GLIDE"
                : Phase.ToString().ToUpperInvariant();
            if (Mathf.Abs(AltitudeError) <= tolerance)
            {
                VerticalInstruction =
                    finalGuidance ? "ON GLIDE" : "ON ALTITUDE";
                SuggestedVerticalSpeed = 0f;
            }
            else if (AltitudeError < 0f)
            {
                VerticalInstruction = "LOW — CLIMB GENTLY";
            }
            else
            {
                VerticalInstruction = finalGuidance
                    ? "HIGH — DESCEND GENTLY"
                    : "HIGH — DESCEND";
            }

            if (waypoint.altitudeRaisedForTerrain)
                VerticalInstruction =
                    "ROUTE ALTITUDE RAISED FOR TERRAIN";
        }

        private void UpdateTerrainClearance()
        {
            if (aircraft == null) return;
            if (Physics.Raycast(
                    aircraft.position + Vector3.up * 2f,
                    Vector3.down,
                    out RaycastHit hit,
                    5000f,
                    obstacleLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                TerrainClearanceBelowAircraft =
                    aircraft.position.y - hit.point.y;
            }
            NavigationWaypoint waypoint = CurrentWaypoint;
            if (waypoint != null &&
                TryGetGroundHeight(
                    waypoint.position, out float waypointGround))
            {
                CurrentSegmentMinimumTerrainClearance =
                    waypoint.targetAltitude - waypointGround;
            }
        }

        private void SelectValidatedApproach(
            AirportDefinition airport,
            bool forceRequestedRunway = false)
        {
            float primaryHeading = Mathf.Repeat(airport.runwayHeading, 360f);
            Vector3 primaryThreshold = airport.ThresholdPoint;
            Vector3 primaryForward =
                Quaternion.Euler(0f, primaryHeading, 0f) * Vector3.forward;
            Vector3 reciprocalThreshold =
                primaryThreshold + primaryForward * airport.runwayLength;
            float reciprocalHeading = Mathf.Repeat(primaryHeading + 180f, 360f);
            float[] distances =
            {
                approachDistanceKm * 1000f,
                Mathf.Min(4000f, approachDistanceKm * 1000f + 750f),
                4000f
            };
            float[] slopes = { glideSlopeDegrees, 3.3f, 3.5f };
            ApproachCandidate lastFailure = default;

            if (forceRequestedRunway)
            {
                Vector3 requestedThreshold = requestedArrivalReciprocal
                    ? reciprocalThreshold
                    : primaryThreshold;
                float requestedHeading = requestedArrivalReciprocal
                    ? reciprocalHeading
                    : primaryHeading;
                for (int geometry = 0; geometry < distances.Length; geometry++)
                {
                    ApproachCandidate requested = ValidateApproach(
                        requestedThreshold, requestedHeading,
                        distances[geometry], slopes[geometry]);
                    if (requested.clear)
                    {
                        AcceptApproach(
                            requestedThreshold, requestedHeading,
                            distances[geometry], slopes[geometry], requested);
                        return;
                    }
                    lastFailure = requested;
                }
                AcceptApproach(
                    requestedThreshold, requestedHeading,
                    distances[^1], slopes[^1], lastFailure);
                ApproachClear = false;
                return;
            }

            if (!approachValidationEnabled)
            {
                AcceptApproach(primaryThreshold, primaryHeading,
                    distances[0], glideSlopeDegrees, default);
                return;
            }

            // First try both runway directions at the preferred geometry.
            ApproachCandidate primary = ValidateApproach(
                primaryThreshold, primaryHeading, distances[0], slopes[0]);
            if (primary.clear)
            {
                AcceptApproach(primaryThreshold, primaryHeading,
                    distances[0], slopes[0], primary);
                return;
            }
            lastFailure = primary;
            ApproachCandidate reciprocal = ValidateApproach(
                reciprocalThreshold, reciprocalHeading, distances[0], slopes[0]);
            if (reciprocal.clear)
            {
                AcceptApproach(reciprocalThreshold, reciprocalHeading,
                    distances[0], slopes[0], reciprocal);
                return;
            }
            lastFailure = reciprocal;

            // Then try farther fixes and a very small glide-path increase.
            for (int geometry = 1; geometry < distances.Length; geometry++)
            {
                primary = ValidateApproach(
                    primaryThreshold, primaryHeading,
                    distances[geometry], slopes[geometry]);
                if (primary.clear)
                {
                    AcceptApproach(primaryThreshold, primaryHeading,
                        distances[geometry], slopes[geometry], primary);
                    return;
                }
                lastFailure = primary;
                reciprocal = ValidateApproach(
                    reciprocalThreshold, reciprocalHeading,
                    distances[geometry], slopes[geometry]);
                if (reciprocal.clear)
                {
                    AcceptApproach(reciprocalThreshold, reciprocalHeading,
                        distances[geometry], slopes[geometry], reciprocal);
                    return;
                }
                lastFailure = reciprocal;
            }

            AcceptApproach(
                primaryThreshold, primaryHeading,
                distances[^1], slopes[^1], lastFailure);
            ApproachClear = false;
        }

        private ApproachCandidate ValidateApproach(
            Vector3 threshold,
            float heading,
            float distance,
            float slope)
        {
            ApproachCandidate result = new()
            {
                clear = true,
                terrainClearance = float.PositiveInfinity
            };
            Quaternion rotation = Quaternion.Euler(0f, heading, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            HashSet<Collider> checkedColliders = new();
            int samples = Mathf.Max(12, validationSamples);
            float segmentLength = distance / samples;

            for (int i = 1; i <= samples; i++)
            {
                float fromThreshold = i * segmentLength;
                float t = fromThreshold / distance;
                float pathHeight =
                    thresholdCrossingHeightMeters +
                    Mathf.Tan(slope * Mathf.Deg2Rad) * fromThreshold;
                Vector3 pathPoint =
                    threshold - forward * fromThreshold +
                    Vector3.up * pathHeight;
                float width = Mathf.Lerp(
                    approachCorridorWidthNear,
                    approachCorridorWidthFar,
                    t) + approachObstacleMargin * 2f;
                float verticalHalf =
                    (approachVerticalClearanceAbove +
                     approachVerticalClearanceBelow) * 0.5f +
                    approachObstacleMargin;
                Vector3 center = pathPoint + Vector3.up *
                    (approachVerticalClearanceAbove -
                     approachVerticalClearanceBelow) * 0.5f;
                Vector3 halfExtents = new(
                    width * 0.5f,
                    verticalHalf,
                    Mathf.Max(10f, segmentLength * 0.55f));
                Collider[] hits = Physics.OverlapBox(
                    center, halfExtents, rotation, obstacleLayerMask,
                    QueryTriggerInteraction.Ignore);
                foreach (Collider hit in hits)
                {
                    if (!IsApproachObstacle(hit, threshold, fromThreshold) ||
                        !checkedColliders.Add(hit))
                        continue;
                    result.clear = false;
                    result.reason = "OBSTACLE_BLOCKED";
                    result.blockerName = hit.name;
                    result.blockerDistance = fromThreshold;
                    Vector3 relative = hit.bounds.center - pathPoint;
                    string lateral = Mathf.Abs(Vector3.Dot(relative, right)) < 5f
                        ? "CENTER"
                        : Vector3.Dot(relative, right) < 0f ? "LEFT" : "RIGHT";
                    string vertical = Mathf.Abs(relative.y) < 5f
                        ? "ON PATH"
                        : relative.y > 0f ? "ABOVE PATH" : "BELOW PATH";
                    result.relativePosition = $"{lateral}, {vertical}";
                    return result;
                }

                if (fromThreshold < terrainCheckStartDistance) continue;
                Vector3 rayStart = pathPoint + Vector3.up * 1000f;
                if (!Physics.Raycast(
                        rayStart, Vector3.down, out RaycastHit terrainHit,
                        2500f, obstacleLayerMask,
                        QueryTriggerInteraction.Ignore))
                    continue;
                if (terrainHit.collider != null &&
                    IsIgnoredSurface(terrainHit.collider.name))
                {
                    float clearance = pathPoint.y - terrainHit.point.y;
                    result.terrainClearance =
                        Mathf.Min(result.terrainClearance, clearance);
                    float requiredClearance =
                        (terrainMinimumClearance + approachObstacleMargin) *
                        Mathf.InverseLerp(
                            terrainCheckStartDistance,
                            terrainCheckStartDistance + 1000f,
                            fromThreshold);
                    if (clearance < requiredClearance)
                    {
                        result.clear = false;
                        result.reason = "TERRAIN_BLOCKED";
                        result.blockerName = terrainHit.collider.name;
                        result.blockerDistance = fromThreshold;
                        return result;
                    }
                }
            }
            return result;
        }

        private bool IsApproachObstacle(
            Collider collider, Vector3 threshold, float distance)
        {
            if (collider == null || collider.transform.IsChildOf(transform))
                return false;
            Rigidbody attached = collider.attachedRigidbody;
            if (aircraft != null &&
                (collider.transform.IsChildOf(aircraft) ||
                 attached != null && attached.transform == aircraft))
                return false;
            if (IsIgnoredSurface(collider.name)) return false;
            if (collider.bounds.max.y < threshold.y + 4f) return false;
            // Airport fixtures below shoulder height near the threshold are safe.
            if (distance < 180f &&
                collider.bounds.max.y < threshold.y + 3f)
                return false;
            return true;
        }

        private static bool IsIgnoredSurface(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            string lower = objectName.ToLowerInvariant();
            return lower.Contains("runway") ||
                   lower.Contains("ground") ||
                   lower.Contains("terrain") ||
                   lower.Contains("grass") ||
                   lower.Contains("ocean") ||
                   lower.Contains("water") ||
                   lower.Contains("grid") ||
                   lower.Contains("marking") ||
                   lower.Contains("taxiway") ||
                   lower.Contains("apron");
        }

        private void AcceptApproach(
            Vector3 threshold,
            float heading,
            float distance,
            float slope,
            ApproachCandidate validation)
        {
            SelectedRunwayThreshold = threshold;
            SelectedRunwayHeading = heading;
            SelectedGlideSlopeDegrees = slope;
            FinalApproachFixDistance = distance;
            glideSlopeDegrees = slope;
            Vector3 forward =
                Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            TouchdownTargetPoint = useTouchdownPointForGlideSlope
                ? threshold +
                  forward * touchdownTargetDistanceFromThresholdMeters
                : threshold;
            SelectedFinalApproachFix =
                threshold - forward * distance +
                Vector3.up * (
                    thresholdCrossingHeightMeters +
                    Mathf.Tan(slope * Mathf.Deg2Rad) * distance);
            ApproachClear = !approachValidationEnabled || validation.clear;
            ApproachBlockedReason = ApproachClear
                ? string.Empty
                : validation.reason ?? "APPROACH_BLOCKED";
            BlockingObjectName = ApproachClear
                ? string.Empty
                : validation.blockerName ?? "Unknown obstacle";
            BlockingObjectDistanceFromThreshold =
                validation.blockerDistance;
            BlockingObjectRelativePosition = ApproachClear
                ? string.Empty
                : validation.relativePosition ?? string.Empty;
            TerrainClearanceMin = validation.terrainClearance;
        }

        private struct ApproachCandidate
        {
            public bool clear;
            public string reason;
            public string blockerName;
            public float blockerDistance;
            public float terrainClearance;
            public string relativePosition;
        }

        private void OnDrawGizmosSelected()
        {
            if (SelectedFinalApproachFix == Vector3.zero) return;
            Gizmos.color = ApproachClear
                ? new Color(0.2f, 1f, 0.35f, 0.8f)
                : new Color(1f, 0.15f, 0.1f, 0.9f);
            Gizmos.DrawSphere(SelectedFinalApproachFix, 22f);
            Gizmos.DrawSphere(SelectedRunwayThreshold, 12f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(TouchdownTargetPoint, 14f);
            Gizmos.color = ApproachClear
                ? new Color(0.2f, 1f, 0.35f, 0.8f)
                : new Color(1f, 0.15f, 0.1f, 0.9f);
            Gizmos.DrawLine(
                SelectedFinalApproachFix, TouchdownTargetPoint);

            Quaternion rotation =
                Quaternion.Euler(0f, SelectedRunwayHeading, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 farLeft = SelectedFinalApproachFix -
                              right * approachCorridorWidthFar * 0.5f;
            Vector3 farRight = SelectedFinalApproachFix +
                               right * approachCorridorWidthFar * 0.5f;
            Vector3 nearLeft = SelectedRunwayThreshold -
                               right * approachCorridorWidthNear * 0.5f;
            Vector3 nearRight = SelectedRunwayThreshold +
                                right * approachCorridorWidthNear * 0.5f;
            Gizmos.DrawLine(farLeft, nearLeft);
            Gizmos.DrawLine(farRight, nearRight);
            Gizmos.DrawLine(farLeft, farRight);
            Gizmos.DrawLine(nearLeft, nearRight);

            if (!ApproachClear && BlockingObjectDistanceFromThreshold > 0f)
            {
                Vector3 forward = rotation * Vector3.forward;
                Vector3 blockedPoint =
                    SelectedRunwayThreshold -
                    forward * BlockingObjectDistanceFromThreshold +
                    Vector3.up *
                    Mathf.Tan(
                        SelectedGlideSlopeDegrees * Mathf.Deg2Rad) *
                    BlockingObjectDistanceFromThreshold;
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(blockedPoint, 35f);
            }
        }

        private static Vector3 BuildVfrCruisePoint(
            Vector3 departurePoint,
            Vector3 arrivalPoint,
            float routeProgress = 0.5f)
        {
            Vector3 middle = Vector3.Lerp(
                departurePoint, arrivalPoint, routeProgress);
            Vector3 route = arrivalPoint - departurePoint;
            Vector3 side = Vector3.Cross(Vector3.up, route.normalized);
            return middle + side * Mathf.Min(1200f, route.magnitude * 0.15f);
        }

        private static Vector3 GetRunwayThreshold(
            AirportDefinition airport, bool reciprocal)
        {
            if (airport == null) return Vector3.zero;
            return reciprocal
                ? airport.ThresholdPoint + airport.Forward * airport.runwayLength
                : airport.ThresholdPoint;
        }

        private bool TryGetRouteSurfaceHeight(
            Vector3 point, out float surfaceHeight)
        {
            Vector3 origin = new(point.x, 2500f, point.z);
            RaycastHit[] hits = Physics.RaycastAll(
                origin, Vector3.down, 5000f, obstacleLayerMask,
                QueryTriggerInteraction.Ignore);
            surfaceHeight = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (aircraft != null &&
                    hit.collider.transform.IsChildOf(aircraft))
                    continue;
                surfaceHeight = Mathf.Max(surfaceHeight, hit.point.y);
            }
            return !float.IsNegativeInfinity(surfaceHeight);
        }

        private void AddWaypoint(
            string name,
            Vector3 position,
            float radius,
            NavigationPhase phase,
            float targetAltitude,
            float tolerance,
            float recommendedSpeed,
            string instruction,
            bool enforceTerrainClearance = true)
        {
            bool raised = false;
            if (enforceTerrainClearance &&
                TryGetGroundHeight(position, out float groundHeight))
            {
                float safeAltitude =
                    groundHeight + minimumSafeTerrainClearanceMeters;
                if (targetAltitude < safeAltitude)
                {
                    targetAltitude = safeAltitude;
                    raised = true;
                    RouteAltitudeRaisedForTerrain = true;
                }
            }
            position.y = targetAltitude;
            waypoints.Add(new NavigationWaypoint
            {
                name = name,
                position = position,
                captureRadius = radius,
                phase = phase,
                targetAltitude = targetAltitude,
                altitudeReference = AltitudeReferenceType.MSL,
                altitudeTolerance = tolerance,
                recommendedSpeed = recommendedSpeed,
                instruction = raised
                    ? "ROUTE ALTITUDE RAISED FOR TERRAIN"
                    : instruction,
                altitudeRaisedForTerrain = raised
            });
        }

        private bool TryGetGroundHeight(
            Vector3 position, out float groundHeight)
        {
            Vector3 origin = new(
                position.x,
                Mathf.Max(position.y + 1500f, 2000f),
                position.z);
            if (Physics.Raycast(
                    origin, Vector3.down, out RaycastHit hit,
                    5000f, obstacleLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                groundHeight = hit.point.y;
                return true;
            }
            groundHeight = 0f;
            return false;
        }

        private AirportDefinition GetAirport(int index)
        {
            AirportDefinition[] definitions = airports?.Airports;
            return definitions != null && index >= 0 && index < definitions.Length
                ? definitions[index] : null;
        }

        private void ResolveReferences()
        {
            if (airports == null)
                airports = FindAnyObjectByType<AirportLocationManager>();
            if (flightPhysics == null)
                flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
            if (flightPhysics != null && aircraft == null)
                aircraft = flightPhysics.transform;
            if (landing == null && aircraft != null)
                landing = aircraft.GetComponent<LandingController>();
        }

        private static float HeadingFromDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return 0f;
            return Mathf.Repeat(
                Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360f);
        }
    }
}
