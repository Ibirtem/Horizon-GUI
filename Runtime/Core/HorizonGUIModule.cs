using UnityEngine;
using UnityEngine.UI;

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
#if UDONSHARP
    public class HorizonGUIModule : UdonSharpBehaviour
#else
    public class HorizonGUIModule : MonoBehaviour
#endif
    {
        [Header("Module Settings")]
        [Tooltip("If true, this module will be hidden from logic iterations if necessary.")]
        public bool isHidden = false;

        public virtual void OnShow()
        {
            gameObject.SetActive(true);
        }

        public virtual void OnHide()
        {
            gameObject.SetActive(false);
        }
    }
}