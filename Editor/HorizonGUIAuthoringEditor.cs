using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using BlackHorizon.HorizonGUI.Editor;
using BlackHorizon.HorizonGUI.Editor.Parsing;
using UdonSharp;
using System.Collections.Generic;

namespace BlackHorizon.HorizonGUI
{
    /// <summary>
    /// Custom Inspector for HorizonGUIAuthoring that provides the main entry point for compiling HTML/CSS into Unity UI.
    /// Handles Canvas generation, visual layering, layout rebuilding, and Udon logic binding.
    /// </summary>
    [CustomEditor(typeof(HorizonGUIAuthoring))]
    public class HorizonGUIAuthoringEditor : UnityEditor.Editor
    {
        private const string GENERATED_CANVAS_NAME = "GeneratedUI_Canvas";
        private const float DEFAULT_CANVAS_WIDTH = 1000f;
        private const float DEFAULT_CANVAS_HEIGHT = 600f;

        private SerializedProperty _htmlFileProp;
        private SerializedProperty _cssFileProp;
        private SerializedProperty _backingLogicProp;
        private SerializedProperty _clearOnBuildProp;

        private void OnEnable()
        {
            _htmlFileProp = serializedObject.FindProperty("htmlFile");
            _cssFileProp = serializedObject.FindProperty("cssFile");
            _backingLogicProp = serializedObject.FindProperty("backingLogic");
            _clearOnBuildProp = serializedObject.FindProperty("clearOnBuild");
        }

        /// <summary>
        /// Renders the custom inspector. 
        /// Provides a simplified interface for template management and logic initialization.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            HorizonGUIAuthoring authoring = (HorizonGUIAuthoring)target;

            // 1. HEADER
            HorizonEditorUtils.DrawHorizonHeader("UI COMPILER", this);

            // 2. RESOURCE MAP
            SerializedProperty resMapProp = serializedObject.FindProperty("resourceMap");
            EditorGUILayout.PropertyField(resMapProp);
            if (resMapProp.objectReferenceValue == null)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
                if (GUILayout.Button("Auto-Create Resource Map"))
                {
                    HorizonGUIBuilder.SetupDefaultTemplates(authoring);
                    serializedObject.Update();
                }
                GUI.backgroundColor = Color.white;
            }

