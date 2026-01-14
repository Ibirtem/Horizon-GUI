using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UdonSharp;
using UdonSharpEditor;
using VRC.Udon;

namespace BlackHorizon.HorizonGUI.Editor.Parsing
{
    /// <summary>
    /// The core translation engine that converts a HorizonNode tree into Unity UI GameObjects.
    /// Manages CSS application, Udon logic binding, and hierarchical layout construction.
    /// </summary>
    public static class HorizonCompiler
    {
        /// <summary>
        /// Clears the existing UI and rebuilds the interface from the provided node tree.
        /// </summary>
        /// <param name="rootContainer">The parent GameObject for the generated UI.</param>
        /// <param name="rootNode">The parsed HTML-like node tree.</param>
        /// <param name="styleSheet">The parsed CSS stylesheet.</param>
        /// <param name="logicTarget">The UdonSharpBehaviour that receives events and bindings.</param>
        public static void BuildInterface(GameObject rootContainer, HorizonNode rootNode, HorizonStyleSheet styleSheet, UdonSharpBehaviour rootLogic, HorizonResourceMap resourceMap)
        {
            while (rootContainer.transform.childCount > 0)
                GameObject.DestroyImmediate(rootContainer.transform.GetChild(0).gameObject);

            foreach (var child in rootNode.Children)
            {
                BuildNode(child, rootContainer, styleSheet, rootLogic, resourceMap);
            }
        }

