using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UdonSharpEditor;

namespace BlackHorizon.HorizonGUI.Editor
{
    /// <summary>
    /// Entry point for creating the Horizon UI System.
    /// Handles default template copying, resource map generation, and component initialization.
    /// </summary>
    public static class HorizonGUIBuilder
    {
        private const string SYSTEM_ROOT_NAME = "Horizon UI System";

        private const string DEFAULT_LAYOUT_NAME = "Horizon_Default_Layout";
        private const string DEFAULT_THEME_NAME = "Horizon_Default_Theme";
        private const string DEFAULT_RESOURCES_NAME = "Horizon_Default_Resources";

        private const string DASHBOARD_ROOT = "Assets/Horizon GUI/Horizon-Dashboard";
        private const string DASHBOARD_EDITOR_PATH = "Assets/Horizon GUI/Horizon-Dashboard/Editor";

        [MenuItem("GameObject/Horizon/Create UI System", false, 10)]
        public static void CreateNewSystem(MenuCommand menuCommand)
        {
            GameObject systemRoot = new GameObject(SYSTEM_ROOT_NAME);

            GameObjectUtility.SetParentAndAlign(systemRoot, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(systemRoot, "Create " + systemRoot.name);

            Selection.activeObject = systemRoot;

            systemRoot.AddComponent<HorizonGUIManager>();
            var authoring = systemRoot.AddComponent<HorizonGUIAuthoring>();

            SetupDefaultTemplates(authoring);

            Debug.Log("<color=#33FF33>[Horizon]</color> UI System initialized.");
        }

        /// <summary>
        /// Full setup routine:
        /// 1. Copies/Assigns default HTML/CSS templates.
        /// 2. Creates the necessary persistent GameObjects for logic (Home, Weather, About).
        /// 3. Attaches the corresponding UdonSharpBehaviours to them.
        /// </summary>
        public static void SetupDashboardEnvironment(HorizonGUIAuthoring authoring)
        {
            SetupDefaultTemplates(authoring);

            GameObject root = authoring.gameObject;

            CreateLogicModule(root, "Logic_Home", "HorizonGUI_HomeModule");
            CreateLogicModule(root, "Logic_Weather", "HorizonGUI_WeatherModule");
            CreateLogicModule(root, "Logic_About", "HorizonGUI_AboutModule");

            EditorUtility.SetDirty(root);

            Debug.Log("<color=#33FF33>[Horizon]</color> Dashboard Environment initialized. Discovery will now find these scripts.");
        }

        /// <summary>
        /// Helper to create a child GameObject and attach a specific Udon script by name.
        /// Prevents ghost objects in hierarchy if script attachment fails.
        /// </summary>
        private static void CreateLogicModule(GameObject parent, string objectName, string scriptTypeName)
        {
            Transform existing = parent.transform.Find(objectName);
            if (existing != null) return;

            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent.transform, false);
            go.transform.SetSiblingIndex(0);

            var script = HorizonGUIFactory.AttachLogicByString(go, scriptTypeName);

            if (script == null)
            {
                Debug.LogWarning($"[HorizonBuilder] Could not find or attach script '{scriptTypeName}' to '{objectName}'. Destroying ghost object.");
                Object.DestroyImmediate(go);
                return;
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Horizon Module");
        }

        /// <summary>
        /// Ensures templates and resource maps exist in the project, then assigns them to the authoring component.
        /// </summary>
        public static void SetupDefaultTemplates(HorizonGUIAuthoring authoring)
        {
            EnsureTemplateDirectoryExists();

            TextAsset html = GetOrCopyAsset(DEFAULT_LAYOUT_NAME, ".html");
            TextAsset css = GetOrCopyAsset(DEFAULT_THEME_NAME, ".css");

            if (html != null) authoring.htmlFile = html;
            if (css != null) authoring.cssFile = css;

            HorizonResourceMap resources = GetOrCreateDefaultResourceMap();
            authoring.resourceMap = resources;

            EditorUtility.SetDirty(authoring);
        }

        private static void EnsureTemplateDirectoryExists()
        {
            bool created = false;

            if (!Directory.Exists(DASHBOARD_ROOT))
            {
                Directory.CreateDirectory(DASHBOARD_ROOT);
                created = true;
            }

            if (!Directory.Exists(DASHBOARD_EDITOR_PATH))
            {
                Directory.CreateDirectory(DASHBOARD_EDITOR_PATH);
                created = true;
            }

            if (created)
            {
                AssetDatabase.Refresh();
            }
        }

        private static HorizonResourceMap GetOrCreateDefaultResourceMap()
        {
            string assetPath = $"{DASHBOARD_EDITOR_PATH}/{DEFAULT_RESOURCES_NAME}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<HorizonResourceMap>(assetPath);
            if (existing != null) return existing;

            if (!Directory.Exists(DASHBOARD_EDITOR_PATH))
            {
                Debug.LogError($"[Horizon] Critical Error: Target directory does not exist: {DASHBOARD_EDITOR_PATH}");
                return null;
            }

            var newMap = ScriptableObject.CreateInstance<HorizonResourceMap>();
            newMap.searchFolders = new List<string>();

            string[] guids = AssetDatabase.FindAssets("HorizonGUIManager");
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                string coreRuntimeFolder = Path.GetDirectoryName(scriptPath);
                string runtimeFolder = Path.GetDirectoryName(coreRuntimeFolder);
                string coreFolder = Path.GetDirectoryName(runtimeFolder);

                string texturesPath = Path.Combine(runtimeFolder, "Textures").Replace("\\", "/");
                newMap.searchFolders.Add(texturesPath);
            }

            newMap.searchFolders.Add(DASHBOARD_EDITOR_PATH);

            string resPath = DASHBOARD_ROOT + "/Resources";
            if (Directory.Exists(resPath)) newMap.searchFolders.Add(resPath);

            AssetDatabase.CreateAsset(newMap, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return newMap;
        }

        private static TextAsset GetOrCopyAsset(string fileName, string extension)
        {
            string destPath = $"{DASHBOARD_EDITOR_PATH}/{fileName}{extension}";

            TextAsset existingInDest = AssetDatabase.LoadAssetAtPath<TextAsset>(destPath);
            if (existingInDest != null) return existingInDest;

            string[] guids = AssetDatabase.FindAssets(fileName);
            if (guids.Length == 0) return null;

            string sourcePath = AssetDatabase.GUIDToAssetPath(guids[0]);

            if (AssetDatabase.CopyAsset(sourcePath, destPath))
            {
                AssetDatabase.Refresh();
                return AssetDatabase.LoadAssetAtPath<TextAsset>(destPath);
            }

            return AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
        }
    }
}