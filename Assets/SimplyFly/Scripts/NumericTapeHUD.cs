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
        [SerializeField] private Color frameColor = new Color(0.01f, 0.03f, 0.045f, 0.89f);
        [SerializeField] private Color accentColor = new Color(0.15f, 0.95f, 1f, 0.98f);
        [SerializeField] private Color labelColor = new Color(0.88f, 0.95f, 1f, 0.94f);
        [SerializeField] private Color valueBoxColor = new Color(0.018f, 0.09f, 0.12f, 0.96f);

        private readonly List<TapeMark> marks = new List<TapeMark>();
        private readonly List<TapeBandView> bandViews = new List<TapeBandView>();
        private RectTransform content;
        private TMP_Text valueLabel;
        private TMP_Text warningLabel;
        private GameObject warningIcon;
        private RectTransform trendLine;
        private float targetValue;
        private float displayedValue;
        private float smoothVelocity;
        private float displayedNumericValue;
        private float numericSmoothVelocity;
        private bool built;
        private int mediumTickEvery;
        private bool airspeedPresentation;
        private bool altitudePresentation;

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
            public Image TickImage;
            public RectTransform OppositeTick;
            public Image OppositeTickImage;
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

        public void ConfigureAirspeedPresentation()
        {
            if (built) return;
            airspeedPresentation = true;
            mediumTickEvery = 5;
            visibleMinorSteps = 72;
        }

        public void ConfigureAltitudePresentation()
        {
            if (built) return;
            altitudePresentation = true;
            mediumTickEvery = 5;
            visibleMinorSteps = 72;
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
                rect.anchorMin = rect.anchorMax = new Vector2(
                    altitudePresentation ? 1f : 0f, 0.5f);
                rect.pivot = new Vector2(altitudePresentation ? 1f : 0f, 0.5f);
                go.AddComponent<Image>().color = band.Color;
                bandViews.Add(new TapeBandView { Band = band, Rect = rect });
            }
        }

        public void SetWarning(string warning, Color color)
        {
            if (!built) Build();
            warningLabel.text = warning ?? string.Empty;
            warningLabel.color = color;
            bool visible = !string.IsNullOrEmpty(warning);
            warningLabel.gameObject.SetActive(visible);
            if (warningIcon != null) warningIcon.SetActive(visible);
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
            frameImage.color = FlightHUDStyle.PrimaryPanel;
            FlightHUDStyle.AddOutline(frame);

            TMP_Text titleLabel = CreateText("Title", frameRect, 18f, FontStyles.Bold);
            titleLabel.text = title;
            titleLabel.alignment = TextAlignmentOptions.Center;
            bool polishedTape = airspeedPresentation || altitudePresentation;
            titleLabel.color = polishedTape
                ? FlightHUDStyle.White
                : new Color(0.55f, 0.9f, 1f);
            SetRect(titleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(150f, 28f));

            if (polishedTape)
            {
                TMP_Text headerUnit = CreateText("Header Unit", frameRect, 14f, FontStyles.Normal);
                headerUnit.text = unit;
                headerUnit.alignment = TextAlignmentOptions.Center;
                headerUnit.color = FlightHUDStyle.Cyan;
                SetRect(headerUnit.rectTransform, new Vector2(0.5f, 1f),
                    new Vector2(0f, -40f), new Vector2(80f, 20f));
                RectTransform headerDivider = (RectTransform)CreateUI("Header Divider", frameRect).transform;
                SetRect(headerDivider, new Vector2(0.5f, 1f),
                    new Vector2(0f, -57f), new Vector2(118f, 1f));
                headerDivider.gameObject.AddComponent<Image>().color = FlightHUDStyle.Divider;
            }

            GameObject viewport = CreateUI("Viewport", frameRect);
            RectTransform viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = new Vector2(8f, 36f);
            viewportRect.offsetMax = new Vector2(-8f, polishedTape ? -62f : -42f);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.25f);
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

                RectTransform oppositeTick = null;
                Image oppositeTickImage = null;
                if (altitudePresentation)
                {
                    GameObject oppositeTickObject = CreateUI("Opposite Tick", markRect);
                    oppositeTick = (RectTransform)oppositeTickObject.transform;
                    oppositeTick.anchorMin = oppositeTick.anchorMax = new Vector2(0f, 0.5f);
                    oppositeTick.pivot = new Vector2(0f, 0.5f);
                    oppositeTickImage = oppositeTickObject.AddComponent<Image>();
                    oppositeTickImage.color = labelColor;
                }

                TMP_Text label = CreateText("Label", markRect, 19f, FontStyles.Normal);
                label.alignment = TextAlignmentOptions.MidlineRight;
                label.color = labelColor;
                SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(72f, 25f));
                marks.Add(new TapeMark
                {
                    Rect = markRect,
                    Tick = tickRect,
                    TickImage = tickImage,
                    OppositeTick = oppositeTick,
                    OppositeTickImage = oppositeTickImage,
                    Label = label
                });
            }

            if (altitudePresentation)
            {
                GameObject rangeTrack = CreateUI("Operating Range Track", viewportRect);
                RectTransform rangeTrackRect = (RectTransform)rangeTrack.transform;
                rangeTrackRect.anchorMin = new Vector2(1f, 0f);
                rangeTrackRect.anchorMax = new Vector2(1f, 1f);
                rangeTrackRect.pivot = new Vector2(1f, 0.5f);
                rangeTrackRect.anchoredPosition = new Vector2(-2f, 0f);
                rangeTrackRect.sizeDelta = new Vector2(7f, 0f);
                rangeTrack.AddComponent<Image>().color = new Color(0.23f, 0.3f, 0.33f, 0.55f);
            }

            GameObject centerLine = CreateUI("Readout Connector", host);
            RectTransform centerRect = (RectTransform)centerLine.transform;
            centerRect.anchorMin = centerRect.anchorMax = new Vector2(0f, 0.5f);
            centerRect.pivot = new Vector2(0f, 0.5f);
            // The callout and tape are separated by a fixed 10 px gutter.
            // Draw only inside that gutter so the connector never crosses the tape.
            centerRect.anchoredPosition = new Vector2(
                altitudePresentation ? 90f : 140f, 0f);
            centerRect.sizeDelta = new Vector2(10f, 3f);
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
            valueRect.anchoredPosition = new Vector2(
                altitudePresentation ? -18f : calloutOnLeft ? 0f : 150f, 0f);
            valueRect.sizeDelta = new Vector2(108f, 76f);
            valueBox.AddComponent<Image>().color = FlightHUDStyle.ValueBox;
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
                new Vector2(0f, 2f), new Vector2(90f, 32f));

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
            SetRect(warningLabel.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(airspeedPresentation ? 12f : 0f, 45f),
                new Vector2(airspeedPresentation ? 112f : 156f, 24f));
            if (airspeedPresentation)
            {
                RectTransform footerDivider = (RectTransform)CreateUI("Warning Divider", frameRect).transform;
                SetRect(footerDivider, new Vector2(0.5f, 0f),
                    new Vector2(0f, 70f), new Vector2(118f, 1f));
                footerDivider.gameObject.AddComponent<Image>().color = FlightHUDStyle.Divider;
                warningIcon = CreateUI("Warning Icon", frameRect);
                RectTransform warningIconRect = (RectTransform)warningIcon.transform;
                warningIconRect.anchorMin = warningIconRect.anchorMax = new Vector2(0.5f, 0f);
                warningIconRect.anchoredPosition = new Vector2(-50f, 47f);
                warningIconRect.sizeDelta = new Vector2(18f, 16f);
                TapeTriangleGraphic warningTriangle =
                    warningIcon.AddComponent<TapeTriangleGraphic>();
                warningTriangle.color = new Color(1f, 0.66f, 0.08f, 0.98f);
                warningTriangle.raycastTarget = false;
                TMP_Text exclamation = CreateText("Warning Mark", warningIcon.transform, 11f, FontStyles.Bold);
                exclamation.text = "!";
                exclamation.color = new Color(0.08f, 0.06f, 0.01f, 1f);
                exclamation.alignment = TextAlignmentOptions.Center;
                Stretch(exclamation.rectTransform);
                warningIcon.SetActive(false);
            }
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
                bool medium = !major && mediumTickEvery > 0 &&
                              Mod(index, mediumTickEvery) == 0;
                mark.Tick.sizeDelta = new Vector2(
                    major ? 32f : medium ? 24f : 13f,
                    major ? 2.5f : medium ? 2f : 1.25f);
                mark.Tick.anchoredPosition = new Vector2(-2f, 0f);
                if (mark.OppositeTick != null)
                {
                    mark.OppositeTick.sizeDelta = new Vector2(
                        major ? 32f : medium ? 24f : 13f,
                        major ? 2.5f : medium ? 2f : 1.25f);
                    mark.OppositeTick.anchoredPosition = new Vector2(2f, 0f);
                }
                mark.Label.gameObject.SetActive(major);
                if (major) mark.Label.text = Mathf.RoundToInt(value).ToString("000");
                if (altitudePresentation)
                {
                    float distance = Mathf.Abs(mark.Rect.anchoredPosition.y);
                    float alpha = Mathf.Lerp(0.38f, 1f, 1f - Mathf.Clamp01(distance / 170f));
                    Color faded = labelColor;
                    faded.a *= alpha;
                    mark.Label.color = faded;
                    mark.TickImage.color = faded;
                    if (mark.OppositeTickImage != null) mark.OppositeTickImage.color = faded;
                }
            }

            float pixelsPerUnit = pixelsPerMinorStep / minorStep;
            foreach (TapeBandView view in bandViews)
            {
                float start = Mathf.Max(minimumValue, view.Band.Start);
                float end = Mathf.Max(start, view.Band.End);
                float midpoint = (start + end) * 0.5f;
                view.Rect.anchoredPosition = new Vector2(
                    altitudePresentation ? -2f : 2f,
                    (midpoint - displayedValue) * pixelsPerUnit);
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

    internal sealed class TapeTriangleGraphic : MaskableGraphic
    {
        public bool PointsRight { get; set; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            Color32 c = color;
            if (PointsRight)
            {
                vh.AddVert(new Vector3(rect.width * 0.5f, 0f), c, Vector2.zero);
                vh.AddVert(new Vector3(-rect.width * 0.5f, rect.height * 0.5f),
                    c, Vector2.zero);
                vh.AddVert(new Vector3(-rect.width * 0.5f, -rect.height * 0.5f),
                    c, Vector2.zero);
            }
            else
            {
                vh.AddVert(new Vector3(0f, rect.height * 0.5f), c, Vector2.zero);
                vh.AddVert(new Vector3(-rect.width * 0.5f, -rect.height * 0.5f),
                    c, Vector2.zero);
                vh.AddVert(new Vector3(rect.width * 0.5f, -rect.height * 0.5f),
                    c, Vector2.zero);
            }
            vh.AddTriangle(0, 1, 2);
        }
    }
}
