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
        public bool DebugUIEnabled { get; private set; }

        private void Awake()
        {
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
            DebugUIEnabled = GetBool("settings.debugUI", false);
            ApplyAll();
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

        public void SetDebugUIEnabled(bool value)
        {
            DebugUIEnabled = value;
            debugUI?.SetDebugVisibility(value);
            SaveBool("settings.debugUI", value);
        }

        public void ApplyAll()
        {
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
            debugUI?.SetDebugVisibility(DebugUIEnabled);
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
