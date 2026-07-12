using UnityEngine;

namespace SimplyFly.GroundOps
{
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
        public bool AwarenessEnabled { get; private set; }
        public bool PitchCoachEnabled { get; private set; }
        public bool CameraRecenterEnabled { get; private set; }
        public float MotionCueStrength { get; private set; }
        public float MouseSensitivity { get; private set; }
        public bool CameraInvertY { get; private set; }
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

        private void Awake()
        {
            ResolveWeather();
            MasterVolume = PlayerPrefs.GetFloat("settings.masterVolume", 0.85f);
            EngineVolume = PlayerPrefs.GetFloat("settings.engineVolume", 0.8f);
            WindVolume = PlayerPrefs.GetFloat("settings.windVolume", 0.7f);
            WarningVolume =
                PlayerPrefs.GetFloat("settings.warningVolume", 0.85f);
            HUDEnabled = GetBool("settings.hud", true);
            AwarenessEnabled = GetBool("settings.awareness", true);
            PitchCoachEnabled = GetBool("settings.pitchCoach", true);
            CameraRecenterEnabled = GetBool("settings.cameraRecenter", true);
            MotionCueStrength =
                PlayerPrefs.GetFloat("settings.motionStrength", 1f);
            MouseSensitivity =
                PlayerPrefs.GetFloat("settings.mouseSensitivity", 0.18f);
            CameraInvertY = GetBool("settings.cameraInvertY", false);
            cameraMode = (CameraController.CameraMode)Mathf.Clamp(
                PlayerPrefs.GetInt("settings.cameraMode", 0),
                0,
                System.Enum.GetValues(typeof(CameraController.CameraMode)).Length - 1);
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
            CameraRecenterEnabled = value;
            cameraController?.SetAutoRecenterEnabled(value);
            SaveBool("settings.cameraRecenter", value);
        }

        public void SetMotionCueStrength(float value)
        {
            MotionCueStrength = Mathf.Clamp(value, 0f, 2f);
            awarenessAssist?.SetMotionCueStrength(MotionCueStrength);
            SaveFloat("settings.motionStrength", MotionCueStrength);
        }

        public void SetMouseSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, 0.02f, 1f);
            cameraController?.SetMouseSensitivity(MouseSensitivity);
            SaveFloat("settings.mouseSensitivity", MouseSensitivity);
        }

        public void SetCameraInvertY(bool value)
        {
            CameraInvertY = value;
            cameraController?.SetInvertY(value);
            SaveBool("settings.cameraInvertY", value);
        }

        public void SetCameraMode(CameraController.CameraMode value)
        {
            cameraMode = value;
            cameraController?.SetCameraMode(value);
            PlayerPrefs.SetInt("settings.cameraMode", (int)value);
            PlayerPrefs.Save();
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
            awarenessAssist?.SetAssistEnabled(AwarenessEnabled);
            pitchCoach?.SetSettingEnabled(PitchCoachEnabled);
            cameraController?.SetAutoRecenterEnabled(CameraRecenterEnabled);
            awarenessAssist?.SetMotionCueStrength(MotionCueStrength);
            cameraController?.SetMouseSensitivity(MouseSensitivity);
            cameraController?.SetInvertY(CameraInvertY);
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
