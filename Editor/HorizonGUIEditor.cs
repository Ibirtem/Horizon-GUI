using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;

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
            var modulesList = new List<HorizonGUIModule>();
            var foundModules = manager.GetComponentsInChildren<HorizonGUIModule>(true);

            if (foundModules.Length > 0)
            {
                manager.modules = foundModules;
                EditorUtility.SetDirty(manager);
                Debug.Log($"[HorizonGUI] Auto-linked {foundModules.Length} modules found in hierarchy.");
            }
            else
            {
                Debug.LogWarning("[HorizonGUI] No HorizonGUIModule components found in children.");
            }
        }
    }
}