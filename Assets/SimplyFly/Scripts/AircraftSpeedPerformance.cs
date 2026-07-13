using UnityEngine;

namespace SimplyFly.GroundOps
{
    /// <summary>Aircraft-owned indicated-airspeed limitations used by HUD instruments.</summary>
    [DisallowMultipleComponent]
    public sealed class AircraftSpeedPerformance : MonoBehaviour
    {
        public const float DefaultStallWarningSpeed = 42f;
        public const float DefaultGreenRangeStart = 48f;
        public const float DefaultGreenRangeEnd = 129f;
        public const float DefaultAmberRangeStart = 129f;
        public const float DefaultAmberRangeEnd = 163f;
        public const float DefaultRedLineSpeed = 163f;

        [Min(0f)] [SerializeField] private float stallWarningSpeed = DefaultStallWarningSpeed;
        [Min(0f)] [SerializeField] private float greenRangeStart = DefaultGreenRangeStart;
        [Min(0f)] [SerializeField] private float greenRangeEnd = DefaultGreenRangeEnd;
        [Min(0f)] [SerializeField] private float amberRangeStart = DefaultAmberRangeStart;
        [Min(0f)] [SerializeField] private float amberRangeEnd = DefaultAmberRangeEnd;
        [Min(0f)] [SerializeField] private float redLineSpeed = DefaultRedLineSpeed;

        public float StallWarningSpeed => stallWarningSpeed;
        public float GreenRangeStart => greenRangeStart;
        public float GreenRangeEnd => greenRangeEnd;
        public float AmberRangeStart => amberRangeStart;
        public float AmberRangeEnd => amberRangeEnd;
        public float RedLineSpeed => redLineSpeed;

        private void OnValidate()
        {
            stallWarningSpeed = Mathf.Max(0f, stallWarningSpeed);
            greenRangeStart = Mathf.Max(stallWarningSpeed, greenRangeStart);
            greenRangeEnd = Mathf.Max(greenRangeStart, greenRangeEnd);
            amberRangeStart = Mathf.Max(greenRangeEnd, amberRangeStart);
            amberRangeEnd = Mathf.Max(amberRangeStart, amberRangeEnd);
            redLineSpeed = Mathf.Max(amberRangeEnd, redLineSpeed);
        }
    }
}
