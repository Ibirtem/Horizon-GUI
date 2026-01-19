using UnityEngine;
using TMPro;
using UdonSharp;

namespace BlackHorizon.HorizonGUI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonGUI_AboutModule : UdonSharpBehaviour
    {
        public string githubUrl = "https://github.com/Ibirtem/Horizon-GUI";
        public string boostyUrl = "https://boosty.to/ibirtem";

        [Header("Direct Bindings")]
        public GameObject About_View;
        public TMP_InputField About_GithubField;
        public TMP_InputField About_BoostyField;

        public void OnHorizonBuild() => ResetText();

        public void OnShow()
        {
            if (About_View != null) About_View.SetActive(true);
        }

        public void OnHide()
        {
            if (About_View != null) About_View.SetActive(false);
        }

        public void ResetText()
        {
            if (About_GithubField != null) About_GithubField.text = githubUrl;
            if (About_BoostyField != null) About_BoostyField.text = boostyUrl;
        }
    }
}