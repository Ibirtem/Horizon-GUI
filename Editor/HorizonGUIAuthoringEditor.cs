using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using BlackHorizon.HorizonGUI.Editor;
using BlackHorizon.HorizonGUI.Editor.Parsing;

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
            GUILayout.Label("HORIZON UI COMPILER", headerStyle);
            GUILayout.Space(5);

            HorizonGUIAuthoring authoring = (HorizonGUIAuthoring)target;

            base.OnInspectorGUI();

            GUILayout.Space(15);

            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            if (GUILayout.Button("COMPILE INTERFACE", GUILayout.Height(40)))
            {
                if (authoring.htmlFile == null)
                {
                    EditorUtility.DisplayDialog("Horizon GUI", "Please assign an HTML file first!", "OK");
                    return;
                }

                BuildInterface(authoring);
            }
            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// Orchestrates the entire build process: Parsing, Canvas setup, Compilation, and Baking.
        /// </summary>
        private void BuildInterface(HorizonGUIAuthoring authoring)
        {
            var rootNode = HorizonMarkupParser.Parse(authoring.htmlFile.text);

            HorizonStyleSheet styleSheet = new HorizonStyleSheet();
            if (authoring.cssFile != null)
                styleSheet = HorizonCSSParser.Parse(authoring.cssFile.text);

            string rootName = "GeneratedUI_Canvas";
            Transform container = authoring.transform.Find(rootName);

            if (container == null)
            {
                GameObject go = new GameObject(rootName);
                go.transform.SetParent(authoring.transform, false);
                container = go.transform;
            }

            GameObject canvasObj = container.gameObject;

            Canvas c = canvasObj.GetComponent<Canvas>();
            if (c == null) c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;

            CanvasScaler cs = canvasObj.GetComponent<CanvasScaler>();
            if (cs == null) cs = canvasObj.AddComponent<CanvasScaler>();
            cs.dynamicPixelsPerUnit = 10;

            GraphicRaycaster gr = canvasObj.GetComponent<GraphicRaycaster>();
            if (gr == null) gr = canvasObj.AddComponent<GraphicRaycaster>();

            canvasObj.transform.localScale = Vector3.one * 0.001f;
            canvasObj.transform.localPosition = Vector3.zero;
            canvasObj.transform.localRotation = Quaternion.identity;

            var vlg = canvasObj.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = canvasObj.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;

            var fitter = canvasObj.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = canvasObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Debug.Log("<b>[Horizon]</b> Building GameObjects...");

            HorizonCompiler.BuildInterface(canvasObj, rootNode, styleSheet, authoring.backingLogic);

            Canvas.ForceUpdateCanvases();

            foreach (var layout in canvasObj.GetComponentsInChildren<LayoutGroup>())
                LayoutRebuilder.ForceRebuildLayoutImmediate(layout.GetComponent<RectTransform>());

            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasObj.GetComponent<RectTransform>());

            var buttons = canvasObj.GetComponentsInChildren<Button>();
            foreach (var btn in buttons)
            {
                BoxCollider col = btn.GetComponent<BoxCollider>();
                RectTransform rt = btn.GetComponent<RectTransform>();

                if (col != null && rt != null)
                {
                    col.size = new Vector3(rt.rect.width, rt.rect.height, 10f);
                    col.center = new Vector3(0, 0, 0);
                }
            }

            HorizonGUIFactory.AddInteraction(canvasObj, canvasObj.GetComponent<RectTransform>().sizeDelta);

            HorizonGUIBaker.BakeInterface(canvasObj.name);

            Debug.Log($"[Horizon] Build Complete. Root: {canvasObj.name}");
        }
    }
}