using UnityEngine;
using UnityEngine.UI;
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
    public class HorizonGUIManager : UdonSharpBehaviour
#else
    public class HorizonGUIManager : MonoBehaviour
#endif
    {
        [Header("Runtime References")]
        public Transform pageContentContainer;

        [Header("Styling")]
        public Color activeTabColor = new Color(1f, 1f, 1f, 0.2f);
        public Color inactiveTabColor = new Color(0f, 0f, 0f, 0.0f);

        [Header("Pre-Baked References")]
        [Tooltip("List of modules in the scene.")]
        public HorizonGUIModule[] modules;

        [Tooltip("List of navigation buttons corresponding to modules.")]
        public HorizonGUINavigationButton[] navigationButtons;

        [Header("Clock")]
        public TextMeshProUGUI clockText;
        public bool use24HourFormat = true;

        private int _currentTabIndex = -1;

        private void Start()
        {
            if (modules != null && modules.Length > 0)
            {
                _currentTabIndex = 0;

                for (int i = 0; i < modules.Length; i++)
                {
                    if (modules[i] != null) modules[i].gameObject.SetActive(i == _currentTabIndex);
                }

                UpdateNavigationVisuals();
            }
        }

        private void Update()
        {
            if (clockText != null)
            {
                System.DateTime now = System.DateTime.Now;
                string format = use24HourFormat ? "d MMMM HH:mm" : "d MMMM h:mm tt";
                clockText.text = now.ToString(format);
            }
        }

        public void OpenTab(int index)
        {
            if (modules == null || index < 0 || index >= modules.Length) return;
            if (index == _currentTabIndex) return;

            if (_currentTabIndex >= 0 && _currentTabIndex < modules.Length)
            {
                if (modules[_currentTabIndex] != null) modules[_currentTabIndex].OnHide();
            }

            _currentTabIndex = index;
            if (modules[_currentTabIndex] != null)
            {
                modules[_currentTabIndex].OnShow();
            }

            UpdateNavigationVisuals();
        }

        private void UpdateNavigationVisuals()
        {
            if (navigationButtons == null) return;

            for (int i = 0; i < navigationButtons.Length; i++)
            {
                if (navigationButtons[i] != null)
                {
                    navigationButtons[i].UpdateVisuals(i == _currentTabIndex, activeTabColor, inactiveTabColor);
                }
            }
        }
    }
}