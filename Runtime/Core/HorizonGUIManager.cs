using UnityEngine;
using TMPro;

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    /// <summary>
    /// The core controller for the Horizon UI System.
    /// Manages high-level state: active modules, tabs, and global overlays.
    /// Content-specific logic (like Player Grids) should be handled by individual modules.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUIManager : UdonSharpBehaviour
    {
        [Header("System Core")]
        [Tooltip("List of all top-level modules. Managed by the compiler.")]
        public HorizonGUIModule[] modules;

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
        /// Switches the active module and updates its visibility.
        /// </summary>
        /// <param name="index">The index of the module in the 'modules' array.</param>
        public void OpenTab(int index)
        {
            if (modules == null || index < 0 || index >= modules.Length) return;

            _currentTabIndex = index;

            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != null)
                {
                    bool isActive = (i == index);

                    if (isActive) modules[i].OnShow();
                    else modules[i].OnHide();

                    modules[i].gameObject.SetActive(isActive);
                }
            }
        }

        // --- LEGACY NAVIGATION SUPPORT ---
        // TODO: Replace with generic u-arg event system in the future updates.
        public void OnNavHome() => OpenTab(0);
        public void OnNavWeather() => OpenTab(1);
        public void OnNavAbout() => OpenTab(2);

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