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
#if HORIZON_WEATHER_INTEGRATION
        [Header("Integration")]
        public WeatherTimeSystem weatherSystem;
#endif

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
            UpdateStatusVisuals();
        }

        public void OnHide()
        {
            if (Weather_View != null) Weather_View.SetActive(false);
        }

        public void TryConnectSystem()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystem != null) return;
            GameObject sysObj = GameObject.Find("WeatherTimeSystem");
            if (sysObj != null) weatherSystem = sysObj.GetComponent<WeatherTimeSystem>();
#endif
        }

        private void Update()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystem != null && Weather_TimeSlider != null && weatherSystem.timeMode == TimeMode.SyncWithSystemClock)
            {
                Weather_TimeSlider.SetValueWithoutNotify(weatherSystem._sunTimeOfDay);
            }
#endif
        }

        public void UpdateStatusVisuals()
        {
            if (Weather_StatusText != null)
            {
#if HORIZON_WEATHER_INTEGRATION
                Weather_StatusText.text = (weatherSystem != null)
                    ? "Status: <color=#33FF33>Connected</color>"
                    : "Status: <color=#FF3333>Not Found</color>";
#else
                Weather_StatusText.text = "Status: <color=#FF3333>Module Missing</color>";
#endif
            }
        }

        private void SyncUI()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystem == null) return;
            
            bool isRealTime = weatherSystem.timeMode == TimeMode.SyncWithSystemClock;
            
            if (Weather_RealTimeToggle != null) Weather_RealTimeToggle.isOn = isRealTime;
            if (Weather_TimeSlider != null)
            {
                Weather_TimeSlider.SetValueWithoutNotify(weatherSystem._sunTimeOfDay);
                Weather_TimeSlider.interactable = !isRealTime;
            }
#endif
        }

        public void OnRealTimeChanged()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystem == null || Weather_RealTimeToggle == null) return;
            
            bool isRealTime = Weather_RealTimeToggle.isOn;
            
            weatherSystem.timeMode = isRealTime ? TimeMode.SyncWithSystemClock : TimeMode.StaticManual;
            
            if (Weather_TimeSlider != null)
            {
                Weather_TimeSlider.interactable = !isRealTime;
                if (isRealTime) Weather_TimeSlider.SetValueWithoutNotify(weatherSystem._sunTimeOfDay);
            }
            if (isRealTime) weatherSystem.ReleaseExternalControl();
            UpdateStatusVisuals();
#endif
        }

        public void OnTimeSliderChanged()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystem == null || Weather_TimeSlider == null || weatherSystem.timeMode == TimeMode.SyncWithSystemClock) return;
            weatherSystem.SetExternalTime(Weather_TimeSlider.value);
#endif
        }

        public void OnProfileClear() => SetWeather(0);
        public void OnProfileSnow() => SetWeather(1);
        public void OnProfileRain() => SetWeather(2);

        private void SetWeather(int index)
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystem != null) weatherSystem.SetWeatherProfile(index); 
#endif
        }
    }
}