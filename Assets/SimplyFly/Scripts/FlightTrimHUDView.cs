using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    public sealed class FlightTrimHUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text recommendationLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private RectTransform trimMarker;
        [SerializeField] private Image trimMarkerImage;
        [Min(10f)] [SerializeField] private float trimBarWidth = 260f;

        private readonly Color normalColor = new Color(0.25f, 0.95f, 1f);
        private readonly Color trimmedColor = new Color(0.25f, 1f, 0.48f);
        private readonly Color cautionColor = new Color(1f, 0.75f, 0.18f);

        public void UpdateView(AssistedTrimController trim)
        {
            if (trim == null) return;
            gameObject.SetActive(trim.TrimEnabled);
            if (!trim.TrimEnabled) return;

            if (statusLabel != null)
            {
                statusLabel.text =
                    $"TRIM {trim.Status.ToString().ToUpperInvariant()}  " +
                    $"{trim.CurrentTrimOffset:+0.00;-0.00;0.00}";
                statusLabel.color = trim.Status == AssistedTrimController.TrimStatus.Trimmed
                    ? trimmedColor
                    : normalColor;
            }
            if (recommendationLabel != null)
            {
                recommendationLabel.text = trim.Recommendation;
                recommendationLabel.color = trim.Recommendation == "TRIMMED"
                    ? trimmedColor
                    : cautionColor;
            }
            if (messageLabel != null)
            {
                messageLabel.text = trim.TransientMessage;
                messageLabel.gameObject.SetActive(
                    !string.IsNullOrEmpty(trim.TransientMessage));
                messageLabel.color = trim.TransientMessage == "CANNOT TRIM NOW"
                    ? cautionColor
                    : trimmedColor;
            }
            if (trimMarker != null)
            {
                float normalized = trim.CurrentTrimOffset >= 0f
                    ? trim.CurrentTrimOffset /
                      Mathf.Max(0.01f, trim.MaximumNoseUpTrim)
                    : trim.CurrentTrimOffset /
                      Mathf.Max(0.01f, trim.MaximumNoseDownTrim);
                Vector2 position = trimMarker.anchoredPosition;
                position.x = Mathf.Clamp(normalized, -1f, 1f) * trimBarWidth * 0.5f;
                trimMarker.anchoredPosition = position;
            }
            if (trimMarkerImage != null)
                trimMarkerImage.color = trim.Status == AssistedTrimController.TrimStatus.Active
                    ? cautionColor
                    : normalColor;
        }
    }
}
