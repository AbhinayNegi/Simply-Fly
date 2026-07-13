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
        [SerializeField] private float minimumValue;
        [Header("Formatting")]
        [SerializeField] private string title = "AIRSPEED";
        [SerializeField] private string unit = "KT";
        [SerializeField] private string valueFormat = "000";

        private readonly List<TapeMark> marks = new List<TapeMark>();
        private RectTransform content;
        private TMP_Text valueLabel;
        private float targetValue;
        private float displayedValue;
        private float smoothVelocity;
        private bool built;

        private sealed class TapeMark
        {
            public RectTransform Rect;
            public RectTransform Tick;
            public TMP_Text Label;
        }

        public void SetValue(float value)
        {
            targetValue = Mathf.Max(minimumValue, value);
            if (!built) Build();
        }

        private void Awake() => Build();

        private void LateUpdate()
        {
            if (!built) return;
            displayedValue = Mathf.SmoothDamp(
                displayedValue, targetValue, ref smoothVelocity,
                smoothingTime, Mathf.Infinity, Time.unscaledDeltaTime);
            RefreshMarks();
            valueLabel.text = Mathf.RoundToInt(displayedValue).ToString(valueFormat);
        }

        private void Build()
        {
            if (built) return;
            built = true;
            displayedValue = targetValue;

            RectTransform host = (RectTransform)transform;
            host.sizeDelta = new Vector2(178f, 392f);

            GameObject frame = CreateUI("Tape Frame", host);
            RectTransform frameRect = (RectTransform)frame.transform;
            Stretch(frameRect);
            Image frameImage = frame.AddComponent<Image>();
            frameImage.color = new Color(0.015f, 0.035f, 0.055f, 0.86f);

            TMP_Text titleLabel = CreateText("Title", frameRect, 18f, FontStyles.Bold);
            titleLabel.text = title;
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
                tickImage.color = new Color(0.78f, 0.9f, 0.95f, 0.95f);

                TMP_Text label = CreateText("Label", markRect, 19f, FontStyles.Normal);
                label.alignment = TextAlignmentOptions.MidlineRight;
                label.color = new Color(0.88f, 0.95f, 1f);
                SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(72f, 25f));
                marks.Add(new TapeMark { Rect = markRect, Tick = tickRect, Label = label });
            }

            GameObject centerLine = CreateUI("Center Marker", frameRect);
            RectTransform centerRect = (RectTransform)centerLine.transform;
            centerRect.anchorMin = centerRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerRect.sizeDelta = new Vector2(166f, 3f);
            centerLine.AddComponent<Image>().color = new Color(0.15f, 0.95f, 1f, 0.95f);

            GameObject valueBox = CreateUI("Current Speed", frameRect);
            RectTransform valueRect = (RectTransform)valueBox.transform;
            valueRect.anchorMin = valueRect.anchorMax = new Vector2(1f, 0.5f);
            valueRect.pivot = new Vector2(1f, 0.5f);
            valueRect.anchoredPosition = new Vector2(-2f, 0f);
            valueRect.sizeDelta = new Vector2(92f, 50f);
            valueBox.AddComponent<Image>().color = new Color(0.025f, 0.12f, 0.17f, 0.98f);
            valueLabel = CreateText("Value", valueRect, 30f, FontStyles.Bold);
            valueLabel.alignment = TextAlignmentOptions.Center;
            valueLabel.color = new Color(0.35f, 1f, 1f);
            Stretch(valueLabel.rectTransform);

            TMP_Text unitLabel = CreateText("Unit", frameRect, 15f, FontStyles.Bold);
            unitLabel.text = unit;
            unitLabel.color = new Color(0.55f, 0.9f, 1f);
            SetRect(unitLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 17f), new Vector2(140f, 25f));
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
