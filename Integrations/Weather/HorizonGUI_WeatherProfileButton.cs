using UnityEngine;
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;

namespace BlackHorizon.HorizonGUI.Integrations.Weather
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_WeatherProfileButton : UdonSharpBehaviour
    {
        [Header("Configuration")]
        public UdonBehaviour targetUdon;
        public int profileIndex;

        public void OnClick()
        {
            if (targetUdon == null) return;

            targetUdon.SetProgramVariable("_pendingProfileIndex", profileIndex);
            targetUdon.SendCustomEvent("ApplyPendingProfile");
        }
    }
}