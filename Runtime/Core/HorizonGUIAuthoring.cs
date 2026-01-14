using UnityEngine;
using UdonSharp;

namespace BlackHorizon.HorizonGUI
{
    [RequireComponent(typeof(HorizonGUIManager))]
    public class HorizonGUIAuthoring : MonoBehaviour
    {
        [Header("Template Source")]
        public TextAsset htmlFile;
        public TextAsset cssFile;

        [Header("Resources")]
        [Tooltip("Optional: Defines where to search for images referenced in HTML.")]
        public HorizonResourceMap resourceMap;

        [Header("Logic Binding")]
        [Tooltip("The UdonSharpBehaviour that will receive events (u-click) and variable links (u-bind).")]
        public UdonSharpBehaviour backingLogic;

        [Header("Settings")]
        public bool clearOnBuild = true;
    }
}