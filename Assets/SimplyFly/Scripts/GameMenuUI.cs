using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameMenuUI : MonoBehaviour
    {
        [SerializeField] private GameFlowController flow;
        [SerializeField] private AirportLocationManager airports;
        [SerializeField] private FlightLessonSystem lessons;
        [SerializeField] private GameSettingsManager settings;
        [SerializeField] private AudioClip uiClickClip;

        private VisualElement overlay;
        private VisualElement content;
        private AudioSource uiAudioSource;
        private int selectedAirport;
        private FreeFlightSpawnType selectedSpawn =
            FreeFlightSpawnType.RunwayStart;

        private void OnEnable()
        {
            if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
            if (airports == null)
                airports = FindAnyObjectByType<AirportLocationManager>();
            if (lessons == null)
                lessons = FindAnyObjectByType<FlightLessonSystem>();
            if (settings == null)
                settings = FindAnyObjectByType<GameSettingsManager>();
            if (flow != null) flow.ModeChanged += HandleModeChanged;
        }

        private void OnDisable()
        {
            if (flow != null) flow.ModeChanged -= HandleModeChanged;
        }

        private void Start()
        {
            uiAudioSource = GetComponent<AudioSource>();
            if (uiAudioSource == null)
                uiAudioSource = gameObject.AddComponent<AudioSource>();
            uiAudioSource.playOnAwake = false;
            uiAudioSource.spatialBlend = 0f;
            uiAudioSource.ignoreListenerPause = true;
            BuildRoot();
            HandleModeChanged();
        }

        private void BuildRoot()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0f;
            overlay.style.right = 0f;
            overlay.style.top = 0f;
            overlay.style.bottom = 0f;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.backgroundColor =
                new Color(0.008f, 0.02f, 0.028f, 0.88f);
            root.Add(overlay);

            content = new VisualElement();
            content.style.width = 680f;
            content.style.maxHeight = Length.Percent(92f);
            content.style.paddingLeft = 30f;
            content.style.paddingRight = 30f;
            content.style.paddingTop = 24f;
            content.style.paddingBottom = 24f;
            content.style.backgroundColor =
                new Color(0.018f, 0.06f, 0.078f, 0.97f);
            content.style.borderTopLeftRadius = 12f;
            content.style.borderTopRightRadius = 12f;
            content.style.borderBottomLeftRadius = 12f;
            content.style.borderBottomRightRadius = 12f;
            overlay.Add(content);
        }

        private void HandleModeChanged()
        {
            if (overlay == null || flow == null) return;
            bool menuVisible =
                flow.Mode == SimplyFlyGameMode.MainMenu ||
                flow.Mode == SimplyFlyGameMode.Paused;
            overlay.style.display =
                menuVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!menuVisible) return;
            if (flow.Mode == SimplyFlyGameMode.Paused) ShowPauseMenu();
            else ShowMainMenu();
        }

        private void ShowMainMenu()
        {
            BeginScreen("SIMPLY FLY", "A believable, welcoming flight simulator.");
            AddPrimaryButton("FREE FLIGHT", ShowFreeFlight);
            AddPrimaryButton("FLIGHT SCHOOL", ShowFlightSchool);
            AddPrimaryButton("SETTINGS", () => ShowSettings(ShowMainMenu));
            AddPrimaryButton("CONTROLS", () => ShowControls(ShowMainMenu));
            AddPrimaryButton("QUIT", () => flow?.QuitGame());
        }

        private void ShowFreeFlight()
        {
            BeginScreen(
                "FREE FLIGHT",
                "Choose an airport and starting position.");
            AddSection("AIRPORT");
            AirportDefinition[] definitions = airports?.Airports;
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Length; i++)
                {
                    int index = i;
                    string name = definitions[i]?.airportName ??
                                  $"Airport {i + 1}";
                    AddChoiceButton(
                        name,
                        () =>
                        {
                            selectedAirport = index;
                            ShowFreeFlight();
                        },
                        selectedAirport == i);
                }
            }

            AddSection("SPAWN TYPE");
            AddChoiceButton(
                "RUNWAY START",
                () => SelectSpawn(FreeFlightSpawnType.RunwayStart),
                selectedSpawn == FreeFlightSpawnType.RunwayStart);
            AddChoiceButton(
                "AIRBORNE",
                () => SelectSpawn(FreeFlightSpawnType.Airborne),
                selectedSpawn == FreeFlightSpawnType.Airborne);
            AddChoiceButton(
                "FINAL APPROACH",
                () => SelectSpawn(FreeFlightSpawnType.FinalApproach),
                selectedSpawn == FreeFlightSpawnType.FinalApproach);

            AddPrimaryButton(
                "START FREE FLIGHT",
                () => flow?.StartFreeFlight(
                    selectedAirport, selectedSpawn),
                new Color(0.1f, 0.48f, 0.28f));
            AddBackButton(ShowMainMenu);
        }

        private void SelectSpawn(FreeFlightSpawnType spawn)
        {
            selectedSpawn = spawn;
            ShowFreeFlight();
        }

        private void ShowFlightSchool()
        {
            BeginScreen(
                "FLIGHT SCHOOL",
                "Choose a guided lesson. Each lesson prepares its own aircraft state.");
            FlightLessonDefinition[] definitions = lessons?.Lessons;
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Length; i++)
                {
                    int lessonIndex = i;
                    FlightLessonDefinition lesson = definitions[i];
                    Button button = AddPrimaryButton(
                        lesson.title,
                        () => flow?.StartLesson(lessonIndex));
                    button.tooltip = lesson.description;
                }
            }
            AddBackButton(ShowMainMenu);
        }

        private void ShowSettings(Action backAction)
        {
            BeginScreen(
                "SETTINGS",
                "Settings are saved automatically.");
            if (settings == null)
            {
                AddText("Settings manager unavailable.", 15, Color.red);
                AddBackButton(backAction);
                return;
            }

            AddSlider("MASTER VOLUME", settings.MasterVolume, 0f, 1f,
                settings.SetMasterVolume);
            AddSlider("ENGINE VOLUME", settings.EngineVolume, 0f, 1f,
                settings.SetEngineVolume);
            AddSlider("WIND VOLUME", settings.WindVolume, 0f, 1f,
                settings.SetWindVolume);
            AddSlider("WARNING VOLUME", settings.WarningVolume, 0f, 1f,
                settings.SetWarningVolume);
            AddToggle("HUD", settings.HUDEnabled, settings.SetHUDEnabled);
            AddToggle("PILOT AWARENESS ASSIST",
                settings.AwarenessEnabled, settings.SetAwarenessEnabled);
            AddToggle("PITCH COACH",
                settings.PitchCoachEnabled, settings.SetPitchCoachEnabled);
            AddToggle("CAMERA AUTO-RECENTER",
                settings.CameraRecenterEnabled,
                settings.SetCameraRecenterEnabled);
            AddSlider("MOTION CUE STRENGTH",
                settings.MotionCueStrength, 0f, 2f,
                settings.SetMotionCueStrength);
            AddSlider("MOUSE SENSITIVITY",
                settings.MouseSensitivity, 0.02f, 0.5f,
                settings.SetMouseSensitivity);
            AddToggle("DEBUG UI",
                settings.DebugUIEnabled, settings.SetDebugUIEnabled);
            AddBackButton(backAction);
        }

        private void ShowControls(Action backAction)
        {
            BeginScreen(
                "CONTROLS",
                "Keyboard and mouse controls");
            string controls =
                "J / L  -  Rudder left / right\n" +
                "I  -  Toggle Pitch Coach\n" +
                "G  -  Toggle Flight School panel\n" +
                "E  —  Start engine\n" +
                "Shift / Ctrl  —  Throttle up / down\n" +
                "W / S  —  Nose down / nose up\n" +
                "A / D  —  Ground steering / airborne roll\n" +
                "Space  —  Brake\n" +
                "F / V  —  Extend / retract flaps\n" +
                "T  —  Assisted trim\n" +
                "Shift + T  —  Reset trim\n" +
                "Z / X  —  Quick look left / right\n" +
                "C or Middle Mouse  —  Recenter camera\n" +
                "H  —  Toggle HUD\n" +
                "F10  —  Toggle debug UI\n" +
                "F1 / F2 / F3  —  Developer spawn shortcuts\n" +
                "Esc  —  Pause";
            Label label = AddText(controls, 16, Color.white);
            label.style.whiteSpace = WhiteSpace.Normal;
            AddBackButton(backAction);
        }

        private void ShowPauseMenu()
        {
            BeginScreen("PAUSED", "Flight simulation is paused.");
            AddPrimaryButton("RESUME", () => flow?.Resume());
            AddPrimaryButton("RESTART FLIGHT", () => flow?.RestartFlight());
            AddPrimaryButton("RETURN TO MAIN MENU", () => flow?.EnterMainMenu());
            AddPrimaryButton("SETTINGS", () => ShowSettings(ShowPauseMenu));
            AddPrimaryButton("CONTROLS", () => ShowControls(ShowPauseMenu));
            AddPrimaryButton("QUIT", () => flow?.QuitGame());
        }

        private void BeginScreen(string title, string subtitle)
        {
            content.Clear();
            Label titleLabel = AddText(
                title, 34, new Color(0.28f, 0.95f, 1f), FontStyle.Bold);
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            Label subtitleLabel = AddText(
                subtitle, 14, new Color(0.68f, 0.82f, 0.85f));
            subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            subtitleLabel.style.marginBottom = 18f;
        }

        private void AddSection(string text)
        {
            Label section = AddText(
                text, 13, new Color(0.35f, 0.82f, 0.88f), FontStyle.Bold);
            section.style.marginTop = 9f;
            section.style.marginBottom = 4f;
        }

        private Button AddPrimaryButton(
            string text,
            Action action,
            Color? color = null)
        {
            Button button = new Button(
                () =>
                {
                    PlayUIClick();
                    action?.Invoke();
                })
            {
                text = text
            };
            button.style.height = 43f;
            button.style.marginTop = 4f;
            button.style.marginBottom = 4f;
            button.style.backgroundColor =
                color ?? new Color(0.055f, 0.2f, 0.25f);
            button.style.color = Color.white;
            button.style.fontSize = 16f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            content.Add(button);
            return button;
        }

        private void AddChoiceButton(
            string text,
            Action action,
            bool selected)
        {
            AddPrimaryButton(
                selected ? $"●  {text}" : $"○  {text}",
                action,
                selected
                    ? new Color(0.08f, 0.38f, 0.42f)
                    : new Color(0.045f, 0.15f, 0.18f));
        }

        private void AddBackButton(Action action)
        {
            Button back = AddPrimaryButton("BACK", action);
            back.style.marginTop = 14f;
            back.style.backgroundColor = new Color(0.16f, 0.18f, 0.2f);
        }

        private void AddSlider(
            string label,
            float value,
            float minimum,
            float maximum,
            Action<float> callback)
        {
            Slider slider = new Slider(label, minimum, maximum)
            {
                value = value,
                showInputField = true
            };
            slider.style.height = 38f;
            slider.style.color = Color.white;
            slider.RegisterValueChangedCallback(
                evt => callback?.Invoke(evt.newValue));
            content.Add(slider);
        }

        private void AddToggle(
            string label,
            bool value,
            Action<bool> callback)
        {
            Toggle toggle = new Toggle(label) { value = value };
            toggle.style.height = 32f;
            toggle.style.color = Color.white;
            toggle.RegisterValueChangedCallback(
                evt => callback?.Invoke(evt.newValue));
            content.Add(toggle);
        }

        private Label AddText(
            string text,
            int size,
            Color color,
            FontStyle style = FontStyle.Normal)
        {
            Label label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = style;
            content.Add(label);
            return label;
        }

        private void PlayUIClick()
        {
            if (uiAudioSource != null && uiClickClip != null)
                uiAudioSource.PlayOneShot(uiClickClip, 0.55f);
        }
    }
}
