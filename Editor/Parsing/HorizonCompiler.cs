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

            // Init Context
            _activeStyleSheet = styleSheet;
            _activeResourceMap = resourceMap;
            _isBuildingTemplate = false;

            // 1. Build Visual Tree
            foreach (var child in rootNode.Children)
            {
                BuildNode(child, rootContainer);
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
        /// Recursive entry point for building the UI tree.
        /// Uses static context fields instead of passing arguments recursively.
        /// </summary>
        private static void BuildNode(HorizonNode node, GameObject parent)
        {
            GameObject createdObj = null;
            var styles = _activeStyleSheet.GetComputedStyle(node);

            if (node.Attributes.TryGetValue("style", out string inlineStyle))
            {
                var overrides = ParseInlineStyle(inlineStyle);
                foreach (var kvp in overrides) styles[kvp.Key] = kvp.Value;
            }

            string tag = node.Tag.ToLower();

            switch (tag)
            {
                case "scroll":
                    createdObj = BuildScroll(node, parent, styles);
                    break;

                case "h-grid":
                    createdObj = BuildDataGrid(node, parent, styles);
                    break;

                case "button":
                    createdObj = BuildButton(node, parent, styles);
                    break;

                case "text":
                case "h1":
                case "h2":
                case "p":
                case "label":
                    createdObj = BuildText(node, parent, styles);
                    break;

                case "input":
                    string type = node.Attributes.ContainsKey("type") ? node.Attributes["type"].ToLower() : "text";
                    if (type == "range") createdObj = BuildRangeInput(node, parent, styles);
                    else createdObj = BuildTextInput(node, parent, styles);
                    break;

                case "toggle":
                    createdObj = BuildToggle(node, parent, styles);
                    break;

                case "icon":
                case "img":
                    createdObj = BuildIcon(node, parent, styles, _activeResourceMap);
                    break;

                case "view":
                case "div":
                case "section":
                    createdObj = BuildContainer(GetNodeName(node), parent, styles, node);
                    break;

                case "hr":
                    createdObj = BuildSeparator(node, parent, styles);
                    break;

                default:
                    createdObj = BuildContainer(GetNodeName(node), parent, styles, node);
                    break;
            }

            if (createdObj != null)
            {
                if (node.Attributes.TryGetValue("u-bind", out string bindName))
                {
                    if (_isBuildingTemplate)
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
                    if (!_isBuildingTemplate)
                    {
                        _pendingEvents.Add(new PendingEvent
                        {
                            SourceObj = createdObj,
                            MethodName = methodName
                        });
                    }
                }

                RegisterChannels(createdObj, node);

                if (tag != "h-grid")
                {
                    foreach (var child in node.Children)
                    {
                        BuildNode(child, createdObj);
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

        /// <summary>
        /// Constructs a vertical scrollable area.
        /// </summary>
        private static GameObject BuildScroll(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            GameObject content = HorizonGUIFactory.CreateScrollableColumn(GetNodeName(node), parent);
            if (content.transform.parent != null && content.transform.parent.parent != null)
            {
                GameObject root = content.transform.parent.parent.gameObject;
                ApplyLayoutStyles(root, styles);
                ApplyContainerStyles(content, styles, node);
            }
            return content;
        }

        /// <summary>
        /// Constructs a thin horizontal line (separator).
        /// Uses a wrapper structure to allow for internal padding (indentation).
        /// </summary>
        private static GameObject BuildSeparator(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            GameObject wrapper = HorizonGUIFactory.CreateBlock(GetNodeName(node), parent);

            GameObject line = HorizonGUIFactory.CreatePanel("Visual_Line", wrapper);
            Image img = line.GetComponent<Image>();
            img.sprite = null;

            ApplyContainerStyles(line, styles, node);

            float height = ParseFloat(styles, "height", 2);
            float width = ParseFloat(styles, "width", -1);

            HorizonGUIFactory.SetLayoutSize(wrapper,
                minH: height,
                prefH: height,
                prefW: width > 0 ? width : (float?)null,
                flexW: width > 0 ? 0 : 1
            );

            float padding = ParseFloat(styles, "padding", 0);
            RectTransform lineRect = line.GetComponent<RectTransform>();
            lineRect.anchorMin = Vector2.zero;
            lineRect.anchorMax = Vector2.one;
            lineRect.offsetMin = new Vector2(padding, 0);
            lineRect.offsetMax = new Vector2(-padding, 0);

            return wrapper;
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

        /// <summary>
        /// Constructs a complex DataGrid with pooled items.
        /// </summary>
        private static GameObject BuildDataGrid(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            int poolSize = ParseInt(node.Attributes, "pool", 64);
            GameObject gridObj = CreateGridContainer(node, parent, styles);
            var manager = HorizonGUIFactory.AttachLogic<HorizonDataGrid>(gridObj);

            GameObject prototype = PrepareGridTemplate(node);
            bool isTemplated = (prototype != null);

            var slots = new List<HorizonSmartSlot>();

            for (int i = 0; i < poolSize; i++)
            {
                GameObject slotObj = CreateSlotInstance(gridObj, prototype, node, i);

                HorizonSmartSlot slotLogic = ConfigureSlotLogic(slotObj, manager, i, isTemplated);

                slots.Add(slotLogic);
            }

            if (prototype != null && prototype.transform.parent != null)
            {
                Object.DestroyImmediate(prototype.transform.parent.gameObject);
            }

            ConfigureGridManager(gridObj, manager, slots, poolSize);

            return gridObj;
        }

        // --- SUB-ROUTINES ---

        /// <summary>
        /// Creates the root GameObject for the grid and applies layout styles.
        /// </summary>
        private static GameObject CreateGridContainer(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            float w = ParseFloat(node.Attributes, "cell-w", 100);
            float h = ParseFloat(node.Attributes, "cell-h", 100);

            if (!styles.ContainsKey("gap") && !styles.ContainsKey("spacing"))
            {
                float attrSpacing = ParseFloat(node.Attributes, "spacing", 10);
                styles["gap"] = $"{attrSpacing}px";
            }

            GameObject gridObj = HorizonGUIFactory.CreateGrid(GetNodeName(node), parent, new Vector2(w, h), Vector2.zero);

            ApplyLayoutStyles(gridObj, styles, node);
            ApplyContainerStyles(gridObj, styles, node);

            return gridObj;
        }

        /// <summary>
        /// Parses the children of <h-grid> to create a prototype GameObject.
        /// Returns null if no children exist.
        /// </summary>
        private static GameObject PrepareGridTemplate(HorizonNode node)
        {
            if (node.Children.Count == 0) return null;

            GameObject tempHolder = new GameObject("Temp_Holder");
            tempHolder.SetActive(false);

            _isBuildingTemplate = true;
            try
            {
                BuildNode(node.Children[0], tempHolder);
            }
            catch
            {
                Object.DestroyImmediate(tempHolder);
                throw;
            }
            finally
            {
                _isBuildingTemplate = false;
            }

            if (tempHolder.transform.childCount > 0)
            {
                return tempHolder.transform.GetChild(0).gameObject;
            }

            Object.DestroyImmediate(tempHolder);
            return null;
        }

        /// <summary>
        /// Instantiates a slot from the prototype.
        /// Requires a template defined in HTML.
        /// </summary>
        private static GameObject CreateSlotInstance(GameObject gridParent, GameObject prototype, HorizonNode node, int index)
        {
            string slotName = $"Slot_{index:00}";

            if (prototype != null)
            {
                GameObject instance = Object.Instantiate(prototype, gridParent.transform);
                instance.name = slotName;
                instance.SetActive(true);
                return instance;
            }
            else
            {
                Debug.LogError($"<color=red>[HorizonCompiler]</color> Grid '<b>{GetNodeName(node)}</b>' has no template! Please add children to <h-grid> in your HTML.");
                ValidationErrors++;

                GameObject errorObj = new GameObject(slotName + "_ERROR");
                errorObj.transform.SetParent(gridParent.transform);
                var img = errorObj.AddComponent<Image>();
                img.color = Color.red;
                return errorObj;
            }
        }

        /// <summary>
        /// Attaches the SmartSlot component, bakes bindings, and wires click events.
        /// </summary>
        private static HorizonSmartSlot ConfigureSlotLogic(GameObject slotObj, HorizonDataGrid manager, int index, bool isTemplated)
        {
            var item = HorizonGUIFactory.AttachLogic<HorizonSmartSlot>(slotObj);

            BakeSmartSlotBindings(slotObj, item, isTemplated);

            HorizonGUIFactory.ConfigureLogic<HorizonSmartSlot>(slotObj, binder =>
            {
                binder.Bind("gridManager", manager);
                binder.BindVal("slotIndex", index);
            });

            Button btn = slotObj.GetComponent<Button>() ?? slotObj.GetComponentInChildren<Button>();

            if (btn != null)
            {
                HorizonGUIFactory.ConfigureLogic<HorizonSmartSlot>(slotObj, binder =>
                {
                    binder.Bind("mainButton", btn);
                });

                var backingItem = UdonSharpEditorUtility.GetBackingUdonBehaviour(item);
                if (backingItem != null)
                {
                    UnityEditor.Events.UnityEventTools.AddStringPersistentListener(
                        btn.onClick,
                        backingItem.SendCustomEvent,
                        "OnClick"
                    );
                }
            }

            return item;
        }

        /// <summary>
        /// Pushes the created slots into the DataGrid manager.
        /// </summary>
        private static void ConfigureGridManager(GameObject gridObj, HorizonDataGrid manager, List<HorizonSmartSlot> slots, int poolSize)
        {
            HorizonGUIFactory.ConfigureLogic<HorizonDataGrid>(gridObj, binder =>
            {
                binder.BindArray("slotPool", slots);
                binder.BindVal("itemsPerPage", poolSize);
            });
        }

        /// <summary>
        /// Recursively scans a slot instance for components with 'u-bind' and populates the SmartSlot arrays.
        /// </summary>
        private static void BakeSmartSlotBindings(GameObject root, HorizonSmartSlot slot, bool isTemplated)
        {
            var textKeys = new List<string>();
            var textTargets = new List<TextMeshProUGUI>();
            var imgKeys = new List<string>();
            var imgTargets = new List<Image>();

            if (!isTemplated)
            {
                var txt = root.GetComponentInChildren<TextMeshProUGUI>();
                if (txt) { textKeys.Add("MainText"); textTargets.Add(txt); }

                var imgs = root.GetComponentsInChildren<Image>();
                foreach (var img in imgs)
                {
                    if (img.gameObject != root)
                    {
                        imgKeys.Add("MainIcon"); imgTargets.Add(img);
                        break;
                    }
                }
            }
            else
            {
                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var tr in allTransforms)
                {
                    string name = tr.name;
                    if (name.Contains("__BIND__"))
                    {
                        string[] parts = name.Split(new string[] { "__BIND__" }, System.StringSplitOptions.None);
                        if (parts.Length < 2) continue;

                        string key = parts[1];

                        var txt = tr.GetComponent<TextMeshProUGUI>();
                        if (txt != null)
                        {
                            textKeys.Add(key);
                            textTargets.Add(txt);
                        }

                        var img = tr.GetComponent<Image>();
                        if (img != null)
                        {
                            imgKeys.Add(key);
                            imgTargets.Add(img);
                        }

                        tr.name = parts[0];
                    }
                }
            }

            HorizonGUIFactory.ConfigureLogic<HorizonSmartSlot>(slot.gameObject, binder =>
            {
                binder.BindArray("textKeys", textKeys);
                binder.BindArray("textTargets", textTargets);
                binder.BindArray("imageKeys", imgKeys);
                binder.BindArray("imageTargets", imgTargets);
            });
        }

        /// <summary>
        /// Constructs a range input (Slider) with VRC interaction setup.
        /// </summary>
        private static GameObject BuildRangeInput(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            float min = ParseFloat(node.Attributes, "min", 0f);
            float max = ParseFloat(node.Attributes, "max", 1f);
            float val = ParseFloat(node.Attributes, "value", 0f);

            Slider s = HorizonGUIFactory.CreateSlider(parent, min, max, val);
            GameObject go = s.gameObject;
            go.name = GetNodeName(node);

            if (go.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null) go.AddComponent<VRC.SDK3.Components.VRCUiShape>();
            BoxCollider col = go.GetComponent<BoxCollider>();
            if (col == null) col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;

            ApplyLayoutStyles(go, styles, node);
            ApplyContainerStyles(go, styles, node);

            return go;
        }

        /// <summary>
        /// Constructs an interactive button with state and interaction layers.
        /// </summary>
        private static GameObject BuildButton(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            GameObject btnRoot = HorizonGUIFactory.CreatePanel(GetNodeName(node), parent);
            Image bgImg = btnRoot.GetComponent<Image>();
            bgImg.raycastTarget = true;

            GameObject hoverObj = HorizonGUIFactory.CreatePanel("Interaction_Overlay", btnRoot);
            HorizonGUIFactory.Stretch(hoverObj);
            Image hoverImg = hoverObj.GetComponent<Image>();

            LayoutElement le = hoverObj.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            Button btn = btnRoot.AddComponent<Button>();
            btn.targetGraphic = hoverImg;
            btn.transition = Selectable.Transition.ColorTint;

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.clear;
            cb.highlightedColor = new Color(1, 1, 1, 0.1f);
            cb.pressedColor = new Color(1, 1, 1, 0.2f);
            cb.selectedColor = Color.clear;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            ApplyContainerStyles(btnRoot, styles, node);
            ApplyLayoutStyles(btnRoot, styles);

            if (btnRoot.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null)
                btnRoot.AddComponent<VRC.SDK3.Components.VRCUiShape>();

            BoxCollider col = btnRoot.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = btnRoot.AddComponent<BoxCollider>();
                col.isTrigger = true;
            }

            return btnRoot;
        }

        /// <summary>
        /// Constructs a generic layout block (div).
        /// </summary>
        private static GameObject BuildContainer(string name, GameObject parent, Dictionary<string, string> styles, HorizonNode node)
        {
            GameObject go = HorizonGUIFactory.CreateBlock(name, parent);
            ApplyContainerStyles(go, styles, node);
            ApplyLayoutStyles(go, styles, node);
            return go;
        }

        private static string GetFrameworkVersion()
        {
            string[] guids = AssetDatabase.FindAssets("HorizonGUIManager t:MonoScript");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Object scriptAsset = AssetDatabase.LoadAssetAtPath<Object>(path);
                return HorizonEditorUtils.GetVersion(scriptAsset);
            }
            return "?.?.?";
        }

        /// <summary>
        /// Constructs a text label with specific predefined styles (H1, H2, Label, Body).
        /// </summary>
        private static GameObject BuildText(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            if (node.Attributes.ContainsKey("h-version"))
            {
                string ver = GetFrameworkVersion();
                if (!string.IsNullOrEmpty(node.TextContent) && node.TextContent.Contains("{v}"))
                    node.TextContent = node.TextContent.Replace("{v}", ver);
                else
                    node.TextContent = $"v{ver}";
            }

            var tmp = HorizonGUIFactory.CreateText(parent, node.TextContent);
            tmp.gameObject.name = GetNodeName(node);

            ApplyTextStyles(tmp, styles);
            ApplyLayoutStyles(tmp.gameObject, styles);

            return tmp.gameObject;
        }

        /// <summary>
        /// Constructs a styled text input field.
        /// </summary>
        private static GameObject BuildTextInput(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            Sprite bg = HorizonGUIFactory.GetOrGenerateRoundedSprite();

            GameObject root = HorizonGUIFactory.CreatePanel(GetNodeName(node), parent);
            Image img = root.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0.1f);
            img.sprite = bg;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 3.0f;

            GameObject textArea = HorizonGUIFactory.CreateBlock("Text Area", root);
            RectTransform taRect = textArea.GetComponent<RectTransform>();
            taRect.anchorMin = Vector2.zero; taRect.anchorMax = Vector2.one;
            taRect.offsetMin = new Vector2(10, 0); taRect.offsetMax = new Vector2(-10, 0);

            string initialText = node.Attributes.ContainsKey("value") ? node.Attributes["value"] : "";

            var t = HorizonGUIFactory.CreateText(textArea, initialText);
            t.fontSize = 24;
            t.color = Color.white;

            GameObject placeObj = HorizonGUIFactory.CreateBlock("Placeholder", textArea);
            HorizonGUIFactory.Stretch(placeObj);

            string placeText = node.Attributes.ContainsKey("placeholder") ? node.Attributes["placeholder"] : "Enter text...";

            var p = HorizonGUIFactory.CreateText(placeObj, placeText);
            p.fontSize = 24;
            p.color = new Color(1, 1, 1, 0.5f);
            p.fontStyle = FontStyles.Italic;

            TMP_InputField inp = root.AddComponent<TMP_InputField>();
            inp.textViewport = taRect;
            inp.textComponent = t;
            inp.placeholder = p;
            inp.targetGraphic = img;
            inp.text = initialText;

            if (node.Attributes.ContainsKey("readonly"))
            {
                inp.readOnly = true;
            }

            if (root.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null) root.AddComponent<VRC.SDK3.Components.VRCUiShape>();
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;

            HorizonGUIFactory.SetLayoutSize(root, minH: 50, prefH: 50);

            ApplyContainerStyles(root, styles, node);
            ApplyLayoutStyles(root, styles);

            LayoutElement le = root.GetComponent<LayoutElement>();
            if (le != null) le.flexibleHeight = 0;

            return root;
        }

        /// <summary>
        /// Constructs a styled toggle switch.
        /// </summary>
        private static GameObject BuildToggle(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            float spacing = styles.ContainsKey("gap") ? ParseFloat(styles, "gap", 15) : 15;

            GameObject root = HorizonGUIFactory.CreateRow(GetNodeName(node), parent, spacing: spacing, align: TextAnchor.MiddleLeft);

            GameObject bgObj = HorizonGUIFactory.CreatePanel("Background", root);
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(1, 1, 1, 0.1f);
            bgImg.sprite = HorizonGUIFactory.GetOrGenerateRoundedSprite();

            HorizonGUIFactory.SetLayoutSize(bgObj, 40, 40, 40, 40);
            bgImg.raycastTarget = true;

            GameObject checkObj = HorizonGUIFactory.CreatePanel("Checkmark", bgObj);
            Image checkImg = checkObj.GetComponent<Image>();
            checkImg.color = Color.white;
            checkImg.sprite = HorizonGUIFactory.LoadPackageSprite("checkmark.png");

            RectTransform checkRect = checkObj.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.2f, 0.2f); checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = Vector2.zero; checkRect.offsetMax = Vector2.zero;

            Toggle tog = root.AddComponent<Toggle>();
            tog.targetGraphic = bgImg;
            tog.graphic = checkImg;
            tog.isOn = false;

            tog.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = tog.colors;
            cb.normalColor = new Color(1, 1, 1, 0.1f);
            cb.highlightedColor = new Color(1, 1, 1, 0.25f);
            cb.pressedColor = new Color(1, 1, 1, 0.4f);
            cb.selectedColor = new Color(1, 1, 1, 0.1f);
            cb.fadeDuration = 0.1f;
            tog.colors = cb;

            if (!string.IsNullOrEmpty(node.TextContent))
            {
                var label = HorizonGUIFactory.CreateText(root, node.TextContent);
                label.fontSize = 24;
                ApplyTextStyles(label, styles);
            }

            if (root.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null) root.AddComponent<VRC.SDK3.Components.VRCUiShape>();
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;

            ApplyContainerStyles(root, styles, node);
            ApplyLayoutStyles(root, styles);

            return root;
        }

        /// <summary>
        /// Constructs an icon or image element.
        /// </summary>
        private static GameObject BuildIcon(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonResourceMap resourceMap)
        {
            GameObject go = HorizonGUIFactory.CreateBlock(GetNodeName(node), parent);
            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;

            if (node.Attributes.TryGetValue("src", out string src))
            {
                img.sprite = HorizonGUIFactory.LoadSprite(src, resourceMap);

                if (img.sprite != null) img.color = Color.white;
                else
                {
                    img.color = Color.magenta;
                }
            }

            HorizonGUIFactory.SetLayoutSize(go, 32, 32, 32, 32);
            ApplyLayoutStyles(go, styles, node);
            return go;
        }

        /// <summary>
        /// Generates a readable name for a GameObject based on node ID or class.
        /// </summary>
        private static string GetNodeName(HorizonNode node)
        {
            if (node.Attributes.ContainsKey("id")) return node.Attributes["id"];
            if (node.Attributes.ContainsKey("class")) return $"{node.Tag}.{node.Attributes["class"].Split(' ')[0]}";
            return node.Tag;
        }

        /// <summary>
        /// Applies container-specific styles like backgrounds and layout groups.
        /// Includes smart calculation for pixelsPerUnitMultiplier to prevent ovals.
        /// </summary>
        private static void ApplyContainerStyles(GameObject go, Dictionary<string, string> styles, HorizonNode node)
        {
            if (styles.TryGetValue("background-color", out string hex))
            {
                Image img = go.GetComponent<Image>();
                if (img == null) img = go.AddComponent<Image>();

                if (ColorUtility.TryParseHtmlString(hex, out Color col))
                {
                    img.color = col;

                    if (col.a <= 0.01f)
                    {
                        img.raycastTarget = false;
                    }

                    if (img.sprite == null)
                    {
                        img.sprite = HorizonGUIFactory.GetOrGenerateRoundedSprite();
                        img.type = Image.Type.Sliced;

                        // 1. Physical properties of generated texture
                        const float SRC_RADIUS = 64f;

                        // 2. Determine Dimensions
                        RectTransform rt = go.GetComponent<RectTransform>();
                        float w = rt.rect.width;
                        float h = rt.rect.height;
                        if (w <= 1) w = ParseFloat(styles, "width", 100);
                        if (h <= 1) h = ParseFloat(styles, "height", 100);

                        float minSide = Mathf.Min(w, h);
                        float maxPossibleRadius = minSide / 2f;

                        // 3. Determine Desired Target Radius
                        float targetRadius = 20f;

                        if (styles.ContainsKey("border-radius"))
                        {
                            targetRadius = ParseFloat(styles, "border-radius", 20f);
                        }
                        else
                        {
                            bool isFullRound = false;
                            if (node != null && node.Attributes.TryGetValue("class", out string cls))
                            {
                                string lowCls = cls.ToLower();
                                if (lowCls.Contains("sidebar") || lowCls.Contains("nav-btn") || lowCls.Contains("profile-btn") || lowCls.Contains("circle"))
                                {
                                    isFullRound = true;
                                }
                            }

                            if (isFullRound) targetRadius = maxPossibleRadius;
                        }

                        // 4. Safety Clamp
                        if (targetRadius > maxPossibleRadius) targetRadius = maxPossibleRadius;
                        if (targetRadius < 1f) targetRadius = 1f;

                        // 5. Apply
                        img.pixelsPerUnitMultiplier = SRC_RADIUS / targetRadius;
                    }
                }
            }

            // --- Layout & Padding Logic (Unchanged) ---

            bool isRow = styles.ContainsKey("flex-direction") && styles["flex-direction"] == "row";
            float spacing = ParseFloat(styles, "gap", 0) + ParseFloat(styles, "spacing", 0);

            int pAll = (int)ParseFloat(styles, "padding", 0);
            int pTop = (int)ParseFloat(styles, "padding-top", pAll);
            int pBot = (int)ParseFloat(styles, "padding-bottom", pAll);
            int pLeft = (int)ParseFloat(styles, "padding-left", pAll);
            int pRight = (int)ParseFloat(styles, "padding-right", pAll);

            TextAnchor align = TextAnchor.UpperLeft;
            if (styles.TryGetValue("align-items", out string alignVal))
            {
                if (alignVal == "center") align = TextAnchor.MiddleCenter;
                if (alignVal == "flex-end") align = TextAnchor.LowerRight;
                if (alignVal == "stretch") align = TextAnchor.UpperLeft;
            }

            LayoutGroup lg = go.GetComponent<LayoutGroup>();
            if (lg == null)
            {
                if (go.GetComponent<Slider>() == null && go.GetComponent<TMP_InputField>() == null)
                {
                    if (isRow) lg = go.AddComponent<HorizontalLayoutGroup>();
                    else lg = go.AddComponent<VerticalLayoutGroup>();
                }
            }

            if (lg != null)
            {
                lg.padding = new RectOffset(pLeft, pRight, pTop, pBot);

                if (lg is HorizontalLayoutGroup hlg)
                {
                    hlg.spacing = spacing;
                    hlg.childAlignment = align;
                    hlg.childControlWidth = true;
                    hlg.childControlHeight = true;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                }
                if (lg is VerticalLayoutGroup vlg)
                {
                    vlg.spacing = spacing;
                    vlg.childAlignment = align;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = true;
                    vlg.childForceExpandHeight = false;
                    vlg.childForceExpandWidth = false;
                }
            }

            if (go.GetComponent<GridLayoutGroup>() is GridLayoutGroup glg)
            {
                glg.padding = new RectOffset(pLeft, pRight, pTop, pBot);
                glg.spacing = new Vector2(spacing, spacing);
            }
        }
        /// <summary>
        /// Applies LayoutElement properties (width, height, flex-grow) to the GameObject.
        /// Handles 'ignore-layout' for overlay positioning.
        /// </summary>
        private static void ApplyLayoutStyles(GameObject go, Dictionary<string, string> styles, HorizonNode node = null)
        {
            if (node != null && node.Attributes.ContainsKey("ignore-layout"))
            {
                LayoutElement le = go.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                HorizonGUIFactory.Stretch(go);

                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                return;
            }

            float w = ParseFloat(styles, "width", -1);
            float h = ParseFloat(styles, "height", -1);
            float flex = ParseFloat(styles, "flex-grow", -1);

            float flexW = -1;
            float flexH = -1;

            if (flex >= 0)
            {
                flexW = flex;
                flexH = flex;
            }
            else
            {
                if (w > 0) flexW = 0;
                if (h > 0) flexH = 0;
            }

            HorizonGUIFactory.SetLayoutSize(go,
                minW: w > 0 ? w : (float?)null,
                minH: h > 0 ? h : (float?)null,
                prefW: w > 0 ? w : (float?)null,
                prefH: h > 0 ? h : (float?)null,
                flexH: flexH,
                flexW: flexW
            );
        }

        /// <summary>
        /// Applies typography styles to a TextMeshProUGUI component.
        /// </summary>
        private static void ApplyTextStyles(TextMeshProUGUI tmp, Dictionary<string, string> styles)
        {
            if (styles.TryGetValue("color", out string hex))
            {
                if (ColorUtility.TryParseHtmlString(hex, out Color col)) tmp.color = col;
            }

            if (styles.ContainsKey("font-size"))
            {
                float size = ParseFloat(styles, "font-size", tmp.fontSize);
                tmp.fontSize = size;
            }

            if (styles.TryGetValue("text-align", out string align))
            {
                if (align == "center") tmp.alignment = TextAlignmentOptions.Center;
                if (align == "right") tmp.alignment = TextAlignmentOptions.Right;
                if (align == "left") tmp.alignment = TextAlignmentOptions.Left;
            }

            if (styles.TryGetValue("font-style", out string fStyle))
            {
                fStyle = fStyle.ToLower();
                if (fStyle.Contains("bold")) tmp.fontStyle |= FontStyles.Bold;
                if (fStyle.Contains("italic")) tmp.fontStyle |= FontStyles.Italic;
                if (fStyle.Contains("normal")) tmp.fontStyle = FontStyles.Normal;
            }
        }

        private static float ParseFloat(Dictionary<string, string> attrs, string key, float def)
        {
            if (attrs.TryGetValue(key, out string val))
            {
                val = val.Replace("px", "").Trim();
                if (float.TryParse(val, out float result)) return result;
            }
            return def;
        }

        private static int ParseInt(Dictionary<string, string> attrs, string key, int def)
        {
            if (attrs.TryGetValue(key, out string val))
            {
                if (int.TryParse(val, out int result)) return result;
            }
            return def;
        }
    }
}