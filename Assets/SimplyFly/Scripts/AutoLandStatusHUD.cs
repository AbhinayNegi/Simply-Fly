using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    public sealed class AutoLandStatusHUD : MonoBehaviour
    {
        private AutoLandController controller;
        private TMP_Text label;
        private Image background;

        public void Bind(AutoLandController value)
        {
            controller = value;
            EnsureBuilt();
        }

        private void Awake() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (label != null) return;
            RectTransform rect = transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-18f, -272f);
            rect.sizeDelta = new Vector2(320f, 34f);
            background = gameObject.GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = new Color(0.015f, 0.08f, 0.11f, 0.88f);
            FlightHUDStyle.ApplyRoundedPanel(background);

            GameObject textObject = new("Label", typeof(RectTransform));
            textObject.transform.SetParent(transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 2f);
            textRect.offsetMax = new Vector2(-10f, -2f);
            label = textObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 18f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.25f, 0.95f, 1f);
        }

        private void Update()
        {
            EnsureBuilt();
            if (controller == null)
            {
                gameObject.SetActive(false);
                return;
            }
            string message = controller.Active ||
                controller.Phase == AutoLandController.AutoLandPhase.Complete
                ? controller.StatusMessage
                : controller.AvailabilityMessage;
            bool relevant = controller.Active ||
                controller.Phase == AutoLandController.AutoLandPhase.Complete ||
                message.StartsWith("AUTOLAND AVAILABLE");
            background.enabled = relevant;
            label.enabled = relevant;
            if (!relevant) return;
            label.text = message;
            label.color = controller.Active
                ? new Color(0.3f, 1f, 0.48f)
                : new Color(0.25f, 0.95f, 1f);
        }
    }
}
