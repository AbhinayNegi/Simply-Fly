using TMPro;
using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class SlipSkidHUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text indicatorLabel;
        [SerializeField] private TMP_Text detailLabel;
        [Range(3, 12)] [SerializeField] private int halfTrackCharacters = 5;

        public void UpdateView(RudderController rudder)
        {
            float ball = rudder == null ? 0f : rudder.SlipBallPosition;
            int trackWidth = halfTrackCharacters * 2 + 1;
            int ballIndex = Mathf.Clamp(
                Mathf.RoundToInt(
                    Mathf.Lerp(0f, trackWidth - 1f, (ball + 1f) * 0.5f)),
                0,
                trackWidth - 1);
            if (indicatorLabel != null)
            {
                indicatorLabel.text =
                    "[" +
                    new string(' ', ballIndex) +
                    "o" +
                    new string(' ', trackWidth - ballIndex - 1) +
                    "]";
            }
            if (detailLabel != null)
            {
                detailLabel.text = rudder == null
                    ? "COORDINATED"
                    : $"{rudder.SlipDirection.ToUpperInvariant()}  " +
                      $"{rudder.SlipAmount:+0.00;-0.00;0.00}";
            }
        }
    }
}
