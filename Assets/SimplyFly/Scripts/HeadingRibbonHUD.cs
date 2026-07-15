using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    /// <summary>Physics-agnostic presentation for a horizontally scrolling heading ribbon.</summary>
    [DisallowMultipleComponent]
    public sealed class HeadingRibbonHUD : MonoBehaviour
    {
        [Min(10f)] [SerializeField] private float pixelsPerFiveDegrees = 34f;
        [Range(15, 41)] [SerializeField] private int visibleMarkCount = 35;
        [Range(0.1f, 0.45f)] [SerializeField] private float edgeFadeFraction = 0.27f;

        private readonly List<HeadingMark> marks = new List<HeadingMark>();
        private RectTransform viewport;
        private TMP_Text currentHeadingLabel;
        private TMP_Text headingCaption;
        private RectTransform courseMarker;
        private TMP_Text courseLabel;
        private TMP_Text leftCourseArrow;
        private TMP_Text rightCourseArrow;
        private TMP_Text waypointLabel;
        private RectTransform waypointPanel;
        private CanvasGroup courseMarkerGroup;
        private CanvasGroup leftArrowGroup;
        private CanvasGroup rightArrowGroup;
        private bool built;
        private float heading;
        private bool navigationVisible;
        private float courseHeading;
        private string waypointText;

        private sealed class HeadingMark
        {
            public RectTransform Rect;
            public RectTransform Tick;
            public TMP_Text Label;
            public CanvasGroup Group;
        }

        public void SetHeading(float continuousHeading)
        {
            heading = continuousHeading;
            if (!built) Build();
        }

        public void SetNavigationGuidance(bool visible, float continuousCourse,
            string waypoint, float distanceNauticalMiles)
        {
            navigationVisible = visible;
            courseHeading = continuousCourse;
            waypointText = visible
                ? $"{waypoint}   {distanceNauticalMiles:0.0} NM"
                : string.Empty;
            if (!built) Build();
        }

        private void LateUpdate()
        {
            if (!built) return;
            Refresh();
            int rounded = Mathf.RoundToInt(heading) % 360;
            if (rounded < 0) rounded += 360;
            currentHeadingLabel.text = rounded.ToString("000");
            RefreshNavigation();
            UpdateAttentionPulse();
        }

        private void Build()
        {
            built = true;
            RectTransform host = (RectTransform)transform;
            Image legacyPanel = host.GetComponent<Image>();
            if (legacyPanel != null) legacyPanel.enabled = false;
            for (int i = 0; i < host.childCount; i++) host.GetChild(i).gameObject.SetActive(false);

            host.anchorMin = host.anchorMax = new Vector2(0.5f, 1f);
            host.pivot = new Vector2(0.5f, 1f);
            host.anchoredPosition = new Vector2(0f, -22f);
            host.sizeDelta = new Vector2(1020f, 128f);

            viewport = CreateRect("Heading Viewport", host);
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 0f);
            viewport.pivot = new Vector2(0.5f, 0f);
            viewport.anchoredPosition = new Vector2(0f, 4f);
            viewport.sizeDelta = new Vector2(0f, 66f);
            viewport.gameObject.AddComponent<RectMask2D>();

            for (int i = 0; i < visibleMarkCount; i++)
            {
                RectTransform mark = CreateRect($"Heading Mark {i}", viewport);
                mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 0f);
                mark.pivot = new Vector2(0.5f, 0f);
                mark.sizeDelta = new Vector2(48f, 62f);
                CanvasGroup group = mark.gameObject.AddComponent<CanvasGroup>();

                RectTransform tick = CreateRect("Tick", mark);
                tick.anchorMin = tick.anchorMax = new Vector2(0.5f, 0f);
                tick.pivot = new Vector2(0.5f, 0f);
                tick.anchoredPosition = Vector2.zero;
                tick.gameObject.AddComponent<Image>().color = new Color(0.92f, 0.97f, 1f, 0.95f);

                TMP_Text label = CreateText("Label", mark, 18f, FontStyles.Normal);
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(0.92f, 0.97f, 1f, 0.98f);
                label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                label.rectTransform.anchoredPosition = new Vector2(0f, 34f);
                label.rectTransform.sizeDelta = new Vector2(62f, 26f);
                marks.Add(new HeadingMark { Rect = mark, Tick = tick, Label = label, Group = group });
            }

            RectTransform pointerLine = CreateRect("Center Pointer", host);
            pointerLine.anchorMin = pointerLine.anchorMax = new Vector2(0.5f, 0f);
            pointerLine.pivot = new Vector2(0.5f, 0f);
            pointerLine.anchoredPosition = new Vector2(0f, 4f);
            pointerLine.sizeDelta = new Vector2(3f, 40f);
            pointerLine.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.95f, 1f, 0.98f);

            RectTransform valueBox = CreateRect("Current Heading", host);
            valueBox.anchorMin = valueBox.anchorMax = new Vector2(0.5f, 0f);
            valueBox.pivot = new Vector2(0.5f, 0f);
            valueBox.anchoredPosition = new Vector2(0f, 64f);
            valueBox.sizeDelta = new Vector2(112f, 58f);
            Image valueBackground = valueBox.gameObject.AddComponent<Image>();
            valueBackground.color = FlightHUDStyle.ValueBox;
            FlightHUDStyle.ApplyRoundedPanel(valueBackground);
            Outline outline = valueBox.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.95f, 1f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            currentHeadingLabel = CreateText("Value", valueBox, 29f, FontStyles.Bold);
            currentHeadingLabel.alignment = TextAlignmentOptions.Center;
            currentHeadingLabel.color = FlightHUDStyle.Cyan;
            Stretch(currentHeadingLabel.rectTransform);
            currentHeadingLabel.rectTransform.offsetMax = new Vector2(0f, -12f);
            headingCaption = CreateText("Heading Caption", valueBox, 10f, FontStyles.Normal);
            headingCaption.text = "HDG";
            headingCaption.color = FlightHUDStyle.Muted;
            headingCaption.alignment = TextAlignmentOptions.Center;
            headingCaption.rectTransform.anchorMin = new Vector2(0f, 1f);
            headingCaption.rectTransform.anchorMax = new Vector2(1f, 1f);
            headingCaption.rectTransform.pivot = new Vector2(0.5f, 1f);
            headingCaption.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            headingCaption.rectTransform.sizeDelta = new Vector2(0f, 14f);

            Color magenta = new Color(1f, 0.08f, 0.78f, 1f);
            courseMarker = CreateRect("Course Marker", host);
            courseMarker.anchorMin = courseMarker.anchorMax = new Vector2(0.5f, 0f);
            courseMarker.pivot = new Vector2(0.5f, 0f);
            courseMarker.anchoredPosition = new Vector2(0f, 4f);
            courseMarker.sizeDelta = new Vector2(8f, 48f);
            courseMarker.gameObject.AddComponent<Image>().color = magenta;
            courseMarkerGroup = courseMarker.gameObject.AddComponent<CanvasGroup>();
            courseLabel = CreateText("Course Value", courseMarker, 20f, FontStyles.Bold);
            courseLabel.alignment = TextAlignmentOptions.Center;
            courseLabel.color = magenta;
            courseLabel.rectTransform.anchorMin = courseLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            // Keep the selected-course number inside the compass ribbon. At
            // small heading errors, placing it above the marker collides with
            // the current-heading value box.
            courseLabel.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            courseLabel.rectTransform.sizeDelta = new Vector2(70f, 28f);
            AddTextContrast(courseLabel, magenta);
            courseLabel.gameObject.SetActive(false);

            leftCourseArrow = CreateText("Course Left", host, 44f, FontStyles.Bold);
            leftCourseArrow.text = "<<"; leftCourseArrow.color = magenta;
            leftCourseArrow.alignment = TextAlignmentOptions.Center;
            leftCourseArrow.rectTransform.anchorMin = leftCourseArrow.rectTransform.anchorMax = new Vector2(0f, 0f);
            leftCourseArrow.rectTransform.anchoredPosition = new Vector2(30f, 25f);
            leftCourseArrow.rectTransform.sizeDelta = new Vector2(70f, 50f);
            leftArrowGroup = leftCourseArrow.gameObject.AddComponent<CanvasGroup>();
            AddTextContrast(leftCourseArrow, magenta);
            rightCourseArrow = CreateText("Course Right", host, 44f, FontStyles.Bold);
            rightCourseArrow.text = ">>"; rightCourseArrow.color = magenta;
            rightCourseArrow.alignment = TextAlignmentOptions.Center;
            rightCourseArrow.rectTransform.anchorMin = rightCourseArrow.rectTransform.anchorMax = new Vector2(1f, 0f);
            rightCourseArrow.rectTransform.anchoredPosition = new Vector2(-30f, 25f);
            rightCourseArrow.rectTransform.sizeDelta = new Vector2(70f, 50f);
            rightArrowGroup = rightCourseArrow.gameObject.AddComponent<CanvasGroup>();
            AddTextContrast(rightCourseArrow, magenta);

            waypointPanel = CreateRect("Waypoint Panel", host);
            waypointPanel.anchorMin = waypointPanel.anchorMax = new Vector2(0.5f, 0f);
            waypointPanel.anchoredPosition = new Vector2(0f, -18f);
            waypointPanel.sizeDelta = new Vector2(300f, 28f);
            waypointPanel.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.025f, 0.05f, 0.82f);
            Outline waypointOutline = waypointPanel.gameObject.AddComponent<Outline>();
            waypointOutline.effectColor = new Color(magenta.r, magenta.g, magenta.b, 0.72f);
            waypointOutline.effectDistance = new Vector2(1f, -1f);
            waypointLabel = CreateText("Active Waypoint", waypointPanel, 16f, FontStyles.Bold);
            waypointLabel.alignment = TextAlignmentOptions.Center;
            waypointLabel.color = magenta;
            Stretch(waypointLabel.rectTransform);
            AddTextContrast(waypointLabel, magenta);
            Refresh();
            RefreshNavigation();
        }

        private void RefreshNavigation()
        {
            if (courseMarker == null) return;
            courseMarker.gameObject.SetActive(false);
            leftCourseArrow.gameObject.SetActive(false);
            rightCourseArrow.gameObject.SetActive(false);
            waypointLabel.gameObject.SetActive(navigationVisible);
            waypointPanel.gameObject.SetActive(navigationVisible);
            if (!navigationVisible) return;

            float delta = courseHeading - heading;
            float x = delta / 5f * pixelsPerFiveDegrees;
            float limit = viewport.rect.width * 0.5f - 28f;
            bool onScreen = Mathf.Abs(x) <= limit;
            courseMarker.gameObject.SetActive(onScreen);
            if (onScreen)
            {
                courseMarker.anchoredPosition = new Vector2(x, 4f);
            }
            else if (x < 0f) leftCourseArrow.gameObject.SetActive(true);
            else rightCourseArrow.gameObject.SetActive(true);
            waypointLabel.text = waypointText;
        }

        private void UpdateAttentionPulse()
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7.5f);
            wave = Mathf.SmoothStep(0f, 1f, wave);
            float pulse = Mathf.Lerp(0.18f, 1f, wave);
            float pulseScale = Mathf.Lerp(0.88f, 1.38f, wave);
            if (courseMarkerGroup != null && courseMarker.gameObject.activeSelf)
            {
                courseMarkerGroup.alpha = 1f;
                courseMarker.localScale = Vector3.one;
            }
            if (leftArrowGroup != null && leftCourseArrow.gameObject.activeSelf)
            {
                leftArrowGroup.alpha = pulse;
                leftCourseArrow.rectTransform.localScale = Vector3.one * pulseScale;
            }
            if (rightArrowGroup != null && rightCourseArrow.gameObject.activeSelf)
            {
                rightArrowGroup.alpha = pulse;
                rightCourseArrow.rectTransform.localScale = Vector3.one * pulseScale;
            }
        }

        private static void AddTextContrast(TMP_Text text, Color accent)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void Refresh()
        {
            int centerIndex = Mathf.FloorToInt(heading / 5f);
            float centerDegrees = centerIndex * 5f;
            float fraction = (heading - centerDegrees) / 5f;
            int half = marks.Count / 2;
            float halfWidth = viewport.rect.width * 0.5f;
            float fadeStart = halfWidth * (1f - edgeFadeFraction);

            for (int i = 0; i < marks.Count; i++)
            {
                int absoluteIndex = centerIndex + i - half;
                float degrees = absoluteIndex * 5f;
                float x = ((i - half) - fraction) * pixelsPerFiveDegrees;
                HeadingMark mark = marks[i];
                mark.Rect.anchoredPosition = new Vector2(x, 0f);

                int normalized = ((Mathf.RoundToInt(degrees) % 360) + 360) % 360;
                bool major = normalized % 10 == 0;
                bool labeled = normalized % 30 == 0;
                mark.Tick.sizeDelta = new Vector2(major ? 2f : 1.5f, major ? 22f : 12f);
                mark.Label.gameObject.SetActive(labeled);
                if (labeled) mark.Label.text = HeadingLabel(normalized);

                float distance = Mathf.Abs(x);
                mark.Group.alpha = distance <= fadeStart
                    ? 1f
                    : Mathf.Clamp01((halfWidth - distance) / Mathf.Max(1f, halfWidth - fadeStart));
            }
        }

        private static string HeadingLabel(int headingValue)
        {
            switch (headingValue)
            {
                case 0: return "N";
                case 90: return "E";
                case 180: return "S";
                case 270: return "W";
                default: return headingValue.ToString("000");
            }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        private static TMP_Text CreateText(string name, Transform parent, float size, FontStyles style)
        {
            TMP_Text text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size; text.fontStyle = style; text.raycastTarget = false; return text;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
