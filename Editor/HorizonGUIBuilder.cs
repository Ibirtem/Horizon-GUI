using UnityEngine;
using UnityEditor;
using System.IO;

namespace BlackHorizon.HorizonGUI.Editor
{
    /// <summary>
    /// Entry point for creating the Horizon UI System in the Unity Hierarchy.
    /// Handles default template copying and component initialization.
    /// </summary>
    public static class HorizonGUIBuilder
    {
        private const string SYSTEM_ROOT_NAME = "Horizon UI System";
        private const string DEFAULT_LAYOUT_NAME = "Horizon_Default_Layout";
        private const string DEFAULT_THEME_NAME = "Horizon_Default_Theme";
        private const string USER_TEMPLATE_PATH = "Assets/Horizon GUI/Templates";

        /// <summary>
        /// Creates a new Horizon UI System GameObject with the necessary management and authoring components.
        /// Linked to the Unity GameObject menu (Right-click in Hierarchy -> Horizon -> Create UI System).
        /// </summary>
        /// <param name="menuCommand">Context provided by the Unity Menu.</param>
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

            Debug.Log("<color=#33FF33>[Horizon]</color> UI System initialized with default templates.");
        }

        /// <summary>
        /// Finds the default templates in the package, copies them to the user's project if needed,
        /// and assigns them to the Authoring component.
        /// </summary>
        public static void SetupDefaultTemplates(HorizonGUIAuthoring authoring)
        {
            EnsureUserDirectoryExists();

            TextAsset html = GetOrCopyAsset(DEFAULT_LAYOUT_NAME, ".html");
            TextAsset css = GetOrCopyAsset(DEFAULT_THEME_NAME, ".css");

            if (html != null) authoring.htmlFile = html;
            if (css != null) authoring.cssFile = css;

            EditorUtility.SetDirty(authoring);
        }

        private static void EnsureUserDirectoryExists()
        {
            if (!Directory.Exists(USER_TEMPLATE_PATH))
            {
                Directory.CreateDirectory(USER_TEMPLATE_PATH);
                AssetDatabase.Refresh();
            }
        }

        private static TextAsset GetOrCopyAsset(string fileName, string extension)
        {
            string destPath = $"{USER_TEMPLATE_PATH}/{fileName}{extension}";

            TextAsset existing = AssetDatabase.LoadAssetAtPath<TextAsset>(destPath);
            if (existing != null) return existing;

            string[] guids = AssetDatabase.FindAssets(fileName);
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[Horizon] Could not find default template '{fileName}' in package.");
                return null;
            }

            string sourcePath = AssetDatabase.GUIDToAssetPath(guids[0]);

            if (AssetDatabase.CopyAsset(sourcePath, destPath))
            {
                AssetDatabase.Refresh();
                return AssetDatabase.LoadAssetAtPath<TextAsset>(destPath);
            }

            return null;
        }
    }
}