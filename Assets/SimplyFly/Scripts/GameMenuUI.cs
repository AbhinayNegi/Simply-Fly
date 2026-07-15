using System;
using System.Collections.Generic;
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
        [SerializeField] private NavigationManager navigation;
        [SerializeField] private FlightHUDController flightHUD;
        [SerializeField] private NavigationUI navigationUI;
        [SerializeField] private WindController wind;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private AudioClip uiClickClip;

        private VisualElement overlay;
        private VisualElement content;
        private VisualElement activeContent;
        private AudioSource uiAudioSource;
        private int selectedAirport;
        private int selectedArrivalAirport = -1;
        private NavigationRouteType selectedRouteType =
            NavigationRouteType.SimpleVFR;
        private float selectedCruiseAltitude = 300f;
        private bool selectedDepartureRunwayReciprocal;
        private bool selectedArrivalRunwayReciprocal;
        private bool plannerOpen;
        private bool windPanelOpen;
        private bool draftWindEnabled;
        private float draftWindDirection;
        private float draftWindSpeed;
        private bool draftGustsEnabled;
        private float draftGustStrength;
        private Label windTelemetryLabel;
        private bool departureDropdownOpen;
        private bool arrivalDropdownOpen;
        private VisualElement animatedDropdown;
        private float dropdownProgress;
        private bool dropdownClosing;
        private float dropdownSelectionHoldUntil;
        private readonly List<Canvas> plannerHiddenCanvases = new();
        private readonly List<VisualElement> plannerHiddenDocumentRoots = new();
        private readonly List<DisplayStyle> plannerHiddenDocumentDisplays = new();
        private readonly List<UIController> plannerHiddenDebugPanels = new();
        private static Font plannerMonoFont;
        private FreeFlightSpawnType selectedSpawn =
            FreeFlightSpawnType.RunwayStart;
        private WeatherPreset selectedWeather = WeatherPreset.Clear;

        private void OnEnable()
        {
            if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
            if (airports == null)
                airports = FindAnyObjectByType<AirportLocationManager>();
            if (lessons == null)
                lessons = FindAnyObjectByType<FlightLessonSystem>();
            if (settings == null)
                settings = FindAnyObjectByType<GameSettingsManager>();
            if (navigation == null)
                navigation = FindAnyObjectByType<NavigationManager>();
            if (flightHUD == null)
                flightHUD = FindAnyObjectByType<FlightHUDController>();
            if (navigationUI == null)
                navigationUI = FindAnyObjectByType<NavigationUI>();
            if (wind == null) wind = FindAnyObjectByType<WindController>();
            if (flightPhysics == null)
                flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
            if (settings != null) selectedWeather = settings.WeatherPreset;
            selectedAirport = PlayerPrefs.GetInt(
                "flightPlanner.lastDepartureAirport", selectedAirport);
            selectedArrivalAirport = PlayerPrefs.GetInt(
                "flightPlanner.lastArrivalAirport", -1);
            selectedCruiseAltitude = PlayerPrefs.GetFloat(
                "flightPlanner.cruiseAltitude", 300f);
            if (airports?.Airports != null && airports.Airports.Length > 0)
                selectedAirport = Mathf.Clamp(
                    selectedAirport, 0, airports.Airports.Length - 1);
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

        private void Update()
        {
            UpdateDropdownAnimation();
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (keyboard[UnityEngine.InputSystem.Key.L].wasPressedThisFrame)
            {
                bool flightSchool = flow != null &&
                    (flow.Mode == SimplyFlyGameMode.FlightSchool ||
                     flow.ActiveFlightMode == SimplyFlyGameMode.FlightSchool);
                if (!flightSchool)
                {
                    if (flow != null && flow.IsInFlight) OpenFlightPlanner();
                    else if (plannerOpen && flow != null &&
                             flow.Mode == SimplyFlyGameMode.Paused)
                        CloseFlightPlanner();
                }
            }
            if (keyboard[UnityEngine.InputSystem.Key.F9].wasPressedThisFrame)
            {
                if (flow != null && flow.IsInFlight) OpenWindPanel();
                else if (windPanelOpen && flow != null &&
                         flow.Mode == SimplyFlyGameMode.Paused)
                    CloseWindPanel(true);
            }
            UpdateWindTelemetry();
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
            content.style.overflow = Overflow.Hidden;
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
            activeContent = content;
        }

        private void HandleModeChanged()
        {
            if (overlay == null || flow == null) return;
            if (plannerOpen && flow.Mode == SimplyFlyGameMode.Paused)
            {
                overlay.style.display = DisplayStyle.Flex;
                ShowFlightPlanner();
                return;
            }
            if (windPanelOpen && flow.Mode == SimplyFlyGameMode.Paused)
            {
                overlay.style.display = DisplayStyle.Flex;
                ShowWindPanel();
                return;
            }
            if (plannerOpen && flow.Mode != SimplyFlyGameMode.Paused)
            {
                plannerOpen = false;
                flightHUD?.SetNavigationMapSuppressed(false);
                navigationUI?.SetPlannerSuppressed(false);
                RestoreNonPlannerHud();
            }
            if (windPanelOpen && flow.Mode != SimplyFlyGameMode.Paused)
            {
                wind?.SaveSettings();
                windPanelOpen = false;
                flightHUD?.SetNavigationMapSuppressed(false);
                navigationUI?.SetPlannerSuppressed(false);
                RestoreNonPlannerHud();
            }
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
            AddPrimaryButton("FREE FLIGHT", ShowFreeFlightStart);
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
            AddSection("DEPARTURE AIRPORT");
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
                            RefreshPreservingScroll(ShowFreeFlight);
                        },
                        selectedAirport == i);
                }
            }

            AddSection("ARRIVAL AIRPORT");
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
                            selectedArrivalAirport = index;
                            RefreshPreservingScroll(ShowFreeFlight);
                        },
                        selectedArrivalAirport == i);
                }
            }

            AddSection("ROUTE TYPE");
            AddChoiceButton(
                "DIRECT GPS",
                () =>
                {
                    selectedRouteType = NavigationRouteType.DirectGPS;
                    RefreshPreservingScroll(ShowFreeFlight);
                },
                selectedRouteType == NavigationRouteType.DirectGPS);
            AddChoiceButton(
                "SIMPLE VFR ROUTE",
                () =>
                {
                    selectedRouteType = NavigationRouteType.SimpleVFR;
                    RefreshPreservingScroll(ShowFreeFlight);
                },
                selectedRouteType == NavigationRouteType.SimpleVFR);

            AddSection("CRUISE ALTITUDE");
            AddChoiceButton(
                "LOW — 200 m",
                () =>
                {
                    selectedCruiseAltitude = 200f;
                    RefreshPreservingScroll(ShowFreeFlight);
                },
                Mathf.Approximately(selectedCruiseAltitude, 200f));
            AddChoiceButton(
                "NORMAL — 300 m",
                () =>
                {
                    selectedCruiseAltitude = 300f;
                    RefreshPreservingScroll(ShowFreeFlight);
                },
                Mathf.Approximately(selectedCruiseAltitude, 300f));
            AddChoiceButton(
                "HIGH — 500 m",
                () =>
                {
                    selectedCruiseAltitude = 500f;
                    RefreshPreservingScroll(ShowFreeFlight);
                },
                Mathf.Approximately(selectedCruiseAltitude, 500f));

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
            AddChoiceButton(
                "PARKING / APRON",
                () => SelectSpawn(FreeFlightSpawnType.ParkingApron),
                selectedSpawn == FreeFlightSpawnType.ParkingApron);

            AddSection("WEATHER");
            foreach (WeatherPreset preset in Enum.GetValues(typeof(WeatherPreset)))
            {
                WeatherPreset choice = preset;
                AddChoiceButton(
                    WeatherManager.FormatPreset(choice).ToUpperInvariant(),
                    () =>
                    {
                        selectedWeather = choice;
                        settings?.SetWeatherPreset(choice);
                        RefreshPreservingScroll(ShowFreeFlight);
                    },
                    selectedWeather == choice);
            }

            string departureName =
                definitions != null &&
                selectedAirport >= 0 &&
                selectedAirport < definitions.Length
                    ? definitions[selectedAirport].airportName
                    : "Departure";
            AirportDefinition arrivalDefinition =
                definitions != null &&
                selectedArrivalAirport >= 0 &&
                selectedArrivalAirport < definitions.Length
                    ? definitions[selectedArrivalAirport]
                    : null;
            string arrivalName =
                arrivalDefinition?.airportName ?? "Arrival";
            Label summary = AddText(
                $"PLAN  {departureName} → {arrivalName}\n" +
                $"{selectedRouteType}   CRUISE {selectedCruiseAltitude:0} m\n" +
                $"APPROACH RWY {(arrivalDefinition == null ? "--" : Mathf.RoundToInt(arrivalDefinition.runwayHeading / 10f).ToString("00"))}   " +
                "FINAL ALTITUDE CALCULATED FROM GLIDE PATH",
                13,
                new Color(0.68f, 0.86f, 0.9f));
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.marginTop = 8f;

            AddPrimaryButton(
                "START FREE FLIGHT",
                StartPlannedFreeFlight,
                new Color(0.1f, 0.48f, 0.28f));
            AddBackButton(ShowMainMenu);
        }

        private void ShowFreeFlightStart()
        {
            BeginScreen("FREE FLIGHT", "Choose where and how your flight begins.");
            AirportDefinition[] definitions = airports?.Airports;
            AddSection("DEPARTURE AIRPORT");
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Length; i++)
                {
                    int index = i;
                    AddChoiceButton(
                        definitions[i]?.airportName ?? $"Airport {i + 1}",
                        () =>
                        {
                            selectedAirport = index;
                            RefreshPreservingScroll(ShowFreeFlightStart);
                        },
                        selectedAirport == i);
                }
            }

            AirportDefinition departure = GetAirport(selectedAirport);
            AddSection("ACTIVE RUNWAY");
            AddChoiceButton(
                FormatRunwayChoice(departure, false),
                () =>
                {
                    selectedDepartureRunwayReciprocal = false;
                    RefreshPreservingScroll(ShowFreeFlightStart);
                },
                !selectedDepartureRunwayReciprocal);
            AddChoiceButton(
                FormatRunwayChoice(departure, true),
                () =>
                {
                    selectedDepartureRunwayReciprocal = true;
                    RefreshPreservingScroll(ShowFreeFlightStart);
                },
                selectedDepartureRunwayReciprocal);

            AddSection("SPAWN TYPE");
            AddChoiceButton("PARKING",
                () => SelectStartSpawn(FreeFlightSpawnType.ParkingApron),
                selectedSpawn == FreeFlightSpawnType.ParkingApron);
            AddChoiceButton("RUNWAY READY",
                () => SelectStartSpawn(FreeFlightSpawnType.RunwayStart),
                selectedSpawn == FreeFlightSpawnType.RunwayStart);
            AddChoiceButton("FINAL APPROACH",
                () => SelectStartSpawn(FreeFlightSpawnType.FinalApproach),
                selectedSpawn == FreeFlightSpawnType.FinalApproach);
            AddChoiceButton("AIRBORNE",
                () => SelectStartSpawn(FreeFlightSpawnType.Airborne),
                selectedSpawn == FreeFlightSpawnType.Airborne);

            Label summary = AddText(
                $"{departure?.airportName ?? "Departure"}   " +
                $"{FormatRunwayChoice(departure, selectedDepartureRunwayReciprocal)}\n" +
                FormatSpawnType(selectedSpawn),
                13,
                new Color(0.68f, 0.86f, 0.9f));
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.marginTop = 8f;
            AddPrimaryButton("START FLIGHT", StartSelectedFreeFlight,
                new Color(0.1f, 0.48f, 0.28f));
            AddBackButton(ShowMainMenu);
        }

        private void SelectStartSpawn(FreeFlightSpawnType spawn)
        {
            selectedSpawn = spawn;
            RefreshPreservingScroll(ShowFreeFlightStart);
        }

        private void StartSelectedFreeFlight()
        {
            airports?.SetActiveRunway(selectedDepartureRunwayReciprocal);
            navigation?.CancelFlightPlan();
            PlayerPrefs.SetInt("flightPlanner.lastDepartureAirport", selectedAirport);
            PlayerPrefs.Save();
            flow?.StartFreeFlight(selectedAirport, selectedSpawn);
        }

        private void StartPlannedFreeFlight()
        {
            settings?.SetWeatherPreset(selectedWeather);
            flow?.StartFreeFlight(selectedAirport, selectedSpawn);
            if (settings == null ||
                (settings.NavigationEnabled && settings.FlightPlannerEnabled))
            {
                navigation?.SetCruiseAltitude(selectedCruiseAltitude);
                navigation?.CreateFlightPlan(
                    selectedAirport,
                    selectedArrivalAirport,
                    selectedRouteType);
            }
        }

        private void SelectSpawn(FreeFlightSpawnType spawn)
        {
            selectedSpawn = spawn;
            RefreshPreservingScroll(ShowFreeFlight);
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
            AddSection("WEATHER");
            AddPrimaryButton(
                $"PRESET: {WeatherManager.FormatPreset(settings.WeatherPreset).ToUpperInvariant()}",
                () =>
                {
                    int count = Enum.GetValues(typeof(WeatherPreset)).Length;
                    WeatherPreset next =
                        (WeatherPreset)(((int)settings.WeatherPreset + 1) % count);
                    settings.SetWeatherPreset(next);
                    selectedWeather = next;
                    RefreshPreservingScroll(() => ShowSettings(backAction));
                });
            AddText(
                "Weather transitions live. F11 toggles its compact test readout.",
                12,
                new Color(0.68f, 0.82f, 0.85f));
            AddToggle("HUD", settings.HUDEnabled, settings.SetHUDEnabled);
            AddToggle("FLIGHT PATH MARKER",
                settings.FlightPathMarkerEnabled,
                settings.SetFlightPathMarkerEnabled);
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
            AddToggle("INVERT CAMERA Y",
                settings.CameraInvertY, settings.SetCameraInvertY);
            AddSection("MOUSE YOKE");
            AddToggle("ENABLE MOUSE YOKE",
                settings.MouseYokeEnabled, settings.SetMouseYokeEnabled);
            AddSlider("YOKE SENSITIVITY",
                settings.MouseYokeSensitivity, 0.2f, 3f,
                settings.SetMouseYokeSensitivity);
            AddSlider("YOKE DEAD ZONE",
                settings.MouseYokeDeadZone, 0f, 0.25f,
                settings.SetMouseYokeDeadZone);
            AddSlider("YOKE SMOOTHING",
                settings.MouseYokeSmoothing, 1f, 24f,
                settings.SetMouseYokeSmoothing);
            AddSlider("YOKE MAXIMUM INPUT",
                settings.MouseYokeMaximumInput, 0.25f, 1f,
                settings.SetMouseYokeMaximumInput);
            AddText(
                "Move mouse to control pitch/roll. U toggles the yoke, middle mouse recenters, and RMB looks around.",
                12,
                new Color(0.68f, 0.82f, 0.85f));
            AddPrimaryButton(
                $"DEFAULT CAMERA: {settings.CameraMode.ToString().ToUpperInvariant()}",
                () =>
                {
                    int count = Enum.GetValues(
                        typeof(CameraController.CameraMode)).Length;
                    CameraController.CameraMode next =
                        (CameraController.CameraMode)(((int)settings.CameraMode + 1) % count);
                    settings.SetCameraMode(next);
                    RefreshPreservingScroll(() => ShowSettings(backAction));
                });
            AddSection("FLIGHT ASSISTS");
            AddText(
                "TRIM ENABLED: OFF (temporarily unavailable)",
                14,
                new Color(0.68f, 0.72f, 0.74f));
            AddToggle("PITCH HOLD ASSIST",
                settings.PitchHoldEnabled, settings.SetPitchHoldEnabled);
            AddSlider("PITCH HOLD STRENGTH",
                settings.PitchHoldStrength, 0f, 0.12f,
                settings.SetPitchHoldStrength);
            AddToggle("VERTICAL SPEED HOLD ASSIST",
                settings.VerticalSpeedHoldEnabled,
                settings.SetVerticalSpeedHoldEnabled);
            AddSlider("VS HOLD STRENGTH",
                settings.VerticalSpeedHoldStrength, 0f, 0.3f,
                settings.SetVerticalSpeedHoldStrength);
            AddSection("NAVIGATION");
            AddToggle("NAVIGATION ENABLED",
                settings.NavigationEnabled, settings.SetNavigationEnabled);
            AddToggle("NAVIGATION HUD",
                settings.NavigationHUDEnabled, settings.SetNavigationHUDEnabled);
            AddToggle("NAVIGATION MAP",
                settings.NavigationMapEnabled, settings.SetNavigationMapEnabled);
            AddToggle("HEADING BUG",
                settings.HeadingBugEnabled, settings.SetHeadingBugEnabled);
            AddToggle("VERTICAL PROFILE HELPER",
                settings.VerticalProfileEnabled,
                settings.SetVerticalProfileEnabled);
            AddSection("APPROACH HELPERS");
            AddToggle("RUNWAY ALIGNMENT GUIDANCE",
                settings.RunwayAlignmentEnabled,
                settings.SetRunwayAlignmentEnabled);
            AddToggle("APPROACH HELPERS",
                settings.ApproachHelpersEnabled,
                settings.SetApproachHelpersEnabled);
            AddToggle("SIDE VIEW GLIDE HELPER",
                settings.SideViewGlideEnabled,
                settings.SetSideViewGlideEnabled);
            AddToggle("TOP-DOWN LATERAL HELPER",
                settings.TopDownLateralEnabled,
                settings.SetTopDownLateralEnabled);
            AddPrimaryButton(
                $"APPROACH GUIDANCE MODE: {FormatGuidanceMode(settings.ApproachGuidanceMode)}",
                () =>
                {
                    ApproachGuidanceDisplayMode next =
                        settings.ApproachGuidanceMode == ApproachGuidanceDisplayMode.Off
                            ? ApproachGuidanceDisplayMode.TwoDHelpers
                            : ApproachGuidanceDisplayMode.Off;
                    settings.SetApproachGuidanceMode(next);
                    RefreshPreservingScroll(() => ShowSettings(backAction));
                });
            AddToggle("AUTO SHOW ON FINAL",
                settings.AutoShowApproachEnabled,
                settings.SetAutoShowApproachEnabled);
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
                "A / D  -  Rudder and ground steering left / right\n" +
                "Left / Right Arrow  -  Airborne bank / roll\n" +
                "Mouse  -  Pitch / roll when Mouse Yoke is enabled\n" +
                "Middle Mouse  -  Recenter mouse yoke and camera\n" +
                "RMB Drag  -  Camera look while Mouse Yoke is enabled\n" +
                "U  -  Toggle Mouse Yoke during flight\n" +
                "Y  -  Toggle Flight School panel\n" +
                "I  -  Toggle Pitch Coach\n" +
                "G  -  Cycle approach aids\n" +
                "L  -  Open in-flight Flight Planner\n" +
                "E  —  Start engine\n" +
                "Shift / Ctrl  —  Throttle up / down\n" +
                "W / S  —  Nose down / nose up\n" +
                "A / D  —  Rudder / ground steering only\n" +
                "Left / Right Arrow  —  Airborne roll only\n" +
                "Space  —  Service brake (hold)\n" +
                "J  —  Toggle parking brake\n" +
                "F / V  —  Extend / retract flaps\n" +
                "P  —  Toggle Pitch Hold Assist\n" +
                "Alt + P  —  Toggle Vertical Speed Hold Assist\n" +
                "+ / - or Mouse Wheel  —  Adjust active VS target\n" +
                "T / Shift + T  —  Trim disabled for now\n" +
                "Z / X  —  Quick look left / right\n" +
                "C  —  Cycle camera mode\n" +
                "R or Middle Mouse  —  Recenter camera\n" +
                "RMB Drag  —  Orbit chase / free camera\n" +
                "H  —  Toggle HUD\n" +
                "F9  —  Wind control panel\n" +
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

        private void OpenFlightPlanner()
        {
            if (flow == null || !flow.IsInFlight) return;
            if (navigation != null && navigation.IsActive)
            {
                selectedAirport = navigation.DepartureAirportIndex;
                selectedArrivalAirport = navigation.ArrivalAirportIndex;
                AirportDefinition plannedDeparture = GetAirport(selectedAirport);
                AirportDefinition plannedArrival = GetAirport(selectedArrivalAirport);
                selectedDepartureRunwayReciprocal = plannedDeparture != null &&
                    Mathf.Abs(Mathf.DeltaAngle(
                        plannedDeparture.runwayHeading,
                        navigation.SelectedDepartureRunwayHeading)) > 90f;
                selectedArrivalRunwayReciprocal = plannedArrival != null &&
                    Mathf.Abs(Mathf.DeltaAngle(
                        plannedArrival.runwayHeading,
                        navigation.SelectedRunwayHeading)) > 90f;
                selectedCruiseAltitude = navigation.SelectedCruiseAltitude;
            }
            else
            {
                selectedAirport = airports == null
                    ? selectedAirport
                    : airports.SelectedAirportIndex;
                selectedDepartureRunwayReciprocal =
                    airports != null && airports.ActiveRunwayReciprocal;
            }
            if (selectedArrivalAirport < 0)
                selectedArrivalAirport = PlayerPrefs.GetInt(
                    "flightPlanner.lastArrivalAirport", -1);
            selectedCruiseAltitude = PlayerPrefs.GetFloat(
                "flightPlanner.cruiseAltitude", 300f);
            plannerOpen = true;
            departureDropdownOpen = false;
            arrivalDropdownOpen = false;
            flightHUD?.SetNavigationMapSuppressed(true);
            navigationUI?.SetPlannerSuppressed(true);
            HideNonPlannerHud();
            flow.Pause();
        }

        private void OpenWindPanel()
        {
            if (flow == null || !flow.IsInFlight || plannerOpen) return;
            if (wind == null) wind = FindAnyObjectByType<WindController>();
            if (flightPhysics == null)
                flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
            draftWindEnabled = wind != null && wind.WindEnabled;
            draftWindDirection = wind == null ? 180f : wind.WindFromHeading;
            draftWindSpeed = wind == null ? 0f : wind.SteadyWindSpeedKnots;
            draftGustsEnabled = wind != null && wind.GustsEnabled;
            draftGustStrength = wind == null ? 0f : wind.GustStrengthKnots;
            windPanelOpen = true;
            flightHUD?.SetNavigationMapSuppressed(true);
            navigationUI?.SetPlannerSuppressed(true);
            HideNonPlannerHud();
            flow.Pause();
        }

        private void ShowWindPanel()
        {
            BeginScreen("WIND CONTROL", "Live airport wind and gust setup — F9 to apply and close.");
            content.style.width = 620f;
            AddToggle("WIND ENABLED", draftWindEnabled, value =>
            {
                draftWindEnabled = value;
                ApplyWindDraft(false);
            });
            AddSlider("DIRECTION FROM (DEG)", draftWindDirection, 0f, 359f,
                value =>
                {
                    draftWindDirection = Mathf.Round(value);
                    ApplyWindDraft(false);
                });
            AddSlider("WIND SPEED (KT)", draftWindSpeed, 0f, 30f,
                value =>
                {
                    draftWindSpeed = value;
                    ApplyWindDraft(false);
                });
            AddToggle("GUSTS ENABLED", draftGustsEnabled, value =>
            {
                draftGustsEnabled = value;
                ApplyWindDraft(false);
            });
            AddSlider("GUST STRENGTH (KT)", draftGustStrength, 0f, 12f,
                value =>
                {
                    draftGustStrength = value;
                    ApplyWindDraft(false);
                });

            windTelemetryLabel = AddText(string.Empty, 13,
                new Color(0.65f, 0.92f, 0.94f), FontStyle.Bold);
            windTelemetryLabel.style.marginTop = 12f;
            windTelemetryLabel.style.marginBottom = 12f;
            UpdateWindTelemetry();

            AddPrimaryButton("APPLY AND CLOSE", () => CloseWindPanel(true));
            AddPrimaryButton("RESET TO CALM", () =>
            {
                draftWindEnabled = false;
                draftWindDirection = 180f;
                draftWindSpeed = 0f;
                draftGustsEnabled = false;
                draftGustStrength = 0f;
                ApplyWindDraft(false);
                ShowWindPanel();
            });
            AddPrimaryButton("RESET TO DEFAULT", () =>
            {
                draftWindEnabled = true;
                draftWindDirection = 180f;
                draftWindSpeed = 7f;
                draftGustsEnabled = true;
                draftGustStrength = 2.5f;
                ApplyWindDraft(false);
                ShowWindPanel();
            });
            AddBackButton(() => CloseWindPanel(true));
        }

        private void ApplyWindDraft(bool save)
        {
            if (wind == null) return;
            wind.SetWind(draftWindDirection, draftWindSpeed);
            wind.SetGusts(draftGustsEnabled, draftGustStrength);
            wind.SetWindEnabled(draftWindEnabled);
            if (save) wind.SaveSettings();
            UpdateWindTelemetry();
        }

        private void UpdateWindTelemetry()
        {
            if (!windPanelOpen || windTelemetryLabel == null) return;
            float airspeed = flightPhysics == null
                ? 0f : flightPhysics.AirspeedKnots;
            float groundSpeed = flightPhysics == null
                ? 0f : flightPhysics.HorizontalGroundSpeed * 1.943844f;
            windTelemetryLabel.text =
                $"WIND {(draftWindEnabled ? "ON" : "OFF")}   " +
                $"FROM {draftWindDirection:000}° / {draftWindSpeed:0.0} KT\n" +
                $"GUSTS {(draftGustsEnabled ? "ON" : "OFF")}   " +
                $"STRENGTH {draftGustStrength:0.0} KT\n" +
                $"HEADWIND {wind?.HeadwindKnots:+0.0;-0.0;0.0} KT   " +
                $"CROSSWIND {wind?.CrosswindKnots:+0.0;-0.0;0.0} KT\n" +
                $"AIRSPEED {airspeed:0.0} KT   GROUND SPEED {groundSpeed:0.0} KT";
        }

        private void CloseWindPanel(bool apply)
        {
            if (apply) ApplyWindDraft(true);
            windPanelOpen = false;
            windTelemetryLabel = null;
            flightHUD?.SetNavigationMapSuppressed(false);
            navigationUI?.SetPlannerSuppressed(false);
            RestoreNonPlannerHud();
            flow?.Resume();
        }

        private void ShowFlightPlanner()
        {
            BuildGlassyFlightPlanner();
        }

        private void BuildGlassyFlightPlanner()
        {
            Color accent = new(0.37f, 0.9f, 0.82f);
            Color primary = new(0.94f, 0.97f, 0.98f);
            Color secondary = new(0.5f, 0.61f, 0.66f);
            Color cardColor = new(0.09f, 0.17f, 0.21f, 0.94f);

            content.Clear();
            activeContent = content;
            content.style.width = Length.Percent(90f);
            content.style.maxWidth = 920f;
            content.style.maxHeight = Length.Percent(92f);
            content.style.paddingLeft = 30f;
            content.style.paddingRight = 30f;
            content.style.paddingTop = 26f;
            content.style.paddingBottom = 26f;
            content.style.backgroundColor = new Color(0.035f, 0.095f, 0.125f, 0.96f);
            content.style.borderTopWidth = 1f;
            content.style.borderBottomWidth = 1f;
            content.style.borderLeftWidth = 1f;
            content.style.borderRightWidth = 1f;
            content.style.borderTopColor = new Color(1f, 1f, 1f, 0.12f);
            content.style.borderBottomColor = new Color(1f, 1f, 1f, 0.08f);
            content.style.borderLeftColor = new Color(1f, 1f, 1f, 0.08f);
            content.style.borderRightColor = new Color(1f, 1f, 1f, 0.08f);
            content.style.borderTopLeftRadius = 24f;
            content.style.borderTopRightRadius = 24f;
            content.style.borderBottomLeftRadius = 24f;
            content.style.borderBottomRightRadius = 24f;

            AddRadarDecoration(content, accent);

            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 18f;
            content.Add(header);
            VisualElement heading = new();
            heading.Add(PlannerLabel("FLIGHT PLANNER", 25, primary, true));
            Label subtitle = PlannerLabel(
                "Select airports and runways — cruise altitude is automatic", 12,
                secondary, false);
            subtitle.style.marginTop = 2f;
            heading.Add(subtitle);
            header.Add(heading);
            VisualElement status = new();
            status.style.flexDirection = FlexDirection.Row;
            status.style.alignItems = Align.Center;
            status.style.paddingLeft = 12f;
            status.style.paddingRight = 12f;
            status.style.paddingTop = 7f;
            status.style.paddingBottom = 7f;
            status.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
            SetRoundedBorder(status, 20f, new Color(1f, 1f, 1f, 0.1f));
            Label dot = PlannerLabel("●", 12, new Color(0.1f, 1f, 0f), true);
            dot.style.marginRight = 6f;
            status.Add(dot);
            status.Add(PlannerLabel("OPERATIONAL", 11,
                new Color(0.1f, 1f, 0f), false));
            header.Add(status);

            VisualElement airportRow = new();
            airportRow.style.flexDirection = FlexDirection.Row;
            airportRow.style.flexWrap = Wrap.Wrap;
            airportRow.style.marginBottom = 14f;
            content.Add(airportRow);
            VisualElement departureCard = BuildAirportPlannerCard(
                true, accent, primary, secondary, cardColor);
            departureCard.style.marginRight = 7f;
            VisualElement arrivalCard = BuildAirportPlannerCard(
                false, accent, primary, secondary, cardColor);
            arrivalCard.style.marginLeft = 7f;
            airportRow.Add(departureCard);
            airportRow.Add(arrivalCard);

            selectedCruiseAltitude = navigation == null ||
                selectedArrivalAirport < 0
                ? 300f
                : navigation.CalculateSafeCruiseAltitude(
                    selectedAirport, selectedArrivalAirport,
                    selectedDepartureRunwayReciprocal,
                    selectedArrivalRunwayReciprocal);
            VisualElement altitudeCard = CreatePlannerCard(cardColor);
            altitudeCard.style.marginBottom = 14f;
            altitudeCard.Add(PlannerSectionLabel(
                "AUTOMATIC VERTICAL PROFILE", secondary));
            Label autoAltitude = PlannerLabel(
                $"AUTO CRUISE ALTITUDE: {selectedCruiseAltitude:0} m",
                20, accent, true);
            autoAltitude.style.marginTop = 9f;
            altitudeCard.Add(autoAltitude);
            altitudeCard.Add(PlannerLabel(
                "Calculated from route length, terrain clearance, climb and descent room",
                10, secondary, false));
            content.Add(altitudeCard);

            VisualElement actionCard = CreatePlannerCard(cardColor);
            actionCard.style.flexDirection = FlexDirection.Row;
            actionCard.style.alignItems = Align.Center;
            actionCard.style.justifyContent = Justify.SpaceBetween;
            content.Add(actionCard);
            Label ready = PlannerLabel(
                GetAirport(selectedArrivalAirport) == null
                    ? "SELECT AN ARRIVAL AIRPORT"
                    : "ROUTE READY FOR NAVIGATION",
                11,
                GetAirport(selectedArrivalAirport) == null
                    ? new Color(1f, 0.7f, 0.2f)
                    : secondary,
                true);
            actionCard.Add(ready);
            VisualElement actions = new();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Center;
            actionCard.Add(actions);
            Button cancel = PlannerButton("CANCEL", CloseFlightPlanner);
            StyleNeutralButton(cancel);
            cancel.style.width = 110f;
            cancel.style.marginRight = 10f;
            actions.Add(cancel);
            Button create = PlannerButton(
                navigation != null && navigation.IsActive
                    ? "UPDATE FLIGHT PLAN  →"
                    : "CREATE FLIGHT PLAN  →",
                CreateInFlightPlan);
            create.style.width = 260f;
            create.style.height = 56f;
            create.style.backgroundColor = accent;
            create.style.color = new Color(0.015f, 0.08f, 0.065f);
            create.style.unityFontStyleAndWeight = FontStyle.Bold;
            create.style.fontSize = 14f;
            create.style.borderTopLeftRadius = 13f;
            create.style.borderTopRightRadius = 13f;
            create.style.borderBottomLeftRadius = 13f;
            create.style.borderBottomRightRadius = 13f;
            actions.Add(create);
        }

        private VisualElement BuildAirportPlannerCard(
            bool departure,
            Color accent,
            Color primary,
            Color secondary,
            Color cardColor)
        {
            int airportIndex = departure ? selectedAirport : selectedArrivalAirport;
            AirportDefinition airport = GetAirport(airportIndex);
            VisualElement card = CreatePlannerCard(cardColor);
            card.style.flexGrow = 1f;
            card.style.flexBasis = 0f;
            card.style.minWidth = 260f;
            card.style.position = Position.Relative;
            card.style.overflow = Overflow.Visible;
            card.Add(PlannerSectionLabel(
                departure ? "↗  DEPARTURE" : "↙  ARRIVAL", secondary));

            Button selector = PlannerButton(string.Empty, () =>
            {
                ToggleAirportDropdown(departure);
            });
            selector.style.height = 72f;
            selector.style.marginTop = 10f;
            selector.style.marginBottom = 10f;
            selector.style.paddingLeft = 14f;
            selector.style.paddingRight = 14f;
            selector.style.backgroundColor = new Color(0.015f, 0.06f, 0.08f, 0.72f);
            SetRoundedBorder(selector, 11f,
                (departure ? departureDropdownOpen : arrivalDropdownOpen)
                    ? new Color(accent.r, accent.g, accent.b, 0.55f)
                    : new Color(1f, 1f, 1f, 0.1f));
            selector.style.flexDirection = FlexDirection.Row;
            selector.style.justifyContent = Justify.SpaceBetween;
            selector.style.alignItems = Align.Center;
            VisualElement airportText = new();
            airportText.Add(PlannerLabel(
                airport == null ? "SELECT" : GetAirportCode(airport),
                20, primary, true));
            airportText.Add(PlannerLabel(
                airport?.airportName ?? "Choose airport",
                11, secondary, false));
            selector.Add(airportText);
            selector.Add(PlannerLabel(
                departure ? departureDropdownOpen ? "▲" : "▼" :
                arrivalDropdownOpen ? "▲" : "▼", 11, secondary, false));
            card.Add(selector);

            bool dropdownOpen = departure
                ? departureDropdownOpen
                : arrivalDropdownOpen;
            VisualElement dropdown = null;
            if (dropdownOpen)
            {
                dropdown = BuildAirportDropdown(departure, accent, cardColor);
                animatedDropdown = dropdown;
                ApplyDropdownVisual();
                card.Add(dropdown);
            }

            card.Add(PlannerSectionLabel(
                departure ? "ACTIVE RUNWAY" : "LANDING RUNWAY", secondary));
            VisualElement runwayRow = new();
            runwayRow.style.flexDirection = FlexDirection.Row;
            runwayRow.style.marginTop = 8f;
            card.Add(runwayRow);
            if (airport == null)
            {
                runwayRow.Add(PlannerLabel("SELECT AIRPORT", 11, secondary, false));
            }
            else
            {
                bool reciprocal = departure
                    ? selectedDepartureRunwayReciprocal
                    : selectedArrivalRunwayReciprocal;
                runwayRow.Add(CreateRunwayChip(
                    airport, false, !reciprocal, departure, accent));
                runwayRow.Add(CreateRunwayChip(
                    airport, true, reciprocal, departure, accent));
            }
            dropdown?.BringToFront();
            return card;
        }

        private VisualElement BuildAirportDropdown(
            bool departure,
            Color accent,
            Color cardColor)
        {
            VisualElement dropdown = new();
            dropdown.style.position = Position.Absolute;
            dropdown.style.top = 92f;
            dropdown.style.left = 16f;
            dropdown.style.right = 16f;
            dropdown.style.backgroundColor = new Color(0.045f, 0.12f, 0.16f, 0.99f);
            SetRoundedBorder(dropdown, 12f, new Color(1f, 1f, 1f, 0.15f));
            dropdown.style.paddingTop = 5f;
            dropdown.style.paddingBottom = 5f;

            AirportDefinition[] definitions = airports?.Airports;
            if (definitions == null) return dropdown;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (!departure && i == selectedAirport) continue;
                int index = i;
                AirportDefinition option = definitions[i];
                Button row = PlannerButton(string.Empty, () =>
                {
                    if (departure)
                    {
                        selectedAirport = index;
                        selectedDepartureRunwayReciprocal = false;
                        if (selectedArrivalAirport == index)
                            selectedArrivalAirport = -1;
                    }
                    else
                    {
                        selectedArrivalAirport = index;
                        selectedArrivalRunwayReciprocal = false;
                    }
                    dropdownSelectionHoldUntil = Time.unscaledTime + 0.12f;
                    dropdownClosing = true;
                    ShowFlightPlanner();
                });
                row.style.height = 45f;
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingLeft = 12f;
                row.style.paddingRight = 12f;
                row.style.backgroundColor =
                    (departure ? selectedAirport : selectedArrivalAirport) == i
                        ? new Color(accent.r, accent.g, accent.b, 0.13f)
                        : Color.clear;
                row.Add(PlannerLabel(GetAirportCode(option), 13,
                    Color.white, true));
                Label name = PlannerLabel(option?.airportName ?? "Airport",
                    11, new Color(0.5f, 0.61f, 0.66f), false);
                name.style.marginLeft = 14f;
                row.Add(name);
                dropdown.Add(row);
                AddHoverTransition(row,
                    row.style.backgroundColor.value,
                    new Color(accent.r, accent.g, accent.b, 0.2f));
            }
            return dropdown;
        }

        private Button CreateRunwayChip(
            AirportDefinition airport,
            bool reciprocal,
            bool selected,
            bool departure,
            Color accent)
        {
            Button chip = PlannerButton(
                FormatRunwayChoice(airport, reciprocal).Replace("RWY ", ""),
                () =>
                {
                    if (departure)
                        selectedDepartureRunwayReciprocal = reciprocal;
                    else
                        selectedArrivalRunwayReciprocal = reciprocal;
                    ShowFlightPlanner();
                });
            chip.style.minWidth = 58f;
            chip.style.height = 38f;
            chip.style.marginRight = 7f;
            chip.style.fontSize = 12f;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.color = selected
                ? new Color(0.015f, 0.08f, 0.065f)
                : new Color(0.8f, 0.88f, 0.91f);
            chip.style.backgroundColor = selected
                ? new Color(accent.r, accent.g, accent.b, 0.72f)
                : new Color(1f, 1f, 1f, 0.035f);
            SetRoundedBorder(chip, 9f, selected
                ? new Color(accent.r, accent.g, accent.b, 0.9f)
                : new Color(1f, 1f, 1f, 0.1f));
            return chip;
        }

        private static VisualElement CreatePlannerCard(Color fill)
        {
            VisualElement card = new();
            card.style.paddingLeft = 18f;
            card.style.paddingRight = 18f;
            card.style.paddingTop = 16f;
            card.style.paddingBottom = 16f;
            card.style.backgroundColor = fill;
            SetRoundedBorder(card, 16f, new Color(1f, 1f, 1f, 0.09f));
            return card;
        }

        private static Label PlannerSectionLabel(string text, Color color)
        {
            Label label = PlannerLabel(text, 11, color, true);
            label.style.letterSpacing = 1.4f;
            return label;
        }

        private static Label PlannerLabel(
            string text,
            int size,
            Color color,
            bool bold)
        {
            Label label = new(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight =
                bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal;
            plannerMonoFont ??=
                Font.CreateDynamicFontFromOSFont("Consolas", 14);
            if (plannerMonoFont != null)
                label.style.unityFont = plannerMonoFont;
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        private Button PlannerButton(string text, Action action)
        {
            Button button = new(() =>
            {
                PlayUIClick();
                action?.Invoke();
            }) { text = text };
            button.style.color = Color.white;
            button.style.borderTopWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
            button.style.borderRightWidth = 0f;
            return button;
        }

        private static void StyleNeutralButton(Button button)
        {
            button.style.height = 46f;
            button.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);
            button.style.color = new Color(0.75f, 0.84f, 0.88f);
            SetRoundedBorder(button, 11f, new Color(1f, 1f, 1f, 0.1f));
        }

        private static void SetRoundedBorder(
            VisualElement element,
            float radius,
            Color border)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
            element.style.borderTopWidth = 1f;
            element.style.borderBottomWidth = 1f;
            element.style.borderLeftWidth = 1f;
            element.style.borderRightWidth = 1f;
            element.style.borderTopColor = border;
            element.style.borderBottomColor = border;
            element.style.borderLeftColor = border;
            element.style.borderRightColor = border;
        }

        private static void AddRadarDecoration(VisualElement parent, Color accent)
        {
            for (int i = 0; i < 3; i++)
            {
                float size = 260f + i * 170f;
                VisualElement ring = new();
                ring.pickingMode = PickingMode.Ignore;
                ring.style.position = Position.Absolute;
                ring.style.width = size;
                ring.style.height = size;
                ring.style.left = Length.Percent(50f);
                ring.style.top = Length.Percent(50f);
                ring.style.translate = new Translate(
                    Length.Percent(-50f), Length.Percent(-50f));
                SetRoundedBorder(ring, size * 0.5f,
                    new Color(accent.r, accent.g, accent.b, 0.045f));
                parent.Add(ring);
            }
        }

        private static string GetAirportCode(AirportDefinition airport)
        {
            if (airport == null || string.IsNullOrWhiteSpace(airport.airportName))
                return "----";
            string name = airport.airportName.ToUpperInvariant();
            if (name.Contains("TRAINING")) return "TRNG";
            if (name.Contains("VALLEY")) return "VLY";
            if (name.Contains("COASTAL")) return "CSTL";
            string compact = name.Replace(" ", string.Empty);
            return compact.Substring(0, Mathf.Min(4, compact.Length));
        }

        private void AddCruiseChoice(string label, float altitude)
        {
            AddChoiceButton(
                $"{label} — {altitude:0} m",
                () =>
                {
                    selectedCruiseAltitude = altitude;
                    RefreshPreservingScroll(ShowFlightPlanner);
                },
                Mathf.Approximately(selectedCruiseAltitude, altitude));
        }

        private void CreateInFlightPlan()
        {
            if (navigation == null || selectedArrivalAirport < 0)
            {
                RefreshPreservingScroll(ShowFlightPlanner);
                return;
            }
            if (settings != null) settings.SetNavigationEnabled(true);
            else navigation.SetNavigationEnabled(true);
            navigation.CancelFlightPlan();
            airports?.SelectAirport(selectedAirport, false);
            airports?.SetActiveRunway(selectedDepartureRunwayReciprocal);
            airports?.SpawnAtRunway();
            selectedCruiseAltitude = navigation.CalculateSafeCruiseAltitude(
                selectedAirport, selectedArrivalAirport,
                selectedDepartureRunwayReciprocal,
                selectedArrivalRunwayReciprocal);
            navigation.SetCruiseAltitude(selectedCruiseAltitude);
            navigation.SetPlannerRunways(
                selectedDepartureRunwayReciprocal,
                selectedArrivalRunwayReciprocal);
            navigation.CreateFlightPlan(
                selectedAirport,
                selectedArrivalAirport,
                NavigationRouteType.SimpleVFR);
            PlayerPrefs.SetInt(
                "flightPlanner.lastDepartureAirport", selectedAirport);
            PlayerPrefs.SetInt(
                "flightPlanner.lastArrivalAirport", selectedArrivalAirport);
            PlayerPrefs.Save();
            CloseFlightPlanner();
        }

        private void CloseFlightPlanner()
        {
            plannerOpen = false;
            departureDropdownOpen = false;
            arrivalDropdownOpen = false;
            flightHUD?.SetNavigationMapSuppressed(false);
            navigationUI?.SetPlannerSuppressed(false);
            RestoreNonPlannerHud();
            flow?.Resume();
        }

        private void ToggleAirportDropdown(bool departure)
        {
            bool isOpen = departure ? departureDropdownOpen : arrivalDropdownOpen;
            if (isOpen)
            {
                dropdownSelectionHoldUntil = 0f;
                dropdownClosing = true;
                return;
            }
            departureDropdownOpen = departure;
            arrivalDropdownOpen = !departure;
            dropdownProgress = 0f;
            dropdownClosing = false;
            ShowFlightPlanner();
        }

        private void UpdateDropdownAnimation()
        {
            if (!plannerOpen || animatedDropdown == null) return;
            if (dropdownClosing && Time.unscaledTime < dropdownSelectionHoldUntil)
                return;
            float direction = dropdownClosing ? -1f : 1f;
            dropdownProgress = Mathf.Clamp01(
                dropdownProgress + direction * Time.unscaledDeltaTime / 0.16f);
            ApplyDropdownVisual();
            if (!dropdownClosing || dropdownProgress > 0f) return;
            departureDropdownOpen = false;
            arrivalDropdownOpen = false;
            dropdownClosing = false;
            animatedDropdown = null;
            ShowFlightPlanner();
        }

        private void ApplyDropdownVisual()
        {
            if (animatedDropdown == null) return;
            float eased = dropdownProgress * dropdownProgress *
                (3f - 2f * dropdownProgress);
            animatedDropdown.style.opacity = eased;
            animatedDropdown.style.translate = new Translate(
                0f, Mathf.Lerp(-10f, 0f, eased));
        }

        private static void AddHoverTransition(
            VisualElement element, Color normal, Color hover)
        {
            element.RegisterCallback<PointerEnterEvent>(_ =>
                element.style.backgroundColor = hover);
            element.RegisterCallback<PointerLeaveEvent>(_ =>
                element.style.backgroundColor = normal);
        }

        private void HideNonPlannerHud()
        {
            plannerHiddenCanvases.Clear();
            plannerHiddenDocumentRoots.Clear();
            plannerHiddenDocumentDisplays.Clear();
            plannerHiddenDebugPanels.Clear();
            UIDocument ownDocument = GetComponent<UIDocument>();
            foreach (Canvas canvas in FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include))
            {
                if (!canvas.enabled || canvas.gameObject == gameObject) continue;
                plannerHiddenCanvases.Add(canvas);
                canvas.enabled = false;
            }
            foreach (UIDocument document in FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Include))
            {
                if (!document.enabled || document == ownDocument) continue;
                VisualElement root = document.rootVisualElement;
                if (root == null) continue;
                plannerHiddenDocumentRoots.Add(root);
                plannerHiddenDocumentDisplays.Add(root.resolvedStyle.display);
                root.style.display = DisplayStyle.None;
            }
            foreach (UIController debugPanel in FindObjectsByType<UIController>(
                         FindObjectsInactive.Include))
            {
                if (!debugPanel.enabled) continue;
                plannerHiddenDebugPanels.Add(debugPanel);
                debugPanel.enabled = false;
            }
        }

        private void RestoreNonPlannerHud()
        {
            foreach (Canvas canvas in plannerHiddenCanvases)
                if (canvas != null) canvas.enabled = true;
            for (int i = 0; i < plannerHiddenDocumentRoots.Count; i++)
            {
                VisualElement root = plannerHiddenDocumentRoots[i];
                if (root != null)
                    root.style.display = plannerHiddenDocumentDisplays[i];
            }
            foreach (UIController debugPanel in plannerHiddenDebugPanels)
                if (debugPanel != null) debugPanel.enabled = true;
            plannerHiddenCanvases.Clear();
            plannerHiddenDocumentRoots.Clear();
            plannerHiddenDocumentDisplays.Clear();
            plannerHiddenDebugPanels.Clear();
        }

        private AirportDefinition GetAirport(int index)
        {
            AirportDefinition[] definitions = airports?.Airports;
            return definitions != null && index >= 0 && index < definitions.Length
                ? definitions[index]
                : null;
        }

        private static string FormatRunwayChoice(
            AirportDefinition airport,
            bool reciprocal)
        {
            if (airport == null) return "RWY --";
            float heading = Mathf.Repeat(
                airport.runwayHeading + (reciprocal ? 180f : 0f), 360f);
            return AirportLocationManager.FormatRunway(heading);
        }

        private static string FormatSpawnType(FreeFlightSpawnType spawn) =>
            spawn switch
            {
                FreeFlightSpawnType.ParkingApron => "PARKING",
                FreeFlightSpawnType.RunwayStart => "RUNWAY READY",
                FreeFlightSpawnType.FinalApproach => "FINAL APPROACH",
                FreeFlightSpawnType.Airborne => "AIRBORNE",
                _ => spawn.ToString().ToUpperInvariant()
            };

        private void BeginScreen(string title, string subtitle)
        {
            content.Clear();
            activeContent = content;
            content.style.width = 680f;
            content.style.maxWidth = 680f;
            content.style.paddingLeft = 30f;
            content.style.paddingRight = 30f;
            content.style.paddingTop = 24f;
            content.style.paddingBottom = 24f;
            content.style.backgroundColor =
                new Color(0.018f, 0.06f, 0.078f, 0.97f);
            Label titleLabel = AddText(
                title, 34, new Color(0.28f, 0.95f, 1f), FontStyle.Bold);
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            Label subtitleLabel = AddText(
                subtitle, 14, new Color(0.68f, 0.82f, 0.85f));
            subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            subtitleLabel.style.marginBottom = 18f;

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            scroll.style.flexShrink = 1f;
            scroll.style.minHeight = 0f;
            scroll.style.maxHeight = Length.Percent(100f);
            scroll.verticalScrollerVisibility =
                ScrollerVisibility.Auto;
            scroll.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;
            content.Add(scroll);
            activeContent = scroll;
        }

        private void RefreshPreservingScroll(Action rebuild)
        {
            Vector2 previousOffset = activeContent is ScrollView current
                ? current.scrollOffset
                : Vector2.zero;

            rebuild?.Invoke();

            if (activeContent is not ScrollView replacement) return;
            // The replacement ScrollView has no final geometry until the next
            // UI Toolkit layout pass. Restoring then avoids its value being
            // clamped back to the top while the menu is rebuilt.
            replacement.schedule.Execute(
                () => replacement.scrollOffset = previousOffset).StartingIn(1);
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
            activeContent.Add(button);
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

        private static string FormatGuidanceMode(
            ApproachGuidanceDisplayMode mode) => mode switch
        {
            ApproachGuidanceDisplayMode.Off => "OFF",
            ApproachGuidanceDisplayMode.TwoDHelpers => "2D HELPERS",
            ApproachGuidanceDisplayMode.ThreeDTrajectory => "2D HELPERS",
            ApproachGuidanceDisplayMode.Both => "2D HELPERS",
            _ => mode.ToString().ToUpperInvariant()
        };

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
            activeContent.Add(slider);
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
            activeContent.Add(toggle);
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
            activeContent.Add(label);
            return label;
        }

        private void PlayUIClick()
        {
            if (uiAudioSource != null && uiClickClip != null)
                uiAudioSource.PlayOneShot(uiClickClip, 0.55f);
        }
    }
}
