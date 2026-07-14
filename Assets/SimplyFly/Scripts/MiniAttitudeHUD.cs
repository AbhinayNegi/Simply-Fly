using System.Collections.Generic;
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
        private readonly List<BankMark> bankMarks = new List<BankMark>();
        private TMP_Text pitchLabel;
        private TMP_Text bankLabel;
        private float targetPitch;
        private float targetBank;
        private float displayedPitch;
        private float displayedBank;
        private float pitchVelocity;
        private float bankVelocity;
        private bool built;

        private sealed class BankMark
        {
            public float Angle;
            public RectTransform Rect;
            public Image Image;
            public Vector2 NormalSize;
        }

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
            RefreshBankScale();
            pitchLabel.text = $"PITCH\n{displayedPitch:+0.0;-0.0;0.0}°";
            bankLabel.text = $"BANK\n{displayedBank:+0.0;-0.0;0.0}°";
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
            host.sizeDelta = new Vector2(272f, 268f);

            RectTransform panel = Rect("Mini Attitude Panel", host);
            Stretch(panel);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = FlightHUDStyle.PrimaryPanel;
            FlightHUDStyle.AddOutline(panel.gameObject);

            TMP_Text title = Text("Title", panel, 16f, FontStyles.Bold);
            title.text = "ATTITUDE"; title.color = Cyan;
            PlaceCentered(title.rectTransform, new Vector2(0f, 118f),
                new Vector2(220f, 22f));

            RectTransform viewport = Rect("Horizon Viewport", panel);
            viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = new Vector2(0f, 25f);
            viewport.sizeDelta = new Vector2(232f, 166f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportBorder = viewport.gameObject.AddComponent<Image>();
            viewportBorder.color = new Color(0.03f, 0.07f, 0.09f, 1f);

            horizon = Rect("Moving Horizon", viewport);
            horizon.anchorMin = horizon.anchorMax = new Vector2(0.5f, 0.5f);
            horizon.sizeDelta = new Vector2(380f, 380f);

            RectTransform sky = Rect("Sky", horizon);
            sky.anchorMin = new Vector2(0f, 0.5f);
            sky.anchorMax = Vector2.one;
            sky.offsetMin = sky.offsetMax = Vector2.zero;
            sky.gameObject.AddComponent<Image>().color = Sky;
            RectTransform ground = Rect("Ground", horizon);
            ground.anchorMin = Vector2.zero;
            ground.anchorMax = new Vector2(1f, 0.5f);
            ground.offsetMin = ground.offsetMax = Vector2.zero;
            ground.gameObject.AddComponent<Image>().color = Ground;
            AddLine("Horizon Line", horizon, Vector2.zero,
                new Vector2(350f, 2.5f), White);
            AddPitchMarks(horizon);

            // Bank scale is parented to the fixed viewport, not the moving
            // horizon, so all reference angles remain stationary.
            AddBankReferenceScale(viewport);

            // Fixed aircraft reference: wings, center dot and short nose bars.
            AddLine("Aircraft Left Wing", viewport, new Vector2(-49f, 0f),
                new Vector2(68f, 3f), Cyan);
            AddLine("Aircraft Right Wing", viewport, new Vector2(49f, 0f),
                new Vector2(68f, 3f), Cyan);
            AddLine("Aircraft Left Tip", viewport, new Vector2(-82f, -5f),
                new Vector2(3f, 13f), Cyan);
            AddLine("Aircraft Right Tip", viewport, new Vector2(82f, -5f),
                new Vector2(3f, 13f), Cyan);
            RectTransform center = Rect("Aircraft Center", viewport);
            center.anchorMin = center.anchorMax = new Vector2(0.5f, 0.5f);
            center.sizeDelta = new Vector2(11f, 11f);
            center.gameObject.AddComponent<Image>().color = Cyan;

            AddLine("Readout Divider", panel, new Vector2(0f, -68f),
                new Vector2(232f, 1f), new Color(White.r, White.g, White.b, 0.42f));
            pitchLabel = Text("Pitch Value", panel, 14f, FontStyles.Normal);
            pitchLabel.color = Cyan;
            PlaceCentered(pitchLabel.rectTransform, new Vector2(-62f, -101f),
                new Vector2(108f, 45f));
            bankLabel = Text("Bank Value", panel, 14f, FontStyles.Normal);
            bankLabel.color = Cyan;
            PlaceCentered(bankLabel.rectTransform, new Vector2(62f, -101f),
                new Vector2(108f, 45f));
            AddLine("Digital Divider", panel, new Vector2(0f, -101f),
                new Vector2(1f, 40f), new Color(White.r, White.g, White.b, 0.3f));
        }

        private void AddPitchMarks(Transform parent)
        {
            for (int angle = -20; angle <= 20; angle += 10)
            {
                if (angle == 0) continue;
                float y = angle * pixelsPerPitchDegree;
                AddLine($"Pitch Mark {angle}", parent, new Vector2(0f, y),
                    new Vector2(48f, 1.5f), White);
                TMP_Text label = Text($"Pitch Label {angle}", parent,
                    11f, FontStyles.Normal);
                label.text = Mathf.Abs(angle).ToString();
                label.color = White;
                PlaceCentered(label.rectTransform, new Vector2(37f, y),
                    new Vector2(30f, 18f));
            }
        }

        private void AddBankReferenceScale(Transform parent)
        {
            const float radius = 72f;
            for (int angle = -60; angle <= 60; angle += 5)
            {
                if (angle % 10 == 0) continue;
                float radians = angle * Mathf.Deg2Rad;
                RectTransform dot = Rect($"Bank Arc {angle}", parent);
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.anchoredPosition = new Vector2(
                    Mathf.Sin(radians) * radius,
                    Mathf.Cos(radians) * radius);
                dot.sizeDelta = new Vector2(1.5f, 1.5f);
                dot.gameObject.AddComponent<Image>().color =
                    new Color(White.r, White.g, White.b, 0.45f);
            }

            int[] referenceAngles = { -60, -45, -30, -20, -10,
                                       10, 20, 30, 45, 60 };
            for (int i = 0; i < referenceAngles.Length; i++)
            {
                int angle = referenceAngles[i];
                float radians = angle * Mathf.Deg2Rad;
                bool trainingMark = Mathf.Abs(angle) == 30;
                Vector2 size = new Vector2(
                    trainingMark ? 2.5f : 1.7f,
                    trainingMark ? 14f : 9f);
                RectTransform mark = Rect($"Bank Reference {angle}", parent);
                mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 0.5f);
                mark.anchoredPosition = new Vector2(
                    Mathf.Sin(radians) * radius,
                    Mathf.Cos(radians) * radius);
                mark.sizeDelta = size;
                mark.localRotation = Quaternion.Euler(0f, 0f, -angle);
                Image image = mark.gameObject.AddComponent<Image>();
                image.color = White;
                bankMarks.Add(new BankMark
                {
                    Angle = angle,
                    Rect = mark,
                    Image = image,
                    NormalSize = size
                });
            }

            bankPointer = Rect("Current Bank Pointer", parent);
            bankPointer.anchorMin = bankPointer.anchorMax =
                new Vector2(0.5f, 0.5f);
            bankPointer.anchoredPosition = new Vector2(0f, 72f);
            bankPointer.sizeDelta = new Vector2(13f, 9f);
            if (bankPointer.GetComponent<CanvasRenderer>() == null)
                bankPointer.gameObject.AddComponent<CanvasRenderer>();
            AttitudeTriangleGraphic triangle =
                bankPointer.gameObject.AddComponent<AttitudeTriangleGraphic>();
            triangle.color = Cyan;
            triangle.raycastTarget = false;
        }

        private void RefreshBankScale()
        {
            float bank = Mathf.DeltaAngle(0f, displayedBank);
            float pointerBank = Mathf.Clamp(bank, -60f, 60f);
            float radians = pointerBank * Mathf.Deg2Rad;
            if (bankPointer != null)
            {
                bankPointer.anchoredPosition = new Vector2(
                    Mathf.Sin(radians) * 72f,
                    Mathf.Cos(radians) * 72f);
                bankPointer.localRotation =
                    Quaternion.Euler(0f, 0f, -pointerBank);
            }
            for (int i = 0; i < bankMarks.Count; i++)
            {
                BankMark mark = bankMarks[i];
                bool aligned = Mathf.Abs(bank - mark.Angle) <= 2f;
                mark.Image.color = aligned ? Cyan : White;
                mark.Rect.sizeDelta = aligned
                    ? new Vector2(
                        mark.NormalSize.x + 1.5f,
                        mark.NormalSize.y + 3f)
                    : mark.NormalSize;
            }
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
