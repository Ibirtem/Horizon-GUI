using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlackHorizon.HorizonWeatherTime;
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;

namespace BlackHorizon.HorizonGUI.Integrations.Weather
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_WeatherModule : UdonSharpBehaviour
    {
        [Header("Integration")]
        public WeatherTimeSystem weatherSystem;

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

            if (Weather_VersionText != null && !string.IsNullOrEmpty(cachedVersion))
            {
                Weather_VersionText.text = cachedVersion;
            }
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
            if (weatherSystem != null) return;
            GameObject sysObj = GameObject.Find("WeatherTimeSystem");
            if (sysObj != null) weatherSystem = sysObj.GetComponent<WeatherTimeSystem>();
        }

        private void Update()
        {
            if (weatherSystem != null && Weather_TimeSlider != null && weatherSystem.useRealTime)
            {
                Weather_TimeSlider.SetValueWithoutNotify(weatherSystem._sunTimeOfDay);
            }
        }

        public void UpdateStatusVisuals()
        {
            if (Weather_StatusText != null)
            {
                Weather_StatusText.text = (weatherSystem != null)
                    ? "Status: <color=#33FF33>Connected</color>"
                    : "Status: <color=#FF3333>Not Found</color>";
            }
        }

        private void SyncUI()
        {
            if (weatherSystem == null) return;
            if (Weather_RealTimeToggle != null) Weather_RealTimeToggle.isOn = weatherSystem.useRealTime;
            if (Weather_TimeSlider != null)
            {
                Weather_TimeSlider.SetValueWithoutNotify(weatherSystem._sunTimeOfDay);
                Weather_TimeSlider.interactable = !weatherSystem.useRealTime;
            }
        }

        public void OnRealTimeChanged()
        {
            if (weatherSystem == null || Weather_RealTimeToggle == null) return;
            weatherSystem.useRealTime = Weather_RealTimeToggle.isOn;
            if (Weather_TimeSlider != null)
            {
                Weather_TimeSlider.interactable = !Weather_RealTimeToggle.isOn;
                if (Weather_RealTimeToggle.isOn) Weather_TimeSlider.SetValueWithoutNotify(weatherSystem._sunTimeOfDay);
            }
            if (Weather_RealTimeToggle.isOn) weatherSystem.ReleaseExternalControl();
            UpdateStatusVisuals();
        }

        public void OnTimeSliderChanged()
        {
            if (weatherSystem == null || Weather_TimeSlider == null || weatherSystem.useRealTime) return;
            weatherSystem.SetExternalTime(Weather_TimeSlider.value);
        }

        public void OnProfileClear() => SetWeather(0);
        public void OnProfileSnow() => SetWeather(1);
        public void OnProfileRain() => SetWeather(2);
        private void SetWeather(int index) { if (weatherSystem != null) weatherSystem.SetWeatherProfile(index); }
    }
}