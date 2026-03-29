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
        public GameObject targetSystemObj;
        public int profileIndex;

        public void OnClick()
        {
#if HORIZON_WEATHER_INTEGRATION
            if (targetSystemObj != null)
            {
                WeatherTimeSystem wts = targetSystemObj.GetComponent<WeatherTimeSystem>();
                if (wts != null) wts.SetWeatherProfile(profileIndex);
            }
#endif
        }
    }
}