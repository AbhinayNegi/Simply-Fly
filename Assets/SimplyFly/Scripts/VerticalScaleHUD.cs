using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    /// <summary>Reusable stationary vertical scale with a smoothed moving pointer.</summary>
    [DisallowMultipleComponent]
    public sealed class VerticalScaleHUD : MonoBehaviour
    {
        private float minimum = -2000f;
        private float maximum = 2000f;
        private float minorStep = 50f;
        private int majorEvery = 10;
        private float pixelsPerUnit = 0.075f;
        private string title = "VERTICAL SPEED";
        private string unit = "FT/MIN";
        private string format = "+0;-0;0";
        private float targetValue;
        private float displayedValue;
        private float velocity;
        private RectTransform pointer;
        private RectTransform pointerTriangle;
        private RectTransform valueBox;
        private TMP_Text valueLabel;
        private bool built;
        private const float ScaleCenterY = -24f;

        private static readonly Color Accent = FlightHUDStyle.Cyan;
        private static readonly Color Label = FlightHUDStyle.White;
        private static readonly Color Frame = FlightHUDStyle.PrimaryPanel;

        public void Configure(float min, float max, float minor, int major,
            float spacing, string heading, string displayUnit, string valueFormat)
        {
            if (built) return;
            minimum = min; maximum = max; minorStep = minor;
            majorEvery = Mathf.Max(1, major); pixelsPerUnit = spacing / minor;
            title = heading; unit = displayUnit; format = valueFormat;
        }

        public void SetValue(float value)
        {
            targetValue = value;
            if (!built) Build();
        }

        private void LateUpdate()
        {
            if (!built) return;
            displayedValue = Mathf.SmoothDamp(
                displayedValue, targetValue, ref velocity, 0.15f,
                Mathf.Infinity, Time.unscaledDeltaTime);
            float clamped = Mathf.Clamp(displayedValue, minimum, maximum);
            float y = ScaleCenterY + clamped * pixelsPerUnit;
            pointer.anchoredPosition = new Vector2(110f, y);
            pointerTriangle.anchoredPosition = new Vector2(116f, y);
            valueBox.anchoredPosition = new Vector2(0f, y);
            valueLabel.text = displayedValue.ToString(format);
        }

        private void Build()
        {
            built = true;
            displayedValue = targetValue;
            RectTransform host = (RectTransform)transform;
            // Bottom-align with the engine and attitude instruments.
            host.anchorMin = host.anchorMax = new Vector2(1f, 0f);
            host.pivot = new Vector2(1f, 0f);
            host.anchoredPosition = new Vector2(-18f, FlightHUDStyle.BottomSafeMargin);
            Image oldPanel = host.GetComponent<Image>();
            if (oldPanel != null) oldPanel.enabled = false;
            for (int i = 0; i < host.childCount; i++) host.GetChild(i).gameObject.SetActive(false);
            float oldWidth = host.rect.width;
            float rightEdge = host.anchoredPosition.x + oldWidth * (1f - host.pivot.x);
            host.sizeDelta = new Vector2(250f, 360f);
            host.anchoredPosition = new Vector2(
                rightEdge - host.sizeDelta.x * (1f - host.pivot.x),
                host.anchoredPosition.y);

            RectTransform panel = Rect("VSI Panel", host);
            panel.anchorMin = new Vector2(0f, 0f); panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f); panel.sizeDelta = new Vector2(140f, 0f);
            panel.anchoredPosition = new Vector2(110f, 0f);
            panel.gameObject.AddComponent<Image>().color = Frame;
            FlightHUDStyle.AddOutline(panel.gameObject);

            TMP_Text heading = Text("Title", panel, 15f, FontStyles.Bold);
            heading.text = title; heading.color = Label; heading.alignment = TextAlignmentOptions.Center;
            heading.enableAutoSizing = true; heading.fontSizeMin = 11f; heading.fontSizeMax = 15f;
            heading.textWrappingMode = TextWrappingModes.NoWrap;
            Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -15f), new Vector2(136f, 22f));
            TMP_Text units = Text("Units", panel, 14f, FontStyles.Bold);
            units.text = unit; units.color = Accent; units.alignment = TextAlignmentOptions.Center;
            Place(units.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(100f, 18f));

            RectTransform headerDivider = Rect("Header Divider", panel);
            Place(headerDivider, new Vector2(0.5f, 1f),
                new Vector2(0f, -52f), new Vector2(118f, 1f));
            headerDivider.gameObject.AddComponent<Image>().color = FlightHUDStyle.Divider;

            RectTransform scale = Rect("Scale", panel);
            scale.anchorMin = scale.anchorMax = new Vector2(0.5f, 0.5f);
            scale.sizeDelta = new Vector2(128f, (maximum - minimum) * pixelsPerUnit + 30f);
            scale.anchoredPosition = new Vector2(0f, ScaleCenterY);

            int index = 0;
            for (float value = minimum; value <= maximum + 0.001f; value += minorStep, index++)
            {
                bool major = index % majorEvery == 0;
                bool medium = !major && index % 2 == 0;
                float edge = Mathf.InverseLerp(
                    Mathf.Max(Mathf.Abs(minimum), Mathf.Abs(maximum)), 0f,
                    Mathf.Abs(value));
                float alpha = Mathf.Lerp(0.36f, 1f, edge);
                Color fadedLabel = new Color(Label.r, Label.g, Label.b, alpha);
                RectTransform tick = Rect($"Tick {value}", scale);
                tick.anchorMin = tick.anchorMax = new Vector2(1f, 0.5f);
                tick.anchoredPosition = new Vector2(-4f, value * pixelsPerUnit);
                tick.sizeDelta = new Vector2(
                    major ? 31f : medium ? 21f : 12f,
                    major ? 2.5f : medium ? 1.8f : 1.2f);
                tick.gameObject.AddComponent<Image>().color = fadedLabel;
                if (major)
                {
                    TMP_Text number = Text("Label", scale, 18f, FontStyles.Normal);
                    number.text = value > 0f ? $"+{value:0}" : value.ToString("0");
                    number.color = Mathf.Abs(value) < 0.001f ? Accent : fadedLabel;
                    number.fontStyle = Mathf.Abs(value) < 0.001f
                        ? FontStyles.Bold : FontStyles.Normal;
                    number.alignment = TextAlignmentOptions.MidlineRight;
                    number.enableAutoSizing = true;
                    number.fontSizeMin = 12f;
                    number.fontSizeMax = 18f;
                    number.textWrappingMode = TextWrappingModes.NoWrap;
                    Place(number.rectTransform, new Vector2(0f, 0.5f),
                        new Vector2(44f, value * pixelsPerUnit), new Vector2(78f, 24f));
                }
            }

            RectTransform zero = Rect("Zero Reference", panel);
            zero.anchorMin = zero.anchorMax = new Vector2(0.5f, 0.5f);
            zero.anchoredPosition = new Vector2(0f, ScaleCenterY);
            zero.sizeDelta = new Vector2(128f, 3f); zero.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.95f, 1f, 0.68f);

            pointer = Rect("VSI Pointer", host);
            pointer.anchorMin = pointer.anchorMax = new Vector2(0f, 0.5f);
            pointer.pivot = new Vector2(0f, 0.5f); pointer.sizeDelta = new Vector2(30f, 3f);
            pointer.gameObject.AddComponent<Image>().color = Accent;

            pointerTriangle = Rect("VSI Pointer Triangle", host);
            pointerTriangle.anchorMin = pointerTriangle.anchorMax = new Vector2(0f, 0.5f);
            pointerTriangle.pivot = new Vector2(0.5f, 0.5f);
            pointerTriangle.sizeDelta = new Vector2(13f, 18f);
            TapeTriangleGraphic pointerGraphic =
                pointerTriangle.gameObject.AddComponent<TapeTriangleGraphic>();
            pointerGraphic.PointsRight = true;
            pointerGraphic.color = Accent;
            pointerGraphic.raycastTarget = false;

            valueBox = Rect("Current Vertical Speed", host);
            valueBox.anchorMin = valueBox.anchorMax = new Vector2(0f, 0.5f);
            valueBox.pivot = new Vector2(0f, 0.5f); valueBox.sizeDelta = new Vector2(110f, 72f);
            valueBox.gameObject.AddComponent<Image>().color = FlightHUDStyle.ValueBox;
            Outline outline = valueBox.gameObject.AddComponent<Outline>();
            outline.effectColor = Accent; outline.effectDistance = new Vector2(1f, -1f);
            valueLabel = Text("Value", valueBox, 30f, FontStyles.Bold);
            valueLabel.color = Accent; valueLabel.alignment = TextAlignmentOptions.Center;
            valueLabel.enableAutoSizing = true;
            valueLabel.fontSizeMin = 21f;
            valueLabel.fontSizeMax = 30f;
            valueLabel.textWrappingMode = TextWrappingModes.NoWrap;
            Place(valueLabel.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 3f), new Vector2(94f, 34f));
            TMP_Text valueUnit = Text("Value Unit", valueBox, 13f, FontStyles.Bold);
            valueUnit.text = unit; valueUnit.color = Label; valueUnit.alignment = TextAlignmentOptions.Center;
            Place(valueUnit.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(70f, 18f));
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        private static TMP_Text Text(string name, Transform parent, float size, FontStyles style)
        {
            TMP_Text text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size; text.fontStyle = style; text.raycastTarget = false; return text;
        }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
