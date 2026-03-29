using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if HORIZON_WEATHER_INTEGRATION
using BlackHorizon.HorizonWeatherTime;
#endif

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

        public void OnHorizonBuild()
        {
            TryConnectSystem();

#if HORIZON_WEATHER_INTEGRATION
            if (Weather_VersionText != null && !string.IsNullOrEmpty(cachedVersion))
            {
                Weather_VersionText.text = cachedVersion;
            }
#endif
        }

        private void Start()
        {
            TryConnectSystem();
            UpdateStatusVisuals();
            SyncUI();
        }

        public void OnShow()
        {
            if (Weather_View != null) Weather_View.SetActive(true);
            TryConnectSystem();
            UpdateStatusVisuals();
            SyncUI();
        }

        public void OnHide()
        {
            if (Weather_View != null) Weather_View.SetActive(false);
        }

        public void TryConnectSystem()
        {
            if (_wtsUdon != null) return;

#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystemObj == null)
            {
                WeatherTimeSystem wts = (WeatherTimeSystem)Object.FindObjectOfType(typeof(WeatherTimeSystem));
                if (wts != null) weatherSystemObj = wts.gameObject;
            }

            if (weatherSystemObj != null)
            {
                _wtsUdon = (UdonBehaviour)weatherSystemObj.GetComponent(typeof(UdonBehaviour));
            }
#endif
        }

        private void Update()
        {
            if (_wtsUdon == null) return;

#if HORIZON_WEATHER_INTEGRATION
            int timeMode = (int)_wtsUdon.GetProgramVariable("timeMode");
            
            if (Weather_TimeSlider != null && timeMode == 0)
            {
                float time = (float)_wtsUdon.GetProgramVariable("_sunTimeOfDay");
                Weather_TimeSlider.SetValueWithoutNotify(time);
            }
#endif
        }

        public void UpdateStatusVisuals()
        {
            if (Weather_StatusText != null)
            {
                Weather_StatusText.text = (_wtsUdon != null)
                    ? "Status: <color=#33FF33>Connected</color>"
                    : "Status: <color=#FF3333>Not Found</color>";
            }
        }

        private void SyncUI()
        {
            if (_wtsUdon == null) return;

#if HORIZON_WEATHER_INTEGRATION
            int timeMode = (int)_wtsUdon.GetProgramVariable("timeMode");
            bool isRealTime = (timeMode == 0);
            
            if (Weather_RealTimeToggle != null) Weather_RealTimeToggle.isOn = isRealTime;
            
            if (Weather_TimeSlider != null)
            {
                float time = (float)_wtsUdon.GetProgramVariable("_sunTimeOfDay");
                Weather_TimeSlider.SetValueWithoutNotify(time);
                Weather_TimeSlider.interactable = !isRealTime;
            }
#endif
        }

        public void OnRealTimeChanged()
        {
            if (_wtsUdon == null || Weather_RealTimeToggle == null) return;

#if HORIZON_WEATHER_INTEGRATION
            bool isRealTime = Weather_RealTimeToggle.isOn;
            _wtsUdon.SetProgramVariable("timeMode", isRealTime ? 0 : 2);
            
            if (Weather_TimeSlider != null)
            {
                Weather_TimeSlider.interactable = !isRealTime;
            }

            if (isRealTime) _wtsUdon.SendCustomEvent("ReleaseExternalControl");
            else _wtsUdon.SendCustomEvent("Refresh");
#endif
        }

        public void OnTimeSliderChanged()
        {
            if (_wtsUdon == null || Weather_TimeSlider == null) return;

#if HORIZON_WEATHER_INTEGRATION
            int timeMode = (int)_wtsUdon.GetProgramVariable("timeMode");
            if (timeMode == 0) return;

            _wtsUdon.SetProgramVariable("_sunTimeOfDay", Weather_TimeSlider.value);
            _wtsUdon.SendCustomEvent("ForceVisualUpdate");
#endif
        }

        public void OnProfileClear() => SetWeather(0);
        public void OnProfileSnow() => SetWeather(1);
        public void OnProfileRain() => SetWeather(2);

        private void SetWeather(int index)
        {
            if (_wtsUdon != null)
            {
#if HORIZON_WEATHER_INTEGRATION
                _wtsUdon.SetProgramVariable("_currentProfileIndex", index);
                _wtsUdon.SendCustomEvent("SetWeatherProfile");
                _wtsUdon.SendCustomEvent("Refresh");
#endif
            }
        }
    }
}