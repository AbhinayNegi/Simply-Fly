using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    public enum SimplyFlyGameMode
    {
        MainMenu,
        FreeFlight,
        FlightSchool,
        Paused
    }

    public enum FreeFlightSpawnType
    {
        RunwayStart,
        Airborne,
        FinalApproach,
        ParkingApron
    }

    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] private AirportLocationManager airportManager;
        [SerializeField] private AircraftResetController resetController;
        [SerializeField] private FlightLessonSystem lessonSystem;
        [SerializeField] private FlightSchoolUI lessonUI;
        [SerializeField] private FlightHUDController flightHUD;
        [SerializeField] private GameSettingsManager settings;

        public event Action ModeChanged;
        public SimplyFlyGameMode Mode { get; private set; } =
            SimplyFlyGameMode.MainMenu;
        public SimplyFlyGameMode ActiveFlightMode { get; private set; } =
            SimplyFlyGameMode.FreeFlight;
        public int SelectedAirportIndex { get; private set; }
        public FreeFlightSpawnType SelectedSpawnType { get; private set; }
        public int SelectedLessonIndex { get; private set; }
        public bool IsInFlight =>
            Mode == SimplyFlyGameMode.FreeFlight ||
            Mode == SimplyFlyGameMode.FlightSchool;

        private void Start()
        {
            EnterMainMenu();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;
            if (IsInFlight) Pause();
            else if (Mode == SimplyFlyGameMode.Paused) Resume();
        }

        public void StartFreeFlight(
            int airportIndex,
            FreeFlightSpawnType spawnType)
        {
            SelectedAirportIndex = airportIndex;
            SelectedSpawnType = spawnType;
            airportManager?.SelectAirport(airportIndex, false);
            SpawnFreeFlight();
            ActiveFlightMode = SimplyFlyGameMode.FreeFlight;
            SetMode(SimplyFlyGameMode.FreeFlight, false);
            lessonUI?.SetVisible(false);
            flightHUD?.SetHUDVisible(settings == null || settings.HUDEnabled);
        }

        public void StartLesson(int lessonIndex)
        {
            SelectedLessonIndex = lessonIndex;
            lessonSystem?.SelectLesson(lessonIndex);
            ActiveFlightMode = SimplyFlyGameMode.FlightSchool;
            SetMode(SimplyFlyGameMode.FlightSchool, false);
            lessonUI?.SetVisible(true);
            flightHUD?.SetHUDVisible(settings == null || settings.HUDEnabled);
        }

        public void Pause()
        {
            if (!IsInFlight) return;
            ActiveFlightMode = Mode;
            SetMode(SimplyFlyGameMode.Paused, true);
        }

        public void Resume()
        {
            if (Mode != SimplyFlyGameMode.Paused) return;
            SetMode(ActiveFlightMode, false);
        }

        public void RestartFlight()
        {
            if (ActiveFlightMode == SimplyFlyGameMode.FlightSchool)
                lessonSystem?.RestartCurrentLesson();
            else
                SpawnFreeFlight();
            Resume();
        }

        public void EnterMainMenu()
        {
            lessonUI?.SetVisible(false);
            flightHUD?.SetHUDVisible(false);
            SetMode(SimplyFlyGameMode.MainMenu, true);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        private void SpawnFreeFlight()
        {
            switch (SelectedSpawnType)
            {
                case FreeFlightSpawnType.RunwayStart:
                    airportManager?.SpawnAtRunway();
                    break;
                case FreeFlightSpawnType.Airborne:
                    if (airportManager != null) airportManager.SpawnAirborne();
                    else resetController?.SpawnAirborneTest();
                    break;
                case FreeFlightSpawnType.FinalApproach:
                    if (airportManager != null)
                        airportManager.SpawnOnFinalApproach();
                    else
                        resetController?.SpawnFinalApproach();
                    break;
                case FreeFlightSpawnType.ParkingApron:
                    airportManager?.SpawnAtParking();
                    break;
            }
        }

        private void SetMode(SimplyFlyGameMode mode, bool paused)
        {
            Mode = mode;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            Cursor.visible = paused;
            Cursor.lockState =
                paused ? CursorLockMode.None : CursorLockMode.Confined;
            ModeChanged?.Invoke();
        }
    }
}
