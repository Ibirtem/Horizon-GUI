using UnityEngine;
using UnityEditor;

namespace BlackHorizon.HorizonGUI
{
    [CustomEditor(typeof(HorizonGUIManager))]
    public class HorizonGUIEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            HorizonGUIManager manager = (HorizonGUIManager)target;

            GUILayout.Space(10);

            if (GUILayout.Button("Auto-Link Tabs from Hierarchy"))
            {
                AutoLinkTabs(manager);
            }
        }

        private void AutoLinkTabs(HorizonGUIManager manager)
        {
            if (manager.pageContentContainer != null)
            {
                var modulesList = new System.Collections.Generic.List<HorizonGUIModule>();
                foreach (Transform t in manager.pageContentContainer)
                {
                    var module = t.GetComponent<HorizonGUIModule>();
                    if (module != null)
                    {
                        modulesList.Add(module);
                    }
                }
                manager.modules = modulesList.ToArray();
                EditorUtility.SetDirty(manager);
                Debug.Log($"[HorizonGUI] Auto-linked {modulesList.Count} modules found in PageContainer.");
            }
            else
            {
                Debug.LogWarning("[HorizonGUI] Page Content Container is not assigned in Manager.");
            }
        }
    }
}