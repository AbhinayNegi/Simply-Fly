using UnityEngine;

namespace SimplyFly.GroundOps
{
    [System.Serializable]
    public sealed class CameraSettingsData
    {
        public float ExternalFov = 60f;
        public float CockpitFov = 70f;
        public float CinematicFov = 50f;
        public float ExternalSensitivity = 1f;
        public float CockpitSensitivity = 1f;
        public float CockpitPositionX;
        public float CockpitPositionY;
        public float CockpitPositionZ;
        public float MotionIntensity = 0.6f;
        public bool ShakeEnabled = true;
        public bool AutoRecenter = true;
        public float RecenterDelay = 1.5f;
        public float RecenterSpeed = 2.5f;
        public bool InvertExternalY;
        public bool InvertCockpitY;
        public bool ShowCinematicHud;
        public bool CinematicTransitions = true;
        public bool RememberLastView = true;
        public bool IncludeWingViews = true;
        public bool IncludeCloseChase = true;
    }

    public sealed class GameSettingsManager : MonoBehaviour
    {
        [SerializeField] private AircraftAudioController aircraftAudio;
        [SerializeField] private PilotAwarenessAssist awarenessAssist;
        [SerializeField] private FlightHUDController flightHUD;
        [SerializeField] private SideViewTrainingAid pitchCoach;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private UIController debugUI;
        [SerializeField] private AssistedTrimController trim;
        [SerializeField] private FlightHoldAssistController holdAssist;
        [SerializeField] private NavigationManager navigation;
        [SerializeField] private NavigationUI navigationUI;
        [SerializeField] private WeatherManager weather;
        [SerializeField] private MouseYokeController mouseYoke;

        public float MasterVolume { get; private set; }
        public float EngineVolume { get; private set; }
        public float WindVolume { get; private set; }
        public float WarningVolume { get; private set; }
        public bool HUDEnabled { get; private set; }
        public bool FlightPathMarkerEnabled { get; private set; }
        public bool RunwayAlignmentEnabled { get; private set; }
        public bool AwarenessEnabled { get; private set; }
        public bool PitchCoachEnabled { get; private set; }
        public bool CameraRecenterEnabled { get; private set; }
        public float MotionCueStrength { get; private set; }
        public float MouseSensitivity { get; private set; }
        public bool CameraInvertY { get; private set; }
        public bool CameraShakeEnabled { get; private set; }
        public float CameraMotionIntensity { get; private set; }
        public bool ShowHUDInCinematicCameras { get; private set; }
        public CameraSettingsData CameraSettings => cameraSettings;
        public CameraController.CameraMode CameraMode =>
            cameraController == null ? cameraMode : cameraController.CurrentMode;
        public bool DebugUIEnabled { get; private set; }
        public bool TrimEnabled { get; private set; }
        public bool PitchHoldEnabled { get; private set; }
        public bool VerticalSpeedHoldEnabled { get; private set; }
        public float PitchHoldStrength { get; private set; }
        public float VerticalSpeedHoldStrength { get; private set; }
        public bool NavigationEnabled { get; private set; }
        public bool FlightPlannerEnabled { get; private set; }
        public bool NavigationHUDEnabled { get; private set; }
        public bool NavigationMapEnabled { get; private set; }
        public bool HeadingBugEnabled { get; private set; }
        public bool ApproachHelpersEnabled { get; private set; }
        public bool SideViewGlideEnabled { get; private set; }
        public bool TopDownLateralEnabled { get; private set; }
        public bool TrajectoryVisualizerEnabled { get; private set; }
        public ApproachGuidanceDisplayMode ApproachGuidanceMode { get; private set; }
        public float TrajectoryPredictionLength { get; private set; }
        public float TrajectoryVisualIntensity { get; private set; }
        public bool ShowProjectedTouchdownMarker { get; private set; }
        public bool ShowTrajectoryLabels { get; private set; }
        public bool AutoShowApproachEnabled { get; private set; }
        public bool VerticalProfileEnabled { get; private set; }
        public WeatherPreset WeatherPreset { get; private set; }
        public bool MouseYokeEnabled { get; private set; }
        public float MouseYokeSensitivity { get; private set; }
        public float MouseYokeDeadZone { get; private set; }
        public float MouseYokeSmoothing { get; private set; }
        public float MouseYokeMaximumInput { get; private set; }
        private CameraController.CameraMode cameraMode;
        private CameraSettingsData cameraSettings = new();

