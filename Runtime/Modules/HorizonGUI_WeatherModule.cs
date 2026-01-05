using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlackHorizon.HorizonWeatherTime;

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_WeatherModule : HorizonGUIModule
    {
        [Header("Integration")]
        public WeatherTimeSystem targetSystem;

        [Header("UI References")]
        public GameObject controlsContainer;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI versionText;

        [Header("Controls")]
        public Toggle realTimeToggle;
        public Slider timeSlider;

        private bool _isDragging = false;

        private void OnEnable()
        {
            UpdateStatusVisuals();
            SyncUI();
        }

        private void Update()
        {
            if (targetSystem != null && timeSlider != null)
            {
                if (targetSystem.useRealTime)
                {
                    timeSlider.SetValueWithoutNotify(targetSystem._sunTimeOfDay);
                }
            }
        }

        public void UpdateStatusVisuals()
        {
            if (targetSystem != null)
            {
                statusText.text = "System Status: <color=#33FF33>Connected</color>";
            }
            else
            {
                statusText.text = "System Status: <color=#FF3333>Not Found</color>";
            }
        }

        private void SyncUI()
        {
            if (targetSystem == null) return;

            if (realTimeToggle != null)
            {
                realTimeToggle.isOn = targetSystem.useRealTime;
            }

            if (timeSlider != null)
            {
                timeSlider.SetValueWithoutNotify(targetSystem._sunTimeOfDay);
                timeSlider.interactable = !targetSystem.useRealTime;
            }
        }

        // --- EVENTS ---

        public void OnRealTimeChanged()
        {
            if (targetSystem == null || realTimeToggle == null) return;

            // Debug.Log($"[WeatherGUI] Real Time: {realTimeToggle.isOn}");
            targetSystem.useRealTime = realTimeToggle.isOn;

            if (timeSlider != null)
            {
                timeSlider.interactable = !realTimeToggle.isOn;
            }

            if (realTimeToggle.isOn) targetSystem.ReleaseExternalControl();
        }

        public void OnTimeSliderChanged()
        {
            if (targetSystem == null || timeSlider == null) return;

            if (targetSystem.useRealTime) return;

            targetSystem.SetExternalTime(timeSlider.value);
        }
    }
}