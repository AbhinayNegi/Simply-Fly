using System.Collections.Generic;
using UnityEngine;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ApproachTrajectoryVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NavigationManager navigation;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private GameFlowController gameFlow;

        [Header("Prediction")]
        [SerializeField] private bool visualizerEnabled = true;
        [SerializeField] private float predictionSeconds = 12f;
        [SerializeField] private float stepTime = 0.25f;
        [SerializeField] private float maxDistanceMeters = 2500f;
        [SerializeField] private bool useGravityInPrediction;
        [Range(0f, 1f)] [SerializeField] private float gravityInfluence = 0.2f;
        [SerializeField] private float minValidDescentRate = -0.15f;
        [SerializeField] private float minApproachDistance = 120f;
        [SerializeField] private float maxApproachDistance = 5000f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Runway classification")]
        [SerializeField] private float touchdownZoneStartMeters = 150f;
        [SerializeField] private float touchdownZoneEndMeters = 650f;
        [SerializeField] private float lateralRunwayToleranceMeters = 8f;
        [SerializeField] private float shortWarningDistanceMeters = 25f;
        [SerializeField] private float longWarningDistanceMeters = 80f;

        [Header("Visuals")]
        [SerializeField] private bool showTouchdownMarker = true;
        [SerializeField] private bool showTrajectoryLabels = true;
        [Range(0f, 1f)] [SerializeField] private float visualIntensity = 0.65f;
        [SerializeField] private float lineWidth = 1.2f;
        [SerializeField] private float markerSize = 12f;
        [SerializeField] private float labelHeight = 6f;

        public bool VisualizerEnabled => visualizerEnabled;
        public bool Active { get; private set; }
        public bool PredictedImpactValid { get; private set; }
        public Vector3 PredictedImpactWorldPosition { get; private set; }
        public float PredictedImpactDistanceToThreshold { get; private set; }
        public float PredictedImpactDistanceFromCenterline { get; private set; }
        public ProjectedTouchdownStatus Status { get; private set; } =
            ProjectedTouchdownStatus.Hidden;
        public float PredictionTimeToImpact { get; private set; }
        public float PredictionSeconds => predictionSeconds;
        public float CurrentFlightPathAngle =>
            flightPhysics == null ? 0f : flightPhysics.FlightPathAngle;
        public float CurrentGroundTrack { get; private set; }
        public float CurrentVerticalSpeed =>
            flightPhysics == null ? body == null ? 0f : body.linearVelocity.y :
                flightPhysics.VerticalSpeed;
        public string PredictionFailureReason { get; private set; } =
            "INACTIVE";
        public string HudMessage { get; private set; } = string.Empty;

        private readonly List<Vector3> points = new();
        private Rigidbody body;
        private LineRenderer trajectoryLine;
        private Transform markerRoot;
        private TextMesh markerText;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ResolveReferences();
            BuildVisuals();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!Active) return;
            UpdatePrediction();
        }

        public void SetActiveVisual(bool active)
        {
            Active = active && visualizerEnabled;
            if (!Active)
            {
                Status = ProjectedTouchdownStatus.Hidden;
                PredictedImpactValid = false;
                PredictionFailureReason = "HIDDEN";
                HudMessage = string.Empty;
                SetVisible(false);
                return;
            }

            SetVisible(true);
            UpdatePrediction();
        }

        public void ApplySettings(
            bool enabled,
            float seconds,
            float intensity,
            bool marker,
            bool labels)
        {
            visualizerEnabled = enabled;
            predictionSeconds = Mathf.Clamp(seconds, 4f, 25f);
            visualIntensity = Mathf.Clamp01(intensity);
            showTouchdownMarker = marker;
            showTrajectoryLabels = labels;
            if (trajectoryLine != null)
            {
                trajectoryLine.startWidth = lineWidth;
                trajectoryLine.endWidth = lineWidth * 0.2f;
            }
        }

        private void UpdatePrediction()
        {
            ResolveReferences();
            if (!CanPredict())
            {
                SetMarkerVisible(false);
                if (trajectoryLine != null) trajectoryLine.positionCount = 0;
                return;
            }

            Vector3 start = body.position;
            Vector3 velocity = body.linearVelocity;
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                velocity, Vector3.up);
            if (horizontalVelocity.sqrMagnitude > 0.1f)
                CurrentGroundTrack =
                    Mathf.Repeat(
                        Mathf.Atan2(
                            horizontalVelocity.x,
                            horizontalVelocity.z) * Mathf.Rad2Deg,
                        360f);

            points.Clear();
            points.Add(start);
            PredictedImpactValid = false;
            PredictionFailureReason = string.Empty;
            PredictionTimeToImpact = 0f;
            float maxTime = Mathf.Max(stepTime, predictionSeconds);
            float dt = Mathf.Max(0.05f, stepTime);
            Vector3 previous = start;
            float travelled = 0f;
            for (float t = dt; t <= maxTime + 0.001f; t += dt)
            {
                Vector3 next = start + velocity * t;
                if (useGravityInPrediction)
                    next += Physics.gravity * (0.5f * gravityInfluence * t * t);
                travelled += Vector3.Distance(previous, next);
                points.Add(next);
                if (TryFindGroundIntersection(previous, next, t, out Vector3 hit))
                {
                    PredictedImpactValid = true;
                    PredictedImpactWorldPosition = hit;
                    PredictionTimeToImpact = t;
                    points[points.Count - 1] = hit;
                    break;
                }
                previous = next;
                if (travelled >= maxDistanceMeters) break;
            }

            if (!PredictedImpactValid)
            {
                Status = velocity.y > minValidDescentRate
                    ? ProjectedTouchdownStatus.Floating
                    : ProjectedTouchdownStatus.NoImpact;
                PredictionFailureReason = Status.ToString().ToUpperInvariant();
            }
            else ClassifyImpact();

            UpdateLine();
            UpdateMarker();
            UpdateHudMessage();
        }

        private bool CanPredict()
        {
            if (body == null)
            {
                PredictionFailureReason = "NO RIGIDBODY";
                return false;
            }
            if (navigation == null || !navigation.IsActive ||
                !navigation.ApproachClear)
            {
                PredictionFailureReason = "NO ACTIVE APPROACH";
                return false;
            }
            if (gameFlow != null && gameFlow.Mode == SimplyFlyGameMode.Paused)
            {
                PredictionFailureReason = "PAUSED";
                return false;
            }
            if (flightPhysics != null &&
                flightPhysics.State != FlightPhysicsController.AircraftState.Airborne)
            {
                PredictionFailureReason = "NOT AIRBORNE";
                return false;
            }
            if (!navigation.FinalApproachActive)
            {
                PredictionFailureReason = "NOT FINAL";
                return false;
            }
            if (navigation.DistanceToThreshold < minApproachDistance ||
                navigation.DistanceToThreshold > maxApproachDistance)
            {
                PredictionFailureReason = "OUT OF RANGE";
                return false;
            }
            if (body.linearVelocity.sqrMagnitude < 4f)
            {
                PredictionFailureReason = "TOO SLOW";
                return false;
            }
            return true;
        }

        private bool TryFindGroundIntersection(
            Vector3 from,
            Vector3 to,
            float time,
            out Vector3 hitPoint)
        {
            Vector3 direction = to - from;
            float distance = direction.magnitude;
            if (distance > 0.01f &&
                Physics.Raycast(
                    from,
                    direction / distance,
                    out RaycastHit hit,
                    distance,
                    groundMask,
                    QueryTriggerInteraction.Ignore) &&
                hit.collider.attachedRigidbody != body)
            {
                hitPoint = hit.point + Vector3.up * 0.08f;
                PredictionTimeToImpact = time;
                return true;
            }

            float groundHeight = GetApproxGroundHeight(to);
            if (from.y >= groundHeight && to.y <= groundHeight)
            {
                float t = Mathf.InverseLerp(from.y, to.y, groundHeight);
                hitPoint = Vector3.Lerp(from, to, t) + Vector3.up * 0.08f;
                PredictionTimeToImpact = time;
                return true;
            }

            hitPoint = Vector3.zero;
            return false;
        }

        private float GetApproxGroundHeight(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * 500f;
            if (Physics.Raycast(
                    rayStart,
                    Vector3.down,
                    out RaycastHit hit,
                    1200f,
                    groundMask,
                    QueryTriggerInteraction.Ignore) &&
                hit.collider.attachedRigidbody != body)
                return hit.point.y;
            AirportDefinition airport = navigation == null
                ? null
                : navigation.ArrivalAirport;
            return airport == null ? 0f : airport.ThresholdPoint.y;
        }

        private void ClassifyImpact()
        {
            AirportDefinition airport = navigation.ArrivalAirport;
            Vector3 threshold = navigation.SelectedRunwayThreshold;
            Quaternion runwayRotation =
                Quaternion.Euler(0f, navigation.SelectedRunwayHeading, 0f);
            Vector3 forward = runwayRotation * Vector3.forward;
            Vector3 right = runwayRotation * Vector3.right;
            Vector3 fromThreshold = PredictedImpactWorldPosition - threshold;
            float along = Vector3.Dot(fromThreshold, forward);
            float lateral = Vector3.Dot(fromThreshold, right);
            float halfWidth = airport == null ? 20f : airport.runwayWidth * 0.5f;
            float widthTolerance = halfWidth + lateralRunwayToleranceMeters;

            PredictedImpactDistanceToThreshold = along;
            PredictedImpactDistanceFromCenterline = lateral;

            if (Mathf.Abs(lateral) > widthTolerance)
                Status = lateral < 0f
                    ? ProjectedTouchdownStatus.Left
                    : ProjectedTouchdownStatus.Right;
            else if (along < -shortWarningDistanceMeters)
                Status = ProjectedTouchdownStatus.Short;
            else if (along < touchdownZoneStartMeters)
                Status = ProjectedTouchdownStatus.Short;
            else if (along <= touchdownZoneEndMeters)
                Status = ProjectedTouchdownStatus.Good;
            else if (airport != null &&
                     along > airport.runwayLength + longWarningDistanceMeters)
                Status = ProjectedTouchdownStatus.TooLong;
            else
                Status = ProjectedTouchdownStatus.Long;
        }

        private void UpdateLine()
        {
            if (trajectoryLine == null) return;
            trajectoryLine.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                trajectoryLine.SetPosition(i, points[i]);
            Color color = StatusColor();
            color.a = visualIntensity;
            trajectoryLine.startColor = color;
            color.a = visualIntensity * 0.15f;
            trajectoryLine.endColor = color;
        }

        private void UpdateMarker()
        {
            bool visible = showTouchdownMarker &&
                           PredictedImpactValid &&
                           markerRoot != null;
            SetMarkerVisible(visible);
            if (!visible) return;
            markerRoot.position = PredictedImpactWorldPosition + Vector3.up * 0.08f;
            markerRoot.rotation = Quaternion.Euler(
                0f,
                navigation == null ? 0f : navigation.SelectedRunwayHeading,
                0f);
            Color color = StatusColor();
            foreach (Renderer renderer in markerRoot.GetComponentsInChildren<Renderer>())
                renderer.material.color = color;
            if (markerText != null)
            {
                markerText.text = showTrajectoryLabels ? StatusLabel() : string.Empty;
                markerText.color = color;
                Camera camera = Camera.main;
                if (camera != null)
                    markerText.transform.rotation = Quaternion.LookRotation(
                        markerText.transform.position - camera.transform.position,
                        Vector3.up);
            }
        }

        private void UpdateHudMessage()
        {
            string statusText = StatusLabel();
            HudMessage = Status switch
            {
                ProjectedTouchdownStatus.Good =>
                    "PROJECTED TOUCHDOWN: GOOD\nHOLD APPROACH",
                ProjectedTouchdownStatus.Short =>
                    "PROJECTED TOUCHDOWN: SHORT\nADD POWER / REDUCE DESCENT",
                ProjectedTouchdownStatus.Long =>
                    "PROJECTED TOUCHDOWN: LONG\nREDUCE POWER / DESCEND",
                ProjectedTouchdownStatus.TooLong =>
                    "PROJECTED TOUCHDOWN: TOO LONG\nREDUCE POWER",
                ProjectedTouchdownStatus.Left =>
                    "PROJECTED TOUCHDOWN: LEFT\nCORRECT TOWARD CENTERLINE",
                ProjectedTouchdownStatus.Right =>
                    "PROJECTED TOUCHDOWN: RIGHT\nCORRECT TOWARD CENTERLINE",
                ProjectedTouchdownStatus.Floating =>
                    "PROJECTED TOUCHDOWN: FLOATING\nDESCEND TO PATH",
                ProjectedTouchdownStatus.NoImpact =>
                    "PROJECTED TOUCHDOWN: NO IMPACT\nCHECK DESCENT PATH",
                _ => statusText
            };
        }

        private void BuildVisuals()
        {
            GameObject lineObject = new GameObject("Projected Approach Trajectory");
            lineObject.transform.SetParent(transform, false);
            trajectoryLine = lineObject.AddComponent<LineRenderer>();
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            trajectoryLine.receiveShadows = false;
            trajectoryLine.textureMode = LineTextureMode.Tile;
            trajectoryLine.alignment = LineAlignment.View;
            trajectoryLine.positionCount = 0;
            trajectoryLine.startWidth = lineWidth;
            trajectoryLine.endWidth = lineWidth * 0.2f;
            Shader shader = Shader.Find("Sprites/Default") ??
                            Shader.Find("Universal Render Pipeline/Unlit");
            trajectoryLine.material = new Material(shader);

            GameObject marker = new GameObject("Projected Touchdown Marker");
            markerRoot = marker.transform;
            markerRoot.SetParent(null, true);
            CreateMarkerBar("Marker Bar A", Quaternion.Euler(0f, 45f, 0f));
            CreateMarkerBar("Marker Bar B", Quaternion.Euler(0f, -45f, 0f));
            GameObject label = new GameObject("Projected Touchdown Label");
            label.transform.SetParent(markerRoot, false);
            label.transform.localPosition = Vector3.up * labelHeight;
            markerText = label.AddComponent<TextMesh>();
            markerText.anchor = TextAnchor.MiddleCenter;
            markerText.alignment = TextAlignment.Center;
            markerText.fontSize = 32;
            markerText.characterSize = 0.22f;
        }

        private void CreateMarkerBar(string name, Quaternion localRotation)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(markerRoot, false);
            bar.transform.localRotation = localRotation;
            bar.transform.localScale =
                new Vector3(markerSize, 0.12f, markerSize * 0.12f);
            Collider collider = bar.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = bar.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Sprites/Default") ??
                                Shader.Find("Universal Render Pipeline/Unlit");
                renderer.material = new Material(shader);
            }
        }

        private void SetVisible(bool visible)
        {
            if (trajectoryLine != null)
                trajectoryLine.enabled = visible;
            SetMarkerVisible(visible && PredictedImpactValid);
        }

        private void SetMarkerVisible(bool visible)
        {
            if (markerRoot != null)
                markerRoot.gameObject.SetActive(visible);
        }

        private Color StatusColor() => Status switch
        {
            ProjectedTouchdownStatus.Good => new Color(0.25f, 1f, 0.45f),
            ProjectedTouchdownStatus.Short => new Color(1f, 0.75f, 0.15f),
            ProjectedTouchdownStatus.Long => new Color(1f, 0.55f, 0.15f),
            ProjectedTouchdownStatus.TooLong => new Color(1f, 0.22f, 0.12f),
            ProjectedTouchdownStatus.Left => new Color(1f, 0.75f, 0.15f),
            ProjectedTouchdownStatus.Right => new Color(1f, 0.75f, 0.15f),
            _ => new Color(0.4f, 0.9f, 1f)
        };

        private string StatusLabel() => Status switch
        {
            ProjectedTouchdownStatus.Good => "GOOD",
            ProjectedTouchdownStatus.TooLong => "TOO LONG",
            ProjectedTouchdownStatus.NoImpact => "NO IMPACT",
            ProjectedTouchdownStatus.Floating => "FLOATING",
            ProjectedTouchdownStatus.Hidden => string.Empty,
            _ => Status.ToString().ToUpperInvariant()
        };

        private void ResolveReferences()
        {
            if (navigation == null)
                navigation = FindAnyObjectByType<NavigationManager>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>() ??
                                FindAnyObjectByType<FlightPhysicsController>();
            if (gameFlow == null)
                gameFlow = FindAnyObjectByType<GameFlowController>();
        }

        private void OnDisable() => SetVisible(false);
    }
}
