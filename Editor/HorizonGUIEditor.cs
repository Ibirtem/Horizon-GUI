using UnityEngine;
using UnityEditor;
using BlackHorizon.HorizonGUI.Editor;

namespace BlackHorizon.HorizonGUI
{
    [CustomEditor(typeof(HorizonGUIManager))]
    public class HorizonGUIEditor : UnityEditor.Editor
    {
        private SerializedProperty _logicScriptsProp;
        private SerializedProperty _overlayContainerProp;

        private void OnEnable()
        {
            _logicScriptsProp = serializedObject.FindProperty("logicScripts");
            _overlayContainerProp = serializedObject.FindProperty("overlayContainer");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. HEADER
            HorizonEditorUtils.DrawHorizonHeader("GUI MANAGER", target);

            // 2. SYSTEM REFERENCES
            HorizonEditorUtils.DrawSectionHeader("SYSTEM REFERENCES");
            EditorGUILayout.PropertyField(_logicScriptsProp, true);
            EditorGUILayout.PropertyField(_overlayContainerProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}