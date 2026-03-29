using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;

namespace BlackHorizon.HorizonGUI.Integrations.Weather
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_WeatherModule : UdonSharpBehaviour
    {
        [Header("Integration")]
        public GameObject weatherSystemObj;
        public UdonBehaviour _wtsUdon;

        [Header("Direct Bindings")]
        public GameObject Weather_View;
        public TextMeshProUGUI Weather_StatusText;
        public TextMeshProUGUI Weather_VersionText;
        public Toggle Weather_RealTimeToggle;
        public Slider Weather_TimeSlider;

        [HideInInspector]
        public string cachedVersion;

        private bool _isConnected = false;

        private const int TIME_SYNC = 0;
        private const int TIME_SIMULATED = 1;
        private const int TIME_STATIC = 2;

        // =============================================================
        // LIFECYCLE
        // =============================================================

        public void OnHorizonBuild()
        {
            CheckConnection();
            if (Weather_VersionText != null && !string.IsNullOrEmpty(cachedVersion))
                Weather_VersionText.text = cachedVersion;
        }

        private void Start()
        {
            CheckConnection();
            UpdateStatusVisuals();
            SyncUI();
        }

        public void OnShow()
        {
            if (Weather_View != null) Weather_View.SetActive(true);
            CheckConnection();
            UpdateStatusVisuals();
            SyncUI();
        }

        public void OnHide()
        {
            if (Weather_View != null) Weather_View.SetActive(false);
        }

        // =============================================================
        // CONNECTION
        // =============================================================

        private void CheckConnection()
        {
            _isConnected = (_wtsUdon != null);
        }

        // =============================================================
        // STATUS
        // =============================================================

        public void UpdateStatusVisuals()
        {
            if (Weather_StatusText != null)
            {
                Weather_StatusText.text = _isConnected
                    ? "Status: <color=#33FF33>Connected</color>"
                    : "Status: <color=#FF3333>Not Found</color>";
            }
        }

        // =============================================================
        // SYNC UI FROM SYSTEM STATE
        // =============================================================

        private void SyncUI()
        {
            if (_wtsUdon == null) return;

            int mode = (int)_wtsUdon.GetProgramVariable("timeMode");
            bool isRealTime = (mode == TIME_SYNC);

            if (Weather_RealTimeToggle != null)
                Weather_RealTimeToggle.SetIsOnWithoutNotify(isRealTime);

            if (Weather_TimeSlider != null)
            {
                float t = (float)_wtsUdon.GetProgramVariable("_sunTimeOfDay");
                Weather_TimeSlider.SetValueWithoutNotify(t);
                Weather_TimeSlider.interactable = !isRealTime;
            }
        }

        // =============================================================
        // UPDATE
        // =============================================================

        private void Update()
        {
            if (_wtsUdon == null || Weather_TimeSlider == null) return;

            int mode = (int)_wtsUdon.GetProgramVariable("timeMode");
            if (mode == TIME_SYNC || mode == TIME_SIMULATED)
            {
                float t = (float)_wtsUdon.GetProgramVariable("_sunTimeOfDay");
                Weather_TimeSlider.SetValueWithoutNotify(t);
            }
        }

        // =============================================================
        // UI CALLBACKS
        // =============================================================

        public void OnRealTimeChanged()
        {
            if (_wtsUdon == null || Weather_RealTimeToggle == null) return;

            bool isRealTime = Weather_RealTimeToggle.isOn;

            _wtsUdon.SetProgramVariable("timeMode",
                isRealTime ? TIME_SYNC : TIME_STATIC);

            if (Weather_TimeSlider != null)
                Weather_TimeSlider.interactable = !isRealTime;

            if (isRealTime)
                _wtsUdon.SendCustomEvent("ReleaseExternalControl");
            else
                _wtsUdon.SendCustomEvent("Refresh");
        }

        public void OnTimeSliderChanged()
        {
            if (_wtsUdon == null || Weather_TimeSlider == null) return;

            int mode = (int)_wtsUdon.GetProgramVariable("timeMode");
            if (mode == TIME_SYNC) return;

            _wtsUdon.SetProgramVariable("_sunTimeOfDay",
                Weather_TimeSlider.value);
            _wtsUdon.SendCustomEvent("ForceVisualUpdate");
        }

        // =============================================================
        // WEATHER PROFILES
        // =============================================================

        public void OnProfileClear() { SetWeather(0); }
        public void OnProfileSnow() { SetWeather(1); }
        public void OnProfileRain() { SetWeather(2); }

        private void SetWeather(int index)
        {
            if (_wtsUdon == null) return;

            _wtsUdon.SetProgramVariable("_pendingProfileIndex", index);
            _wtsUdon.SendCustomEvent("ApplyPendingProfile");
        }
    }
}