using UnityEngine;
using UnityEngine.UI;
using BlackHorizon.HorizonWeatherTime;

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
        public WeatherTimeSystem targetSystem;
        public int profileIndex;

        public void OnClick()
        {
            if (targetSystem != null)
            {
                targetSystem.SetWeatherProfile(profileIndex);
            }
        }
    }
}