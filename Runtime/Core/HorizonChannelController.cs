using UnityEngine;
#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    /// <summary>
    /// [AUTO-GENERATED] Manages the visibility state of a specific UI Channel.
    /// <para>
    /// Created by HorizonCompiler during the build process when 'h-view' attributes are detected.
    /// It functions as a router, enabling only the requested View ID and disabling others.
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
#if UDONSHARP
    public class HorizonChannelController : UdonSharpBehaviour
#else
    public class HorizonChannelController : MonoBehaviour
#endif
    {
        [Header("System Info")]
        public string channelName;

        [Header("Links")]
        [Tooltip("List of all GameObjects managed by this channel.")]
        public GameObject[] views;
        [Tooltip("Corresponding IDs for the views (e.g., 'Home', 'Settings').")]
        public string[] viewIds;

        [Header("Interop")]
        /// <summary>
        /// Intermediate variable used to receive data from HorizonEventCaller.
        /// <br/>The caller sets this string, then invokes '_SwitchFromEvent'.
        /// </summary>
        [System.NonSerialized] public string _lastEventString;

        /// <summary>
        /// Direct method to switch the active view. 
        /// <para>Note: Difficult to call directly from UI Buttons in Udon due to argument limitations.</para>
        /// </summary>
        /// <param name="targetId">The ID of the view to show.</param>
        public void Switch(string targetId)
        {
            if (views == null || viewIds == null || views.Length != viewIds.Length)
            {
                return;
            }

            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] == null) continue;

                bool match = (viewIds[i] == targetId);

                if (views[i].activeSelf != match)
                    views[i].SetActive(match);
            }
        }

        /// <summary>
        /// Entry point for UI Events via HorizonEventCaller.
        /// <para>
        /// 1. Button sets '_lastEventString' on this behaviour.<br/>
        /// 2. Button sends Custom Event '_SwitchFromEvent'.<br/>
        /// 3. This method reads the string and performs the switch.
        /// </para>
        /// </summary>
        public void _SwitchFromEvent()
        {
            if (!string.IsNullOrEmpty(_lastEventString))
            {
                Switch(_lastEventString);
            }
        }
    }
}