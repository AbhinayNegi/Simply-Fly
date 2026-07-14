using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    /// <summary>Reusable stationary vertical scale with a smoothed moving pointer.</summary>
    [DisallowMultipleComponent]
    public sealed class VerticalScaleHUD : MonoBehaviour
    {
        private float minimum = -10f;
        private float maximum = 10f;
        private float minorStep = 1f;
        private int majorEvery = 2;
        private float pixelsPerUnit = 15f;
        private string title = "VERTICAL SPEED";
        private string unit = "M/S";
        private string format = "+0.0;-0.0;0.0";
        private float targetValue;
        private float displayedValue;
        private float velocity;
        private RectTransform pointer;
        private RectTransform valueBox;
        private TMP_Text valueLabel;
        private bool built;
        private const float ScaleCenterY = -28f;

        private static readonly Color Accent = new Color(0.15f, 0.95f, 1f, 0.95f);
        private static readonly Color Label = new Color(0.88f, 0.95f, 1f, 0.95f);
        private static readonly Color Frame = new Color(0.015f, 0.035f, 0.055f, 0.86f);

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
                displayedValue, targetValue, ref velocity, 0.12f,
                Mathf.Infinity, Time.unscaledDeltaTime);
            float clamped = Mathf.Clamp(displayedValue, minimum, maximum);
            float y = ScaleCenterY + clamped * pixelsPerUnit;
            pointer.anchoredPosition = new Vector2(92f, y);
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
            host.anchoredPosition = new Vector2(-18f, 20f);
            Image oldPanel = host.GetComponent<Image>();
            if (oldPanel != null) oldPanel.enabled = false;
            for (int i = 0; i < host.childCount; i++) host.GetChild(i).gameObject.SetActive(false);
            float oldWidth = host.rect.width;
            float rightEdge = host.anchoredPosition.x + oldWidth * (1f - host.pivot.x);
            host.sizeDelta = new Vector2(230f, 360f);
            host.anchoredPosition = new Vector2(
                rightEdge - host.sizeDelta.x * (1f - host.pivot.x),
                host.anchoredPosition.y);

            RectTransform panel = Rect("VSI Panel", host);
            panel.anchorMin = new Vector2(0f, 0f); panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f); panel.sizeDelta = new Vector2(130f, 0f);
            panel.anchoredPosition = new Vector2(100f, 0f);
            panel.gameObject.AddComponent<Image>().color = Frame;

            TMP_Text heading = Text("Title", panel, 15f, FontStyles.Bold);
            heading.text = title; heading.color = Accent; heading.alignment = TextAlignmentOptions.Center;
            heading.enableAutoSizing = true; heading.fontSizeMin = 11f; heading.fontSizeMax = 15f;
            heading.textWrappingMode = TextWrappingModes.NoWrap;
            Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -15f), new Vector2(126f, 22f));
            TMP_Text units = Text("Units", panel, 14f, FontStyles.Bold);
            units.text = unit; units.color = Label; units.alignment = TextAlignmentOptions.Center;
            Place(units.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(100f, 18f));

            RectTransform scale = Rect("Scale", panel);
            scale.anchorMin = scale.anchorMax = new Vector2(0.5f, 0.5f);
            scale.sizeDelta = new Vector2(120f, (maximum - minimum) * pixelsPerUnit + 30f);
            scale.anchoredPosition = new Vector2(0f, ScaleCenterY);

            int index = 0;
            for (float value = minimum; value <= maximum + 0.001f; value += minorStep, index++)
            {
                bool major = index % majorEvery == 0;
                RectTransform tick = Rect($"Tick {value}", scale);
                tick.anchorMin = tick.anchorMax = new Vector2(1f, 0.5f);
                tick.anchoredPosition = new Vector2(-4f, value * pixelsPerUnit);
                tick.sizeDelta = new Vector2(major ? 26f : 13f, major ? 2.5f : 1.5f);
                tick.gameObject.AddComponent<Image>().color = Label;
                if (major)
                {
                    TMP_Text number = Text("Label", scale, 17f, FontStyles.Normal);
                    number.text = value > 0f ? $"+{value:0}" : value.ToString("0");
                    number.color = Label; number.alignment = TextAlignmentOptions.MidlineRight;
                    Place(number.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, value * pixelsPerUnit), new Vector2(72f, 24f));
                }
            }

            RectTransform zero = Rect("Zero Reference", panel);
            zero.anchorMin = zero.anchorMax = new Vector2(0.5f, 0.5f);
            zero.anchoredPosition = new Vector2(0f, ScaleCenterY);
            zero.sizeDelta = new Vector2(118f, 2f); zero.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.3f);

            pointer = Rect("VSI Pointer", host);
            pointer.anchorMin = pointer.anchorMax = new Vector2(0f, 0.5f);
            pointer.pivot = new Vector2(0f, 0.5f); pointer.sizeDelta = new Vector2(18f, 3f);
            pointer.gameObject.AddComponent<Image>().color = Accent;

            valueBox = Rect("Current Vertical Speed", host);
            valueBox.anchorMin = valueBox.anchorMax = new Vector2(0f, 0.5f);
            valueBox.pivot = new Vector2(0f, 0.5f); valueBox.sizeDelta = new Vector2(88f, 64f);
            valueBox.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.12f, 0.17f, 0.98f);
            Outline outline = valueBox.gameObject.AddComponent<Outline>();
            outline.effectColor = Accent; outline.effectDistance = new Vector2(1f, -1f);
            valueLabel = Text("Value", valueBox, 26f, FontStyles.Bold);
            valueLabel.color = Accent; valueLabel.alignment = TextAlignmentOptions.Center;
            Place(valueLabel.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 3f), new Vector2(78f, 30f));
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
