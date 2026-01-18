using UnityEngine;
using TMPro;
using VRC.SDKBase;

#if UDONSHARP
using UdonSharp;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    /// <summary>
    /// Example Content Module: Displays instance information and a player grid.
    /// This module is self-contained and does not rely on the Manager for data updates.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_HomeModule : HorizonGUIModule
    {
        [Header("Home Info")]
        public TextMeshProUGUI instanceInfoText;

        [Header("Data Grid")]
        public HorizonDataGrid playerGrid;

        /// <summary>
        /// ID received from the Grid when a slot is clicked.
        /// </summary>
        [System.NonSerialized] public int _lastEventInt;

        private void OnEnable()
        {
            UpdateInfo();
        }

        public override void OnShow()
        {
            base.OnShow();
            UpdateInfo();
        }

        private void Update()
        {
            if (Time.frameCount % 120 == 0)
            {
                UpdateInfo();
            }
        }

        public void UpdateInfo()
        {
            int playerCount = 0;
            int validCount = 0;

#if UDONSHARP
            playerCount = VRCPlayerApi.GetPlayerCount();
            VRCPlayerApi[] players = new VRCPlayerApi[playerCount];
            VRCPlayerApi.GetPlayers(players);

            for (int i = 0; i < playerCount; i++)
            {
                if (Utilities.IsValid(players[i])) validCount++;
            }

            int[] ids = new int[validCount];
            string[] names = new string[validCount];
            int writeIndex = 0;

            for (int i = 0; i < playerCount; i++)
            {
                if (Utilities.IsValid(players[i]))
                {
                    ids[writeIndex] = players[i].playerId;
                    names[writeIndex] = players[i].displayName;
                    writeIndex++;
                }
            }

            if (playerGrid != null) playerGrid.LoadData(ids, names, null);
#else
            validCount = 1;
            if (playerGrid != null)
                playerGrid.LoadData(new int[] { 0 }, new string[] { "LocalDev" }, null);
#endif

            if (instanceInfoText != null)
            {
                instanceInfoText.text = $"Instance Players: {validCount}\n" +
                                        $"System Status: <color=#33FF33>Online</color>";
            }
        }

        /// <summary>
        /// Called via Custom Event by playerGrid when a slot is clicked.
        /// </summary>
        public void OnPlayerSlotClicked()
        {
            int playerId = _lastEventInt;
            Debug.Log($"[HorizonHome] Clicked on player ID: {playerId}");
        }
    }
}