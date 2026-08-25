using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UdonSharp;
using UdonSharpEditor;
using VRC.Udon;

namespace BlackHorizon.HorizonGUI.Editor.Parsing
{
    /// <summary>
    /// The core translation engine that converts a HorizonNode tree into Unity UI GameObjects.
    /// Manages CSS application, Direct Dependency Injection, and Event Wiring.
    /// </summary>
    public static class HorizonCompiler
    {
        public static int ValidationErrors { get; private set; }

        public static void IncrementValidationErrors() => ValidationErrors++;

        private static HorizonStyleSheet _activeStyleSheet;
        private static HorizonResourceMap _activeResourceMap;
        private static bool _isBuildingTemplate = false;

        // Registry for u-bind: Matches "Name" to the created GameObject
        private static Dictionary<string, GameObject> _bindingsRegistry;

        // Registry for u-click: Stores buttons that need to be wired to logic scripts
        private struct PendingEvent
        {
            public GameObject SourceObj;
            public string MethodName;
        }
        private static List<PendingEvent> _pendingEvents;

        private class ChannelData
        {
            public List<GameObject> Views = new List<GameObject>();
            public List<string> ViewIds = new List<string>();
            public List<Button> Buttons = new List<Button>();
            public List<string> ButtonTargets = new List<string>();
        }
        private static Dictionary<string, ChannelData> _activeChannels;

        /// <summary>
        /// Clears the existing UI and rebuilds the interface from the provided node tree.
        /// </summary>
        public static void BuildInterface(
            GameObject rootContainer,
            HorizonNode rootNode,
            HorizonStyleSheet styleSheet,
            HorizonResourceMap resourceMap,
            List<UdonSharpBehaviour> logicScripts
        )
        {
            ValidationErrors = 0;
            _activeChannels = new Dictionary<string, ChannelData>();
            _bindingsRegistry = new Dictionary<string, GameObject>();
            _pendingEvents = new List<PendingEvent>();

            _activeStyleSheet = styleSheet;
            _activeResourceMap = resourceMap;

            var context = new HorizonBuildContext(_activeStyleSheet, _activeResourceMap, false, BuildNode);

            // 1. Build Visual Tree
            foreach (var child in rootNode.Children)
            {
                BuildNode(child, rootContainer, context);
            }

            BuildChannelControllers(rootContainer);

            // 2. Inject Dependencies (u-bind)
            if (logicScripts != null && logicScripts.Count > 0)
            {
                InjectDependencies(logicScripts);
                WireEvents(logicScripts);
                TriggerPostBuildEvents(logicScripts);
            }

            // Cleanup
            _activeChannels.Clear();
            _bindingsRegistry.Clear();
            _pendingEvents.Clear();
            _activeStyleSheet = null;
            _activeResourceMap = null;
        }

