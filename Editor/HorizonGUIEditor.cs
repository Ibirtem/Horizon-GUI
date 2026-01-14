using UnityEngine;
using UnityEditor;
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

            serializedObject.ApplyModifiedProperties();
        }
    }
}