            // 3. TEMPLATE SOURCE
            HorizonEditorUtils.DrawSectionHeader("TEMPLATE SOURCE");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_htmlFileProp, new GUIContent("HTML Layout"));
            EditorGUILayout.PropertyField(_cssFileProp, new GUIContent("CSS Styles"));
            EditorGUILayout.EndVertical();

            // 4. LOGIC & BINDING
            HorizonEditorUtils.DrawSectionHeader("LOGIC & BINDING");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "System is ready. Logic scripts are automatically detected from this GameObject and its children during compilation.",
                MessageType.Info
            );

            EditorGUILayout.Space(2);

            // THE MISSING BUTTON
            GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
            if (GUILayout.Button("Initialize Dashboard Environment", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Horizon Setup",
                    "This will assign default templates AND create logic objects (Home, Weather, About) in this hierarchy.\n\nProceed?",
                    "Yes", "Cancel"))
                {
                    HorizonGUIBuilder.SetupDashboardEnvironment(authoring);
                    serializedObject.Update();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();

            // 5. SETTINGS
            HorizonEditorUtils.DrawSectionHeader("BUILD SETTINGS");
            EditorGUILayout.PropertyField(_clearOnBuildProp);

            EditorGUILayout.Space(15);

            // 6. ACTION
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

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Orchestrates the construction of the UI system.
        /// Processes markup, applies styles, sets up the physical Canvas, and links Udon logic.
        /// </summary>
        private void BuildInterface(HorizonGUIAuthoring authoring)
        {
            HorizonGUIFactory.EnsureEventSystemInside(authoring.gameObject);

            HorizonNode rootNode = HorizonMarkupParser.Parse(authoring.htmlFile.text);
            HorizonStyleSheet styleSheet = new HorizonStyleSheet();
            if (authoring.cssFile != null)
                styleSheet = HorizonCSSParser.Parse(authoring.cssFile.text);

            GameObject canvasObj = PrepareCanvasRoot(authoring);
            RectTransform rootRect = canvasObj.GetComponent<RectTransform>();

            ClearExistingGeneratedUI(canvasObj);
            SetupVisualLayers(canvasObj, out GameObject contentRoot);

            // --- LOGIC DISCOVERY PHASE ---
            List<UdonSharpBehaviour> logicScripts = CollectLogicScripts(authoring);
            Debug.Log($"<color=#33FF33>[Horizon]</color> Discovered <b>{logicScripts.Count}</b> logic scripts for binding.");

            var manager = authoring.GetComponent<HorizonGUIManager>();
            if (manager != null)
            {
                var binder = new HorizonGUIFactory.HorizonLogicBinder(manager);
                binder.BindArray("modules", logicScripts);
                binder.Apply();
                EditorUtility.SetDirty(manager);
                Debug.Log($"<color=#33FF33>[Horizon]</color> Populated <b>HorizonGUIManager.modules</b> with discovered scripts.");
            }

            // --- BUILD & INJECT ---
            HorizonCompiler.BuildInterface(contentRoot, rootNode, styleSheet, authoring.resourceMap, logicScripts);

            FinalizeLayoutAndPhysics(canvasObj, rootRect);

            int errors = HorizonCompiler.ValidationErrors;
            if (errors > 0)
            {
                Debug.LogWarning($"<color=yellow><b>[Horizon]</b></color> Build finished with <b>{errors} validation errors</b>. Please check the Console for details.");
            }
            else
            {
                Debug.Log($"<color=#33FF33>[Horizon]</color> Build for '{authoring.name}' completed successfully.");
            }
        }

        /// <summary>
        /// Discovery phase: Scans the local hierarchy for any UdonSharpBehaviour components.
        /// These scripts are used as targets for Dependency Injection and Event Wiring.
        /// </summary>
        /// <param name="authoring">The root authoring component.</param>
        /// <returns>A list of discovered logic scripts.</returns>
        private List<UdonSharpBehaviour> CollectLogicScripts(HorizonGUIAuthoring authoring)
        {
            var results = new List<UdonSharpBehaviour>();
            var allScripts = authoring.GetComponentsInChildren<UdonSharpBehaviour>(true);

            foreach (var script in allScripts)
            {
                if (script == null) continue;
                if (script is HorizonGUIAuthoring) continue;

                results.Add(script);
            }

            return results;
        }

        /// <summary>
        /// Initializes or retrieves the WorldSpace Canvas and configures its root transform.
        /// </summary>
        private GameObject PrepareCanvasRoot(HorizonGUIAuthoring authoring)
        {
            Transform container = authoring.transform.Find(GENERATED_CANVAS_NAME);
            if (container == null)
            {
                GameObject go = new GameObject(GENERATED_CANVAS_NAME);
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

            RectTransform rt = canvasObj.GetComponent<RectTransform>();
            rt.localScale = Vector3.one * 0.001f;
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = new Vector2(DEFAULT_CANVAS_WIDTH, DEFAULT_CANVAS_HEIGHT);

            return canvasObj;
        }

        /// <summary>
        /// Clears all children from the generated canvas to prepare for a fresh build.
        /// </summary>
        private void ClearExistingGeneratedUI(GameObject canvasObj)
        {
            while (canvasObj.transform.childCount > 0)
                DestroyImmediate(canvasObj.transform.GetChild(0).gameObject);
        }

        /// <summary>
        /// Creates the background layers: Stencil Mask, Glass Blur, and the Content container.
        /// Applies consistent 40px rounding (Multiplier 1.6) to match the standard theme.
        /// </summary>
        private void SetupVisualLayers(GameObject canvasObj, out GameObject contentRoot)
        {
            Sprite bgSprite = HorizonGUIFactory.GetOrGenerateRoundedSprite();
            float windowMultiplier = 1.6f;

            // 1. Global Background (The Mask)
            GameObject bgObj = HorizonGUIFactory.CreateBlock("Global_Background", canvasObj);
            HorizonGUIFactory.Stretch(bgObj);

            Image maskImg = bgObj.AddComponent<Image>();
            maskImg.sprite = bgSprite;
            maskImg.type = Image.Type.Sliced;
            maskImg.raycastTarget = false;
            maskImg.color = Color.white;
            maskImg.pixelsPerUnitMultiplier = windowMultiplier;

            Mask mask = bgObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // 2. Glass Layer
            GameObject glass = HorizonGUIFactory.CreatePanel("Glass_Layer", bgObj);
            Image glassImg = glass.GetComponent<Image>();
            glassImg.color = HorizonGUIFactory.ColorGlassDark;
            glassImg.sprite = bgSprite;

            HorizonGUIFactory.Stretch(glass);
            glassImg.material = HorizonGUIFactory.GetGlassMaterial();
            glass.transform.localPosition = new Vector3(0, 0, -0.5f);
            glassImg.raycastTarget = false;
            glassImg.pixelsPerUnitMultiplier = windowMultiplier;

            contentRoot = HorizonGUIFactory.CreateBlock("HTML_Root", canvasObj);
            contentRoot.transform.localPosition = new Vector3(0, 0, -1.0f);
            HorizonGUIFactory.Stretch(contentRoot);
        }

        /// <summary>
        /// Forces layout recalculation and synchronizes BoxColliders with RectTransform bounds.
        /// </summary>
        private void FinalizeLayoutAndPhysics(GameObject canvasObj, RectTransform rootRect)
        {
            Canvas.ForceUpdateCanvases();

            foreach (var layout in canvasObj.GetComponentsInChildren<LayoutGroup>())
                LayoutRebuilder.ForceRebuildLayoutImmediate(layout.GetComponent<RectTransform>());

            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

            var allSelectables = new System.Collections.Generic.List<Selectable>();
            allSelectables.AddRange(canvasObj.GetComponentsInChildren<Button>());
            allSelectables.AddRange(canvasObj.GetComponentsInChildren<Toggle>());

            foreach (var uiElement in allSelectables)
            {
                BoxCollider col = uiElement.GetComponent<BoxCollider>();
                RectTransform rt = uiElement.GetComponent<RectTransform>();

                if (col != null && rt != null)
                {
                    col.size = new Vector3(rt.rect.width, rt.rect.height, 10f);
                    col.center = Vector3.zero;
                }
            }

            HorizonGUIFactory.AddInteraction(canvasObj, rootRect.sizeDelta);
        }
    }
}