using UnityEngine;
using TMPro;

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_AboutModule : HorizonGUIModule
    {
        [Header("About Data")]
        public string githubUrl = "https://github.com/Ibirtem/WeatherSystem";
        public string boostyUrl = "https://boosty.to/ibirtem";

        [Header("References")]
        public TMP_InputField githubField;
        public TMP_InputField linkField;

        private void OnEnable()
        {
            ResetText();
        }

        private void Update()
        {
            if (githubField != null)
            {
                if (githubField.text != githubUrl)
                {
                    githubField.text = githubUrl;
                }
            }

            if (linkField != null)
            {
                if (linkField.text != boostyUrl)
                {
                    linkField.text = boostyUrl;
                }
            }
        }

        public void ResetText()
        {
            if (githubField != null) githubField.text = githubUrl;
            if (linkField != null) linkField.text = boostyUrl;
        }
    }
}