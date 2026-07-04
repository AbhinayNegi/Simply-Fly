using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    public sealed class SpeedSensationHUD : MonoBehaviour
    {
        [SerializeField] private PilotAwarenessAssist awareness;
        [SerializeField] private CanvasGroup cueGroup;
        [SerializeField] private CanvasGroup streakGroup;
        [SerializeField] private RectTransform[] streaks;
        [SerializeField] private TMP_Text energyLabel;
        [SerializeField] private TMP_Text controlPressureLabel;
        [SerializeField] private TMP_Text airflowLabel;

        [Header("Display")]
        [SerializeField] private bool visibleByDefault = true;
        [SerializeField] private Key toggleKey = Key.B;
        [Range(0f, 1f)] [SerializeField] private float maximumStreakAlpha = 0.32f;
        [Min(10f)] [SerializeField] private float streakTravel = 150f;
        [Min(1f)] [SerializeField] private float streakSpeed = 65f;
        [Min(0.1f)] [SerializeField] private float displayResponse = 4f;

        private Vector2[] basePositions;
        private float[] phases;
        private bool visible;
        private float displayedIntensity;

        private void Awake()
        {
            visible = visibleByDefault;
            CacheStreaks();
            ApplyVisibility();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            {
                visible = !visible;
                ApplyVisibility();
            }

            if (!visible || awareness == null) return;
            float targetIntensity = awareness.PeripheralSpeedCuesEnabled
                ? awareness.AirflowIntensity
                : 0f;
            float blend =
                1f - Mathf.Exp(-displayResponse * Time.unscaledDeltaTime);
            displayedIntensity = Mathf.Lerp(
                displayedIntensity, targetIntensity, blend);
            UpdateStreaks();
            UpdateLabels();
        }

        private void CacheStreaks()
        {
            int count = streaks == null ? 0 : streaks.Length;
            basePositions = new Vector2[count];
            phases = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (streaks[i] != null)
                    basePositions[i] = streaks[i].anchoredPosition;
                phases[i] = count <= 1 ? 0f : i / (float)count;
            }
        }

        private void UpdateStreaks()
        {
            if (streakGroup != null)
                streakGroup.alpha =
                    displayedIntensity * maximumStreakAlpha;
            if (streaks == null || basePositions == null) return;

            float motionSpeed =
                streakSpeed * Mathf.Lerp(0.35f, 1.5f, displayedIntensity);
            for (int i = 0; i < streaks.Length; i++)
            {
                RectTransform streak = streaks[i];
                if (streak == null) continue;
                phases[i] = Mathf.Repeat(
                    phases[i] +
                    motionSpeed * Time.unscaledDeltaTime / streakTravel,
                    1f);
                float travel = phases[i] * streakTravel;
                float side = basePositions[i].x < 0f ? -1f : 1f;
                streak.anchoredPosition =
                    basePositions[i] +
                    new Vector2(side * travel * 0.28f, -travel);

                Image image = streak.GetComponent<Image>();
                if (image != null)
                {
                    float fade =
                        Mathf.Sin(phases[i] * Mathf.PI) *
                        displayedIntensity;
                    Color color = image.color;
                    color.a = Mathf.Clamp01(fade);
                    image.color = color;
                }
            }
        }

        private void UpdateLabels()
        {
            if (energyLabel != null)
            {
                energyLabel.text = awareness.EnergyCue;
                energyLabel.color =
                    awareness.EnergyCue.Contains("LOW")
                        ? new Color(1f, 0.35f, 0.16f)
                        : awareness.AirspeedTrend < -0.45f
                            ? new Color(1f, 0.75f, 0.2f)
                            : new Color(0.3f, 1f, 0.55f);
            }
            if (controlPressureLabel != null)
            {
                controlPressureLabel.text = awareness.ControlPressureCue;
                controlPressureLabel.gameObject.SetActive(
                    !string.IsNullOrWhiteSpace(
                        awareness.ControlPressureCue));
            }
            if (airflowLabel != null)
            {
                airflowLabel.text =
                    $"AIRFLOW {awareness.AirflowIntensity * 100f:0}%   [B] HIDE";
            }
        }

        private void ApplyVisibility()
        {
            if (cueGroup != null)
            {
                cueGroup.alpha = visible ? 1f : 0f;
                cueGroup.interactable = false;
                cueGroup.blocksRaycasts = false;
            }
            if (streakGroup != null && !visible)
                streakGroup.alpha = 0f;
        }
    }
}
