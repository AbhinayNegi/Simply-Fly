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
        private float markerX;
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

            float targetX = PositionXForAngle(flaps.ActualAngle);
            markerX = Mathf.SmoothDamp(markerX, targetX, ref markerVelocity,
                transitionSmoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            marker.anchoredPosition = new Vector2(markerX, -36f);

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
            host.anchorMin = host.anchorMax = Vector2.zero;
            host.pivot = new Vector2(0f, 0f);
            // Follow the attitude panel with the shared 24 px gutter.
            host.anchoredPosition = new Vector2(
                646f, FlightHUDStyle.BottomSafeMargin);
            host.sizeDelta = new Vector2(190f, 72f);

            Image panel = gameObject.AddComponent<Image>();
            panel.color = FlightHUDStyle.SecondaryPanel;
            FlightHUDStyle.AddOutline(gameObject);

            TMP_Text title = Text("Title", 14f, FontStyles.Bold,
                new Vector2(8f, -5f), new Vector2(174f, 20f));
            title.text = "FLAPS";
            title.color = White;

            RectTransform track = Rect("Position Track");
            track.anchorMin = track.anchorMax = new Vector2(0f, 1f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.anchoredPosition = new Vector2(95f, -36f);
            track.sizeDelta = new Vector2(142f, 1f);
            track.gameObject.AddComponent<Image>().color = Dim;

            marker = Rect("Position Marker");
            marker.anchorMin = marker.anchorMax = new Vector2(0f, 1f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(3f, 15f);
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
                    new Vector2(PositionX(i) - 19f, -43f), new Vector2(38f, 20f));
                label.text = $"{flaps.GetPositionAngle(i):0}°";
                label.alignment = TextAlignmentOptions.Center;
                positionLabels.Add(label);
                generatedPositionObjects.Add(label.gameObject);

                RectTransform tick = Rect($"Tick {i}");
                tick.anchorMin = tick.anchorMax = new Vector2(0f, 1f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.anchoredPosition = new Vector2(PositionX(i), -36f);
                tick.sizeDelta = new Vector2(1f, 8f);
                tick.gameObject.AddComponent<Image>().color = White;
                generatedPositionObjects.Add(tick.gameObject);
            }
            markerX = PositionXForAngle(flaps.ActualAngle);
            marker.SetAsLastSibling();
        }

        private float PositionXForAngle(float angle)
        {
            if (flaps == null || configuredPositionCount <= 1) return 24f;
            for (int i = 0; i < configuredPositionCount - 1; i++)
            {
                float from = flaps.GetPositionAngle(i);
                float to = flaps.GetPositionAngle(i + 1);
                if (angle > to && i < configuredPositionCount - 2) continue;
                float t = Mathf.InverseLerp(from, to, angle);
                return Mathf.Lerp(PositionX(i), PositionX(i + 1), t);
            }
            return PositionX(configuredPositionCount - 1);
        }

        private float PositionX(int index)
        {
            float t = configuredPositionCount <= 1
                ? 0f
                : index / (float)(configuredPositionCount - 1);
            return Mathf.Lerp(24f, 166f, t);
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
