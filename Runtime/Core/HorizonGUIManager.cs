using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlackHorizon.HorizonWeatherTime;

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    /// <summary>
    /// Central controller for the Horizon UI System.
    /// Handles navigation between modules, global overlays, and core system logic (Clock, Weather, Player Grid).
    /// UI references are automatically populated by the HorizonCompiler via 'u-bind' attributes.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUIManager : UdonSharpBehaviour
    {
        [Header("System Core")]
        [Tooltip("List of all top-level modules. Managed by the compiler.")]
        public HorizonGUIModule[] modules;

        [Tooltip("Global modal/overlay container.")]
        public GameObject overlayContainer;

        [Header("Navigation State")]
        public Color activeTabColor = new Color(1f, 1f, 1f, 0.2f);
        public Color inactiveTabColor = new Color(0f, 0f, 0f, 0.0f);

        [Header("Binding Targets (Auto-populated)")]
        public Image btnNavHome;
        public Image btnNavWeather;
        public Image btnNavAbout;
        public TextMeshProUGUI clockText;
        public TextMeshProUGUI instanceInfoText;
        public HorizonDataGrid playerGrid;

        [Header("Weather Integration")]
        public WeatherTimeSystem weatherSystem;
        public TextMeshProUGUI weatherStatusText;
        public TextMeshProUGUI weatherVersionText;
        public Toggle realTimeToggle;
        public Slider timeSlider;

        private int _currentTabIndex = 0;

        /// <summary>
        /// Property used by HorizonDataGrid to pass the selected item ID back to the manager.
        /// </summary>
        [System.NonSerialized] public int _lastEventInt;

        private void Start()
        {
            InitializeUI();
            OpenTab(0);
        }

        private void Update()
        {
            UpdateSystemClock();

            if (Time.frameCount % 120 == 0) UpdatePlayerList();

            if (_currentTabIndex == 1) SyncWeatherTime();
        }

        #region System Initialization

        private void InitializeUI()
        {
            UpdatePlayerList();
            SyncWeatherUI();
        }

        private void UpdateSystemClock()
        {
            if (clockText != null)
                clockText.text = System.DateTime.Now.ToString("dd MMMM, HH:mm");
        }

        #endregion

        #region Navigation Logic

        public void OnNavHome() => OpenTab(0);
        public void OnNavWeather() => OpenTab(1);
        public void OnNavAbout() => OpenTab(2);

        /// <summary>
        /// Switches the active module and updates navigation button visuals.
        /// </summary>
        /// <param name="index">The index of the module to display.</param>
        public void OpenTab(int index)
        {
            if (modules == null || index < 0 || index >= modules.Length) return;

            _currentTabIndex = index;

            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != null) modules[i].gameObject.SetActive(i == index);
            }

            UpdateNavigationVisuals(index);

            if (index == 0) UpdatePlayerList();
            if (index == 1) SyncWeatherUI();
        }

        private void UpdateNavigationVisuals(int activeIndex)
        {
            SetButtonState(btnNavHome, activeIndex == 0);
            SetButtonState(btnNavWeather, activeIndex == 1);
            SetButtonState(btnNavAbout, activeIndex == 2);
        }

        private void SetButtonState(Image btnBg, bool isActive)
        {
            if (btnBg != null) btnBg.color = isActive ? activeTabColor : inactiveTabColor;
        }

        #endregion

        #region Module: Home (Player Grid)

        /// <summary>
        /// Fetches the current player list. 
        /// Populates names in the data array for internal grid logic, 
        /// even if the current visual style (Circle) hides them.
        /// </summary>
        public void UpdatePlayerList()
        {
            int playerCount = 0;
#if UDONSHARP
            playerCount = VRCPlayerApi.GetPlayerCount();
#else
            playerCount = 1;
#endif

            if (instanceInfoText != null) instanceInfoText.text = $"Instance Players: {playerCount}";

            if (playerGrid != null)
            {
#if UDONSHARP
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
#else
                playerGrid.LoadData(new int[] { 1 }, new string[] { "Editor" }, null);
#endif
            }
        }

        public void OnPlayerSlotClicked()
        {
            Debug.Log($"[Horizon] Selected Player ID: {_lastEventInt}");
        }

        #endregion

        #region Module: Weather

        private void SyncWeatherUI()
        {
            if (weatherSystem == null) return;
            if (weatherStatusText != null) weatherStatusText.text = "Status: <color=#33FF33>Connected</color>";
            if (realTimeToggle != null) realTimeToggle.isOn = weatherSystem.useRealTime;

            SyncWeatherTime();
        }

        private void SyncWeatherTime()
        {
            if (weatherSystem == null || timeSlider == null) return;

            if (weatherSystem.useRealTime)
            {
                timeSlider.SetValueWithoutNotify(weatherSystem._sunTimeOfDay);
            }
            timeSlider.interactable = !weatherSystem.useRealTime;
        }

        public void OnRealTimeChanged()
        {
            if (weatherSystem == null || realTimeToggle == null) return;

            weatherSystem.useRealTime = realTimeToggle.isOn;
            if (realTimeToggle.isOn) weatherSystem.ReleaseExternalControl();

            SyncWeatherUI();
        }

        public void OnTimeSliderChanged()
        {
            if (weatherSystem == null || timeSlider == null || weatherSystem.useRealTime) return;
            weatherSystem.SetExternalTime(timeSlider.value);
        }

        public void OnProfileClear() => SetWeather(0);
        public void OnProfileSnow() => SetWeather(1);
        public void OnProfileRain() => SetWeather(2);

        private void SetWeather(int index)
        {
            if (weatherSystem != null) weatherSystem.SetWeatherProfile(index);
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