        private void Awake()
        {
            ResolveWeather();
            MasterVolume = PlayerPrefs.GetFloat("settings.masterVolume", 0.85f);
            EngineVolume = PlayerPrefs.GetFloat("settings.engineVolume", 0.8f);
            WindVolume = PlayerPrefs.GetFloat("settings.windVolume", 0.7f);
            WarningVolume =
                PlayerPrefs.GetFloat("settings.warningVolume", 0.85f);
            HUDEnabled = GetBool("settings.hud", true);
            FlightPathMarkerEnabled = GetBool("settings.flightPathMarker", false);
            RunwayAlignmentEnabled = GetBool("settings.runwayAlignment", true);
            AwarenessEnabled = GetBool("settings.awareness", true);
            PitchCoachEnabled = GetBool("settings.pitchCoach", true);
            LoadCameraSettings();
            MotionCueStrength =
                PlayerPrefs.GetFloat("settings.motionStrength", 1f);
            cameraMode = cameraSettings.RememberLastView
                ? (CameraController.CameraMode)Mathf.Clamp(
                    PlayerPrefs.GetInt("camera.mode", 0), 0, 4)
                : CameraController.CameraMode.Chase;
            DebugUIEnabled = GetBool("settings.debugUI", false);
            TrimEnabled = false;
            PitchHoldEnabled = GetBool("settings.pitchHold", true);
            VerticalSpeedHoldEnabled = GetBool("settings.vsHold", true);
            PitchHoldStrength =
                PlayerPrefs.GetFloat("settings.pitchHoldStrength", 0.08f);
            VerticalSpeedHoldStrength =
                PlayerPrefs.GetFloat("settings.vsHoldStrength", 0.18f);
            NavigationEnabled = GetBool("settings.navigation", true);
            FlightPlannerEnabled = GetBool("settings.flightPlanner", true);
            NavigationHUDEnabled = GetBool("settings.navigationHUD", true);
            NavigationMapEnabled = GetBool("settings.navigationMap", true);
            HeadingBugEnabled = GetBool("settings.headingBug", true);
            ApproachHelpersEnabled =
                GetBool("settings.approachHelpers", true);
            SideViewGlideEnabled =
                GetBool("settings.sideViewGlide", true);
            TopDownLateralEnabled =
                GetBool("settings.topDownLateral", true);
            TrajectoryVisualizerEnabled =
                GetBool("settings.trajectoryVisualizer", false);
            ApproachGuidanceMode = (ApproachGuidanceDisplayMode)Mathf.Clamp(
                PlayerPrefs.GetInt("settings.approachGuidanceMode", 1),
                0,
                System.Enum.GetValues(typeof(ApproachGuidanceDisplayMode)).Length - 1);
            if (ApproachGuidanceMode == ApproachGuidanceDisplayMode.ThreeDTrajectory ||
                ApproachGuidanceMode == ApproachGuidanceDisplayMode.Both)
                ApproachGuidanceMode = ApproachGuidanceDisplayMode.TwoDHelpers;
            TrajectoryPredictionLength =
                PlayerPrefs.GetFloat("settings.trajectoryPredictionLength", 12f);
            TrajectoryVisualIntensity =
                PlayerPrefs.GetFloat("settings.trajectoryVisualIntensity", 0.65f);
            ShowProjectedTouchdownMarker =
                GetBool("settings.showProjectedTouchdownMarker", true);
            ShowTrajectoryLabels =
                GetBool("settings.showTrajectoryLabels", true);
            AutoShowApproachEnabled =
                GetBool("settings.autoShowApproach", true);
            VerticalProfileEnabled =
                GetBool("settings.verticalProfile", true);
            WeatherPreset = (WeatherPreset)Mathf.Clamp(
                PlayerPrefs.GetInt("settings.weatherPreset", (int)WeatherPreset.Clear),
                0,
                System.Enum.GetValues(typeof(WeatherPreset)).Length - 1);
            MouseYokeEnabled = GetBool("settings.mouseYoke", false);
            MouseYokeSensitivity =
                PlayerPrefs.GetFloat("settings.mouseYokeSensitivity", 1f);
            MouseYokeDeadZone =
                PlayerPrefs.GetFloat("settings.mouseYokeDeadZone", 0.04f);
            MouseYokeSmoothing =
                PlayerPrefs.GetFloat("settings.mouseYokeSmoothing", 8f);
            MouseYokeMaximumInput =
                PlayerPrefs.GetFloat("settings.mouseYokeMaximumInput", 0.85f);
            ApplyAll();
        }

