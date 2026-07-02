using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    public sealed class FlightHUDInstrument : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private Image panel;

        private string lastValue;
        private string lastDetail;
        private Color lastColor;

        public void SetTitle(string title)
        {
            if (titleLabel != null && titleLabel.text != title)
                titleLabel.text = title;
        }

        public void SetDisplay(string value, string detail, Color accent)
        {
            if (valueLabel != null && lastValue != value)
            {
                valueLabel.text = value;
                lastValue = value;
            }
            if (detailLabel != null && lastDetail != detail)
            {
                detailLabel.text = detail;
                lastDetail = detail;
            }
            if (lastColor != accent)
            {
                if (valueLabel != null) valueLabel.color = accent;
                if (panel != null)
                    panel.color = new Color(accent.r * 0.08f, accent.g * 0.08f,
                        accent.b * 0.08f, 0.78f);
                lastColor = accent;
            }
        }
    }
}
