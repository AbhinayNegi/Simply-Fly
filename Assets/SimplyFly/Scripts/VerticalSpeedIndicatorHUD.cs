using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace SimplyFly.GroundOps
{
    /// <summary>
    /// Isolated UI Toolkit VSI. Flight physics remains in metres per second;
    /// conversion to feet per minute happens only at this presentation boundary.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class VerticalSpeedIndicatorHUD : MonoBehaviour
    {
        private const float FeetPerMinutePerMeterPerSecond = 196.8504f;
        private const float Minimum = -2000f;
        private const float Maximum = 2000f;
        private const float MajorEvery = 500f;
        private const float MediumEvery = 100f;
        private const float MinorEvery = 50f;
        private const float ScaleHeight = 314f;
        private const float ScaleTop = 38f;
        private const float RailLeft = 28f;
        private const float Smoothing = 0.15f;

        [SerializeField] private FlightPhysicsController flightPhysics;

        private UIDocument document;
        private VisualElement root;
        private VisualElement tape;
        private VisualElement marker;
        private VisualElement markerTriangle;
        private VisualElement readoutBox;
        private Label readoutValue;
        private float displayedValue;
        private bool initialized;
        private bool desiredVisible = true;

        public void Bind(FlightPhysicsController source) => flightPhysics = source;

        private void Awake()
        {
            HideLegacyUGUI();
            document = GetComponent<UIDocument>();
            ConfigureDocument();
            if (flightPhysics == null)
                flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
        }

        private IEnumerator Start()
        {
            // UIDocument creates its root after its panel and visual tree are assigned.
            yield return null;
            InitializeVisualTree();
        }

        private void ConfigureDocument()
        {
            if (document.panelSettings == null)
            {
                UIDocument[] documents = FindObjectsByType<UIDocument>(
                    FindObjectsInactive.Include);
                foreach (UIDocument candidate in documents)
                {
                    if (candidate != document && candidate.panelSettings != null)
                    {
                        document.panelSettings = candidate.panelSettings;
                        break;
                    }
                }
            }

            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("VSI/VSI");
            document.sortingOrder = 40;
        }

        private void InitializeVisualTree()
        {
            root = document.rootVisualElement;
            StyleSheet style = Resources.Load<StyleSheet>("VSI/VSI");
            if (style != null && !root.styleSheets.Contains(style))
                root.styleSheets.Add(style);

            tape = root.Q<VisualElement>("tape-container");
            marker = root.Q<VisualElement>("marker-line");
            markerTriangle = root.Q<VisualElement>("marker-triangle");
            readoutBox = root.Q<VisualElement>("readout-box");
            readoutValue = root.Q<Label>("readout-value");

            Texture2D triangleTexture = Resources.Load<Texture2D>("VSI/marker-triangle");
            if (markerTriangle != null && triangleTexture != null)
                markerTriangle.style.backgroundImage = new StyleBackground(triangleTexture);

            if (tape == null || marker == null || readoutBox == null ||
                readoutValue == null)
            {
                Debug.LogError("VSI UI Toolkit assets could not be initialized.", this);
                return;
            }

            BuildTicks();
            displayedValue = CurrentVerticalSpeedFeetPerMinute();
            initialized = true;
            root.style.display = desiredVisible ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateVisuals();
        }

        private void Update()
        {
            if (!initialized) return;
            float target = CurrentVerticalSpeedFeetPerMinute();
            float blend = Smoothing <= 0f
                ? 1f
                : 1f - Mathf.Exp(-Time.unscaledDeltaTime / Smoothing);
            displayedValue = Mathf.Lerp(displayedValue, target, blend);
            UpdateVisuals();
        }

        public void SetVisible(bool visible)
        {
            desiredVisible = visible;
            if (root != null)
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private float CurrentVerticalSpeedFeetPerMinute() =>
            flightPhysics == null
                ? 0f
                : flightPhysics.VerticalSpeed * FeetPerMinutePerMeterPerSecond;

        private static float YForValue(float value) =>
            (Maximum - value) * ScaleHeight / (Maximum - Minimum);

        private void UpdateVisuals()
        {
            float clamped = Mathf.Clamp(displayedValue, Minimum, Maximum);
            float y = ScaleTop + YForValue(clamped);
            marker.style.top = y - 1.5f;
            if (markerTriangle != null) markerTriangle.style.top = y - 9f;
            readoutBox.style.top = y - 36f;

            float rounded = Mathf.Round(displayedValue);
            if (Mathf.Abs(rounded) < 5f) rounded = 0f;
            readoutValue.text = rounded > 0f
                ? $"+{rounded:0}"
                : rounded.ToString("0");
        }

        private void BuildTicks()
        {
            tape.Clear();

            VisualElement zero = new();
            zero.AddToClassList("vsi-zero-line");
            zero.style.top = YForValue(0f) - 1.5f;
            zero.style.left = RailLeft;
            zero.style.right = 4f;
            tape.Add(zero);

            for (float value = Maximum; value >= Minimum; value -= MinorEvery)
            {
                bool major = IsMultiple(value, MajorEvery);
                bool medium = !major && IsMultiple(value, MediumEvery);
                float length = major ? 31f : medium ? 21f : 12f;
                float height = major ? 2.5f : medium ? 1.8f : 1.2f;
                float centerStrength = 1f - Mathf.Abs(value) / Maximum;

                VisualElement tick = new();
                tick.AddToClassList("vsi-tick");
                tick.style.top = YForValue(value) - height * 0.5f;
                tick.style.left = RailLeft;
                tick.style.width = length;
                tick.style.height = height;
                tick.style.opacity = Mathf.Lerp(0.36f, 1f, centerStrength);
                tape.Add(tick);

                if (!major) continue;
                Label label = new(value > 0f ? $"+{value:0}" : value.ToString("0"));
                label.AddToClassList("vsi-tick-label");
                label.style.top = YForValue(value) - 10f;
                label.style.left = RailLeft + 36f;
                label.style.opacity = Mathf.Lerp(0.5f, 1f, centerStrength);
                if (Mathf.Abs(value) < 0.01f)
                    label.AddToClassList("vsi-zero-label");
                tape.Add(label);
            }
        }

        private static bool IsMultiple(float value, float interval) =>
            Mathf.Abs(Mathf.Round(value / interval) * interval - value) < 0.01f;

        private void HideLegacyUGUI()
        {
            UnityEngine.UI.Image panel = GetComponent<UnityEngine.UI.Image>();
            if (panel != null) panel.enabled = false;
            foreach (Transform child in transform)
                child.gameObject.SetActive(false);
            VerticalScaleHUD oldScale = GetComponent<VerticalScaleHUD>();
            if (oldScale != null) oldScale.enabled = false;
        }
    }
}
