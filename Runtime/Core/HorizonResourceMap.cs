using UnityEngine;
using System.Collections.Generic;

namespace BlackHorizon.HorizonGUI
{
    /// <summary>
    /// Configuration asset for resolving string references (HTML 'src') to actual Unity assets.
    /// Improves build performance and prevents naming conflicts by restricting search scope.
    /// </summary>
    [CreateAssetMenu(fileName = "HorizonResources", menuName = "Horizon/Resource Map")]
    public class HorizonResourceMap : ScriptableObject
    {
        [Header("Search Configuration")]
        [Tooltip("List of project folders to search for icons (relative to project root).\n" +
                 "Example: 'Assets/Horizon GUI/Textures'\n" +
                 "Leaving this empty while using a Map will result in NO files being found (Strict Mode).")]
        public List<string> searchFolders = new List<string>();

        [Header("Explicit Overrides")]
        [Tooltip("Directly map a short name used in HTML to a specific Sprite asset.\n" +
                 "Overrides take priority over folder search.")]
        public List<IconMapping> overrides = new List<IconMapping>();

        [System.Serializable]
        public struct IconMapping
        {
            [Tooltip("The string used in <icon src='...'>")]
            public string key;
            [Tooltip("The actual sprite to load.")]
            public Sprite sprite;
        }

        /// <summary>
        /// Helper to find an explicit override quickly.
        /// </summary>
        public Sprite GetOverride(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var mapping in overrides)
            {
                if (mapping.key == key) return mapping.sprite;
            }
            return null;
        }
    }
}