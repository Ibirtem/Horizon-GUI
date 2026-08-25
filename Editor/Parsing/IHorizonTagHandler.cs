using System.Collections.Generic;
using UnityEngine;

namespace BlackHorizon.HorizonGUI.Editor.Parsing
{
    /// <summary>
    /// Delegate signature for compiling an individual AST node and attaching it to a parent GameObject.
    /// </summary>
    /// <param name="node">The markup AST node to compile.</param>
    /// <param name="parent">Parent GameObject container in the visual hierarchy.</param>
    /// <param name="context">Active compilation context preserving build flags and service links.</param>
    public delegate void HorizonNodeBuilder(HorizonNode node, GameObject parent, HorizonBuildContext context);

    /// <summary>
    /// Encapsulates the execution state and shared pipeline dependencies passed through the compilation tree.
    /// Preserves template extraction state across recursive child node traversals.
    /// </summary>
    public class HorizonBuildContext
    {
        /// <summary>
        /// Gets the active computed stylesheet applied during compilation.
        /// </summary>
        public HorizonStyleSheet StyleSheet { get; }

        /// <summary>
        /// Gets the active resource map used to resolve sprite paths and explicit key mappings.
        /// </summary>
        public HorizonResourceMap ResourceMap { get; }

        /// <summary>
        /// Indicates whether the current build branch is generating an isolated template prototype (e.g. for pooling in DataGrid).
        /// When true, elements with 'u-bind' are tagged with name suffixes rather than registered directly into logic DI tables.
        /// </summary>
        public bool IsBuildingTemplate { get; set; }

        /// <summary>
        /// Gets the delegate used to recursively invoke node compilation.
        /// </summary>
        public HorizonNodeBuilder NodeBuilder { get; }

        /// <summary>
        /// Initializes a new compilation context instance.
        /// </summary>
        /// <param name="styleSheet">Parsed stylesheet container.</param>
        /// <param name="resourceMap">Optional asset resolver map.</param>
        /// <param name="isBuildingTemplate">Initial template generation state.</param>
        /// <param name="nodeBuilder">Recursive compilation entry point.</param>
        public HorizonBuildContext(
            HorizonStyleSheet styleSheet,
            HorizonResourceMap resourceMap,
            bool isBuildingTemplate,
            HorizonNodeBuilder nodeBuilder)
        {
            StyleSheet = styleSheet;
            ResourceMap = resourceMap;
            IsBuildingTemplate = isBuildingTemplate;
            NodeBuilder = nodeBuilder;
        }
    }

    /// <summary>
    /// Defines the contract for modular tag compilers that translate specific HTML-like tags into Unity UI hierarchies.
    /// </summary>
    public interface IHorizonTagHandler
    {
        /// <summary>
        /// Gets the list of lower-case tag names handled by this compiler implementation (e.g. ["button"], ["img", "icon"]).
        /// </summary>
        string[] SupportedTags { get; }

        /// <summary>
        /// Instantiates, styles, and configures the Unity GameObject corresponding to the AST node.
        /// </summary>
        /// <param name="node">Parsed markup node containing tag, attributes, and text.</param>
        /// <param name="parent">Parent GameObject to attach the newly created UI element to.</param>
        /// <param name="styles">Dictionary of computed CSS properties and inline style overrides.</param>
        /// <param name="context">Active compilation context.</param>
        /// <returns>The root GameObject of the compiled UI element.</returns>
        GameObject Build(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonBuildContext context);
    }
}