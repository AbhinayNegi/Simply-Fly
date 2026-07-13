using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    /// <summary>Reusable, unit-agnostic scrolling vertical tape presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class NumericTapeHUD : MonoBehaviour
    {
        [Header("Scale")]
        [Min(0.1f)] [SerializeField] private float minorStep = 5f;
        [Min(1)] [SerializeField] private int majorEvery = 2;
        [Min(4f)] [SerializeField] private float pixelsPerMinorStep = 30f;
        [Min(3)] [SerializeField] private int visibleMinorSteps = 13;
        [Min(0.01f)] [SerializeField] private float smoothingTime = 0.1f;
        [Min(0.01f)] [SerializeField] private float valueSmoothingTime = 0.07f;
        [SerializeField] private float minimumValue;
        [Header("Formatting")]
        [SerializeField] private string title = "AIRSPEED";
        [SerializeField] private string unit = "KT";
        [SerializeField] private string valueFormat = "000";
        [SerializeField] private bool calloutOnLeft;
        [Header("Appearance")]
        [SerializeField] private Color frameColor = new Color(0.015f, 0.035f, 0.055f, 0.86f);
        [SerializeField] private Color accentColor = new Color(0.15f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color labelColor = new Color(0.88f, 0.95f, 1f);
        [SerializeField] private Color valueBoxColor = new Color(0.025f, 0.12f, 0.17f, 0.98f);

        private readonly List<TapeMark> marks = new List<TapeMark>();
        private readonly List<TapeBandView> bandViews = new List<TapeBandView>();
        private RectTransform content;
        private TMP_Text valueLabel;
        private TMP_Text warningLabel;
        private RectTransform trendLine;
        private float targetValue;
        private float displayedValue;
        private float smoothVelocity;
        private float displayedNumericValue;
        private float numericSmoothVelocity;
        private bool built;

        public readonly struct RangeBand
        {
            public readonly float Start;
            public readonly float End;
            public readonly Color Color;
            public RangeBand(float start, float end, Color color)
            {
                Start = start; End = end; Color = color;
            }
        }

        private sealed class TapeMark
        {
            public RectTransform Rect;
            public RectTransform Tick;
            public TMP_Text Label;
        }

        private sealed class TapeBandView
        {
            public RangeBand Band;
            public RectTransform Rect;
        }

        public void SetValue(float value)
        {
            targetValue = Mathf.Max(minimumValue, value);
            if (!built) Build();
        }

        public void ConfigureScale(float step, int labeledEvery, float spacing,
            string tapeTitle, string tapeUnit, string format, float floor = 0f,
            bool placeCalloutOnLeft = false)
        {
            if (built) return;
            minorStep = Mathf.Max(0.1f, step);
            majorEvery = Mathf.Max(1, labeledEvery);
            pixelsPerMinorStep = Mathf.Max(4f, spacing);
            title = tapeTitle;
            unit = tapeUnit;
            valueFormat = format;
            minimumValue = floor;
            calloutOnLeft = placeCalloutOnLeft;
        }

        public void SetRangeBands(params RangeBand[] bands)
        {
            if (!built) Build();
            foreach (TapeBandView view in bandViews)
                if (view.Rect != null) Destroy(view.Rect.gameObject);
            bandViews.Clear();
            if (bands == null) return;
            foreach (RangeBand band in bands)
            {
                GameObject go = CreateUI("Speed Range", content);
                RectTransform rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                go.AddComponent<Image>().color = band.Color;
                bandViews.Add(new TapeBandView { Band = band, Rect = rect });
            }
        }

        public void SetWarning(string warning, Color color)
        {
            if (!built) Build();
            warningLabel.text = warning ?? string.Empty;
            warningLabel.color = color;
            warningLabel.gameObject.SetActive(!string.IsNullOrEmpty(warning));
        }

        public void SetTrend(float predictedDelta, bool visible)
        {
            if (!built) Build();
            if (trendLine == null) return;
            trendLine.gameObject.SetActive(visible && Mathf.Abs(predictedDelta) > 0.001f);
            if (!trendLine.gameObject.activeSelf) return;
            float height = Mathf.Abs(predictedDelta) * pixelsPerMinorStep / minorStep;
            trendLine.pivot = predictedDelta >= 0f ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
            trendLine.anchoredPosition = new Vector2(44f, 3f);
            trendLine.sizeDelta = new Vector2(3f, height);
        }

        private void LateUpdate()
        {
            if (!built) return;
            displayedValue = Mathf.SmoothDamp(
                displayedValue, targetValue, ref smoothVelocity,
                smoothingTime, Mathf.Infinity, Time.unscaledDeltaTime);
            displayedNumericValue = Mathf.SmoothDamp(
                displayedNumericValue, targetValue, ref numericSmoothVelocity,
                valueSmoothingTime, Mathf.Infinity, Time.unscaledDeltaTime);
            displayedValue = Mathf.Max(minimumValue, displayedValue);
            displayedNumericValue = Mathf.Max(minimumValue, displayedNumericValue);
            RefreshMarks();
            valueLabel.text = Mathf.RoundToInt(displayedNumericValue).ToString(valueFormat);
        }

        private void Build()
        {
            if (built) return;
            built = true;
            displayedValue = targetValue;
            displayedNumericValue = targetValue;

            RectTransform host = (RectTransform)transform;
            Image legacyPanel = host.GetComponent<Image>();
            if (legacyPanel != null) legacyPanel.enabled = false;
            float oldWidth = host.rect.width;
            float leftEdge = host.anchoredPosition.x - oldWidth * host.pivot.x;
            float rightEdge = host.anchoredPosition.x + oldWidth * (1f - host.pivot.x);
            float instrumentWidth = calloutOnLeft ? 240f : 240f;
            host.sizeDelta = new Vector2(instrumentWidth, 392f);
            host.anchoredPosition = new Vector2(
                calloutOnLeft
                    ? rightEdge - host.sizeDelta.x * (1f - host.pivot.x)
                    : leftEdge + host.sizeDelta.x * host.pivot.x,
                host.anchoredPosition.y);
            // The previous compact FlightHUDInstrument remains as a data target for
            // compatibility, but its child labels must not render through the tape.
            for (int i = 0; i < host.childCount; i++)
                host.GetChild(i).gameObject.SetActive(false);

            GameObject frame = CreateUI("Tape Frame", host);
            RectTransform frameRect = (RectTransform)frame.transform;
            frameRect.anchorMin = new Vector2(0f, 0f);
            frameRect.anchorMax = new Vector2(0f, 1f);
            frameRect.pivot = new Vector2(0f, 0.5f);
            frameRect.anchoredPosition = new Vector2(calloutOnLeft ? 100f : 0f, 0f);
            frameRect.sizeDelta = new Vector2(140f, 0f);
            Image frameImage = frame.AddComponent<Image>();
            frameImage.color = frameColor;

            TMP_Text titleLabel = CreateText("Title", frameRect, 18f, FontStyles.Bold);
            titleLabel.text = title;
            titleLabel.alignment = TextAlignmentOptions.Center;
            titleLabel.color = new Color(0.55f, 0.9f, 1f);
            SetRect(titleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(150f, 28f));

            GameObject viewport = CreateUI("Viewport", frameRect);
            RectTransform viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = new Vector2(8f, 36f);
            viewportRect.offsetMax = new Vector2(-8f, -42f);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.18f);
            viewport.AddComponent<RectMask2D>();

            GameObject contentObject = CreateUI("Scrolling Content", viewportRect);
            content = (RectTransform)contentObject.transform;
            Stretch(content);

            int markCount = visibleMinorSteps + 6;
            for (int i = 0; i < markCount; i++)
            {
                GameObject markObject = CreateUI($"Mark {i}", content);
                RectTransform markRect = (RectTransform)markObject.transform;
                markRect.anchorMin = markRect.anchorMax = new Vector2(0.5f, 0.5f);
                markRect.sizeDelta = new Vector2(150f, 24f);

                GameObject tickObject = CreateUI("Tick", markRect);
                RectTransform tickRect = (RectTransform)tickObject.transform;
                tickRect.anchorMin = tickRect.anchorMax = new Vector2(1f, 0.5f);
                Image tickImage = tickObject.AddComponent<Image>();
                tickImage.color = labelColor;

                TMP_Text label = CreateText("Label", markRect, 19f, FontStyles.Normal);
                label.alignment = TextAlignmentOptions.MidlineRight;
                label.color = labelColor;
                SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(72f, 25f));
                marks.Add(new TapeMark { Rect = markRect, Tick = tickRect, Label = label });
            }

            GameObject centerLine = CreateUI("Center Marker", host);
            RectTransform centerRect = (RectTransform)centerLine.transform;
            centerRect.anchorMin = centerRect.anchorMax = new Vector2(0f, 0.5f);
            centerRect.pivot = new Vector2(0f, 0.5f);
            centerRect.anchoredPosition = new Vector2(calloutOnLeft ? 92f : 136f, 0f);
            centerRect.sizeDelta = new Vector2(calloutOnLeft ? 12f : 18f, 3f);
            centerLine.AddComponent<Image>().color = accentColor;

            GameObject trendObject = CreateUI("Speed Trend", viewportRect);
            trendLine = (RectTransform)trendObject.transform;
            trendLine.anchorMin = trendLine.anchorMax = new Vector2(0.5f, 0.5f);
            trendObject.AddComponent<Image>().color = new Color(
                accentColor.r, accentColor.g, accentColor.b, 0.9f);
            trendObject.SetActive(false);

            GameObject valueBox = CreateUI("Current Value", host);
            RectTransform valueRect = (RectTransform)valueBox.transform;
            valueRect.anchorMin = valueRect.anchorMax = new Vector2(0f, 0.5f);
            valueRect.pivot = new Vector2(0f, 0.5f);
            valueRect.anchoredPosition = new Vector2(calloutOnLeft ? 0f : 150f, 0f);
            valueRect.sizeDelta = new Vector2(calloutOnLeft ? 94f : 92f, 64f);
            valueBox.AddComponent<Image>().color = valueBoxColor;
            Outline valueOutline = valueBox.AddComponent<Outline>();
            valueOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.9f);
            valueOutline.effectDistance = new Vector2(1f, -1f);
            valueLabel = CreateText("Value", valueRect, 30f, FontStyles.Bold);
            valueLabel.alignment = TextAlignmentOptions.Center;
            valueLabel.color = accentColor;
            valueLabel.enableAutoSizing = true;
            valueLabel.fontSizeMin = 21f;
            valueLabel.fontSizeMax = 30f;
            SetRect(valueLabel.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 2f), new Vector2(84f, 30f));

            TMP_Text iasLabel = CreateText("Value Label", valueRect, 12f, FontStyles.Bold);
            iasLabel.text = title == "AIRSPEED" ? "IAS" : title;
            iasLabel.alignment = TextAlignmentOptions.Center;
            iasLabel.color = labelColor;
            SetRect(iasLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -9f), new Vector2(80f, 16f));

            TMP_Text unitLabel = CreateText("Unit", valueRect, 15f, FontStyles.Bold);
            unitLabel.text = unit;
            unitLabel.color = labelColor;
            SetRect(unitLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(80f, 18f));

            warningLabel = CreateText("Tape Warning", frameRect, 14f, FontStyles.Bold);
            warningLabel.alignment = TextAlignmentOptions.Center;
            SetRect(warningLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(156f, 24f));
            warningLabel.gameObject.SetActive(false);
            RefreshMarks();
        }

        private void RefreshMarks()
        {
            int centerIndex = Mathf.FloorToInt(displayedValue / minorStep);
            float centerValue = centerIndex * minorStep;
            float fraction = (displayedValue - centerValue) / minorStep;
            int half = marks.Count / 2;

            for (int i = 0; i < marks.Count; i++)
            {
                int index = centerIndex + (i - half);
                float value = index * minorStep;
                bool visible = value >= minimumValue;
                TapeMark mark = marks[i];
                mark.Rect.gameObject.SetActive(visible);
                if (!visible) continue;
                mark.Rect.anchoredPosition = new Vector2(0f,
                    ((i - half) - fraction) * pixelsPerMinorStep);
                bool major = Mod(index, majorEvery) == 0;
                mark.Tick.sizeDelta = new Vector2(major ? 32f : 17f, major ? 2.5f : 1.5f);
                mark.Tick.anchoredPosition = new Vector2(-2f, 0f);
                mark.Label.gameObject.SetActive(major);
                if (major) mark.Label.text = Mathf.RoundToInt(value).ToString("000");
            }

            float pixelsPerUnit = pixelsPerMinorStep / minorStep;
            foreach (TapeBandView view in bandViews)
            {
                float start = Mathf.Max(minimumValue, view.Band.Start);
                float end = Mathf.Max(start, view.Band.End);
                float midpoint = (start + end) * 0.5f;
                view.Rect.anchoredPosition = new Vector2(2f, (midpoint - displayedValue) * pixelsPerUnit);
                view.Rect.sizeDelta = new Vector2(7f, Mathf.Max(3f, (end - start) * pixelsPerUnit));
            }
        }

        private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
        private static GameObject CreateUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
        private static TMP_Text CreateText(string name, Transform parent, float size, FontStyles style)
        {
            TMP_Text text = CreateUI(name, parent).AddComponent<TextMeshProUGUI>();
            text.fontSize = size; text.fontStyle = style; text.raycastTarget = false;
            return text;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
    }
}
