using UnityEngine;
using TMPro;
using VRC.SDKBase;

#if UDONSHARP
using UdonSharp;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_HomeModule : HorizonGUIModule
    {
        [Header("Home Info")]
        public TextMeshProUGUI instanceInfoText;

        [Header("Data Grid")]
        /// <summary>
        /// Reference to the centralized grid manager for players.
        /// </summary>
        public HorizonDataGrid playerGrid;

        /// <summary>
        /// Storage for the last clicked item ID, sent by HorizonDataGrid.
        /// </summary>
        [System.NonSerialized] public int _lastEventInt;

        /// <summary>
        /// Called via Custom Event by playerGrid when a slot is clicked.
        /// </summary>
        public void OnPlayerSlotClicked()
        {
            int playerId = _lastEventInt;
            Debug.Log($"[HorizonHome] Clicked on player ID: {playerId}");
        }

        private void OnEnable()
        {
            UpdateInfo();
        }

        private void Update()
        {
            if (Time.frameCount % 100 == 0)
            {
                UpdateInfo();
            }
        }

        public void UpdateInfo()
        {
            int playerCount = 0;

#if UDONSHARP
            playerCount = VRCPlayerApi.GetPlayerCount();
            
            if (playerGrid != null)
            {
                VRCPlayerApi[] players = new VRCPlayerApi[playerCount];
                VRCPlayerApi.GetPlayers(players);

                int[] ids = new int[playerCount];
                string[] names = new string[playerCount];

                for (int i = 0; i < playerCount; i++)
                {
                    if (Utilities.IsValid(players[i]))
                    {
                        ids[i] = players[i].playerId;
                        names[i] = players[i].displayName;
                    }
                }

                playerGrid.LoadData(ids, names, null);
            }
#else
            playerCount = 1;
            if (playerGrid != null)
            {
                playerGrid.LoadData(new int[] { 0 }, new string[] { "LocalDev" }, null);
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