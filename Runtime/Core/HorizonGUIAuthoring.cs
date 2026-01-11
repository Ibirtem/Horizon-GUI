using UnityEngine;

namespace BlackHorizon.HorizonGUI
{
    [RequireComponent(typeof(HorizonGUIManager))]
    public class HorizonGUIAuthoring : MonoBehaviour
    {
        [Header("Design Settings")]
        public HorizonTheme theme;

        [Tooltip("Clean existing UI children before building?")]
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