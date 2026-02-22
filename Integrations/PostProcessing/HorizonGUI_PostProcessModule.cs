using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;

namespace BlackHorizon.HorizonGUI.Integrations.PostProcessing
{
    /// <summary>
    /// Manages the activation state of global Post Processing volume overrides.
    /// Acts as a bridge between the Horizon UI toggles and the physical PostProcessVolume GameObjects.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_PostProcessModule : UdonSharpBehaviour
    {
        [Header("Direct Bindings")]
        public GameObject PP_View;

        [Header("UI Toggles")]
        public Toggle Toggle_Bloom;
        public Toggle Toggle_AO;
        public Toggle Toggle_CA;
        public Toggle Toggle_Grain;

        [Header("Volume Objects (Overrides)")]
        public GameObject VolumeObj_Bloom;
        public GameObject VolumeObj_AO;
        public GameObject VolumeObj_CA;
        public GameObject VolumeObj_Grain;

        private void Start()
        {
            SyncUI();
        }

        /// <summary>
        /// Triggered by the HorizonGUIManager when this module's view is activated.
        /// </summary>
        public void OnShow()
        {
            if (PP_View != null)
            {
                PP_View.SetActive(true);
            }
        }

        /// <summary>
        /// Triggered by the HorizonGUIManager when another module is opened.
        /// </summary>
        public void OnHide()
        {
            if (PP_View != null)
            {
                PP_View.SetActive(false);
            }
        }

        /// <summary>
        /// Synchronizes the initial state of the UI toggles with the actual active state 
        /// of the volume GameObjects in the scene.
        /// </summary>
        private void SyncUI()
        {
            if (Toggle_Bloom != null && VolumeObj_Bloom != null)
            {
                Toggle_Bloom.isOn = VolumeObj_Bloom.activeSelf;
            }

            if (Toggle_AO != null && VolumeObj_AO != null)
            {
                Toggle_AO.isOn = VolumeObj_AO.activeSelf;
            }

            if (Toggle_CA != null && VolumeObj_CA != null)
            {
                Toggle_CA.isOn = VolumeObj_CA.activeSelf;
            }

            if (Toggle_Grain != null && VolumeObj_Grain != null)
            {
                Toggle_Grain.isOn = VolumeObj_Grain.activeSelf;
            }
        }

        public void OnBloomChanged()
        {
            if (VolumeObj_Bloom != null && Toggle_Bloom != null)
            {
                VolumeObj_Bloom.SetActive(Toggle_Bloom.isOn);
            }
        }

        public void OnAOChanged()
        {
            if (VolumeObj_AO != null && Toggle_AO != null)
            {
                VolumeObj_AO.SetActive(Toggle_AO.isOn);
            }
        }

        public void OnCAChanged()
        {
            if (VolumeObj_CA != null && Toggle_CA != null)
            {
                VolumeObj_CA.SetActive(Toggle_CA.isOn);
            }
        }

        public void OnGrainChanged()
        {
            if (VolumeObj_Grain != null && Toggle_Grain != null)
            {
                VolumeObj_Grain.SetActive(Toggle_Grain.isOn);
            }
        }
    }
}