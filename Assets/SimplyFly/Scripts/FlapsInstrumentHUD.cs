using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    [DisallowMultipleComponent]
    public sealed class FlapsInstrumentHUD : MonoBehaviour
    {
        [SerializeField] private FlapController flaps;
        [Min(0.01f)] [SerializeField] private float transitionSmoothing = 0.15f;

        private readonly List<TMP_Text> positionLabels = new();
        private readonly List<GameObject> generatedPositionObjects = new();
        private RectTransform marker;
        private float markerY;
        private float markerVelocity;
        private int configuredPositionCount = -1;

        private static readonly Color Cyan = FlightHUDStyle.Cyan;
        private static readonly Color White = FlightHUDStyle.White;
        private static readonly Color Dim = new(0.68f, 0.78f, 0.82f, 0.62f);

        public void Bind(FlapController source)
        {
            flaps = source;
            RebuildPositions();
        }

        private void Awake()
        {
            if (flaps == null) flaps = FindAnyObjectByType<FlapController>();
            BuildFrame();
            RebuildPositions();
        }

        private void LateUpdate()
        {
            bool available = flaps != null && flaps.PositionCount > 1;
            if (gameObject.activeSelf != available)
            {
                gameObject.SetActive(available);
                if (!available) return;
            }
            if (!available) return;
            if (configuredPositionCount != flaps.PositionCount) RebuildPositions();

            float targetY = PositionYForAngle(flaps.ActualAngle);
            markerY = Mathf.SmoothDamp(markerY, targetY, ref markerVelocity,
                transitionSmoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            marker.anchoredPosition = new Vector2(9f, markerY);

            for (int i = 0; i < positionLabels.Count; i++)
            {
                bool selected = i == flaps.SelectedIndex;
                positionLabels[i].color = selected ? Cyan : Dim;
                positionLabels[i].fontStyle = selected
                    ? FontStyles.Bold
                    : FontStyles.Normal;
            }
        }

        private void BuildFrame()
        {
            RectTransform host = (RectTransform)transform;
            host.anchorMin = host.anchorMax = new Vector2(0.5f, 0f);
            host.pivot = new Vector2(0f, 0f);
            // Mini attitude is 220 px wide; keep a 45 px visual gutter.
            host.anchoredPosition = new Vector2(155f, FlightHUDStyle.BottomSafeMargin);
            host.sizeDelta = new Vector2(120f, 206f);

            Image panel = gameObject.AddComponent<Image>();
            panel.color = FlightHUDStyle.SecondaryPanel;
            FlightHUDStyle.AddOutline(gameObject);

            TMP_Text title = Text("Title", 14f, FontStyles.Bold,
                new Vector2(8f, -7f), new Vector2(88f, 20f));
            title.text = "FLAPS";
            title.color = White;

            marker = Rect("Position Marker");
            marker.anchorMin = marker.anchorMax = new Vector2(0f, 1f);
            marker.pivot = new Vector2(0f, 0.5f);
            marker.sizeDelta = new Vector2(12f, 4f);
            marker.gameObject.AddComponent<Image>().color = Cyan;
        }

        private void RebuildPositions()
        {
            foreach (GameObject generated in generatedPositionObjects)
                if (generated != null) Destroy(generated);
            positionLabels.Clear();
            generatedPositionObjects.Clear();
            configuredPositionCount = flaps == null ? 0 : flaps.PositionCount;
            if (configuredPositionCount <= 1)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            for (int i = 0; i < configuredPositionCount; i++)
            {
                TMP_Text label = Text($"Position {i}", 14f, FontStyles.Normal,
                    new Vector2(19f, PositionY(i) - 10f), new Vector2(76f, 20f));
                label.text = $"{flaps.GetPositionAngle(i):0}°";
                label.alignment = TextAlignmentOptions.Left;
                positionLabels.Add(label);
                generatedPositionObjects.Add(label.gameObject);

                RectTransform tick = Rect($"Tick {i}");
                tick.anchorMin = tick.anchorMax = new Vector2(0f, 1f);
                tick.pivot = new Vector2(0f, 0.5f);
                tick.anchoredPosition = new Vector2(10f, PositionY(i));
                tick.sizeDelta = new Vector2(7f, 1f);
                tick.gameObject.AddComponent<Image>().color = White;
                generatedPositionObjects.Add(tick.gameObject);
            }
            markerY = PositionYForAngle(flaps.ActualAngle);
        }

        private float PositionYForAngle(float angle)
        {
            if (flaps == null || configuredPositionCount <= 1) return -44f;
            for (int i = 0; i < configuredPositionCount - 1; i++)
            {
                float from = flaps.GetPositionAngle(i);
                float to = flaps.GetPositionAngle(i + 1);
                if (angle > to && i < configuredPositionCount - 2) continue;
                float t = Mathf.InverseLerp(from, to, angle);
                return Mathf.Lerp(PositionY(i), PositionY(i + 1), t);
            }
            return PositionY(configuredPositionCount - 1);
        }

        private float PositionY(int index)
        {
            float t = configuredPositionCount <= 1
                ? 0f
                : index / (float)(configuredPositionCount - 1);
            // Keep the final label and degree glyph within the 170 px panel.
            return Mathf.Lerp(-50f, -174f, t);
        }

        private TMP_Text Text(string name, float size, FontStyles style,
            Vector2 position, Vector2 dimensions)
        {
            TMP_Text label = Rect(name).gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = dimensions;
            return label;
        }

        private RectTransform Rect(string name)
        {
            GameObject child = new(name, typeof(RectTransform));
            child.transform.SetParent(transform, false);
            return (RectTransform)child.transform;
        }
    }
}
