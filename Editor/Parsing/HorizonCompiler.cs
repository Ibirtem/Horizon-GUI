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
    /// Core engine that translates HorizonNode tree into Unity UI GameObjects.
    /// Handles CSS application and Udon logic binding.
    /// </summary>
    public static class HorizonCompiler
    {
        private static UdonSharpBehaviour _targetLogic;

        public static void BuildInterface(GameObject rootContainer, HorizonNode rootNode, HorizonStyleSheet styleSheet, UdonSharpBehaviour logicTarget)
        {
            _targetLogic = logicTarget;

            // Clear old
            while (rootContainer.transform.childCount > 0)
                GameObject.DestroyImmediate(rootContainer.transform.GetChild(0).gameObject);

            // Build new
            foreach (var child in rootNode.Children)
            {
                BuildNode(child, rootContainer, styleSheet);
            }
        }

        private static void BuildNode(HorizonNode node, GameObject parent, HorizonStyleSheet styleSheet)
        {
            GameObject createdObj = null;
            var styles = styleSheet.GetComputedStyle(node);
            string tag = node.Tag.ToLower();
            string objName = GetNodeName(node);

            // --- 1. ELEMENT CREATION STRATEGY ---
            switch (tag)
            {
                case "scroll":
                    createdObj = BuildScroll(objName, parent, styles);
                    break;

                case "button":
                    createdObj = BuildButton(objName, parent, styles);
                    break;

                case "text":
                case "h1":
                case "h2":
                case "p":
                case "label":
                    createdObj = BuildText(node, parent, styles);
                    break;

                case "input":
                    createdObj = BuildInput(node, parent, styles);
                    break;

                case "toggle":
                    createdObj = BuildToggle(node, parent, styles);
                    break;

                case "icon":
                case "img":
                    createdObj = BuildIcon(node, parent, styles);
                    break;

                case "div":
                case "view":
                case "section":
                default:
                    createdObj = BuildContainer(objName, parent, styles);
                    break;
            }

            // --- 2. GENERIC PROCESSING ---
            if (createdObj != null)
            {
                ProcessLogic(createdObj, node);

                // --- 3. RECURSION ---
                foreach (var child in node.Children)
                {
                    BuildNode(child, createdObj, styleSheet);
                }

                // Reset Z for safety
                if (createdObj.transform is RectTransform rt)
                {
                    Vector3 pos = rt.anchoredPosition3D;
                    pos.z = 0;
                    rt.anchoredPosition3D = pos;
                    rt.localRotation = Quaternion.identity;
                }
            }
        }

        // --- BUILDERS ---

        private static GameObject BuildScroll(string name, GameObject parent, Dictionary<string, string> styles)
        {
            GameObject content = HorizonGUIFactory.CreateScrollableColumn(name, parent);

            // Navigate up to Root: Content -> Viewport -> ScrollView(Root)
            if (content.transform.parent != null && content.transform.parent.parent != null)
            {
                GameObject root = content.transform.parent.parent.gameObject;
                ApplyLayoutStyles(root, styles);
                ApplyContainerStyles(content, styles);
            }

            return content;
        }

        private static GameObject BuildButton(string name, GameObject parent, Dictionary<string, string> styles)
        {
            // 1. Root & State Layer (Background)
            Sprite bgSprite = HorizonGUIFactory.GetOrGenerateRoundedSprite();
            Color baseColor = new Color(1, 1, 1, 0.05f);

            GameObject btnRoot = HorizonGUIFactory.CreatePanel(name, parent, baseColor, bgSprite);
            Image stateImg = btnRoot.GetComponent<Image>();
            stateImg.raycastTarget = true;

            // 2. Interaction Layer (Hover/Press Overlay)
            GameObject hoverObj = HorizonGUIFactory.CreatePanel("Interaction_Overlay", btnRoot, Color.white, bgSprite);
            HorizonGUIFactory.Stretch(hoverObj);
            Image hoverImg = hoverObj.GetComponent<Image>();
            hoverImg.raycastTarget = false;
            hoverImg.type = Image.Type.Sliced;

            LayoutElement le = hoverObj.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // 3. Button Component
            Button btn = btnRoot.AddComponent<Button>();
            btn.targetGraphic = hoverImg;
            btn.transition = Selectable.Transition.ColorTint;

            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(1, 1, 1, 0f); // Invisible overlay by default
            cb.highlightedColor = new Color(1, 1, 1, 0.1f);
            cb.pressedColor = new Color(1, 1, 1, 0.2f);
            cb.selectedColor = new Color(1, 1, 1, 0.0f);
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            // 4. Layout for Children
            if (!styles.ContainsKey("align-items")) styles["align-items"] = "center";
            if (!styles.ContainsKey("padding")) styles["padding"] = "10px";

            ApplyContainerStyles(btnRoot, styles);
            ApplyLayoutStyles(btnRoot, styles);

            // Ensure VRC Interaction
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

        private static GameObject BuildContainer(string name, GameObject parent, Dictionary<string, string> styles)
        {
            GameObject go = HorizonGUIFactory.CreateBlock(name, parent);
            ApplyContainerStyles(go, styles);
            ApplyLayoutStyles(go, styles);
            return go;
        }

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

        private static GameObject BuildInput(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            Sprite bg = HorizonGUIFactory.GetOrGenerateRoundedSprite();
            GameObject root = HorizonGUIFactory.CreatePanel(GetNodeName(node), parent, new Color(1, 1, 1, 0.1f), bg);
            Image img = root.GetComponent<Image>();
            img.raycastTarget = true;

            GameObject textArea = HorizonGUIFactory.CreateBlock("Text Area", root);
            RectTransform taRect = textArea.GetComponent<RectTransform>();
            taRect.anchorMin = Vector2.zero; taRect.anchorMax = Vector2.one;
            taRect.offsetMin = new Vector2(10, 0); taRect.offsetMax = new Vector2(-10, 0);

            var t = HorizonGUIFactory.CreateText(textArea, "", HorizonGUIFactory.TextStyle.Body);
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

            // Interaction
            if (root.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null) root.AddComponent<VRC.SDK3.Components.VRCUiShape>();
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;

            HorizonGUIFactory.SetLayoutSize(root, minH: 40, prefH: 40);
            ApplyContainerStyles(root, styles);
            ApplyLayoutStyles(root, styles);

            return root;
        }

        private static GameObject BuildToggle(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            GameObject root = HorizonGUIFactory.CreateRow(GetNodeName(node), parent, spacing: 10, align: TextAnchor.MiddleLeft);

            GameObject bgObj = HorizonGUIFactory.CreatePanel("Background", root, new Color(1, 1, 1, 0.1f), HorizonGUIFactory.GetOrGenerateRoundedSprite());
            HorizonGUIFactory.SetLayoutSize(bgObj, 32, 32, 32, 32);

            GameObject checkObj = HorizonGUIFactory.CreatePanel("Checkmark", bgObj, Color.white, HorizonGUIFactory.LoadPackageSprite("checkmark.png"));
            // Center checkmark
            RectTransform checkRect = checkObj.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.2f, 0.2f); checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = Vector2.zero; checkRect.offsetMax = Vector2.zero;

            Toggle tog = root.AddComponent<Toggle>();
            tog.targetGraphic = bgObj.GetComponent<Image>();
            tog.graphic = checkObj.GetComponent<Image>();
            tog.isOn = false;

            if (!string.IsNullOrEmpty(node.TextContent))
            {
                HorizonGUIFactory.CreateText(root, node.TextContent, HorizonGUIFactory.TextStyle.Body);
            }

            if (root.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null) root.AddComponent<VRC.SDK3.Components.VRCUiShape>();
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;

            ApplyContainerStyles(root, styles);
            ApplyLayoutStyles(root, styles);

            return root;
        }

        private static GameObject BuildIcon(HorizonNode node, GameObject parent, Dictionary<string, string> styles)
        {
            GameObject go = HorizonGUIFactory.CreateBlock(GetNodeName(node), parent);
            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;

            if (node.Attributes.TryGetValue("src", out string src))
            {
                img.sprite = HorizonGUIFactory.LoadPackageSprite(src);
                if (img.sprite != null) img.color = Color.white;
            }

            HorizonGUIFactory.SetLayoutSize(go, 32, 32, 32, 32);
            ApplyLayoutStyles(go, styles);
            return go;
        }

        // --- HELPERS ---

        private static void ProcessLogic(GameObject obj, HorizonNode node)
        {
            // u-click
            if (node.Attributes.TryGetValue("u-click", out string methodName))
            {
                if (_targetLogic == null) return;

                Button btn = obj.GetComponent<Button>();
                Toggle tog = obj.GetComponent<Toggle>();

                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(_targetLogic);
                if (backing == null) backing = _targetLogic.GetComponent<UdonBehaviour>();

                if (backing != null)
                {
                    if (btn != null)
                    {
                        int count = btn.onClick.GetPersistentEventCount();
                        for (int i = count - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(btn.onClick, i);
                        UnityEventTools.AddStringPersistentListener(btn.onClick, backing.SendCustomEvent, methodName);
                    }
                    if (tog != null)
                    {
                        int count = tog.onValueChanged.GetPersistentEventCount();
                        for (int i = count - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(tog.onValueChanged, i);
                        UnityEventTools.AddStringPersistentListener(tog.onValueChanged, backing.SendCustomEvent, methodName);
                    }
                }
            }

            // u-bind
            if (node.Attributes.TryGetValue("u-bind", out string varName))
            {
                if (_targetLogic != null)
                {
                    Component comp = obj.GetComponent<TMP_InputField>();
                    if (comp == null) comp = obj.GetComponent<Slider>();
                    if (comp == null) comp = obj.GetComponent<Toggle>();
                    if (comp == null) comp = obj.GetComponent<TextMeshProUGUI>();
                    if (comp == null) comp = obj.GetComponent<Transform>();

                    var binder = new HorizonGUIFactory.HorizonLogicBinder(_targetLogic);
                    binder.Bind(varName, comp);
                    binder.Apply();
                }
            }
        }

        private static string GetNodeName(HorizonNode node)
        {
            if (node.Attributes.ContainsKey("id")) return node.Attributes["id"];
            if (node.Attributes.ContainsKey("class")) return $"{node.Tag}.{node.Attributes["class"].Split(' ')[0]}";
            return node.Tag;
        }

        private static void ApplyContainerStyles(GameObject go, Dictionary<string, string> styles)
        {
            // Background
            if (styles.TryGetValue("background-color", out string hex))
            {
                Image img = go.GetComponent<Image>();
                if (img == null) img = go.AddComponent<Image>();
                if (ColorUtility.TryParseHtmlString(hex, out Color col))
                {
                    img.color = col;
                    if (img.sprite == null) { img.sprite = HorizonGUIFactory.GetOrGenerateRoundedSprite(); img.type = Image.Type.Sliced; }
                }
            }

            // Layout Group
            bool isRow = styles.ContainsKey("flex-direction") && styles["flex-direction"] == "row";
            float spacing = ParseFloat(styles, "gap", 0) + ParseFloat(styles, "spacing", 0);
            int padding = (int)ParseFloat(styles, "padding", 0);

            TextAnchor align = TextAnchor.UpperLeft;
            if (styles.TryGetValue("align-items", out string alignVal))
            {
                if (alignVal == "center") align = TextAnchor.MiddleCenter;
                if (alignVal == "flex-end") align = TextAnchor.LowerRight;
            }

            LayoutGroup lg = go.GetComponent<LayoutGroup>();
            if (lg == null)
            {
                if (isRow) lg = go.AddComponent<HorizontalLayoutGroup>();
                else lg = go.AddComponent<VerticalLayoutGroup>();
            }

            lg.padding = new RectOffset(padding, padding, padding, padding);

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
                vlg.childForceExpandWidth = true;
            }
        }

        private static void ApplyLayoutStyles(GameObject go, Dictionary<string, string> styles)
        {
            float w = ParseFloat(styles, "width", -1);
            float h = ParseFloat(styles, "height", -1);
            float flex = ParseFloat(styles, "flex-grow", -1);

            HorizonGUIFactory.SetLayoutSize(go,
                minW: w > 0 ? w : (float?)null,
                minH: h > 0 ? h : (float?)null,
                prefW: w > 0 ? w : (float?)null,
                prefH: h > 0 ? h : (float?)null,
                flexH: flex >= 0 ? flex : -1,
                flexW: flex >= 0 ? flex : -1
            );
        }

        private static void ApplyTextStyles(TextMeshProUGUI tmp, Dictionary<string, string> styles)
        {
            if (styles.TryGetValue("color", out string hex)) { if (ColorUtility.TryParseHtmlString(hex, out Color col)) tmp.color = col; }
            if (styles.TryGetValue("font-size", out string sizeStr)) { if (float.TryParse(sizeStr, out float size)) tmp.fontSize = size; }
            if (styles.TryGetValue("text-align", out string align))
            {
                if (align == "center") tmp.alignment = TextAlignmentOptions.Center;
                if (align == "right") tmp.alignment = TextAlignmentOptions.Right;
                if (align == "left") tmp.alignment = TextAlignmentOptions.Left;
            }
        }

        private static float ParseFloat(Dictionary<string, string> styles, string key, float def)
        {
            if (styles.TryGetValue(key, out string val))
            {
                val = val.Replace("px", "").Trim();
                if (float.TryParse(val, out float result)) return result;
            }
            return def;
        }
    }
}