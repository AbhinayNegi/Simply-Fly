using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AttitudeTriangleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect area = rectTransform.rect;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector3(area.xMin, area.yMax);
            helper.AddVert(vertex);
            vertex.position = new Vector3(area.xMax, area.yMax);
            helper.AddVert(vertex);
            vertex.position = new Vector3(0f, area.yMin);
            helper.AddVert(vertex);
            helper.AddTriangle(0, 1, 2);
        }
    }

    /// <summary>Physics-agnostic miniature artificial-horizon presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class MiniAttitudeHUD : MonoBehaviour
    {
        [Min(0.01f)] [SerializeField] private float smoothingTime = 0.1f;
        [Range(15f, 60f)] [SerializeField] private float displayedPitchLimit = 35f;
        [Min(0.5f)] [SerializeField] private float pixelsPerPitchDegree = 2.35f;

        private RectTransform horizon;
        private RectTransform bankPointer;
        private TMP_Text pitchLabel;
        private TMP_Text bankLabel;
        private float targetPitch;
        private float targetBank;
        private float displayedPitch;
        private float displayedBank;
        private float pitchVelocity;
        private float bankVelocity;
        private bool built;

        private static readonly Color Cyan = FlightHUDStyle.Cyan;
        private static readonly Color White = FlightHUDStyle.White;
        private static readonly Color Sky = new Color(0.12f, 0.38f, 0.68f, 0.96f);
        private static readonly Color Ground = new Color(0.29f, 0.19f, 0.11f, 0.98f);

        public void SetAttitude(float pitchDegrees, float bankDegrees)
        {
            if (!built) Build();
            targetPitch = pitchDegrees;
            targetBank = bankDegrees;
        }

        private void LateUpdate()
        {
            if (!built) return;
            displayedPitch = Mathf.SmoothDampAngle(
                displayedPitch, targetPitch, ref pitchVelocity,
                smoothingTime, Mathf.Infinity, Time.unscaledDeltaTime);
            displayedBank = Mathf.SmoothDampAngle(
                displayedBank, targetBank, ref bankVelocity,
                smoothingTime, Mathf.Infinity, Time.unscaledDeltaTime);

            float clampedPitch = Mathf.Clamp(
                displayedPitch, -displayedPitchLimit, displayedPitchLimit);
            horizon.anchoredPosition = new Vector2(
                0f, -clampedPitch * pixelsPerPitchDegree);
            // The horizon moves opposite aircraft roll while the cyan aircraft
            // reference remains fixed in screen space.
            horizon.localRotation = Quaternion.Euler(0f, 0f, displayedBank);
            RefreshBankPointer();
            pitchLabel.text = $"{displayedPitch:+0.0;-0.0;0.0}°";
            bankLabel.text = $"{displayedBank:+0.0;-0.0;0.0}°";
        }

        private void Build()
        {
            built = true;
            RectTransform host = (RectTransform)transform;
            Image legacyPanel = host.GetComponent<Image>();
            if (legacyPanel != null) legacyPanel.enabled = false;
            for (int i = 0; i < host.childCount; i++)
                host.GetChild(i).gameObject.SetActive(false);
            if (host.anchorMin.y <= 0.01f && host.anchorMax.y <= 0.01f)
                host.anchoredPosition = new Vector2(
                    host.anchoredPosition.x,
                    Mathf.Max(host.anchoredPosition.y, FlightHUDStyle.BottomSafeMargin));
            host.sizeDelta = new Vector2(312f, 308f);

            RectTransform panel = Rect("Mini Attitude Panel", host);
            Stretch(panel);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = FlightHUDStyle.PrimaryPanel;
            FlightHUDStyle.AddOutline(panel.gameObject);
            AddLine("Panel Top Highlight", panel, new Vector2(0f, 152f),
                new Vector2(274f, 1f), new Color(0.7f, 0.9f, 1f, 0.18f));
            AddLine("Panel Lower Edge", panel, new Vector2(0f, -152f),
                new Vector2(274f, 2f), new Color(0f, 0.01f, 0.015f, 0.5f));

            TMP_Text title = Text("Title", panel, 16f, FontStyles.Bold);
            title.text = "ATTITUDE"; title.color = Cyan;
            PlaceCentered(title.rectTransform, new Vector2(0f, 138f),
                new Vector2(270f, 24f));
            AddLine("Header Divider", panel, new Vector2(0f, 120f),
                new Vector2(274f, 1f), FlightHUDStyle.Divider);

            RectTransform viewport = Rect("Horizon Viewport", panel);
            viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = new Vector2(0f, 28f);
            viewport.sizeDelta = new Vector2(276f, 204f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportBorder = viewport.gameObject.AddComponent<Image>();
            viewportBorder.color = new Color(0.03f, 0.07f, 0.09f, 1f);
            Outline horizonOutline = viewport.gameObject.AddComponent<Outline>();
            horizonOutline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f);
            horizonOutline.effectDistance = new Vector2(1f, -1f);

            horizon = Rect("Moving Horizon", viewport);
            horizon.anchorMin = horizon.anchorMax = new Vector2(0.5f, 0.5f);
            horizon.sizeDelta = new Vector2(540f, 540f);

            RectTransform sky = Rect("Sky", horizon);
            sky.anchorMin = new Vector2(0f, 0.5f);
            sky.anchorMax = Vector2.one;
            sky.offsetMin = sky.offsetMax = Vector2.zero;
            sky.gameObject.AddComponent<Image>().color = Sky;
            RectTransform skyTop = Rect("Sky Upper Shade", sky);
            skyTop.anchorMin = new Vector2(0f, 0.5f);
            skyTop.anchorMax = Vector2.one;
            skyTop.offsetMin = skyTop.offsetMax = Vector2.zero;
            skyTop.gameObject.AddComponent<Image>().color =
                new Color(0.04f, 0.14f, 0.34f, 0.34f);
            RectTransform ground = Rect("Ground", horizon);
            ground.anchorMin = Vector2.zero;
            ground.anchorMax = new Vector2(1f, 0.5f);
            ground.offsetMin = ground.offsetMax = Vector2.zero;
            ground.gameObject.AddComponent<Image>().color = Ground;
            RectTransform groundBottom = Rect("Ground Lower Shade", ground);
            groundBottom.anchorMin = Vector2.zero;
            groundBottom.anchorMax = new Vector2(1f, 0.5f);
            groundBottom.offsetMin = groundBottom.offsetMax = Vector2.zero;
            groundBottom.gameObject.AddComponent<Image>().color =
                new Color(0.09f, 0.04f, 0.015f, 0.28f);
            AddLine("Horizon Line", horizon, Vector2.zero,
                new Vector2(500f, 3f), new Color(0.82f, 1f, 1f, 1f));
            AddPitchMarks(horizon);

            // Bank scale is parented to the fixed viewport, not the moving
            // horizon, so all reference angles remain stationary.
            AddBankReferenceScale(viewport);

            // Fixed aircraft reference: wings, center dot and short nose bars.
            AddLine("Aircraft Left Wing", viewport, new Vector2(-58f, 0f),
                new Vector2(82f, 4f), Cyan);
            AddLine("Aircraft Right Wing", viewport, new Vector2(58f, 0f),
                new Vector2(82f, 4f), Cyan);
            AddLine("Aircraft Left Tip", viewport, new Vector2(-98f, -6f),
                new Vector2(4f, 16f), Cyan);
            AddLine("Aircraft Right Tip", viewport, new Vector2(98f, -6f),
                new Vector2(4f, 16f), Cyan);
            RectTransform center = Rect("Aircraft Center", viewport);
            center.anchorMin = center.anchorMax = new Vector2(0.5f, 0.5f);
            center.sizeDelta = new Vector2(14f, 14f);
            center.gameObject.AddComponent<Image>().color = Cyan;

            AddLine("Readout Divider", panel, new Vector2(0f, -82f),
                new Vector2(276f, 1f), new Color(White.r, White.g, White.b, 0.42f));
            TMP_Text pitchTitle = Text("Pitch Label", panel, 11f, FontStyles.Bold);
            pitchTitle.text = "PITCH"; pitchTitle.color = White;
            PlaceCentered(pitchTitle.rectTransform, new Vector2(-74f, -101f),
                new Vector2(126f, 18f));
            pitchLabel = Text("Pitch Value", panel, 19f, FontStyles.Normal);
            pitchLabel.color = Cyan;
            PlaceCentered(pitchLabel.rectTransform, new Vector2(-74f, -126f),
                new Vector2(126f, 28f));
            TMP_Text bankTitle = Text("Bank Label", panel, 11f, FontStyles.Bold);
            bankTitle.text = "BANK"; bankTitle.color = White;
            PlaceCentered(bankTitle.rectTransform, new Vector2(74f, -101f),
                new Vector2(126f, 18f));
            bankLabel = Text("Bank Value", panel, 19f, FontStyles.Normal);
            bankLabel.color = Cyan;
            PlaceCentered(bankLabel.rectTransform, new Vector2(74f, -126f),
                new Vector2(126f, 28f));
            AddLine("Digital Divider", panel, new Vector2(0f, -119f),
                new Vector2(1f, 62f), new Color(White.r, White.g, White.b, 0.3f));
        }

        private void AddPitchMarks(Transform parent)
        {
            for (int angle = -40; angle <= 40; angle += 5)
            {
                if (angle == 0) continue;
                bool major = angle % 10 == 0;
                float y = angle * pixelsPerPitchDegree;
                AddLine($"Pitch Mark {angle}", parent, new Vector2(0f, y),
                    new Vector2(major ? 70f : 40f, major ? 2f : 1.25f),
                    new Color(White.r, White.g, White.b, major ? 0.98f : 0.72f));
                if (!major) continue;
                TMP_Text label = Text($"Pitch Label {angle}", parent,
                    12f, FontStyles.Normal);
                label.text = Mathf.Abs(angle).ToString();
                label.color = White;
                PlaceCentered(label.rectTransform, new Vector2(51f, y),
                    new Vector2(30f, 18f));
                TMP_Text opposite = Text($"Pitch Opposite Label {angle}", parent,
                    12f, FontStyles.Normal);
                opposite.text = Mathf.Abs(angle).ToString();
                opposite.color = White;
                PlaceCentered(opposite.rectTransform, new Vector2(-51f, y),
                    new Vector2(30f, 18f));
            }
        }

        private void AddBankReferenceScale(Transform parent)
        {
            const float radius = 91f;
            for (int angle = -30; angle <= 30; angle += 5)
            {
                float radians = angle * Mathf.Deg2Rad;
                bool major = angle % 10 == 0;
                Vector2 size = new Vector2(
                    major ? 2.4f : 1.5f,
                    angle == 0 ? 15f : major ? 12f : 7f);
                RectTransform mark = Rect($"Bank Reference {angle}", parent);
                mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 0.5f);
                mark.anchoredPosition = new Vector2(
                    Mathf.Sin(radians) * radius,
                    Mathf.Cos(radians) * radius);
                mark.sizeDelta = size;
                mark.localRotation = Quaternion.Euler(0f, 0f, -angle);
                Image image = mark.gameObject.AddComponent<Image>();
                image.color = new Color(White.r, White.g, White.b,
                    major ? 0.98f : 0.62f);
            }

            bankPointer = Rect("Current Bank Pointer", parent);
            bankPointer.anchorMin = bankPointer.anchorMax =
                new Vector2(0.5f, 0.5f);
            bankPointer.anchoredPosition = new Vector2(0f, 78f);
            bankPointer.sizeDelta = new Vector2(17f, 12f);
            if (bankPointer.GetComponent<CanvasRenderer>() == null)
                bankPointer.gameObject.AddComponent<CanvasRenderer>();
            AttitudeTriangleGraphic triangle =
                bankPointer.gameObject.AddComponent<AttitudeTriangleGraphic>();
            triangle.color = Cyan;
            triangle.raycastTarget = false;
        }

        private void RefreshBankPointer()
        {
            if (bankPointer == null) return;
            float bank = Mathf.Clamp(
                Mathf.DeltaAngle(0f, displayedBank), -30f, 30f);
            float radians = bank * Mathf.Deg2Rad;
            bankPointer.anchoredPosition = new Vector2(
                Mathf.Sin(radians) * 78f,
                Mathf.Cos(radians) * 78f);
            bankPointer.localRotation = Quaternion.Euler(0f, 0f, -bank);
        }

        private static void AddLine(string name, Transform parent,
            Vector2 position, Vector2 size, Color color)
        {
            RectTransform line = Rect(name, parent);
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = position; line.sizeDelta = size;
            line.gameObject.AddComponent<Image>().color = color;
        }

        private static TMP_Text Text(string name, Transform parent, float size,
            FontStyles style)
        {
            TMP_Text text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size; text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void PlaceCentered(RectTransform rect, Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
