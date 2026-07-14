using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    public enum EngineInstrumentState { Off, Starting, Running, Warning }

    /// <summary>Engine-agnostic presentation for command, speed and state.</summary>
    [DisallowMultipleComponent]
    public sealed class EngineInstrumentHUD : MonoBehaviour
    {
        [Min(0.01f)] [SerializeField] private float throttleSmoothing = 0.045f;
        [Min(0.01f)] [SerializeField] private float rpmSmoothing = 0.16f;
        [Min(0.1f)] [SerializeField] private float statusFadeSpeed = 5f;

        private const int SegmentCount = 20;
        private const float BarWidth = 194f;

        private readonly Image[] throttleSegments = new Image[SegmentCount];
        private TMP_Text throttleValue;
        private TMP_Text rpmValue;
        private TMP_Text maximumRpmLabel;
        private TMP_Text statusLabel;
        private RectTransform throttleMarker;
        private RectTransform rpmFill;
        private Image statusDot;
        private CanvasGroup statusGroup;

        private float targetThrottle;
        private float displayedThrottle;
        private float throttleVelocity;
        private float targetRpm;
        private float displayedRpm;
        private float rpmVelocity;
        private float maximumRpm = 2700f;
        private EngineInstrumentState displayedState;
        private EngineInstrumentState pendingState;
        private bool stateChanging;
        private bool built;

        private static readonly Color Cyan = new(0.15f, 0.95f, 1f, 0.98f);
        private static readonly Color White = new(0.88f, 0.95f, 1f, 0.92f);
        private static readonly Color Muted = new(0.62f, 0.7f, 0.73f, 0.86f);
        private static readonly Color Green = new(0.28f, 0.92f, 0.12f, 0.98f);
        private static readonly Color Amber = new(1f, 0.65f, 0.06f, 0.98f);
        private static readonly Color Red = new(1f, 0.16f, 0.12f, 0.98f);
        private static readonly Color Empty = new(0.16f, 0.21f, 0.23f, 0.78f);

        public void SetData(float throttlePosition, float rpm,
            float configuredMaximumRpm, EngineInstrumentState state)
        {
            if (!built) Build();
            targetThrottle = Mathf.Clamp01(throttlePosition);
            targetRpm = Mathf.Max(0f, rpm);
            maximumRpm = Mathf.Max(1f, configuredMaximumRpm);
            if (state != displayedState && (!stateChanging || state != pendingState))
            {
                pendingState = state;
                stateChanging = true;
            }
        }

        private void LateUpdate()
        {
            if (!built) return;
            float dt = Time.unscaledDeltaTime;
            displayedThrottle = Mathf.SmoothDamp(displayedThrottle, targetThrottle,
                ref throttleVelocity, throttleSmoothing, Mathf.Infinity, dt);
            displayedRpm = Mathf.SmoothDamp(displayedRpm, targetRpm,
                ref rpmVelocity, rpmSmoothing, Mathf.Infinity, dt);

            throttleValue.text = $"{displayedThrottle * 100f:0}%";
            rpmValue.text = $"{displayedRpm:0}";
            maximumRpmLabel.text = $"MAX {maximumRpm:0} RPM";
            RefreshSegments(throttleSegments, displayedThrottle, Cyan);
            throttleMarker.anchoredPosition = new Vector2(
                Mathf.Lerp(0f, BarWidth, Mathf.Clamp01(displayedThrottle)), 0f);
            rpmFill.sizeDelta = new Vector2(
                BarWidth * Mathf.Clamp01(displayedRpm / maximumRpm), 4f);

            if (stateChanging)
            {
                statusGroup.alpha = Mathf.MoveTowards(
                    statusGroup.alpha, 0f, statusFadeSpeed * dt);
                if (statusGroup.alpha <= 0.01f)
                {
                    displayedState = pendingState;
                    ApplyState();
                    stateChanging = false;
                }
            }
            else
            {
                statusGroup.alpha = Mathf.MoveTowards(
                    statusGroup.alpha, 1f, statusFadeSpeed * dt);
            }
        }

        private void Build()
        {
            built = true;
            RectTransform host = (RectTransform)transform;
            Image legacyPanel = host.GetComponent<Image>();
            if (legacyPanel != null) legacyPanel.enabled = false;
            for (int i = 0; i < host.childCount; i++)
                host.GetChild(i).gameObject.SetActive(false);
            host.sizeDelta = new Vector2(230f, 270f);

            RectTransform panel = Rect("Engine Panel", host);
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color =
                new Color(0.01f, 0.035f, 0.05f, 0.82f);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.72f, 0.76f, 0.52f);
            outline.effectDistance = new Vector2(1f, -1f);

            TMP_Text title = Text("Title", panel, 18f, FontStyles.Bold,
                new Vector2(-14f, 122f), new Vector2(150f, 24f));
            title.text = "ENGINE"; title.color = Cyan;
            BuildEngineIcon(panel, new Vector2(88f, 122f));
            Divider(panel, 104f);

            Label(panel, "THROTTLE", 85f);
            throttleValue = Value(panel, "Throttle Value", 60f, 29f);
            BuildSegmentBar(panel, "Throttle Bar", 35f, throttleSegments, true);
            ScaleLabels(panel, 21f);
            Divider(panel, 4f);

            Label(panel, "RPM", -15f);
            rpmValue = Value(panel, "RPM Value", -42f, 30f);
            maximumRpmLabel = Text("Maximum RPM", panel, 11f, FontStyles.Normal,
                new Vector2(-46f, -65f), new Vector2(106f, 18f));
            maximumRpmLabel.color = White;
            RectTransform rpmTrack = Rect("RPM Track", panel);
            Place(rpmTrack, new Vector2(-BarWidth * 0.5f, -81f),
                new Vector2(BarWidth, 4f));
            rpmTrack.gameObject.AddComponent<Image>().color = Empty;
            rpmFill = Rect("RPM Fill", rpmTrack);
            rpmFill.anchorMin = rpmFill.anchorMax = new Vector2(0f, 0.5f);
            rpmFill.pivot = new Vector2(0f, 0.5f);
            rpmFill.anchoredPosition = Vector2.zero;
            rpmFill.sizeDelta = new Vector2(0f, 4f);
            rpmFill.gameObject.AddComponent<Image>().color = Green;
            Divider(panel, -95f);

            RectTransform stateRow = Rect("Engine State", panel);
            Place(stateRow, new Vector2(-82f, -126f), new Vector2(164f, 24f));
            statusGroup = stateRow.gameObject.AddComponent<CanvasGroup>();
            statusDot = Rect("Status Dot", stateRow).gameObject.AddComponent<Image>();
            Place(statusDot.rectTransform, new Vector2(-70f, -5f), new Vector2(10f, 10f));
            statusLabel = Text("Status", stateRow, 14f, FontStyles.Bold,
                new Vector2(10f, 0f), new Vector2(128f, 22f));
            displayedState = EngineInstrumentState.Off;
            pendingState = displayedState;
            ApplyState();
        }

        private void BuildSegmentBar(Transform parent, string name, float y,
            Image[] segments, bool addMarker)
        {
            RectTransform bar = Rect(name, parent);
            Place(bar, new Vector2(-BarWidth * 0.5f, y), new Vector2(BarWidth, 12f));
            float gap = 2f;
            float width = (BarWidth - gap * (SegmentCount - 1)) / SegmentCount;
            for (int i = 0; i < SegmentCount; i++)
            {
                Image segment = Rect($"Segment {i + 1}", bar).gameObject.AddComponent<Image>();
                Place(segment.rectTransform, new Vector2(
                        -BarWidth * 0.5f + i * (width + gap), -6f),
                    new Vector2(width, 12f));
                segment.color = Empty;
                segments[i] = segment;
            }
            if (!addMarker) return;
            throttleMarker = Rect("Throttle Position Marker", bar);
            throttleMarker.anchorMin = throttleMarker.anchorMax = new Vector2(0f, 0.5f);
            throttleMarker.pivot = new Vector2(0.5f, 0.5f);
            throttleMarker.sizeDelta = new Vector2(4f, 20f);
            throttleMarker.gameObject.AddComponent<Image>().color = White;
        }

        private static void RefreshSegments(Image[] segments, float value, Color fill)
        {
            float count = Mathf.Clamp01(value) * SegmentCount;
            for (int i = 0; i < segments.Length; i++)
                segments[i].color = i + 0.5f < count ? fill : Empty;
        }

        private void ApplyState()
        {
            Color color;
            switch (displayedState)
            {
                case EngineInstrumentState.Starting:
                    statusLabel.text = "STARTING"; color = Amber; break;
                case EngineInstrumentState.Running:
                    statusLabel.text = "RUNNING"; color = Green; break;
                case EngineInstrumentState.Warning:
                    statusLabel.text = "HOT"; color = Red; break;
                default:
                    statusLabel.text = "OFF"; color = Muted; break;
            }
            statusLabel.color = color;
            statusDot.color = color;
            rpmFill.GetComponent<Image>().color =
                displayedState == EngineInstrumentState.Warning ? Red :
                displayedState == EngineInstrumentState.Starting ? Amber : Green;
        }

        private static void BuildEngineIcon(Transform parent, Vector2 center)
        {
            RectTransform body = Rect("Engine Icon", parent);
            Place(body, center - new Vector2(18f, 8f), new Vector2(31f, 17f));
            body.gameObject.AddComponent<Image>().color =
                new Color(0.01f, 0.035f, 0.05f, 0.01f);
            Outline border = body.gameObject.AddComponent<Outline>();
            border.effectColor = Cyan; border.effectDistance = new Vector2(1f, -1f);
            Image top = Rect("Engine Top", body).gameObject.AddComponent<Image>();
            Place(top.rectTransform, new Vector2(-8.5f, 8.5f), new Vector2(14f, 3f));
            top.color = Cyan;
            Image nose = Rect("Engine Nose", body).gameObject.AddComponent<Image>();
            Place(nose.rectTransform, new Vector2(15.5f, -3.5f), new Vector2(5f, 8f));
            nose.color = Cyan;
        }

        private static void Label(Transform parent, string value, float y)
        {
            TMP_Text label = Text(value, parent, 12f, FontStyles.Bold,
                new Vector2(0f, y), new Vector2(200f, 18f));
            label.text = value; label.color = White;
        }

        private static TMP_Text Value(Transform parent, string name, float y, float size)
        {
            TMP_Text value = Text(name, parent, size, FontStyles.Normal,
                new Vector2(0f, y), new Vector2(204f, 38f));
            value.color = Cyan;
            return value;
        }

        private static void ScaleLabels(Transform parent, float y)
        {
            string[] values = { "0%", "50%", "100%" };
            float[] x = { -86f, 0f, 86f };
            for (int i = 0; i < values.Length; i++)
            {
                TMP_Text label = Text($"Scale {values[i]}", parent, 9f,
                    FontStyles.Normal, new Vector2(x[i], y), new Vector2(40f, 14f));
                label.text = values[i]; label.color = Muted;
            }
        }

        private static void Divider(Transform parent, float y)
        {
            RectTransform line = Rect("Divider", parent);
            Place(line, new Vector2(-103f, y), new Vector2(206f, 1f));
            line.gameObject.AddComponent<Image>().color =
                new Color(0.65f, 0.78f, 0.8f, 0.34f);
        }

        private static TMP_Text Text(string name, Transform parent, float size,
            FontStyles style, Vector2 center, Vector2 dimensions)
        {
            TMP_Text text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size; text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            Place(text.rectTransform, center - dimensions * 0.5f, dimensions);
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
