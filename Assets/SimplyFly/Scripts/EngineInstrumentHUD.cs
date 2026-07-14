using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    public enum EngineInstrumentState { Off, Starting, Running, Warning }

    /// <summary>Engine-agnostic presentation accepting normalized output, RPM and state.</summary>
    [DisallowMultipleComponent]
    public sealed class EngineInstrumentHUD : MonoBehaviour
    {
        [Min(0.01f)] [SerializeField] private float valueSmoothing = 0.16f;
        [Min(0.01f)] [SerializeField] private float throttleSmoothing = 0.06f;
        [Min(0.1f)] [SerializeField] private float statusFadeSpeed = 4f;

        private RectTransform barFill;
        private TMP_Text outputLabel;
        private TMP_Text throttleLabel;
        private TMP_Text rpmLabel;
        private TMP_Text statusLabel;
        private CanvasGroup statusGroup;
        private float targetOutput;
        private float displayedOutput;
        private float outputVelocity;
        private float targetThrottle;
        private float displayedThrottle;
        private float throttleVelocity;
        private float targetRpm;
        private float displayedRpm;
        private float rpmVelocity;
        private EngineInstrumentState displayedState;
        private EngineInstrumentState pendingState;
        private bool stateChanging;
        private bool built;

        private const float BarWidth = 174f;
        private static readonly Color Cyan = new Color(0.15f, 0.95f, 1f, 0.96f);
        private static readonly Color White = new Color(0.88f, 0.95f, 1f, 0.9f);
        private static readonly Color Green = new Color(0.28f, 0.92f, 0.23f, 0.96f);
        private static readonly Color Amber = new Color(1f, 0.68f, 0.08f, 0.98f);
        private static readonly Color Red = new Color(1f, 0.16f, 0.12f, 0.98f);

        public void SetData(float normalizedOutput, float throttlePosition, float rpm,
            EngineInstrumentState state)
        {
            if (!built) Build();
            targetOutput = Mathf.Clamp01(normalizedOutput);
            targetThrottle = Mathf.Clamp01(throttlePosition);
            targetRpm = Mathf.Max(0f, rpm);
            if (state != displayedState &&
                (!stateChanging || state != pendingState))
            {
                pendingState = state;
                stateChanging = true;
            }
        }

        private void LateUpdate()
        {
            if (!built) return;
            displayedOutput = Mathf.SmoothDamp(
                displayedOutput, targetOutput, ref outputVelocity,
                valueSmoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            displayedRpm = Mathf.SmoothDamp(
                displayedRpm, targetRpm, ref rpmVelocity,
                valueSmoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            displayedThrottle = Mathf.SmoothDamp(
                displayedThrottle, targetThrottle, ref throttleVelocity,
                throttleSmoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            outputLabel.text = $"{displayedOutput * 100f:0}%";
            throttleLabel.text = $"{displayedThrottle * 100f:0}%";
            rpmLabel.text = $"{displayedRpm:0000}";
            barFill.sizeDelta = new Vector2(
                BarWidth * Mathf.Clamp01(displayedOutput), 10f);

            if (stateChanging)
            {
                statusGroup.alpha = Mathf.MoveTowards(
                    statusGroup.alpha, 0f,
                    statusFadeSpeed * Time.unscaledDeltaTime);
                if (statusGroup.alpha <= 0.01f)
                {
                    displayedState = pendingState;
                    ApplyState();
                    stateChanging = false;
                }
            }
            else
            {
                statusGroup.alpha = Mathf.MoveTowards(
                    statusGroup.alpha, 1f,
                    statusFadeSpeed * Time.unscaledDeltaTime);
            }
        }

        private void Build()
        {
            built = true;
            RectTransform host = (RectTransform)transform;
            Image legacyPanel = host.GetComponent<Image>();
            if (legacyPanel != null) legacyPanel.enabled = false;
            for (int i = 0; i < host.childCount; i++)
                host.GetChild(i).gameObject.SetActive(false);
            host.sizeDelta = new Vector2(210f, 220f);

            RectTransform panel = Rect("Engine Panel", host);
            Stretch(panel);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.01f, 0.035f, 0.05f, 0.84f);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.72f, 0.76f, 0.5f);
            outline.effectDistance = new Vector2(1f, -1f);

            TMP_Text title = Text("Title", panel, 17f, FontStyles.Bold,
                new Vector2(0f, 95f), new Vector2(190f, 24f));
            title.text = "ENGINE"; title.color = White;
            outputLabel = Text("Output", panel, 32f, FontStyles.Normal,
                new Vector2(0f, 66f), new Vector2(180f, 34f));
            outputLabel.color = Cyan;
            TMP_Text powerCaption = Text("Power Label", panel, 12f,
                FontStyles.Normal, new Vector2(0f, 44f), new Vector2(130f, 18f));
            powerCaption.text = "ENGINE OUTPUT"; powerCaption.color = White;

            RectTransform barTrack = Rect("Power Bar Track", panel);
            Place(barTrack, new Vector2(-BarWidth * 0.5f, 27f),
                new Vector2(BarWidth, 10f));
            barTrack.gameObject.AddComponent<Image>().color =
                new Color(0.2f, 0.25f, 0.27f, 0.75f);
            barFill = Rect("Power Bar Fill", barTrack);
            barFill.anchorMin = barFill.anchorMax = new Vector2(0f, 0.5f);
            barFill.pivot = new Vector2(0f, 0.5f);
            barFill.anchoredPosition = Vector2.zero;
            barFill.sizeDelta = new Vector2(0f, 10f);
            barFill.gameObject.AddComponent<Image>().color = Green;

            TMP_Text throttleCaption = Text("Throttle Label", panel, 12f,
                FontStyles.Normal, new Vector2(-38f, 7f), new Vector2(90f, 18f));
            throttleCaption.text = "THROTTLE"; throttleCaption.color = White;
            throttleLabel = Text("Throttle", panel, 20f, FontStyles.Normal,
                new Vector2(49f, 7f), new Vector2(82f, 24f));
            throttleLabel.color = Cyan;

            TMP_Text rpmCaption = Text("RPM Label", panel, 12f,
                FontStyles.Normal, new Vector2(0f, -25f), new Vector2(80f, 18f));
            rpmCaption.text = "RPM"; rpmCaption.color = White;
            rpmLabel = Text("RPM", panel, 25f, FontStyles.Normal,
                new Vector2(0f, -47f), new Vector2(130f, 31f));
            rpmLabel.color = Cyan;

            RectTransform divider = Rect("Divider", panel);
            Place(divider, new Vector2(-87f, -70f), new Vector2(174f, 1f));
            divider.gameObject.AddComponent<Image>().color =
                new Color(0.65f, 0.78f, 0.8f, 0.35f);
            statusLabel = Text("Status", panel, 14f, FontStyles.Bold,
                new Vector2(0f, -88f), new Vector2(170f, 22f));
            statusGroup = statusLabel.gameObject.AddComponent<CanvasGroup>();
            displayedState = EngineInstrumentState.Off;
            pendingState = displayedState;
            ApplyState();
        }

        private void ApplyState()
        {
            Color stateColor;
            switch (displayedState)
            {
                case EngineInstrumentState.Starting:
                    statusLabel.text = "●  STARTING"; stateColor = Amber; break;
                case EngineInstrumentState.Running:
                    statusLabel.text = "●  RUNNING"; stateColor = Green; break;
                case EngineInstrumentState.Warning:
                    statusLabel.text = "●  WARNING"; stateColor = Red; break;
                default:
                    statusLabel.text = "●  OFF"; stateColor = White; break;
            }
            statusLabel.color = stateColor;
            barFill.GetComponent<Image>().color = displayedState switch
            {
                EngineInstrumentState.Starting => Amber,
                EngineInstrumentState.Warning => Red,
                EngineInstrumentState.Running => Green,
                _ => new Color(0.35f, 0.42f, 0.45f, 0.85f)
            };
        }

        private static TMP_Text Text(string name, Transform parent, float size,
            FontStyles style, Vector2 position, Vector2 dimensions)
        {
            TMP_Text text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size; text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            Place(text.rectTransform, position - dimensions * 0.5f, dimensions);
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rect, Vector2 position,
            Vector2 dimensions)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
