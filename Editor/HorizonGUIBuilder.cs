using UnityEngine;
using UnityEditor;

namespace BlackHorizon.HorizonGUI.Editor
{
    /// <summary>
    /// Entry point for creating the Horizon UI System in the Unity Hierarchy.
    /// This class provides menu items to initialize the system and set up default authoring components.
    /// </summary>
    public static class HorizonGUIBuilder
    {
        private const string SYSTEM_ROOT_NAME = "Horizon UI System";

        /// <summary>
        /// Creates a new Horizon UI System GameObject with the necessary management and authoring components.
        /// Linked to the Unity GameObject menu (Right-click in Hierarchy -> Horizon -> Create UI System).
        /// </summary>
        /// <param name="menuCommand">Context provided by the Unity Menu.</param>
        [MenuItem("GameObject/Horizon/Create UI System", false, 10)]
        public static void CreateNewSystem(MenuCommand menuCommand)
        {
            GameObject systemRoot = new GameObject(SYSTEM_ROOT_NAME);

            // Alignment and Undo registration
            GameObjectUtility.SetParentAndAlign(systemRoot, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(systemRoot, "Create " + systemRoot.name);

            Selection.activeObject = systemRoot;

            // Attachment of core logic and authoring components
            systemRoot.AddComponent<HorizonGUIManager>();
            systemRoot.AddComponent<HorizonGUIAuthoring>();

            Debug.Log("<color=#33FF33>[Horizon]</color> UI System initialized. Please assign your HTML/CSS files in the Inspector.");
        }
    }
}