        private void ResolveWeather()
        {
            if (weather == null) weather = FindAnyObjectByType<WeatherManager>();
            if (weather != null) return;
            GameObject weatherObject = new("Weather System");
            weather = weatherObject.AddComponent<WeatherManager>();
        }

        private void ResolveMouseYoke()
        {
            if (mouseYoke == null)
                mouseYoke = FindAnyObjectByType<MouseYokeController>();
            if (mouseYoke != null) return;
            AircraftInput aircraftInput = FindAnyObjectByType<AircraftInput>();
            if (aircraftInput != null)
                mouseYoke = aircraftInput.gameObject.AddComponent<MouseYokeController>();
        }

        public void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            AudioListener.volume = MasterVolume;
            SaveFloat("settings.masterVolume", MasterVolume);
        }

        public void SetEngineVolume(float value)
        {
            EngineVolume = Mathf.Clamp01(value);
            aircraftAudio?.SetEngineVolumeMultiplier(EngineVolume);
            SaveFloat("settings.engineVolume", EngineVolume);
        }

        public void SetWindVolume(float value)
        {
            WindVolume = Mathf.Clamp01(value);
            aircraftAudio?.SetWindVolumeMultiplier(WindVolume);
            SaveFloat("settings.windVolume", WindVolume);
        }

        public void SetWarningVolume(float value)
        {
            WarningVolume = Mathf.Clamp01(value);
            aircraftAudio?.SetWarningVolumeMultiplier(WarningVolume);
            SaveFloat("settings.warningVolume", WarningVolume);
        }

        public void SetHUDEnabled(bool value)
        {
            HUDEnabled = value;
            flightHUD?.SetHUDVisible(value);
            SaveBool("settings.hud", value);
        }

        public void SetFlightPathMarkerEnabled(bool value)
        {
            FlightPathMarkerEnabled = value;
            flightHUD?.SetFlightPathMarkerEnabled(value);
            SaveBool("settings.flightPathMarker", value);
        }

        public void SetRunwayAlignmentEnabled(bool value)
        {
            RunwayAlignmentEnabled = value;
            flightHUD?.SetRunwayAlignmentEnabled(value);
            SaveBool("settings.runwayAlignment", value);
        }

        public void SetAwarenessEnabled(bool value)
        {
            AwarenessEnabled = value;
            awarenessAssist?.SetAssistEnabled(value);
            SaveBool("settings.awareness", value);
        }

        public void SetPitchCoachEnabled(bool value)
        {
            PitchCoachEnabled = value;
            pitchCoach?.SetSettingEnabled(value);
            SaveBool("settings.pitchCoach", value);
        }

        public void SetCameraRecenterEnabled(bool value)
        {
            cameraSettings.AutoRecenter = value;
            ApplyAndSaveCameraSettings();
        }

