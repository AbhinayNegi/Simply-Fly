using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class FlightSchoolUI : MonoBehaviour
    {
        [SerializeField] private FlightLessonSystem lessonSystem;
        [SerializeField] private SideViewTrainingAid pitchCoach;
        [SerializeField] private NavigationUI navigationUI;
        [SerializeField] private Key toggleKey = Key.Y;
        [SerializeField] private bool visibleByDefault;

        private VisualElement panel;
        private Label titleLabel;
        private Label descriptionLabel;
        private Label progressLabel;
        private Label objectiveLabel;
        private Label hintLabel;
        private Label statusLabel;
        private Label warningLabel;
        private VisualElement progressFill;
        private bool visible;
        private int lastLesson = -1;
        private int lastObjective = -1;

        private void OnEnable()
        {
            if (lessonSystem == null)
                lessonSystem = FindAnyObjectByType<FlightLessonSystem>();
            if (navigationUI == null)
                navigationUI = FindAnyObjectByType<NavigationUI>();
            if (pitchCoach == null)
                pitchCoach = FindAnyObjectByType<SideViewTrainingAid>();
            visible = visibleByDefault;
            if (panel != null) ApplyVisibility();
        }

        private void Start()
        {
            // Build after every scene object's Awake has run so the lesson
            // system has populated its default serializable lesson catalog.
            BuildInterface();
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
            Refresh();
            ApplyVisibility();
        }

        private void BuildInterface()
        {
            UIDocument document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;

            panel = new VisualElement { name = "flight-school-panel" };
            panel.pickingMode = PickingMode.Position;
            panel.style.position = Position.Absolute;
            panel.style.left = 20f;
            panel.style.top = 18f;
            panel.style.width = 420f;
            panel.style.minHeight = 390f;
            panel.style.paddingLeft = 18f;
            panel.style.paddingRight = 18f;
            panel.style.paddingTop = 14f;
            panel.style.paddingBottom = 14f;
            panel.style.backgroundColor =
                new Color(0.012f, 0.04f, 0.055f, 0.94f);
            panel.style.borderTopLeftRadius = 8f;
            panel.style.borderTopRightRadius = 8f;
            panel.style.borderBottomLeftRadius = 8f;
            panel.style.borderBottomRightRadius = 8f;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderTopWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftColor = new Color(0.18f, 0.55f, 0.62f);
            panel.style.borderRightColor = new Color(0.18f, 0.55f, 0.62f);
            panel.style.borderTopColor = new Color(0.18f, 0.55f, 0.62f);
            panel.style.borderBottomColor = new Color(0.18f, 0.55f, 0.62f);
            root.Add(panel);

            Label header = CreateLabel(
                "FLIGHT SCHOOL  [L]",
                18,
                new Color(0.28f, 0.95f, 1f),
                FontStyle.Bold);
            header.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(header);

            VisualElement lessonButtons = new VisualElement();
            lessonButtons.style.flexDirection = FlexDirection.Row;
            lessonButtons.style.justifyContent = Justify.SpaceBetween;
            lessonButtons.style.marginTop = 8f;
            lessonButtons.style.marginBottom = 9f;
            panel.Add(lessonButtons);
            int lessonCount = lessonSystem?.Lessons?.Length ?? 0;
            float lessonButtonWidth = lessonCount <= 0
                ? 44f
                : Mathf.Clamp(
                    (panel.resolvedStyle.width > 0f
                        ? panel.resolvedStyle.width - 52f
                        : 368f) / lessonCount - 4f,
                    36f,
                    54f);
            for (int i = 0; i < lessonCount; i++)
            {
                int lessonIndex = i;
                Button button = new Button(
                    () => lessonSystem?.SelectLesson(lessonIndex))
                {
                    text = (i + 1).ToString()
                };
                button.tooltip = lessonSystem.Lessons[i].title;
                button.style.width = lessonButtonWidth;
                button.style.height = 30f;
                button.style.backgroundColor =
                    new Color(0.05f, 0.18f, 0.22f);
                button.style.color = Color.white;
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
                lessonButtons.Add(button);
            }

            titleLabel = CreateLabel(
                string.Empty, 18, Color.white, FontStyle.Bold);
            titleLabel.style.marginTop = 2f;
            panel.Add(titleLabel);
            descriptionLabel = CreateLabel(
                string.Empty, 13, new Color(0.7f, 0.83f, 0.86f));
            descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            descriptionLabel.style.marginBottom = 8f;
            panel.Add(descriptionLabel);

            progressLabel = CreateLabel(
                string.Empty, 13, new Color(0.35f, 1f, 0.55f),
                FontStyle.Bold);
            panel.Add(progressLabel);

            VisualElement progressTrack = new VisualElement();
            progressTrack.style.height = 5f;
            progressTrack.style.marginTop = 4f;
            progressTrack.style.marginBottom = 10f;
            progressTrack.style.backgroundColor =
                new Color(0.12f, 0.25f, 0.28f);
            panel.Add(progressTrack);
            progressFill = new VisualElement();
            progressFill.style.height = Length.Percent(100f);
            progressFill.style.width = Length.Percent(0f);
            progressFill.style.backgroundColor =
                new Color(0.25f, 1f, 0.48f);
            progressTrack.Add(progressFill);

            Label objectiveHeader = CreateLabel(
                "CURRENT OBJECTIVE", 12,
                new Color(0.35f, 0.8f, 0.85f), FontStyle.Bold);
            panel.Add(objectiveHeader);
            objectiveLabel = CreateLabel(
                string.Empty, 17, Color.white, FontStyle.Bold);
            objectiveLabel.style.whiteSpace = WhiteSpace.Normal;
            objectiveLabel.style.marginBottom = 8f;
            panel.Add(objectiveLabel);

            hintLabel = CreateLabel(
                string.Empty, 14, new Color(1f, 0.79f, 0.3f));
            hintLabel.style.whiteSpace = WhiteSpace.Normal;
            hintLabel.style.paddingLeft = 10f;
            hintLabel.style.paddingRight = 10f;
            hintLabel.style.paddingTop = 7f;
            hintLabel.style.paddingBottom = 7f;
            hintLabel.style.backgroundColor =
                new Color(0.13f, 0.11f, 0.035f, 0.72f);
            panel.Add(hintLabel);

            statusLabel = CreateLabel(
                string.Empty, 14, new Color(0.3f, 1f, 0.52f),
                FontStyle.Bold);
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.style.marginTop = 8f;
            panel.Add(statusLabel);
            warningLabel = CreateLabel(
                string.Empty, 14, new Color(1f, 0.3f, 0.15f),
                FontStyle.Bold);
            warningLabel.style.whiteSpace = WhiteSpace.Normal;
            warningLabel.style.marginTop = 5f;
            panel.Add(warningLabel);

            Label controls = CreateLabel(
                "N SKIP OBJECTIVE   •   R RESTART LESSON",
                11,
                new Color(0.55f, 0.72f, 0.75f));
            controls.style.unityTextAlign = TextAnchor.MiddleCenter;
            controls.style.marginTop = 12f;
            panel.Add(controls);
            Refresh(true);
        }

        private void Refresh(bool force = false)
        {
            if (lessonSystem == null || panel == null) return;
            FlightLessonDefinition lesson = lessonSystem.CurrentLesson;
            LessonObjectiveDefinition objective =
                lessonSystem.CurrentObjective;
            if (lesson == null || objective == null) return;

            if (force ||
                lastLesson != lessonSystem.CurrentLessonIndex ||
                lastObjective != lessonSystem.CurrentObjectiveIndex)
            {
                titleLabel.text = lesson.title;
                descriptionLabel.text = lesson.description;
                objectiveLabel.text = objective.instruction;
                hintLabel.text = "HINT  " + objective.hint;
                lastLesson = lessonSystem.CurrentLessonIndex;
                lastObjective = lessonSystem.CurrentObjectiveIndex;
                if (lessonSystem.PitchCoachRequested)
                    pitchCoach?.ShowForLesson();
                if (lessonSystem.ApproachAidLessonRequested)
                    navigationUI?.ShowApproachHelpersForLesson();
            }

            int objectiveCount = lesson.objectives?.Length ?? 0;
            progressLabel.text = lessonSystem.LessonComplete
                ? $"COMPLETE  •  {objectiveCount}/{objectiveCount}"
                : $"OBJECTIVE {lessonSystem.CurrentObjectiveIndex + 1}/{objectiveCount}";
            float overallProgress = objectiveCount <= 0
                ? 0f
                : (lessonSystem.CurrentObjectiveIndex +
                   lessonSystem.ObjectiveProgress) / objectiveCount;
            if (lessonSystem.LessonComplete) overallProgress = 1f;
            progressFill.style.width =
                Length.Percent(Mathf.Clamp01(overallProgress) * 100f);

            statusLabel.text = lessonSystem.StatusMessage;
            statusLabel.style.display =
                string.IsNullOrWhiteSpace(lessonSystem.StatusMessage)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            warningLabel.text = lessonSystem.WarningMessage;
            warningLabel.style.display =
                string.IsNullOrWhiteSpace(lessonSystem.WarningMessage)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
        }

        private void ApplyVisibility()
        {
            if (panel != null)
                panel.style.display =
                    visible &&
                    (navigationUI == null || !navigationUI.MapVisible)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            ApplyVisibility();
        }

        private static Label CreateLabel(
            string text,
            int size,
            Color color,
            FontStyle fontStyle = FontStyle.Normal)
        {
            Label label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = fontStyle;
            return label;
        }
    }
}
