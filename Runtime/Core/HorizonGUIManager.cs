using UnityEngine;
using TMPro;
using UdonSharp;
using VRC.Udon;

namespace BlackHorizon.HorizonGUI
{
    /// <summary>
    /// The core controller for the Horizon UI System.
    /// Manages high-level state and acts as a registry for discovered logic scripts.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUIManager : UdonSharpBehaviour
    {
        [Header("System Core")]
        [Tooltip("List of all logic scripts discovered and bound by the compiler.")]
        public UdonSharpBehaviour[] logicScripts;
        public GameObject overlayContainer;

        private int _currentTabIndex = 0;

        /// <summary>
        /// Shared integer variable for inter-module communication (e.g., passing IDs).
        /// </summary>
        [System.NonSerialized] public int _lastEventInt;

        private void Start()
        {
            OpenTab(0);
        }

        #region Navigation Logic

        /// <summary>
        /// Triggers 'OnShow' and 'OnHide' events on registered logic scripts based on the active index.
        /// This ensures loose coupling, allowing any custom script to react to tab changes without strict inheritance.
        /// </summary>
        /// <param name="index">The target index within the 'logicScripts' array.</param>
        public void OpenTab(int index)
        {
            if (logicScripts == null || index < 0 || index >= logicScripts.Length) return;

            _currentTabIndex = index;

            for (int i = 0; i < logicScripts.Length; i++)
            {
                if (logicScripts[i] != null)
                {
                    bool isActive = (i == index);

                    if (isActive) logicScripts[i].SendCustomEvent("OnShow");
                    else logicScripts[i].SendCustomEvent("OnHide");
                }
            }
        }

        #endregion

        #region Global Overlays

        public void ToggleOverlay(bool show)
        {
            if (overlayContainer != null) overlayContainer.SetActive(show);
        }

        public void CloseOverlay() => ToggleOverlay(false);

        #endregion
    }
}