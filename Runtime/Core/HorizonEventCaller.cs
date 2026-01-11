using UnityEngine;
using UnityEngine.UI;
#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    [AddComponentMenu("Horizon/Horizon Event Caller")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
#if UDONSHARP
    public class HorizonEventCaller : UdonSharpBehaviour
#else
    public class HorizonEventCaller : MonoBehaviour
#endif
    {
        [Header("Target Configuration")]
        [Tooltip("The UdonBehaviour that will receive the event.")]
        public UdonBehaviour targetBehaviour;

        [Tooltip("The name of the Custom Event to call.")]
        public string eventName;

        [Header("Payload")]
        [Tooltip("Integer value to pass to the target (stored in _lastEventInt).")]
        public int intPayload;

        [Tooltip("String value to pass to the target (stored in _lastEventString).")]
        public string stringPayload;

        /// <summary>
        /// Called by the UI Button via UnityEvent.
        /// </summary>
        public void OnClick()
        {
            if (targetBehaviour == null) return;

            targetBehaviour.SetProgramVariable("_lastEventInt", intPayload);
            targetBehaviour.SetProgramVariable("_lastEventString", stringPayload);

            targetBehaviour.SendCustomEvent(eventName);
        }
    }
}