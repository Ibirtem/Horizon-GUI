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
        }

        public void OnHide()
        {
            if (Weather_View != null) Weather_View.SetActive(false);
        }

        public void TryConnectSystem()
        {
            if (weatherSystemObj != null) return;

#if HORIZON_WEATHER_INTEGRATION
            WeatherTimeSystem found = null;
#if UNITY_EDITOR
                found = Object.FindObjectOfType<WeatherTimeSystem>(true);
#else
                found = Object.FindObjectOfType<WeatherTimeSystem>();
#endif
            
            if (found != null) weatherSystemObj = found.gameObject;
#endif
        }

        private void Update()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystemObj == null) return;
            WeatherTimeSystem wts = weatherSystemObj.GetComponent<WeatherTimeSystem>();
            if (wts == null) return;

            if (Weather_TimeSlider != null && wts.timeMode == TimeMode.SyncWithSystemClock)
            {
                Weather_TimeSlider.SetValueWithoutNotify(wts._sunTimeOfDay);
            }
#endif
        }

        public void UpdateStatusVisuals()
        {
            if (Weather_StatusText != null)
            {
                bool connected = false;
#if HORIZON_WEATHER_INTEGRATION
                connected = weatherSystemObj != null;
#endif
                Weather_StatusText.text = connected
                    ? "Status: <color=#33FF33>Connected</color>"
                    : "Status: <color=#FF3333>Not Found</color>";
            }
        }

        private void SyncUI()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystemObj == null) return;
            WeatherTimeSystem wts = weatherSystemObj.GetComponent<WeatherTimeSystem>();
            if (wts == null) return;
            
            bool isRealTime = wts.timeMode == TimeMode.SyncWithSystemClock;
            if (Weather_RealTimeToggle != null) Weather_RealTimeToggle.isOn = isRealTime;
            
            if (Weather_TimeSlider != null)
            {
                Weather_TimeSlider.SetValueWithoutNotify(wts._sunTimeOfDay);
                Weather_TimeSlider.interactable = !isRealTime;
            }
#endif
        }

        public void OnRealTimeChanged()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystemObj == null || Weather_RealTimeToggle == null) return;
            WeatherTimeSystem wts = weatherSystemObj.GetComponent<WeatherTimeSystem>();
            if (wts == null) return;
            
            bool isRealTime = Weather_RealTimeToggle.isOn;
            wts.timeMode = isRealTime ? TimeMode.SyncWithSystemClock : TimeMode.StaticManual;
            
            if (Weather_TimeSlider != null)
            {
                Weather_TimeSlider.interactable = !isRealTime;
                if (isRealTime) Weather_TimeSlider.SetValueWithoutNotify(wts._sunTimeOfDay);
            }
            
            if (isRealTime) wts.ReleaseExternalControl();
            UpdateStatusVisuals();
#endif
        }

        public void OnTimeSliderChanged()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystemObj == null || Weather_TimeSlider == null) return;
            WeatherTimeSystem wts = weatherSystemObj.GetComponent<WeatherTimeSystem>();
            if (wts == null || wts.timeMode == TimeMode.SyncWithSystemClock) return;

            wts.SetExternalTime(Weather_TimeSlider.value);
#endif
        }

        public void OnProfileClear() => SetWeather(0);
        public void OnProfileSnow() => SetWeather(1);
        public void OnProfileRain() => SetWeather(2);

        private void SetWeather(int index)
        {
#if HORIZON_WEATHER_INTEGRATION
            if (weatherSystemObj != null)
            {
                WeatherTimeSystem wts = weatherSystemObj.GetComponent<WeatherTimeSystem>();
                if (wts != null) wts.SetWeatherProfile(index); 
            }
#endif
        }
    }
}