        /// <summary>
        /// Recursive entry point for building the UI tree.
        /// Propagates the <paramref name="contextLogic"/> down the hierarchy to bind events (`u-click`) and variables (`u-bind`).
        /// </summary>
        /// <param name="contextLogic">The current active UdonSharpBehaviour (Manager or Module) acting as the controller.</param>
        private static void BuildNode(HorizonNode node, GameObject parent, HorizonStyleSheet styleSheet, UdonSharpBehaviour contextLogic, HorizonResourceMap resourceMap)
        {
            GameObject createdObj = null;
            var styles = styleSheet.GetComputedStyle(node);
            string tag = node.Tag.ToLower();

            UdonSharpBehaviour nextContext = contextLogic;

            switch (tag)
            {
                case "scroll":
                    createdObj = BuildScroll(node, parent, styles);
                    break;

                case "h-grid":
                    createdObj = BuildDataGrid(node, parent, styles, contextLogic);
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
                    if (type == "range")
                        createdObj = BuildRangeInput(node, parent, styles);
                    else
                        createdObj = BuildTextInput(node, parent, styles);
                    break;

                case "toggle":
                    createdObj = BuildToggle(node, parent, styles);
                    break;

                case "icon":
                case "img":
                    createdObj = BuildIcon(node, parent, styles, resourceMap);
                    break;

                case "module":
                case "view":
                    createdObj = BuildModule(node, parent, styles, ref nextContext);
                    break;

                case "hr":
                    createdObj = BuildSeparator(node, parent, styles);
                    break;

                case "div":
                case "section":
                default:
                    createdObj = BuildContainer(GetNodeName(node), parent, styles, node);
                    break;
            }

            if (createdObj != null)
            {
                ProcessLogic(createdObj, node, nextContext);

                if (tag != "h-grid")
                {
                    foreach (var child in node.Children)
                    {
                        BuildNode(child, createdObj, styleSheet, nextContext, resourceMap);
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
            float height = styles.ContainsKey("height") ? ParseFloat(styles, "height", 5) : 5;
            float padding = ParseFloat(styles, "padding", 0);
            float width = ParseFloat(styles, "width", -1);

            GameObject wrapper = HorizonGUIFactory.CreateBlock(GetNodeName(node), parent);

            GameObject line = HorizonGUIFactory.CreatePanel("Visual_Line", wrapper, new Color(1, 1, 1, 0.2f), null);

            ApplyContainerStyles(line, styles, node);

            HorizonGUIFactory.SetLayoutSize(wrapper,
                minH: height,
                prefH: height,
                prefW: width > 0 ? width : (float?)null,
                flexW: width > 0 ? 0 : 1
            );

            RectTransform lineRect = line.GetComponent<RectTransform>();
            lineRect.anchorMin = Vector2.zero;
            lineRect.anchorMax = Vector2.one;
            lineRect.offsetMin = new Vector2(padding, 0);
            lineRect.offsetMax = new Vector2(-padding, 0);

            return wrapper;
        }

        /// <summary>
        /// Constructs a Module container and evaluates the <c>u-script</c> attribute.
        /// <para>
        /// If <c>u-script</c> is present, this method attaches the specified script and 
        /// <b>switches the <paramref name="logicContext"/></b> for all subsequent children of this node.
        /// </para>
        /// </summary>
        /// <param name="logicContext">
        /// Reference to the current logic controller. Modified only if a new script is attached.
        /// </param>
        private static GameObject BuildModule(HorizonNode node, GameObject parent, Dictionary<string, string> styles, ref UdonSharpBehaviour logicContext)
        {
            GameObject go = BuildContainer(GetNodeName(node), parent, styles, node);

            if (node.Attributes.TryGetValue("u-script", out string scriptName))
            {
                var newScript = HorizonGUIFactory.AttachLogicByString(go, scriptName);
                if (newScript != null)
                {
                    logicContext = newScript;
                }
            }
            else
            {
                HorizonGUIFactory.AttachLogic<HorizonGUIModule>(go);
            }

            return go;
        }

        /// <summary>
        /// Constructs a complex DataGrid with pooled items.
        /// </summary>
        private static GameObject BuildDataGrid(HorizonNode node, GameObject parent, Dictionary<string, string> styles, UdonSharpBehaviour targetLogic)
        {
            int pool = ParseInt(node.Attributes, "pool", 64);
            float w = ParseFloat(node.Attributes, "cell-w", 100);
            float h = ParseFloat(node.Attributes, "cell-h", 100);

            if (!styles.ContainsKey("gap") && !styles.ContainsKey("spacing"))
            {
                float attrSpacing = ParseFloat(node.Attributes, "spacing", 10);
                styles["gap"] = $"{attrSpacing}px";
            }

            bool isCircle = false;
            if (node.Attributes.TryGetValue("style", out string styleVal))
                if (styleVal.ToLower() == "circle") isCircle = true;

            string eventName = "OnItemSelected";
            UdonSharpBehaviour target = null;

            if (node.Attributes.TryGetValue("u-click", out string clickMethod))
            {
                eventName = clickMethod;
                target = targetLogic;
            }

            var manager = HorizonGUIFactory.CreateDataGrid(
                GetNodeName(node),
                parent,
                pool,
                new Vector2(w, h),
                target,
                eventName,
                isCircle
            );

            GameObject go = manager.gameObject;

            ApplyLayoutStyles(go, styles);
            ApplyContainerStyles(go, styles, node);

            return go;
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

            ApplyLayoutStyles(go, styles);
            ApplyContainerStyles(go, styles, node);

            return go;
        }

        /// <summary>
        /// Constructs an interactive button with state and interaction layers.
        /// </summary>
        private static GameObject BuildButton(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            Sprite bgSprite = HorizonGUIFactory.GetOrGenerateRoundedSprite();
            GameObject btnRoot = HorizonGUIFactory.CreatePanel(GetNodeName(node), parent, new Color(1, 1, 1, 0.05f), bgSprite);

            Image stateImg = btnRoot.GetComponent<Image>();
            stateImg.raycastTarget = true;
            stateImg.type = Image.Type.Sliced;
            stateImg.pixelsPerUnitMultiplier = 1.0f;

            GameObject hoverObj = HorizonGUIFactory.CreatePanel("Interaction_Overlay", btnRoot, Color.white, bgSprite);
            HorizonGUIFactory.Stretch(hoverObj);
            Image hoverImg = hoverObj.GetComponent<Image>();
            hoverImg.type = Image.Type.Sliced;
            hoverImg.pixelsPerUnitMultiplier = 1.0f;

            LayoutElement le = hoverObj.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            Button btn = btnRoot.AddComponent<Button>();
            btn.targetGraphic = hoverImg;
            btn.transition = Selectable.Transition.ColorTint;

            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(1, 1, 1, 0f);
            cb.highlightedColor = new Color(1, 1, 1, 0.1f);
            cb.pressedColor = new Color(1, 1, 1, 0.2f);
            cb.selectedColor = new Color(1, 1, 1, 0.0f);
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            if (!styles.ContainsKey("align-items")) styles["align-items"] = "center";
            if (!styles.ContainsKey("justify-content")) styles["justify-content"] = "center";
            if (!styles.ContainsKey("padding")) styles["padding"] = "10px";

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
            ApplyLayoutStyles(go, styles);
            return go;
        }

        /// <summary>
        /// Constructs a text label with specific predefined styles (H1, H2, Label, Body).
        /// </summary>
        private static GameObject BuildText(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            HorizonGUIFactory.TextStyle defStyle = HorizonGUIFactory.TextStyle.Body;
            string tag = node.Tag.ToLower();

            if (tag == "h1") defStyle = HorizonGUIFactory.TextStyle.H1;
            else if (tag == "h2") defStyle = HorizonGUIFactory.TextStyle.H2;
            else if (tag == "label") defStyle = HorizonGUIFactory.TextStyle.SmallDim;

            var tmp = HorizonGUIFactory.CreateText(parent, node.TextContent, defStyle);
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
            GameObject root = HorizonGUIFactory.CreatePanel(GetNodeName(node), parent, new Color(1, 1, 1, 0.1f), bg);
            Image img = root.GetComponent<Image>();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 3.0f;

            GameObject textArea = HorizonGUIFactory.CreateBlock("Text Area", root);
            RectTransform taRect = textArea.GetComponent<RectTransform>();
            taRect.anchorMin = Vector2.zero; taRect.anchorMax = Vector2.one;
            taRect.offsetMin = new Vector2(10, 0); taRect.offsetMax = new Vector2(-10, 0);

            string initialText = node.Attributes.ContainsKey("value") ? node.Attributes["value"] : "";

            var t = HorizonGUIFactory.CreateText(textArea, initialText, HorizonGUIFactory.TextStyle.Body);
            t.color = Color.white;

            GameObject placeObj = HorizonGUIFactory.CreateBlock("Placeholder", textArea);
            HorizonGUIFactory.Stretch(placeObj);

            string placeText = node.Attributes.ContainsKey("placeholder") ? node.Attributes["placeholder"] : "Enter text...";
            var p = HorizonGUIFactory.CreateText(placeObj, placeText, HorizonGUIFactory.TextStyle.BodyDim);
            p.fontStyle = FontStyles.Italic;

            TMP_InputField inp = root.AddComponent<TMP_InputField>();
            inp.textViewport = taRect;
            inp.textComponent = t;
            inp.placeholder = p;
            inp.targetGraphic = img;
            inp.text = initialText;

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

            GameObject bgObj = HorizonGUIFactory.CreatePanel("Background", root, new Color(1, 1, 1, 0.1f), HorizonGUIFactory.GetOrGenerateRoundedSprite());
            HorizonGUIFactory.SetLayoutSize(bgObj, 40, 40, 40, 40);

            bgObj.GetComponent<Image>().raycastTarget = true;

            GameObject checkObj = HorizonGUIFactory.CreatePanel("Checkmark", bgObj, Color.white, HorizonGUIFactory.LoadPackageSprite("checkmark.png"));
            RectTransform checkRect = checkObj.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.2f, 0.2f); checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = Vector2.zero; checkRect.offsetMax = Vector2.zero;

            Toggle tog = root.AddComponent<Toggle>();
            tog.targetGraphic = bgObj.GetComponent<Image>();
            tog.graphic = checkObj.GetComponent<Image>();
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
                var label = HorizonGUIFactory.CreateText(root, node.TextContent, HorizonGUIFactory.TextStyle.Body);
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
            ApplyLayoutStyles(go, styles);
            return go;
        }

        /// <summary>
        /// Links Unity UI events to UdonSharp events and binds properties to script variables.
        /// </summary>
        private static void ProcessLogic(GameObject obj, HorizonNode node, UdonSharpBehaviour logicTarget)
        {
            if (logicTarget == null) return;

            if (node.Tag.ToLower() != "h-grid" && node.Attributes.TryGetValue("u-click", out string methodName))
            {
                Button btn = obj.GetComponent<Button>();
                Toggle tog = obj.GetComponent<Toggle>();
                Slider sld = obj.GetComponent<Slider>();

                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(logicTarget);
                if (backing == null) backing = logicTarget.GetComponent<UdonBehaviour>();

                if (backing != null)
                {
                    if (btn != null)
                    {
                        int count = btn.onClick.GetPersistentEventCount();
                        for (int i = count - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(btn.onClick, i);
                        UnityEventTools.AddStringPersistentListener(btn.onClick, backing.SendCustomEvent, methodName);
                    }
                    else if (tog != null)
                    {
                        int count = tog.onValueChanged.GetPersistentEventCount();
                        for (int i = count - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(tog.onValueChanged, i);
                        UnityEventTools.AddStringPersistentListener(tog.onValueChanged, backing.SendCustomEvent, methodName);
                    }
                    else if (sld != null)
                    {
                        int count = sld.onValueChanged.GetPersistentEventCount();
                        for (int i = count - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(sld.onValueChanged, i);
                        UnityEventTools.AddStringPersistentListener(sld.onValueChanged, backing.SendCustomEvent, methodName);
                    }
                }
            }

            if (node.Attributes.TryGetValue("u-bind", out string varName))
            {
                Component comp = obj.GetComponent<TMP_InputField>();
                if (comp == null) comp = obj.GetComponent<Slider>();
                if (comp == null) comp = obj.GetComponent<Toggle>();
                if (comp == null) comp = obj.GetComponent<HorizonDataGrid>();
                if (comp == null) comp = obj.GetComponent<TextMeshProUGUI>();
                if (comp == null) comp = obj.GetComponent<Transform>();

                var binder = new HorizonGUIFactory.HorizonLogicBinder(logicTarget);
                binder.Bind(varName, comp);
                binder.Apply();
            }
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
        /// Determines the pixelsPerUnitMultiplier based on the node context (e.g., sidebar vs rows).
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
                    if (img.sprite == null)
                    {
                        img.sprite = HorizonGUIFactory.GetOrGenerateRoundedSprite();
                        img.type = Image.Type.Sliced;

                        bool isFullRound = false;
                        if (node != null && node.Attributes.TryGetValue("class", out string cls))
                        {
                            string lowCls = cls.ToLower();
                            if (lowCls.Contains("sidebar") || lowCls.Contains("nav-btn") || lowCls.Contains("profile-btn"))
                                isFullRound = true;
                        }
                        img.pixelsPerUnitMultiplier = isFullRound ? 1.0f : 3.0f;
                    }
                }
            }

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
        /// </summary>
        private static void ApplyLayoutStyles(GameObject go, Dictionary<string, string> styles)
        {
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