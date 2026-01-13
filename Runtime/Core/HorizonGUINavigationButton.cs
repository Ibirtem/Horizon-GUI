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
    public class HorizonGUINavigationButton : UdonSharpBehaviour
#else
    public class HorizonGUINavigationButton : MonoBehaviour
#endif
    {
        [Header("Settings")]
        public int tabIndex;
        public HorizonGUIManager manager;

        public void OnClick()
        {
            if (manager != null)
            {
                manager.OpenTab(tabIndex);
            }
            else
            {
                Debug.LogError($"<b><color=#FF3333>[ERROR]</color></b> <color=white>[HorizonGUI] Manager is NULL on button '{gameObject.name}'!</color>");
            }
        }
    }
}