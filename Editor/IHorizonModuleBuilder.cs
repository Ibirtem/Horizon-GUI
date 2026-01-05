using UnityEngine;

namespace BlackHorizon.HorizonGUI.Editor
{
    /// <summary>
    /// A contract for any module that wants to be part of the Horizon GUI.
    /// </summary>
    public interface IHorizonModuleBuilder
    {
        string ModuleName { get; }
        string IconName { get; }
        
        /// <summary>
        /// Creates page content and returns the module script (UdonSharpBehaviour).
        /// </summary>
        HorizonGUIModule BuildPage(GameObject container);
    }
}