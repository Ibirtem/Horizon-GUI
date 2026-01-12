using UnityEngine;
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace BlackHorizon.HorizonGUI.Test
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonTestLogic : UdonSharpBehaviour
    {
        [Header("UI Links")]
        public TextMeshProUGUI title;

        public void OnSubmit()
        {
            Debug.Log("<b><color=#00ff00>[TEST]</color></b> OnSubmit called!");

            if (title != null)
            {
                title.text = "Hello VRChat!";
            }
        }
    }
}