using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    /// <summary>Shared visual tokens for the runtime-built flight HUD.</summary>
    public static class FlightHUDStyle
    {
        private static Sprite roundedPanelSprite;
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

        public static void ApplyRoundedPanel(Image image)
        {
            if (image == null) return;
            if (roundedPanelSprite == null)
            {
                const int size = 32;
                const int radius = 8;
                Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Runtime Rounded HUD Panel",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nearestX = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float nearestY = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float dx = x + 0.5f - nearestX;
                    float dy = y + 0.5f - nearestY;
                    byte alpha = dx * dx + dy * dy <= radius * radius
                        ? (byte)255 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                roundedPanelSprite = Sprite.Create(
                    texture, new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect,
                    new Vector4(radius, radius, radius, radius));
                roundedPanelSprite.name = "Runtime Rounded HUD Panel";
                roundedPanelSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            image.sprite = roundedPanelSprite;
            image.type = Image.Type.Sliced;
        }
    }
}
