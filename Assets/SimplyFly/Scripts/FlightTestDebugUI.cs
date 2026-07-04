using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class FlightTestDebugUI : MonoBehaviour
    {
        [SerializeField] private FlightTestRunner runner;
        [SerializeField] private FlightTelemetryRecorder recorder;
        [SerializeField] private Key toggleKey = Key.F5;
        [SerializeField] private bool visibleByDefault;

        private VisualElement panel;
        private Label scenarioLabel;
        private Label descriptionLabel;
        private Label recordingLabel;
        private Label timeLabel;
        private Label resultLabel;
        private Label pathLabel;
        private Button manualButton;
        private Button runButton;
        private bool visible;

        private void OnEnable()
        {
            if (runner == null) runner = FindAnyObjectByType<FlightTestRunner>();
            if (recorder == null)
                recorder = FindAnyObjectByType<FlightTelemetryRecorder>();
            visible = visibleByDefault;
            if (panel != null) ApplyVisibility();
        }

        private void Start()
        {
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
        }

        private void BuildInterface()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            panel = new VisualElement();
            panel.pickingMode = PickingMode.Position;
            panel.style.position = Position.Absolute;
            panel.style.right = 20f;
            panel.style.bottom = 20f;
            panel.style.width = 430f;
            panel.style.paddingLeft = 14f;
            panel.style.paddingRight = 14f;
            panel.style.paddingTop = 12f;
            panel.style.paddingBottom = 12f;
            panel.style.backgroundColor =
                new Color(0.015f, 0.035f, 0.045f, 0.95f);
            panel.style.borderTopLeftRadius = 7f;
            panel.style.borderTopRightRadius = 7f;
            panel.style.borderBottomLeftRadius = 7f;
            panel.style.borderBottomRightRadius = 7f;
            root.Add(panel);

            Label title = Label("FLIGHT TEST RECORDER  [F5]", 17,
                new Color(0.3f, 0.95f, 1f), FontStyle.Bold);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(title);
            scenarioLabel = Label(string.Empty, 16, Color.white, FontStyle.Bold);
            scenarioLabel.style.marginTop = 7f;
            panel.Add(scenarioLabel);
            descriptionLabel = Label(
                string.Empty, 12, new Color(0.68f, 0.8f, 0.83f));
            descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(descriptionLabel);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = 9f;
            panel.Add(row);
            Button previous = new Button(() => runner?.SelectPreviousScenario())
                { text = "◀ F6" };
            Button next = new Button(() => runner?.SelectNextScenario())
                { text = "F7 ▶" };
            runButton = new Button(ToggleScenario) { text = "RUN  F9" };
            StyleButton(previous, 92f);
            StyleButton(runButton, 150f);
            StyleButton(next, 92f);
            row.Add(previous);
            row.Add(runButton);
            row.Add(next);

            manualButton = new Button(ToggleManualRecording)
                { text = "START MANUAL RECORDING  F8" };
            manualButton.style.height = 32f;
            manualButton.style.marginTop = 7f;
            manualButton.style.backgroundColor =
                new Color(0.07f, 0.22f, 0.25f);
            manualButton.style.color = Color.white;
            panel.Add(manualButton);

            recordingLabel = Label(string.Empty, 14, Color.white, FontStyle.Bold);
            recordingLabel.style.marginTop = 8f;
            timeLabel = Label(string.Empty, 13, Color.white);
            resultLabel = Label(string.Empty, 16, Color.white, FontStyle.Bold);
            pathLabel = Label(
                string.Empty, 11, new Color(0.65f, 0.8f, 0.84f));
            pathLabel.style.whiteSpace = WhiteSpace.Normal;
            pathLabel.style.marginTop = 5f;
            panel.Add(recordingLabel);
            panel.Add(timeLabel);
            panel.Add(resultLabel);
            panel.Add(pathLabel);
            Refresh();
        }

        private void ToggleScenario()
        {
            if (runner == null) return;
            if (runner.IsRunning) runner.StopScenarioEarly();
            else runner.RunSelectedScenario();
        }

        private void ToggleManualRecording()
        {
            if (recorder == null) return;
            if (runner != null && runner.IsRunning)
            {
                runner.StopScenarioEarly();
                return;
            }
            if (recorder.IsRecording)
                recorder.StopRecording(
                    "MANUAL", null, System.Array.Empty<string>());
            else
                recorder.StartRecording("Manual Flight", false);
        }

        private void Refresh()
        {
            if (panel == null || runner == null || recorder == null) return;
            FlightTestScenario scenario = runner.SelectedScenario;
            scenarioLabel.text = scenario?.name ?? "NO SCENARIO";
            descriptionLabel.text = scenario?.description ?? string.Empty;
            recordingLabel.text = recorder.IsRecording
                ? recorder.IsAutomatedRun
                    ? "● AUTOMATED TEST RECORDING"
                    : "● MANUAL RECORDING"
                : "○ RECORDER READY";
            recordingLabel.style.color = recorder.IsRecording
                ? new Color(1f, 0.28f, 0.16f)
                : new Color(0.3f, 1f, 0.52f);
            timeLabel.text =
                $"TEST TIME  {(runner.IsRunning ? runner.TestTime : recorder.ElapsedTime):0.0} s" +
                $"   •   INTERVAL {recorder.LogInterval:0.00} s";
            string result = string.IsNullOrWhiteSpace(runner.LastResult)
                ? recorder.LastResult
                : runner.LastResult;
            resultLabel.text = string.IsNullOrWhiteSpace(result)
                ? "RESULT —"
                : $"RESULT  {result}";
            resultLabel.style.color = result == "PASS"
                ? new Color(0.25f, 1f, 0.45f)
                : result == "FAIL"
                    ? new Color(1f, 0.2f, 0.12f)
                    : new Color(1f, 0.75f, 0.18f);
            pathLabel.text = string.IsNullOrWhiteSpace(recorder.LastCsvPath)
                ? "LAST EXPORT —"
                : $"CSV  {recorder.LastCsvPath}\nJSON  {recorder.LastJsonPath}";
            runButton.text = runner.IsRunning ? "STOP TEST  F9" : "RUN  F9";
            manualButton.text = recorder.IsRecording
                ? "STOP RECORDING  F8"
                : "START MANUAL RECORDING  F8";
        }

        private void ApplyVisibility()
        {
            if (panel != null)
                panel.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static Label Label(
            string text, int size, Color color,
            FontStyle style = FontStyle.Normal)
        {
            Label label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = style;
            return label;
        }

        private static void StyleButton(Button button, float width)
        {
            button.style.width = width;
            button.style.height = 32f;
            button.style.backgroundColor =
                new Color(0.06f, 0.2f, 0.24f);
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
    }
}