        /// <summary>
        /// Recursive entry point for translating an AST node into GameObjects using registered tag handlers.
        /// </summary>
        /// <param name="node">AST node to compile.</param>
        /// <param name="parent">Parent container GameObject.</param>
        /// <param name="context">Active compilation context (created automatically if null).</param>
        public static void BuildNode(HorizonNode node, GameObject parent, HorizonBuildContext context = null)
        {
            if (context == null)
            {
                context = new HorizonBuildContext(_activeStyleSheet, _activeResourceMap, _isBuildingTemplate, BuildNode);
            }

            var styles = context.StyleSheet != null ? context.StyleSheet.GetComputedStyle(node) : new Dictionary<string, string>();

            if (node.Attributes.TryGetValue("style", out string inlineStyle))
            {
                var overrides = ParseInlineStyle(inlineStyle);
                foreach (var kvp in overrides) styles[kvp.Key] = kvp.Value;
            }

            IHorizonTagHandler handler = HorizonTagRegistry.GetHandler(node.Tag);
            GameObject createdObj = handler.Build(node, parent, styles, context);

            if (createdObj != null)
            {
                if (node.Attributes.TryGetValue("u-bind", out string bindName))
                {
                    if (context.IsBuildingTemplate || _isBuildingTemplate)
                    {
                        createdObj.name = $"{createdObj.name}__BIND__{bindName}";
                    }
                    else
                    {
                        if (!_bindingsRegistry.ContainsKey(bindName))
                        {
                            _bindingsRegistry.Add(bindName, createdObj);
                        }
                        else
                        {
                            Debug.LogError($"<color=red>[HorizonCompiler]</color> Duplicate u-bind detected: '<b>{bindName}</b>'. Binding might be incorrect.");
                            ValidationErrors++;
                        }
                    }
                }

                if (node.Attributes.TryGetValue("u-click", out string methodName))
                {
                    if (!context.IsBuildingTemplate && !_isBuildingTemplate)
                    {
                        _pendingEvents.Add(new PendingEvent
                        {
                            SourceObj = createdObj,
                            MethodName = methodName
                        });
                    }
                }

                RegisterChannels(createdObj, node);

                if (node.Tag.ToLower() != "h-grid")
                {
                    foreach (var child in node.Children)
                    {
                        BuildNode(child, createdObj, context);
                    }
                }

                if (createdObj.transform is RectTransform rt)
                {
                    Vector3 pos = rt.anchoredPosition3D;
                    pos.z = 0;
                    rt.anchoredPosition3D = pos;
                    rt.localRotation = Quaternion.identity;
                }
            }
        }

