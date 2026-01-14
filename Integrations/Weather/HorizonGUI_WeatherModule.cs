using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlackHorizon.HorizonWeatherTime;

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI.Integrations.Weather
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_WeatherModule : HorizonGUIModule
    {
        [Header("Integration")]
        public WeatherTimeSystem weatherSystem;

        [Header("UI References")]
        public TextMeshProUGUI weatherStatusText;
        public TextMeshProUGUI weatherVersionText;

        [Header("Controls")]
        public Toggle realTimeToggle;
        public Slider timeSlider;

        private void Start()
        {
            TryConnectSystem();
            UpdateStatusVisuals();
            SyncUI();
        }

        private void OnEnable()
        {
            TryConnectSystem();
            UpdateStatusVisuals();
            SyncUI();
        }

        public void TryConnectSystem()
        {
            if (weatherSystem != null) return;

            GameObject sysObj = GameObject.Find("WeatherTimeSystem");
            if (sysObj != null)
            {
                weatherSystem = sysObj.GetComponent<WeatherTimeSystem>();
            }
        }

        private void Update()
        {
            if (weatherSystem != null && timeSlider != null && weatherSystem.useRealTime)
            {
                timeSlider.SetValueWithoutNotify(weatherSystem._sunTimeOfDay);
            }
        }

        public void UpdateStatusVisuals()
        {
            if (weatherStatusText != null)
            {
                weatherStatusText.text = (weatherSystem != null)
                    ? "Status: <color=#33FF33>Connected</color>"
                    : "Status: <color=#FF3333>Not Found</color>";
            }

            if (weatherSystem != null && weatherVersionText != null)
            {
                if (weatherVersionText.text == "---" || string.IsNullOrEmpty(weatherVersionText.text))
                    weatherVersionText.text = "Online";
            }
        }

        private void SyncUI()
        {
            if (weatherSystem == null) return;
            if (realTimeToggle != null) realTimeToggle.isOn = weatherSystem.useRealTime;
            if (timeSlider != null)
            {
                timeSlider.SetValueWithoutNotify(weatherSystem._sunTimeOfDay);
                timeSlider.interactable = !weatherSystem.useRealTime;
            }
        }

        public void OnRealTimeChanged()
        {
            if (weatherSystem == null || realTimeToggle == null) return;
            weatherSystem.useRealTime = realTimeToggle.isOn;
            if (timeSlider != null)
            {
                timeSlider.interactable = !realTimeToggle.isOn;
                if (realTimeToggle.isOn) timeSlider.SetValueWithoutNotify(weatherSystem._sunTimeOfDay);
            }
            if (realTimeToggle.isOn) weatherSystem.ReleaseExternalControl();
            UpdateStatusVisuals();
        }

        public void OnTimeSliderChanged()
        {
            if (weatherSystem == null || timeSlider == null || weatherSystem.useRealTime) return;
            weatherSystem.SetExternalTime(timeSlider.value);
        }

        public void OnProfileClear() => SetWeather(0);
        public void OnProfileSnow() => SetWeather(1);
        public void OnProfileRain() => SetWeather(2);
        private void SetWeather(int index) { if (weatherSystem != null) weatherSystem.SetWeatherProfile(index); }
    }
}