using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using BlackHorizon.HorizonGUI.Editor;
using BlackHorizon.HorizonGUI.Editor.Parsing;
using UdonSharp;
using System.Reflection;
using System.Collections.Generic;
using BlackHorizon.HorizonGUI.Services;

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
        private SerializedProperty _resourceMapProp;
        private SerializedProperty _clearOnBuildProp;

        private void OnEnable()
        {
            _htmlFileProp = serializedObject.FindProperty("htmlFile");
            _cssFileProp = serializedObject.FindProperty("cssFile");
            _resourceMapProp = serializedObject.FindProperty("resourceMap");
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

            // 2. TEMPLATES & RESOURCES
            HorizonEditorUtils.DrawSectionHeader("TEMPLATES & RESOURCES");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.PropertyField(_htmlFileProp, new GUIContent("HTML Layout"));
            EditorGUILayout.PropertyField(_cssFileProp, new GUIContent("CSS Styles"));
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(_resourceMapProp, new GUIContent("Resource Map"));

            if (_resourceMapProp.objectReferenceValue == null)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
                if (GUILayout.Button("Auto-Create Resource Map"))
                {
                    HorizonGUIBuilder.SetupDefaultTemplates(authoring);
                    serializedObject.Update();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndVertical();

            // 3. QUICK SETUP
            HorizonEditorUtils.DrawSectionHeader("QUICK SETUP");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "Start with a fully functional example. This installs default templates and configures the Home, Weather, and Post-Processing modules automatically.",
                MessageType.Info
            );

            EditorGUILayout.Space(2);

            GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
            if (GUILayout.Button("Setup Default Dashboard", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Horizon Setup",
                    "This will assign default templates AND create logic objects (Home, Weather, Post-Processing) in this hierarchy.\n\nProceed?",
                    "Yes", "Cancel"))
                {
                    HorizonGUIBuilder.SetupDashboardEnvironment(authoring);
                    serializedObject.Update();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();

            // 4. SETTINGS
            HorizonEditorUtils.DrawSectionHeader("BUILD SETTINGS");
            EditorGUILayout.PropertyField(_clearOnBuildProp);

            EditorGUILayout.Space(15);

            // 5. ACTION
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

        #region Core Build Logic

        /// <summary>
        /// Orchestrates the construction of the UI system.
        /// </summary>
        private void BuildInterface(HorizonGUIAuthoring authoring)
        {
            // 1. Parse Templates
            HorizonNode rootNode = HorizonMarkupParser.Parse(authoring.htmlFile.text);
            HorizonStyleSheet styleSheet = authoring.cssFile != null ?
                HorizonCSSParser.Parse(authoring.cssFile.text) : new HorizonStyleSheet();

            // 2. Prepare Hierarchy & Layers
            HorizonGUIFactory.EnsureEventSystemInside(authoring.gameObject);
            GameObject canvasObj = PrepareCanvasRoot(authoring);
            RectTransform rootRect = canvasObj.GetComponent<RectTransform>();

            ClearExistingGeneratedUI(canvasObj);
            SetupVisualLayers(canvasObj, out GameObject contentRoot);

            // 3. Discover & Configure Logic
            List<UdonSharpBehaviour> diScripts = ConfigureLogicAndServices(authoring);

            // 4. Compile UI & Inject Dependencies
            HorizonCompiler.BuildInterface(contentRoot, rootNode, styleSheet, authoring.resourceMap, diScripts);

            // 5. Finalize Physics & Layout
            FinalizeLayoutAndPhysics(canvasObj, rootRect);
            LogBuildResults(authoring.name);
        }

        /// <summary>
        /// Handles the discovery of all user scripts, injects singletons (AvatarManager), 
        /// and configures the Manager's routing list.
        /// </summary>
        private List<UdonSharpBehaviour> ConfigureLogicAndServices(HorizonGUIAuthoring authoring)
        {
            List<UdonSharpBehaviour> diScripts = CollectLogicScriptsForDI(authoring);
            Debug.Log($"<color=#33FF33>[Horizon]</color> Discovered <b>{diScripts.Count}</b> logic scripts for DI.");

            HorizonAvatarManager avatarService = HorizonGUIFactory.EnsureAvatarService(authoring.gameObject);
            InjectService(diScripts, avatarService);

            ConfigureRoutingManager(authoring, diScripts);

            return diScripts;
        }

        /// <summary>
        /// Filters out core framework components and registers only user scripts 
        /// into the HorizonGUIManager for OnShow/OnHide event routing.
        /// </summary>
        private void ConfigureRoutingManager(HorizonGUIAuthoring authoring, List<UdonSharpBehaviour> diScripts)
        {
            var manager = authoring.GetComponent<HorizonGUIManager>();
            if (manager == null) return;

            var routingScripts = new List<UdonSharpBehaviour>();
            foreach (var script in diScripts)
            {
                if (IsSystemComponent(script)) continue;
                routingScripts.Add(script);
            }

            var binder = new HorizonGUIFactory.HorizonLogicBinder(manager);
            binder.BindArray("logicScripts", routingScripts);
            binder.Apply();

            EditorUtility.SetDirty(manager);
            Debug.Log($"<color=#33FF33>[Horizon]</color> Populated <b>HorizonGUIManager.logicScripts</b> with {routingScripts.Count} scripts.");
        }

        /// <summary>
        /// Collects all scripts needed for u-bind and u-click operations.
        /// Excludes the Authoring script itself.
        /// </summary>
        private List<UdonSharpBehaviour> CollectLogicScriptsForDI(HorizonGUIAuthoring authoring)
        {
            var results = new List<UdonSharpBehaviour>();
            var allScripts = authoring.GetComponentsInChildren<UdonSharpBehaviour>(true);

            foreach (var script in allScripts)
            {
                if (script == null || script is HorizonGUIAuthoring) continue;
                results.Add(script);
            }

            return results;
        }

        /// <summary>
        /// Identifies whether a script is an internal Horizon framework component.
        /// </summary>
        private bool IsSystemComponent(UdonSharpBehaviour script)
        {
            return script is HorizonGUIManager ||
                   script is HorizonAvatarManager ||
                   script is HorizonDataGrid ||
                   script is HorizonSmartSlot ||
                   script is HorizonEventCaller ||
                   script is HorizonChannelController;
        }

        /// <summary>
        /// Logs the final outcome of the build process.
        /// </summary>
        private void LogBuildResults(string authoringName)
        {
            int errors = HorizonCompiler.ValidationErrors;
            if (errors > 0)
            {
                Debug.LogWarning($"<color=yellow><b>[Horizon]</b></color> Build finished with <b>{errors} validation errors</b>. Please check the Console for details.");
            }
            else
            {
                Debug.Log($"<color=#33FF33>[Horizon]</color> Build for '{authoringName}' completed successfully.");
            }
        }

        /// <summary>
        /// Scans all logic scripts for fields of type HorizonAvatarManager and assigns the singleton instance.
        /// </summary>
        private void InjectService(List<UdonSharpBehaviour> scripts, HorizonAvatarManager service)
        {
            if (service == null) return;

            foreach (var script in scripts)
            {
                if (script == null) continue;

                SerializedObject so = new SerializedObject(script);
                SerializedProperty prop = so.GetIterator();
                bool found = false;

                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var field = script.GetType().GetField(prop.name,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        if (field != null && field.FieldType == typeof(HorizonAvatarManager))
                        {
                            prop.objectReferenceValue = service;
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    so.ApplyModifiedProperties();
                }
            }
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

        #endregion
    }
}