using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

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

        private const string USER_TEMPLATES_PATH = "Assets/Horizon GUI";
        private const string DEFAULT_RESOURCES_NAME = "Horizon_Default_Resources";

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
            if (!Directory.Exists(USER_TEMPLATES_PATH))
            {
                Directory.CreateDirectory(USER_TEMPLATES_PATH);
                AssetDatabase.Refresh();
            }
        }

        private static HorizonResourceMap GetOrCreateDefaultResourceMap()
        {
            string assetPath = $"{USER_TEMPLATES_PATH}/{DEFAULT_RESOURCES_NAME}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<HorizonResourceMap>(assetPath);
            if (existing != null) return existing;

            var newMap = ScriptableObject.CreateInstance<HorizonResourceMap>();
            newMap.searchFolders = new List<string>();

            string[] guids = AssetDatabase.FindAssets("HorizonGUIManager");
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);

                string coreFolder = Path.GetDirectoryName(scriptPath);
                string runtimeFolder = Path.GetDirectoryName(coreFolder);

                string texturesPath = Path.Combine(runtimeFolder, "Textures").Replace("\\", "/");
                newMap.searchFolders.Add(texturesPath);

                string rootFolder = Path.GetDirectoryName(runtimeFolder);
                string templatesPath = Path.Combine(rootFolder, "Editor/Templates").Replace("\\", "/");

                if (templatesPath != USER_TEMPLATES_PATH)
                {
                    newMap.searchFolders.Add(templatesPath);
                }
            }

            newMap.searchFolders.Add(USER_TEMPLATES_PATH);

            AssetDatabase.CreateAsset(newMap, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return newMap;
        }

        private static TextAsset GetOrCopyAsset(string fileName, string extension)
        {
            string destPath = $"{USER_TEMPLATES_PATH}/{fileName}{extension}";

            TextAsset existingInDest = AssetDatabase.LoadAssetAtPath<TextAsset>(destPath);
            if (existingInDest != null) return existingInDest;

            string[] guids = AssetDatabase.FindAssets(fileName);
            if (guids.Length == 0) return null;

            string sourcePath = AssetDatabase.GUIDToAssetPath(guids[0]);

            if (Path.GetFullPath(sourcePath) == Path.GetFullPath(destPath))
            {
                return AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
            }

            if (AssetDatabase.CopyAsset(sourcePath, destPath))
            {
                AssetDatabase.Refresh();
                return AssetDatabase.LoadAssetAtPath<TextAsset>(destPath);
            }

            return null;
        }
    }
}