        public void SetMotionCueStrength(float value)
        {
            MotionCueStrength = Mathf.Clamp(value, 0f, 2f);
            awarenessAssist?.SetMotionCueStrength(MotionCueStrength);
            SaveFloat("settings.motionStrength", MotionCueStrength);
        }

        public void SetMouseSensitivity(float value)
        {
            cameraSettings.ExternalSensitivity = Mathf.Clamp(value, 0.25f, 2f);
            ApplyAndSaveCameraSettings();
        }

        public void SetCameraInvertY(bool value)
        {
            cameraSettings.InvertExternalY = value;
            ApplyAndSaveCameraSettings();
        }

        private void LoadCameraSettings()
        {
            cameraSettings.ExternalFov = LoadCameraFloat("externalFov", 60f, 45f, 85f);
            cameraSettings.CockpitFov = LoadCameraFloat("cockpitFov", 70f, 55f, 95f);
            cameraSettings.CinematicFov = LoadCameraFloat("cinematicFov", 50f, 35f, 80f);
            cameraSettings.ExternalSensitivity = LoadCameraFloat("externalSensitivity", 1f, 0.25f, 2f);
            cameraSettings.CockpitSensitivity = LoadCameraFloat("cockpitSensitivity", 1f, 0.25f, 2f);
            cameraSettings.CockpitPositionX = LoadCameraFloat("cockpitPositionX", 0f, -0.5f, 0.5f);
            cameraSettings.CockpitPositionY = LoadCameraFloat("cockpitPositionY", 0f, -0.4f, 0.4f);
            cameraSettings.CockpitPositionZ = LoadCameraFloat("cockpitPositionZ", 0f, -0.5f, 0.5f);
            cameraSettings.MotionIntensity = LoadCameraFloat("motionIntensity", 0.6f, 0f, 1f);
            cameraSettings.ShakeEnabled = GetBool("camera.shake", true);
            cameraSettings.AutoRecenter = GetBool("camera.autoRecenter", true);
            cameraSettings.RecenterDelay = LoadCameraFloat("recenterDelay", 1.5f, 0f, 5f);
            cameraSettings.RecenterSpeed = LoadCameraFloat("recenterSpeed", 2.5f, 0.25f, 8f);
            cameraSettings.InvertExternalY = GetBool("camera.invertExternalY", false);
            cameraSettings.InvertCockpitY = GetBool("camera.invertCockpitY", false);
            cameraSettings.ShowCinematicHud = GetBool("camera.cinematicHud", false);
            cameraSettings.CinematicTransitions = GetBool("camera.cinematicTransitions", true);
            cameraSettings.RememberLastView = GetBool("camera.rememberLastView", true);
            cameraSettings.IncludeWingViews = GetBool("camera.includeWingViews", true);
            cameraSettings.IncludeCloseChase = GetBool("camera.includeCloseChase", true);
            SyncLegacyCameraProperties();
        }

        private static float LoadCameraFloat(
            string key, float fallback, float minimum, float maximum)
        {
            float value = PlayerPrefs.GetFloat("camera." + key, fallback);
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Mathf.Clamp(value, minimum, maximum);
        }

        private void SyncLegacyCameraProperties()
        {
            CameraRecenterEnabled = cameraSettings.AutoRecenter;
            MouseSensitivity = cameraSettings.ExternalSensitivity;
            CameraInvertY = cameraSettings.InvertExternalY;
            CameraShakeEnabled = cameraSettings.ShakeEnabled;
            CameraMotionIntensity = cameraSettings.MotionIntensity;
            ShowHUDInCinematicCameras = cameraSettings.ShowCinematicHud;
        }

