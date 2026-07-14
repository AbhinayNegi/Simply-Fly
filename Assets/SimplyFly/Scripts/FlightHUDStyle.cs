using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    /// <summary>Shared visual tokens for the runtime-built flight HUD.</summary>
    public static class FlightHUDStyle
    {
        public static readonly Color PrimaryPanel = new(0.01f, 0.03f, 0.045f, 0.89f);
        public static readonly Color SecondaryPanel = new(0.01f, 0.03f, 0.045f, 0.80f);
        public static readonly Color ValueBox = new(0.018f, 0.09f, 0.12f, 0.96f);
        public static readonly Color Border = new(0.48f, 0.68f, 0.72f, 0.58f);
        public static readonly Color Divider = new(0.65f, 0.78f, 0.8f, 0.34f);
        public static readonly Color Cyan = new(0.15f, 0.95f, 1f, 0.98f);
        public static readonly Color White = new(0.88f, 0.95f, 1f, 0.94f);
        public static readonly Color Muted = new(0.62f, 0.7f, 0.73f, 0.84f);
        public static readonly Color Green = new(0.28f, 0.92f, 0.18f, 0.98f);
        public static readonly Color Amber = new(1f, 0.66f, 0.08f, 0.98f);
        public static readonly Color Red = new(1f, 0.16f, 0.12f, 0.98f);
        public const float BottomSafeMargin = 24f;

        public static Outline AddOutline(GameObject target)
        {
            Outline outline = target.GetComponent<Outline>() ??
                              target.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            return outline;
        }
    }
}
