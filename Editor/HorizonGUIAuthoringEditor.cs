using UnityEngine;
using UnityEditor;
using BlackHorizon.HorizonGUI.Editor;

namespace BlackHorizon.HorizonGUI
{
    [CustomEditor(typeof(HorizonGUIAuthoring))]
    public class HorizonGUIAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 13;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Space(10);
            GUILayout.Label("HORIZON UI BUILDER", headerStyle);
            GUILayout.Space(5);

            HorizonGUIAuthoring authoring = (HorizonGUIAuthoring)target;

            base.OnInspectorGUI();

            GUILayout.Space(15);

            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("GENERATE INTERFACE", GUILayout.Height(40)))
            {
                BuildInterface(authoring);
            }
            GUI.backgroundColor = Color.white;
        }

        private void BuildInterface(HorizonGUIAuthoring authoring)
        {
            if (authoring.theme == null)
            {
                EditorUtility.DisplayDialog("Horizon GUI", "Please assign a Theme first!", "OK");
                return;
            }

            HorizonGUIFactory.SetThemeContext(authoring.theme);

            var manager = authoring.GetComponent<HorizonGUIManager>();
            if (manager == null)
            {
                manager = authoring.gameObject.AddComponent<HorizonGUIManager>();
            }

            HorizonGUIBuilder.RebuildOnTarget(manager);

            Debug.Log($"[HorizonGUI] Interface rebuilt using theme: {authoring.theme.name}");
        }
    }
}