        private void SaveCameraSettings()
        {
            PlayerPrefs.SetFloat("camera.externalFov", cameraSettings.ExternalFov);
            PlayerPrefs.SetFloat("camera.cockpitFov", cameraSettings.CockpitFov);
            PlayerPrefs.SetFloat("camera.cinematicFov", cameraSettings.CinematicFov);
            PlayerPrefs.SetFloat("camera.externalSensitivity", cameraSettings.ExternalSensitivity);
            PlayerPrefs.SetFloat("camera.cockpitSensitivity", cameraSettings.CockpitSensitivity);
            PlayerPrefs.SetFloat("camera.cockpitPositionX", cameraSettings.CockpitPositionX);
            PlayerPrefs.SetFloat("camera.cockpitPositionY", cameraSettings.CockpitPositionY);
            PlayerPrefs.SetFloat("camera.cockpitPositionZ", cameraSettings.CockpitPositionZ);
            PlayerPrefs.SetFloat("camera.motionIntensity", cameraSettings.MotionIntensity);
            PlayerPrefs.SetInt("camera.shake", cameraSettings.ShakeEnabled ? 1 : 0);
            PlayerPrefs.SetInt("camera.autoRecenter", cameraSettings.AutoRecenter ? 1 : 0);
            PlayerPrefs.SetFloat("camera.recenterDelay", cameraSettings.RecenterDelay);
            PlayerPrefs.SetFloat("camera.recenterSpeed", cameraSettings.RecenterSpeed);
            PlayerPrefs.SetInt("camera.invertExternalY", cameraSettings.InvertExternalY ? 1 : 0);
            PlayerPrefs.SetInt("camera.invertCockpitY", cameraSettings.InvertCockpitY ? 1 : 0);
            PlayerPrefs.SetInt("camera.cinematicHud", cameraSettings.ShowCinematicHud ? 1 : 0);
            PlayerPrefs.SetInt("camera.cinematicTransitions", cameraSettings.CinematicTransitions ? 1 : 0);
            PlayerPrefs.SetInt("camera.rememberLastView", cameraSettings.RememberLastView ? 1 : 0);
            PlayerPrefs.SetInt("camera.includeWingViews", cameraSettings.IncludeWingViews ? 1 : 0);
            PlayerPrefs.SetInt("camera.includeCloseChase", cameraSettings.IncludeCloseChase ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetCameraShakeEnabled(bool value)
        {
            cameraSettings.ShakeEnabled = value;
            ApplyAndSaveCameraSettings();
        }

        public void SetCameraMotionIntensity(float value)
        {
            cameraSettings.MotionIntensity = Mathf.Clamp01(value);
            ApplyAndSaveCameraSettings();
        }

        public void SetShowHUDInCinematicCameras(bool value)
        {
            cameraSettings.ShowCinematicHud = value;
            ApplyAndSaveCameraSettings();
        }

        public void SetExternalCameraFov(float value) { cameraSettings.ExternalFov = Mathf.Clamp(value, 45f, 85f); ApplyAndSaveCameraSettings(); }
        public void SetCockpitCameraFov(float value) { cameraSettings.CockpitFov = Mathf.Clamp(value, 55f, 95f); ApplyAndSaveCameraSettings(); }
        public void SetCinematicCameraFov(float value) { cameraSettings.CinematicFov = Mathf.Clamp(value, 35f, 80f); ApplyAndSaveCameraSettings(); }
        public void SetCockpitLookSensitivity(float value) { cameraSettings.CockpitSensitivity = Mathf.Clamp(value, 0.25f, 2f); ApplyAndSaveCameraSettings(); }
        public void SetCockpitPositionX(float value) { cameraSettings.CockpitPositionX = Mathf.Clamp(value, -0.5f, 0.5f); ApplyAndSaveCameraSettings(); }
        public void SetCockpitPositionY(float value) { cameraSettings.CockpitPositionY = Mathf.Clamp(value, -0.4f, 0.4f); ApplyAndSaveCameraSettings(); }
        public void SetCockpitPositionZ(float value) { cameraSettings.CockpitPositionZ = Mathf.Clamp(value, -0.5f, 0.5f); ApplyAndSaveCameraSettings(); }
        public void ResetCockpitPosition()
        {
            cameraSettings.CockpitPositionX = 0f;
            cameraSettings.CockpitPositionY = 0f;
            cameraSettings.CockpitPositionZ = 0f;
            ApplyAndSaveCameraSettings();
        }
        public void SetRecenterDelay(float value) { cameraSettings.RecenterDelay = Mathf.Clamp(value, 0f, 5f); ApplyAndSaveCameraSettings(); }
        public void SetRecenterSpeed(float value) { cameraSettings.RecenterSpeed = Mathf.Clamp(value, 0.25f, 8f); ApplyAndSaveCameraSettings(); }
        public void SetInvertCockpitY(bool value) { cameraSettings.InvertCockpitY = value; ApplyAndSaveCameraSettings(); }
        public void SetCinematicTransitions(bool value) { cameraSettings.CinematicTransitions = value; ApplyAndSaveCameraSettings(); }
        public void SetRememberLastCameraView(bool value) { cameraSettings.RememberLastView = value; ApplyAndSaveCameraSettings(); }
        public void SetIncludeWingViews(bool value) { cameraSettings.IncludeWingViews = value; ApplyAndSaveCameraSettings(); }
        public void SetIncludeCloseChase(bool value) { cameraSettings.IncludeCloseChase = value; ApplyAndSaveCameraSettings(); }

        public void ResetCameraSettingsToDefaults()
        {
            cameraSettings = new CameraSettingsData();
            cameraMode = CameraController.CameraMode.Chase;
            ApplyAndSaveCameraSettings();
            PlayerPrefs.SetInt("camera.mode", 0);
            PlayerPrefs.Save();
            cameraController?.SetCameraMode(cameraMode);
        }

        public void NotifyCameraModeSelected(CameraController.CameraMode value)
        {
            cameraMode = value;
            if (!cameraSettings.RememberLastView) return;
            PlayerPrefs.SetInt("camera.mode", (int)value);
            PlayerPrefs.Save();
        }

        private void ApplyAndSaveCameraSettings()
        {
            SyncLegacyCameraProperties();
            cameraController?.ApplyPlayerSettings(cameraSettings);
            SaveCameraSettings();
        }

        public void SetCameraMode(CameraController.CameraMode value)
        {
            cameraMode = value;
            cameraController?.SetCameraMode(value);
            NotifyCameraModeSelected(value);
        }

        public void SetDebugUIEnabled(bool value)
        {
            DebugUIEnabled = value;
            debugUI?.SetDebugVisibility(value);
            SaveBool("settings.debugUI", value);
        }

        public void SetPitchHoldEnabled(bool value)
        {
            PitchHoldEnabled = value;
            holdAssist?.SetPitchHoldEnabled(value);
            SaveBool("settings.pitchHold", value);
        }

        public void SetVerticalSpeedHoldEnabled(bool value)
        {
            VerticalSpeedHoldEnabled = value;
            holdAssist?.SetVerticalSpeedHoldEnabled(value);
            SaveBool("settings.vsHold", value);
        }

        public void SetPitchHoldStrength(float value)
        {
            PitchHoldStrength = Mathf.Clamp(value, 0f, 0.2f);
            holdAssist?.SetPitchHoldStrength(PitchHoldStrength);
            SaveFloat("settings.pitchHoldStrength", PitchHoldStrength);
        }

        public void SetVerticalSpeedHoldStrength(float value)
        {
            VerticalSpeedHoldStrength = Mathf.Clamp(value, 0f, 0.5f);
            holdAssist?.SetVerticalSpeedHoldStrength(VerticalSpeedHoldStrength);
            SaveFloat("settings.vsHoldStrength", VerticalSpeedHoldStrength);
        }

        public void SetNavigationEnabled(bool value)
        {
            NavigationEnabled = value;
            navigation?.SetNavigationEnabled(value);
            SaveBool("settings.navigation", value);
        }

        public void SetFlightPlannerEnabled(bool value)
        {
            FlightPlannerEnabled = value;
            SaveBool("settings.flightPlanner", value);
        }

        public void SetNavigationHUDEnabled(bool value)
        {
            NavigationHUDEnabled = value;
            ApplyNavigationUI();
            SaveBool("settings.navigationHUD", value);
        }

        public void SetNavigationMapEnabled(bool value)
        {
            NavigationMapEnabled = value;
            ApplyNavigationUI();
            SaveBool("settings.navigationMap", value);
        }

        public void SetHeadingBugEnabled(bool value)
        {
            HeadingBugEnabled = value;
            ApplyNavigationUI();
            SaveBool("settings.headingBug", value);
        }

        public void SetApproachHelpersEnabled(bool value)
        {
            ApproachHelpersEnabled = value;
            ApplyNavigationUI();
            SaveBool("settings.approachHelpers", value);
        }

        public void SetSideViewGlideEnabled(bool value)
        {
            SideViewGlideEnabled = value;
            ApplyNavigationUI();
            SaveBool("settings.sideViewGlide", value);
        }

        public void SetTopDownLateralEnabled(bool value)
        {
            TopDownLateralEnabled = value;
            ApplyNavigationUI();
            SaveBool("settings.topDownLateral", value);
        }

        public void SetTrajectoryVisualizerEnabled(bool value)
        {
            TrajectoryVisualizerEnabled = false;
            ApplyNavigationUI();
            SaveBool("settings.trajectoryVisualizer", false);
        }

        public void SetApproachGuidanceMode(ApproachGuidanceDisplayMode value)
        {
            if (value == ApproachGuidanceDisplayMode.ThreeDTrajectory ||
                value == ApproachGuidanceDisplayMode.Both)
                value = ApproachGuidanceDisplayMode.TwoDHelpers;
            ApproachGuidanceMode = value;
            ApplyNavigationUI();
            PlayerPrefs.SetInt("settings.approachGuidanceMode", (int)value);
            PlayerPrefs.Save();
        }

        public void SetTrajectoryPredictionLength(float value)
        {
            TrajectoryPredictionLength = Mathf.Clamp(value, 4f, 25f);
            ApplyNavigationUI();
            SaveFloat(
                "settings.trajectoryPredictionLength",
                TrajectoryPredictionLength);
        }

        public void SetTrajectoryVisualIntensity(float value)
        {
            TrajectoryVisualIntensity = Mathf.Clamp01(value);
            ApplyNavigationUI();
            SaveFloat(
                "settings.trajectoryVisualIntensity",
                TrajectoryVisualIntensity);
        }

        public void SetProjectedTouchdownMarkerEnabled(bool value)
        {
            ShowProjectedTouchdownMarker = value;
            ApplyNavigationUI();
            SaveBool("settings.showProjectedTouchdownMarker", value);
        }

        public void SetTrajectoryLabelsEnabled(bool value)
        {
            ShowTrajectoryLabels = value;
            ApplyNavigationUI();
            SaveBool("settings.showTrajectoryLabels", value);
        }

        public void SetAutoShowApproachEnabled(bool value)
        {
            AutoShowApproachEnabled = value;
            ApplyNavigationUI();
            SaveBool("settings.autoShowApproach", value);
        }

        public void SetVerticalProfileEnabled(bool value)
        {
            VerticalProfileEnabled = value;
            navigationUI?.SetVerticalProfileEnabled(value);
            SaveBool("settings.verticalProfile", value);
        }

        public void SetWeatherPreset(WeatherPreset value)
        {
            WeatherPreset = value;
            weather?.SetPreset(value);
            PlayerPrefs.SetInt("settings.weatherPreset", (int)value);
            PlayerPrefs.Save();
        }

        public void SetMouseYokeEnabled(bool value)
        {
            MouseYokeEnabled = value;
            ResolveMouseYoke();
            mouseYoke?.SetEnabled(value);
            SaveBool("settings.mouseYoke", value);
        }

        public void SetMouseYokeSensitivity(float value)
        {
            MouseYokeSensitivity = Mathf.Clamp(value, 0.2f, 3f);
            mouseYoke?.SetSensitivity(MouseYokeSensitivity);
            SaveFloat("settings.mouseYokeSensitivity", MouseYokeSensitivity);
        }

        public void SetMouseYokeDeadZone(float value)
        {
            MouseYokeDeadZone = Mathf.Clamp(value, 0f, 0.25f);
            mouseYoke?.SetDeadZone(MouseYokeDeadZone);
            SaveFloat("settings.mouseYokeDeadZone", MouseYokeDeadZone);
        }

        public void SetMouseYokeSmoothing(float value)
        {
            MouseYokeSmoothing = Mathf.Clamp(value, 1f, 24f);
            mouseYoke?.SetSmoothing(MouseYokeSmoothing);
            SaveFloat("settings.mouseYokeSmoothing", MouseYokeSmoothing);
        }

        public void SetMouseYokeMaximumInput(float value)
        {
            MouseYokeMaximumInput = Mathf.Clamp(value, 0.25f, 1f);
            mouseYoke?.SetMaximumInput(MouseYokeMaximumInput);
            SaveFloat("settings.mouseYokeMaximumInput", MouseYokeMaximumInput);
        }

        public void ApplyAll()
        {
            ResolveMouseYoke();
            AudioListener.volume = MasterVolume;
            aircraftAudio?.SetEngineVolumeMultiplier(EngineVolume);
            aircraftAudio?.SetWindVolumeMultiplier(WindVolume);
            aircraftAudio?.SetWarningVolumeMultiplier(WarningVolume);
            flightHUD?.SetHUDVisible(HUDEnabled);
            flightHUD?.SetFlightPathMarkerEnabled(FlightPathMarkerEnabled);
            flightHUD?.SetRunwayAlignmentEnabled(RunwayAlignmentEnabled);
            awarenessAssist?.SetAssistEnabled(AwarenessEnabled);
            pitchCoach?.SetSettingEnabled(PitchCoachEnabled);
            awarenessAssist?.SetMotionCueStrength(MotionCueStrength);
            cameraController?.ApplyPlayerSettings(cameraSettings);
            cameraController?.SetCameraMode(cameraMode);
            debugUI?.SetDebugVisibility(DebugUIEnabled);
            trim?.SetTrimEnabled(false);
            holdAssist?.SetPitchHoldEnabled(PitchHoldEnabled);
            holdAssist?.SetVerticalSpeedHoldEnabled(VerticalSpeedHoldEnabled);
            holdAssist?.SetPitchHoldStrength(PitchHoldStrength);
            holdAssist?.SetVerticalSpeedHoldStrength(VerticalSpeedHoldStrength);
            navigation?.SetNavigationEnabled(NavigationEnabled);
            ApplyNavigationUI();
            navigationUI?.SetVerticalProfileEnabled(
                VerticalProfileEnabled);
            weather?.SetPreset(WeatherPreset, true);
            mouseYoke?.SetSensitivity(MouseYokeSensitivity);
            mouseYoke?.SetDeadZone(MouseYokeDeadZone);
            mouseYoke?.SetSmoothing(MouseYokeSmoothing);
            mouseYoke?.SetMaximumInput(MouseYokeMaximumInput);
            mouseYoke?.SetEnabled(MouseYokeEnabled);
        }

        private void ApplyNavigationUI()
        {
            navigationUI?.ApplySettings(
                NavigationHUDEnabled,
                NavigationMapEnabled,
                HeadingBugEnabled,
                ApproachHelpersEnabled,
                SideViewGlideEnabled,
                TopDownLateralEnabled,
                TrajectoryVisualizerEnabled,
                ApproachGuidanceMode,
                AutoShowApproachEnabled);
            navigationUI?.SetTrajectorySettings(
                TrajectoryVisualizerEnabled,
                TrajectoryPredictionLength,
                TrajectoryVisualIntensity,
                ShowProjectedTouchdownMarker,
                ShowTrajectoryLabels);
        }

        private static bool GetBool(string key, bool fallback) =>
            PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;

        private static void SaveBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static void SaveFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }
    }
}
