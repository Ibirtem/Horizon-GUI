using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using BlackHorizon.HorizonGUI.Editor;

namespace BlackHorizon.HorizonGUI
{
    [CustomEditor(typeof(HorizonGUIManager))]
    public class HorizonGUIEditor : UnityEditor.Editor
    {
        private SerializedProperty _modulesProp;
        private SerializedProperty _overlayContainerProp;
        private SerializedProperty _clockTextProp;
        private SerializedProperty _instanceInfoTextProp;
        private SerializedProperty _playerGridProp;

        private void OnEnable()
        {
            _modulesProp = serializedObject.FindProperty("modules");
            _overlayContainerProp = serializedObject.FindProperty("overlayContainer");

            _clockTextProp = serializedObject.FindProperty("clockText");
            _instanceInfoTextProp = serializedObject.FindProperty("instanceInfoText");
            _playerGridProp = serializedObject.FindProperty("playerGrid");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            HorizonGUIManager manager = (HorizonGUIManager)target;

            // 1. HEADER
            HorizonEditorUtils.DrawHorizonHeader("GUI MANAGER", this);

            // 2. SYSTEM REFERENCES
            HorizonEditorUtils.DrawSectionHeader("SYSTEM REFERENCES");
            EditorGUILayout.PropertyField(_modulesProp, true);
            EditorGUILayout.PropertyField(_overlayContainerProp);

            // 3. AUTO-BINDINGS
            HorizonEditorUtils.DrawSectionHeader("BINDING TARGETS");
            GUI.enabled = false;
            EditorGUILayout.PropertyField(_clockTextProp);
            EditorGUILayout.PropertyField(_instanceInfoTextProp);
            EditorGUILayout.PropertyField(_playerGridProp);
            GUI.enabled = true;

            // 4. ACTIONS
            HorizonEditorUtils.DrawSectionHeader("ACTIONS");
            if (GUILayout.Button("Auto-Link Modules from Children"))
            {
                AutoLinkTabs(manager);
            }

            serializedObject.ApplyModifiedProperties();
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