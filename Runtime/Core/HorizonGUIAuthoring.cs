using UnityEngine;
using UdonSharp;

namespace BlackHorizon.HorizonGUI
{
    [RequireComponent(typeof(HorizonGUIManager))]
    public class HorizonGUIAuthoring : MonoBehaviour
    {
        [Header("Template Source")]
        public TextAsset htmlFile;
        public TextAsset cssFile;

        [Header("Logic Binding")]
        [Tooltip("The UdonSharpBehaviour that will receive events (u-click) and variable links (u-bind).")]
        public UdonSharpBehaviour backingLogic;

        [Header("Legacy / Global Settings")]
        public HorizonTheme theme;
        public bool clearOnBuild = true;

        private void Reset()
        {
            if (theme == null)
            {
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:HorizonTheme");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    theme = UnityEditor.AssetDatabase.LoadAssetAtPath<HorizonTheme>(path);
                }
#endif
            }
        }
    }
}