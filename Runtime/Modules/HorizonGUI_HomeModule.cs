using UnityEngine;
using TMPro;

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
#if UDONSHARP
    public class HorizonGUI_HomeModule : HorizonGUIModule
#else
    public class HorizonGUI_HomeModule : HorizonGUIModule
#endif
    {
        [Header("Home Info")]
        public TextMeshProUGUI instanceInfoText;

        [Header("Player Grid")]
        public GameObject[] playerSlots;

        private void OnEnable()
        {
            UpdateInfo();
        }

        private void Update()
        {
            if (Time.frameCount % 60 == 0) 
            {
                UpdateInfo();
            }
        }

        public void UpdateInfo()
        {
            int playerCount = 0;
            
#if UDONSHARP
            playerCount = VRCPlayerApi.GetPlayerCount();
            
            if (playerSlots != null && playerSlots.Length > 0)
            {
                VRCPlayerApi[] players = new VRCPlayerApi[playerSlots.Length];
                VRCPlayerApi.GetPlayers(players);

                for (int i = 0; i < playerSlots.Length; i++)
                {
                    if (playerSlots[i] != null)
                    {
                        bool isActive = (i < players.Length) && Utilities.IsValid(players[i]);
                        playerSlots[i].SetActive(isActive);
                    }
                }
            }
#else
            playerCount = 1;
            if (playerSlots != null && playerSlots.Length > 0)
            {
                for (int i = 0; i < playerSlots.Length; i++)
                {
                    if (playerSlots[i] != null) playerSlots[i].SetActive(i == 0);
                }
            }
#endif

            if (instanceInfoText != null)
            {
                instanceInfoText.text = $"Players in Instance: {playerCount}\n" +
                                        $"System Status: Online";
            }
        }
    }
}