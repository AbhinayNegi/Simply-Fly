using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    /// <summary>
    /// Projects ground velocity into HUD viewport space. This component only reads
    /// the aircraft Rigidbody and never feeds values back into the flight model.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlightPathMarkerHUD : MonoBehaviour
    {
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private Camera projectionCamera;
        [Min(0f)] [SerializeField] private float minimumVisibleSpeed = 2f;
        [Min(0.01f)] [SerializeField] private float smoothingTime = 0.1f;
        [Range(0.01f, 0.2f)] [SerializeField] private float edgeMargin = 0.055f;

        private Rigidbody aircraftBody;
        private RectTransform marker;
        private CanvasGroup markerGroup;
        private Vector2 displayedViewport = new Vector2(0.5f, 0.5f);
        private Vector2 smoothVelocity;
        private bool initialized;
        private bool settingEnabled;

        private static readonly Color Cyan = new Color(0.12f, 0.95f, 1f, 0.68f);

        public void Bind(FlightPhysicsController source)
        {
            flightPhysics = source;
            aircraftBody = source == null ? null : source.GetComponent<Rigidbody>();
        }

        public void SetSettingEnabled(bool enabled)
        {
            settingEnabled = enabled;
            if (markerGroup != null && !enabled) markerGroup.alpha = 0f;
            initialized = false;
        }

        private void Awake()
        {
            settingEnabled = PlayerPrefs.GetInt(
                "settings.flightPathMarker", 0) != 0;
            if (flightPhysics == null)
                flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
            Bind(flightPhysics);
            BuildMarker();
        }

        private void LateUpdate()
        {
            if (marker == null) BuildMarker();
            if (!settingEnabled)
            {
                markerGroup.alpha = 0f;
                return;
            }
            if (aircraftBody == null && flightPhysics != null)
                aircraftBody = flightPhysics.GetComponent<Rigidbody>();

            Camera cameraToUse = ResolveCamera();
            Vector3 velocity = aircraftBody == null ? Vector3.zero : aircraftBody.linearVelocity;
            if (cameraToUse == null || aircraftBody == null ||
                velocity.magnitude < minimumVisibleSpeed)
            {
                markerGroup.alpha = 0f;
                initialized = false;
                return;
            }

            // A point far along the normalized world velocity represents the actual
            // travel direction independently of the aircraft nose orientation.
            Vector3 sample = aircraftBody.worldCenterOfMass + velocity.normalized * 1000f;
            Vector3 viewport3 = cameraToUse.WorldToViewportPoint(sample);
            if (viewport3.z <= 0f)
            {
                markerGroup.alpha = 0f;
                initialized = false;
                return;
            }

            Vector2 raw = new Vector2(viewport3.x, viewport3.y);
            Vector2 clamped = new Vector2(
                Mathf.Clamp(raw.x, edgeMargin, 1f - edgeMargin),
                Mathf.Clamp(raw.y, edgeMargin, 1f - edgeMargin));
            if (!initialized)
            {
                displayedViewport = clamped;
                initialized = true;
            }
            displayedViewport = Vector2.SmoothDamp(
                displayedViewport, clamped, ref smoothVelocity, smoothingTime,
                Mathf.Infinity, Time.unscaledDeltaTime);

            marker.anchorMin = marker.anchorMax = displayedViewport;
            marker.anchoredPosition = Vector2.zero;
            float edgeDistance = Mathf.Min(
                Mathf.Min(raw.x, 1f - raw.x), Mathf.Min(raw.y, 1f - raw.y));
            markerGroup.alpha = Mathf.Lerp(0.22f, 0.78f,
                Mathf.InverseLerp(0f, edgeMargin * 2.5f, edgeDistance));
        }

        private Camera ResolveCamera()
        {
            if (projectionCamera != null && projectionCamera.isActiveAndEnabled)
                return projectionCamera;
            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return Camera.main;
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
                if (cameras[i].isActiveAndEnabled) return cameras[i];
            return null;
        }

        private void BuildMarker()
        {
            if (marker != null) return;
            RectTransform hudRoot = (RectTransform)transform;
            marker = Rect("Flight Path Marker", hudRoot);
            marker.sizeDelta = new Vector2(72f, 46f);
            markerGroup = marker.gameObject.AddComponent<CanvasGroup>();
            markerGroup.interactable = false;
            markerGroup.blocksRaycasts = false;

            TMP_Text ring = Rect("Ring", marker).gameObject.AddComponent<TextMeshProUGUI>();
            ring.text = "○"; ring.fontSize = 42f; ring.fontStyle = FontStyles.Bold;
            ring.alignment = TextAlignmentOptions.Center; ring.color = Cyan;
            ring.raycastTarget = false; Stretch(ring.rectTransform);

            AddLine("Left Wing", marker, new Vector2(-27f, 0f), new Vector2(22f, 2f));
            AddLine("Right Wing", marker, new Vector2(27f, 0f), new Vector2(22f, 2f));
            AddLine("Stem", marker, new Vector2(0f, -17f), new Vector2(2f, 10f));

            RectTransform dot = Rect("Velocity Dot", marker);
            dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.anchoredPosition = Vector2.zero; dot.sizeDelta = new Vector2(6f, 6f);
            dot.gameObject.AddComponent<Image>().color = Cyan;
        }

        private static void AddLine(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform line = Rect(name, parent);
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = position; line.sizeDelta = size;
            line.gameObject.AddComponent<Image>().color = Cyan;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
