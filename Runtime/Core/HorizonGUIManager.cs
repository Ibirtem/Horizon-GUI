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
        public UdonSharpBehaviour[] modules;

        [Tooltip("Global modal/overlay container.")]
        public GameObject overlayContainer;

        [Header("Global Bindings")]
        [Tooltip("Optional: Global clock text usually placed in the header.")]
        public TextMeshProUGUI clockText;

        private int _currentTabIndex = 0;

        /// <summary>
        /// Shared integer variable for inter-module communication (e.g., passing IDs).
        /// </summary>
        [System.NonSerialized] public int _lastEventInt;

        private void Start()
        {
            OpenTab(0);
        }

        private void Update()
        {
            if (clockText != null)
            {
                clockText.text = System.DateTime.Now.ToString("dd MMMM, HH:mm");
            }
        }

        #region Navigation Logic

        /// <summary>
        /// Switches the active module based on the provided index.
        /// Uses 'SendCustomEvent' to notify modules of visibility changes via 'OnShow' and 'OnHide'.
        /// This ensures loose coupling with any discovered logic script.
        /// </summary>
        /// <param name="index">The module index within the 'modules' array.</param>
        public void OpenTab(int index)
        {
            if (modules == null || index < 0 || index >= modules.Length) return;

            _currentTabIndex = index;

            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != null)
                {
                    bool isActive = (i == index);

                    if (isActive)
                    {
                        modules[i].SendCustomEvent("OnShow");
                    }
                    else
                    {
                        modules[i].SendCustomEvent("OnHide");
                    }
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