        /// <summary>
        /// Performs Dependency Injection (DI). 
        /// Scans public/serialized fields in logic scripts and assigns UI references 
        /// where field names match HTML 'u-bind' values.
        /// </summary>
        /// <param name="scripts">List of target scripts to populate.</param>
        private static void InjectDependencies(List<UdonSharpBehaviour> scripts)
        {
            foreach (var script in scripts)
            {
                if (script == null) continue;

                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(script);
                SerializedObject so = new SerializedObject(script);
                bool dirty = false;

                SerializedProperty prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (_bindingsRegistry.TryGetValue(prop.name, out GameObject boundObj))
                    {
                        if (boundObj == null) continue;

                        UnityEngine.Object targetValue = null;

                        if (prop.type.Contains("GameObject"))
                        {
                            targetValue = boundObj;
                        }
                        else
                        {
                            string typeName = prop.type.Replace("PPtr<$", "").Replace(">", "");

                            if (typeName == "Transform" || typeName == "RectTransform")
                                targetValue = boundObj.transform;
                            else
                                targetValue = boundObj.GetComponent(typeName);
                        }

                        if (targetValue != null)
                        {
                            prop.objectReferenceValue = targetValue;

                            if (backing != null)
                            {
                                backing.publicVariables.TrySetVariableValue(prop.name, targetValue);
                            }

                            dirty = true;
                            // Debug.Log($"<color=#33FF33>[Horizon Injector]</color> Linked <b>{script.name}.{prop.name}</b> to UI Object.");
                        }
                    }
                }

                if (dirty)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(script);
                    if (backing != null) EditorUtility.SetDirty(backing);
                }
            }
        }

        /// <summary>
        /// Connects UI events (onClick, onValueChanged) to UdonSharp methods.
        /// Uses 'u-click' attribute values to find matching method names across all discovered scripts.
        /// </summary>
        /// <param name="scripts">List of scripts to search for methods.</param>
        private static void WireEvents(List<UdonSharpBehaviour> scripts)
        {
            foreach (var evt in _pendingEvents)
            {
                UdonSharpBehaviour targetScript = null;
                int matchCount = 0;

                foreach (var script in scripts)
                {
                    var method = script.GetType().GetMethod(evt.MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        if (targetScript == null) targetScript = script;
                        matchCount++;
                    }
                }

                if (matchCount == 0)
                {
                    Debug.LogError($"<color=red>[Horizon Wiring]</color> Method '<b>{evt.MethodName}</b>' not found in any active logic scripts. u-click failed.");
                    ValidationErrors++;
                    continue;
                }
                if (matchCount > 1)
                {
                    Debug.LogWarning($"<color=yellow>[Horizon Wiring]</color> Ambiguity: Method '<b>{evt.MethodName}</b>' found in {matchCount} scripts. Wiring to '{targetScript.name}'. Use unique method names to avoid this.");
                }

                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(targetScript);
                if (backing != null)
                {
                    if (evt.SourceObj.GetComponent<Button>() is Button btn)
                    {
                        int count = btn.onClick.GetPersistentEventCount();
                        for (int i = count - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(btn.onClick, i);

                        UnityEventTools.AddStringPersistentListener(btn.onClick, backing.SendCustomEvent, evt.MethodName);
                        EditorUtility.SetDirty(btn);
                    }
                    else if (evt.SourceObj.GetComponent<Toggle>() is Toggle tog)
                    {
                        int count = tog.onValueChanged.GetPersistentEventCount();
                        for (int i = count - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(tog.onValueChanged, i);

                        UnityEventTools.AddStringPersistentListener(tog.onValueChanged, backing.SendCustomEvent, evt.MethodName);
                    }
                    else if (evt.SourceObj.GetComponent<Slider>() is Slider sld)
                    {
                        int count = sld.onValueChanged.GetPersistentEventCount();
                        for (int i = count - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(sld.onValueChanged, i);

                        UnityEventTools.AddStringPersistentListener(sld.onValueChanged, backing.SendCustomEvent, evt.MethodName);
                    }
                    else if (evt.SourceObj.GetComponent<HorizonDataGrid>() is HorizonDataGrid grid)
                    {
                        HorizonGUIFactory.ConfigureLogic<HorizonDataGrid>(evt.SourceObj, binder =>
                        {
                            binder.Bind("targetCallback", backing);
                            binder.BindVal("callbackEventName", evt.MethodName);
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Invokes the 'OnHorizonBuild' method on any logic script that implements it.
        /// This happens strictly during the editor-time build process for post-initialization.
        /// </summary>
        /// <param name="scripts">List of scripts to check for the build event.</param>
        private static void TriggerPostBuildEvents(List<UdonSharpBehaviour> scripts)
        {
            foreach (var script in scripts)
            {
                var method = script.GetType().GetMethod("OnHorizonBuild", BindingFlags.Instance | BindingFlags.Public);
                if (method != null)
                {
                    method.Invoke(script, null);
                }
            }
        }

        /// <summary>
        /// Analyzes h-change and h-view attributes to populate the channel dictionary.
        /// </summary>
        private static void RegisterChannels(GameObject obj, HorizonNode node)
        {
            // 1. Register View (h-view="Channel:PageID")
            if (node.Attributes.TryGetValue("h-view", out string viewRaw))
            {
                ParseChannelString(viewRaw, out string channel, out string id);
                if (!string.IsNullOrEmpty(channel) && !string.IsNullOrEmpty(id))
                {
                    if (!_activeChannels.ContainsKey(channel)) _activeChannels[channel] = new ChannelData();

                    _activeChannels[channel].Views.Add(obj);
                    _activeChannels[channel].ViewIds.Add(id);
                }
            }

            // 2. Register Trigger (h-change="Channel:TargetID")
            if (node.Attributes.TryGetValue("h-change", out string changeRaw))
            {
                Button btn = obj.GetComponent<Button>();
                if (btn != null)
                {
                    ParseChannelString(changeRaw, out string channel, out string id);
                    if (!string.IsNullOrEmpty(channel) && !string.IsNullOrEmpty(id))
                    {
                        if (!_activeChannels.ContainsKey(channel)) _activeChannels[channel] = new ChannelData();

                        _activeChannels[channel].Buttons.Add(btn);
                        _activeChannels[channel].ButtonTargets.Add(id);
                    }
                }
            }
        }

        /// <summary>
        /// Finalizes the build process by creating Logic Controllers for all detected channels.
        /// <para>
        /// For every unique channel found (e.g. "Main"), a hidden GameObject with <see cref="HorizonChannelController"/> is created.
        /// Buttons with 'h-change' are linked to this controller using <see cref="HorizonEventCaller"/> to ensure Udon compatibility.
        /// </para>
        /// </summary>
        private static void BuildChannelControllers(GameObject rootContainer)
        {
            if (_activeChannels.Count == 0) return;

            GameObject systemRoot = HorizonGUIFactory.CreateBlock("_System_Channels", rootContainer);
            systemRoot.SetActive(false);

            foreach (var kvp in _activeChannels)
            {
                string channelName = kvp.Key;
                ChannelData data = kvp.Value;

                if (data.Views.Count == 0)
                {
                    if (data.Buttons.Count > 0)
                    {
                        Debug.LogWarning($"<color=yellow>[HorizonCompiler]</color> Channel <b>'{channelName}'</b> has {data.Buttons.Count} triggers (h-change) but no views (h-view). Buttons will not function.");
                    }
                    continue;
                }

                // 1. Create Controller Host
                GameObject host = new GameObject($"Channel_{channelName}");
                host.transform.SetParent(systemRoot.transform);

                var controller = HorizonGUIFactory.AttachLogic<HorizonChannelController>(host);

                HorizonGUIFactory.ConfigureLogic<HorizonChannelController>(host, binder =>
                {
                    binder.BindVal("channelName", channelName);
                    binder.BindArray("views", data.Views);
                    binder.BindArray("viewIds", data.ViewIds);
                });

                for (int i = 0; i < data.Views.Count; i++)
                {
                    if (data.Views[i] != null)
                        data.Views[i].SetActive(i == 0);
                }

                // 2. Link Buttons via HorizonEventCaller
                UdonBehaviour backingController = UdonSharpEditorUtility.GetBackingUdonBehaviour(controller);

                if (backingController == null)
                {
                    Debug.LogError($"<color=red>[HorizonCompiler]</color> Critical: Failed to get backing UdonBehaviour for channel '{channelName}'. Check UdonSharp compilation status.");
                    continue;
                }

                for (int i = 0; i < data.Buttons.Count; i++)
                {
                    Button btn = data.Buttons[i];
                    string targetId = data.ButtonTargets[i];

                    int count = btn.onClick.GetPersistentEventCount();
                    for (int k = count - 1; k >= 0; k--) UnityEventTools.RemovePersistentListener(btn.onClick, k);

                    GameObject btnObj = btn.gameObject;
                    var caller = HorizonGUIFactory.AttachLogic<HorizonEventCaller>(btnObj);

                    HorizonGUIFactory.ConfigureLogic<HorizonEventCaller>(btnObj, binder =>
                    {
                        binder.Bind("targetBehaviour", backingController);
                        binder.BindVal("eventName", "_SwitchFromEvent");
                        binder.BindVal("stringPayload", targetId);
                    });

                    UdonBehaviour callerBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(caller);

                    if (callerBacking != null)
                    {
                        UnityEventTools.AddStringPersistentListener(
                            btn.onClick,
                            callerBacking.SendCustomEvent,
                            "OnClick"
                        );
                    }
                }
            }
        }

        private static void ParseChannelString(string raw, out string channel, out string id)
        {
            channel = "";
            id = "";
            string[] parts = raw.Split(':');
            if (parts.Length == 2)
            {
                channel = parts[0].Trim();
                id = parts[1].Trim();
            }
            else
            {
                Debug.LogWarning($"[HorizonCompiler] Invalid channel format: '{raw}'. Expected 'ChannelName:Value'");
            }
        }

        private static Dictionary<string, string> ParseInlineStyle(string styleString)
        {
            var styles = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(styleString)) return styles;

            string[] parts = styleString.Split(';');
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                int colonIndex = part.IndexOf(':');
                if (colonIndex > 0)
                {
                    string key = part.Substring(0, colonIndex).Trim().ToLower();
                    string val = part.Substring(colonIndex + 1).Trim();
                    styles[key] = val;
                }
            }
            return styles;
        }
    }
}