using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackHorizon.HorizonGUI.Editor.Parsing
{
    /// <summary>
    /// Registry for mapping HTML tag identifiers to their corresponding IHorizonTagHandler implementations.
    /// </summary>
    public static class HorizonTagRegistry
    {
        private static readonly Dictionary<string, IHorizonTagHandler> _handlers = new Dictionary<string, IHorizonTagHandler>(StringComparer.OrdinalIgnoreCase);
        private static readonly IHorizonTagHandler _defaultHandler = new Handlers.ContainerTagHandler();

        static HorizonTagRegistry()
        {
            Register(new Handlers.ContainerTagHandler());
            Register(new Handlers.TextTagHandler());
            Register(new Handlers.ButtonTagHandler());
            Register(new Handlers.InputTagHandler());
            Register(new Handlers.ToggleTagHandler());
            Register(new Handlers.IconTagHandler());
            Register(new Handlers.SeparatorTagHandler());
            Register(new Handlers.ScrollTagHandler());
            Register(new Handlers.GridTagHandler());
        }

        /// <summary>
        /// Registers a tag handler for its declared supported tags.
        /// </summary>
        /// <param name="handler">Handler instance to register.</param>
        public static void Register(IHorizonTagHandler handler)
        {
            foreach (var tag in handler.SupportedTags)
            {
                _handlers[tag] = handler;
            }
        }

        /// <summary>
        /// Resolves the appropriate handler for a tag, falling back to ContainerTagHandler.
        /// </summary>
        /// <param name="tag">HTML tag name.</param>
        /// <returns>The matching IHorizonTagHandler.</returns>
        public static IHorizonTagHandler GetHandler(string tag)
        {
            if (_handlers.TryGetValue(tag, out var handler))
                return handler;

            return _defaultHandler;
        }
    }
}