using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using BlackHorizon.HorizonGUI.Editor;
using BlackHorizon.HorizonGUI.Editor.Parsing;

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

        [System.Serializable]
        private class PackageInfo { public string version; }

        public override void OnInspectorGUI()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };

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
        /// Orchestrates the construction of the UI system.
        /// Processes markup, applies styles, sets up the physical Canvas, and links Udon logic.
        /// </summary>
        private void BuildInterface(HorizonGUIAuthoring authoring)
        {
            if (!ValidateBackingLogic(authoring))
            {
                EditorUtility.DisplayDialog("Horizon GUI", "No Backing Logic found! Add HorizonGUIManager to this object before compiling.", "OK");
                return;
            }

            ValidateBackingLogic(authoring);

            HorizonGUIFactory.EnsureEventSystemInside(authoring.gameObject);

            HorizonNode rootNode = HorizonMarkupParser.Parse(authoring.htmlFile.text);
            HorizonStyleSheet styleSheet = new HorizonStyleSheet();
            if (authoring.cssFile != null)
                styleSheet = HorizonCSSParser.Parse(authoring.cssFile.text);

            GameObject canvasObj = PrepareCanvasRoot(authoring);
            RectTransform rootRect = canvasObj.GetComponent<RectTransform>();

            ClearExistingGeneratedUI(canvasObj);

            SetupVisualLayers(canvasObj, out GameObject contentRoot);

            HorizonCompiler.BuildInterface(contentRoot, rootNode, styleSheet, authoring.backingLogic);

            FinalizeLayoutAndPhysics(canvasObj, rootRect);

            PerformLogicBinding(authoring, contentRoot);

            HorizonGUIBaker.BakeInterface(canvasObj.name);

            Debug.Log($"<color=#33FF33>[Horizon]</color> Build for '{authoring.name}' completed successfully.");
        }

        /// <summary>
        /// Ensures a valid UdonSharpBehaviour is assigned.
        /// Returns false if no manager is found to prevent broken builds.
        /// </summary>
        private bool ValidateBackingLogic(HorizonGUIAuthoring authoring)
        {
            if (authoring.backingLogic == null)
            {
                authoring.backingLogic = authoring.GetComponent<UdonSharp.UdonSharpBehaviour>();
                if (authoring.backingLogic != null)
                {
                    EditorUtility.SetDirty(authoring);
                }
                else
                {
                    Debug.LogError("[Horizon] CRITICAL: No Backing Logic found! Please add a HorizonGUIManager component.");
                    return false;
                }
            }
            return true;
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
        /// </summary>
        private void SetupVisualLayers(GameObject canvasObj, out GameObject contentRoot)
        {
            Sprite bgSprite = HorizonGUIFactory.GetOrGenerateRoundedSprite();

            GameObject bgObj = HorizonGUIFactory.CreateBlock("Global_Background", canvasObj);
            HorizonGUIFactory.Stretch(bgObj);

            Image maskImg = bgObj.AddComponent<Image>();
            maskImg.sprite = bgSprite;
            maskImg.type = Image.Type.Sliced;
            maskImg.raycastTarget = false;
            maskImg.color = Color.white;

            Mask mask = bgObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject glass = HorizonGUIFactory.CreatePanel("Glass_Layer", bgObj, HorizonGUIFactory.ColorGlassDark, bgSprite);
            HorizonGUIFactory.Stretch(glass);
            glass.GetComponent<Image>().material = HorizonGUIFactory.GetGlassMaterial();
            glass.transform.localPosition = new Vector3(0, 0, -0.5f);
            glass.GetComponent<Image>().raycastTarget = false;

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

        /// <summary>
        /// Automatically links generated modules and external systems (like Weather) to the HorizonGUIManager.
        /// </summary>
        private void PerformLogicBinding(HorizonGUIAuthoring authoring, GameObject contentRoot)
        {
            var manager = authoring.GetComponent<HorizonGUIManager>();
            if (manager == null) return;

            var binder = new HorizonGUIFactory.HorizonLogicBinder(manager);

            var foundModules = contentRoot.GetComponentsInChildren<HorizonGUIModule>(true);
            if (foundModules.Length > 0)
                binder.BindArray("modules", new System.Collections.Generic.List<HorizonGUIModule>(foundModules));

            var wSys = Object.FindObjectOfType<BlackHorizon.HorizonWeatherTime.WeatherTimeSystem>();
            if (wSys != null)
            {
                binder.Bind("weatherSystem", wSys);
                binder.BindVal("weatherVersion", GetPackageVersion("com.blackhorizon.horizonweathertime"));
            }

            binder.Apply();
        }

        /// <summary>
        /// Retrieves the version string from a package's package.json file.
        /// Logs a warning if the file cannot be read.
        /// </summary>
        private string GetPackageVersion(string packageName)
        {
            try
            {
                string path = $"Packages/{packageName}/package.json";
                if (System.IO.File.Exists(path))
                {
                    var pkg = JsonUtility.FromJson<PackageInfo>(System.IO.File.ReadAllText(path));
                    return pkg.version;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Horizon] Failed to read version for '{packageName}': {ex.Message}");
            }
            return "?.?.?";
        }
    }
}