using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    [DisallowMultipleComponent]
    public sealed class BrakeStatusHUD : MonoBehaviour
    {
        [SerializeField] private BrakeController brakes;
        [Min(0f)] [SerializeField] private float releaseHoldSeconds = 0.35f;
        [Min(0.1f)] [SerializeField] private float fadeSpeed = 4f;

        private CanvasGroup group;
        private TMP_Text label;
        private float releaseTimer;

        private static readonly Color Cyan = new(0.15f, 0.95f, 1f, 0.98f);
        private static readonly Color Amber = new(1f, 0.64f, 0.05f, 0.98f);

        public void Bind(BrakeController source) => brakes = source;

        private void Awake()
        {
            if (brakes == null) brakes = FindAnyObjectByType<BrakeController>();
            Build();
        }

        private void LateUpdate()
        {
            bool parking = brakes != null && brakes.ParkingBrakeEnabled;
            bool braking = brakes != null &&
                (brakes.BrakeInputActive || brakes.IsBraking) && !parking;
            bool active = parking || braking;

            if (active)
            {
                releaseTimer = releaseHoldSeconds;
                group.alpha = 1f;
                label.text = parking ? "PARK BRAKE" : "BRAKING";
                label.color = parking ? Cyan : Amber;
                return;
            }

            releaseTimer = Mathf.Max(0f,
                releaseTimer - Time.unscaledDeltaTime);
            if (releaseTimer <= 0f)
                group.alpha = Mathf.MoveTowards(
                    group.alpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
        }

        private void Build()
        {
            RectTransform host = (RectTransform)transform;
            host.anchorMin = host.anchorMax = new Vector2(1f, 0f);
            host.pivot = new Vector2(1f, 0f);
            // The VSI host is 230 px wide; sit fully to its left with a gap.
            host.anchoredPosition = new Vector2(-270f, 24f);
            host.sizeDelta = new Vector2(138f, 34f);

            Image background = gameObject.AddComponent<Image>();
            background.color = new Color(0.01f, 0.035f, 0.05f, 0.72f);
            Outline outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.68f, 0.72f, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            GameObject textObject = new("Brake Status Label", typeof(RectTransform));
            textObject.transform.SetParent(transform, false);
            label = textObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 14f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);
        }
    }
}
