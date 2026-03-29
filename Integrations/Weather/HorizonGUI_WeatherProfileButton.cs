using UnityEngine;
using UnityEngine.UI;

#if HORIZON_WEATHER_INTEGRATION
using BlackHorizon.HorizonWeatherTime;
#endif

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI.Integrations.Weather
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
#if UDONSHARP
    public class HorizonGUI_WeatherProfileButton : UdonSharpBehaviour
#else
    public class HorizonGUI_WeatherProfileButton : MonoBehaviour
#endif
    {
        [Header("Configuration")]
#if HORIZON_WEATHER_INTEGRATION
        public WeatherTimeSystem targetSystem;
#endif
        public int profileIndex;

        public void OnClick()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (targetSystem != null)
            {
                targetSystem.SetWeatherProfile(profileIndex);
            }
#endif
        }
    }
}