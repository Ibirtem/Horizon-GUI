using UnityEngine;
using TMPro;
using VRC.SDKBase;
using UdonSharp;
using VRC.Udon;

namespace BlackHorizon.HorizonGUI
{
    /// <summary>
    /// Example Content Module: Displays instance information and a player grid.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_HomeModule : UdonSharpBehaviour
    {
        [Header("Direct Bindings")]
        public GameObject Home_View;
        public TextMeshProUGUI Home_InfoText;
        public HorizonDataGrid Home_PlayerGrid;

        /// <summary>
        /// ID received from the Grid when a slot is clicked.
        /// </summary>
        [System.NonSerialized] public int _lastEventInt;

        /// <summary>
        /// Called by Horizon Compiler immediately after building the UI in Editor.
        /// </summary>
        public void OnHorizonBuild()
        {
            if (Home_InfoText != null) Home_InfoText.text = "System Ready (Baked)";
        }

        private void OnEnable()
        {
            UpdateInfo();
        }

        public void OnShow()
        {
            if (Home_View != null) Home_View.SetActive(true);
            UpdateInfo();
        }

        public void OnHide()
        {
            if (Home_View != null) Home_View.SetActive(false);
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

            if (Home_PlayerGrid != null) Home_PlayerGrid.LoadData(ids, names, null);
#else
            validCount = 1;
            if (Home_PlayerGrid != null)
                Home_PlayerGrid.LoadData(new int[] { 0 }, new string[] { "LocalDev" }, null);
#endif
            if (Home_InfoText != null)
            {
                Home_InfoText.text = $"Instance Players: {validCount}\n" +
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