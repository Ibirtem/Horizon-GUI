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

        /// <summary>
        /// Clears the container and builds the interface from the root node.
        /// </summary>
        public static void BuildInterface(GameObject rootContainer, HorizonNode rootNode, HorizonStyleSheet styleSheet, UdonSharpBehaviour logicTarget)
        {
            _targetLogic = logicTarget;

            while (rootContainer.transform.childCount > 0)
                GameObject.DestroyImmediate(rootContainer.transform.GetChild(0).gameObject);

            foreach (var child in rootNode.Children)
            {
                BuildNode(child, rootContainer, styleSheet);
            }
        }

        /// <summary>
        /// Recursively constructs a UI element based on the tag and attributes.
        /// </summary>
        private static void BuildNode(HorizonNode node, GameObject parent, HorizonStyleSheet styleSheet)
        {
            GameObject createdObj = null;
            var styles = styleSheet.GetComputedStyle(node);
            string tag = node.Tag.ToLower();

            if (tag == "div" || tag == "view" || tag == "section")
            {
                createdObj = HorizonGUIFactory.CreateBlock(GetNodeName(node), parent);
                ApplyContainerStyles(createdObj, styles);
            }
            else if (tag == "text" || tag == "h1" || tag == "h2" || tag == "p" || tag == "label")
            {
                HorizonGUIFactory.TextStyle defStyle = HorizonGUIFactory.TextStyle.Body;
                if (tag == "h1") defStyle = HorizonGUIFactory.TextStyle.H1;
                if (tag == "h2") defStyle = HorizonGUIFactory.TextStyle.H2;
                if (tag == "label") defStyle = HorizonGUIFactory.TextStyle.SmallDim;

                var tmp = HorizonGUIFactory.CreateText(parent, node.TextContent, defStyle);
                createdObj = tmp.gameObject;
                createdObj.name = GetNodeName(node);
                ApplyTextStyles(tmp, styles);
            }
            else if (tag == "button")
            {
                Sprite bg = HorizonGUIFactory.GetOrGenerateRoundedSprite();
                Color baseColor = new Color(1, 1, 1, 0.05f);

                createdObj = HorizonGUIFactory.CreatePanel(GetNodeName(node), parent, baseColor, bg);
                Image img = createdObj.GetComponent<Image>();
                img.raycastTarget = true;

                Button btn = createdObj.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.ColorTint;

                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                cb.selectedColor = Color.white;
                cb.fadeDuration = 0.1f;
                btn.colors = cb;

                var hlg = createdObj.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.spacing = 10;
                hlg.padding = new RectOffset(15, 15, 8, 8);

                if (createdObj.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null)
                    createdObj.AddComponent<VRC.SDK3.Components.VRCUiShape>();

                BoxCollider col = createdObj.GetComponent<BoxCollider>();
                if (col == null) col = createdObj.AddComponent<BoxCollider>();
                col.isTrigger = true;

                ApplyContainerStyles(createdObj, styles);
            }
            else if (tag == "input")
            {
                Sprite bg = HorizonGUIFactory.GetOrGenerateRoundedSprite();
                createdObj = HorizonGUIFactory.CreatePanel(GetNodeName(node), parent, new Color(1, 1, 1, 0.1f), bg);
                Image img = createdObj.GetComponent<Image>();
                img.raycastTarget = true;

                GameObject textArea = HorizonGUIFactory.CreateBlock("Text Area", createdObj);
                RectTransform taRect = textArea.GetComponent<RectTransform>();
                taRect.anchorMin = Vector2.zero; taRect.anchorMax = Vector2.one;
                taRect.offsetMin = new Vector2(10, 0); taRect.offsetMax = new Vector2(-10, 0);

                var t = HorizonGUIFactory.CreateText(textArea, "", HorizonGUIFactory.TextStyle.Body);
                t.color = Color.white;

                GameObject placeObj = HorizonGUIFactory.CreateBlock("Placeholder", textArea);
                RectTransform placeRect = placeObj.GetComponent<RectTransform>();
                placeRect.anchorMin = Vector2.zero; placeRect.anchorMax = Vector2.one;
                var p = HorizonGUIFactory.CreateText(placeObj, node.Attributes.ContainsKey("placeholder") ? node.Attributes["placeholder"] : "Enter text...", HorizonGUIFactory.TextStyle.BodyDim);
                p.fontStyle = FontStyles.Italic;

                TMP_InputField inp = createdObj.AddComponent<TMP_InputField>();
                inp.textViewport = taRect;
                inp.textComponent = t;
                inp.placeholder = p;
                inp.targetGraphic = img;

                if (createdObj.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null) createdObj.AddComponent<VRC.SDK3.Components.VRCUiShape>();
                BoxCollider col = createdObj.AddComponent<BoxCollider>();
                col.isTrigger = true;

                HorizonGUIFactory.SetLayoutSize(createdObj, minH: 40, prefH: 40);
                ApplyContainerStyles(createdObj, styles);
            }
            else if (tag == "toggle")
            {
                createdObj = HorizonGUIFactory.CreateRow(GetNodeName(node), parent, spacing: 10, align: TextAnchor.MiddleLeft);

                GameObject bgObj = HorizonGUIFactory.CreatePanel("Background", createdObj, new Color(1, 1, 1, 0.1f), HorizonGUIFactory.GetOrGenerateRoundedSprite());
                HorizonGUIFactory.SetLayoutSize(bgObj, 32, 32, 32, 32);

                GameObject checkObj = HorizonGUIFactory.CreatePanel("Checkmark", bgObj, Color.white, HorizonGUIFactory.LoadPackageSprite("checkmark.png"));
                RectTransform checkRect = checkObj.GetComponent<RectTransform>();
                checkRect.anchorMin = new Vector2(0.2f, 0.2f); checkRect.anchorMax = new Vector2(0.8f, 0.8f);
                checkRect.offsetMin = Vector2.zero; checkRect.offsetMax = Vector2.zero;

                Toggle tog = createdObj.AddComponent<Toggle>();
                tog.targetGraphic = bgObj.GetComponent<Image>();
                tog.graphic = checkObj.GetComponent<Image>();
                tog.isOn = false;

                if (!string.IsNullOrEmpty(node.TextContent))
                {
                    HorizonGUIFactory.CreateText(createdObj, node.TextContent, HorizonGUIFactory.TextStyle.Body);
                }

                if (createdObj.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null) createdObj.AddComponent<VRC.SDK3.Components.VRCUiShape>();
                BoxCollider col = createdObj.AddComponent<BoxCollider>();
                col.isTrigger = true;

                ApplyContainerStyles(createdObj, styles);
            }
            else if (tag == "icon" || tag == "img")
            {
                createdObj = HorizonGUIFactory.CreateBlock(GetNodeName(node), parent);
                Image img = createdObj.AddComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                if (node.Attributes.TryGetValue("src", out string src))
                {
                    img.sprite = HorizonGUIFactory.LoadPackageSprite(src);
                    if (img.sprite != null) img.color = Color.white;
                }
                HorizonGUIFactory.SetLayoutSize(createdObj, 32, 32, 32, 32);
                ApplyLayoutStyles(createdObj, styles);
            }

            if (createdObj == null) createdObj = HorizonGUIFactory.CreateBlock($"Unknown_{tag}", parent);

            ApplyLayoutStyles(createdObj, styles);
            ProcessLogic(createdObj, node);

            foreach (var child in node.Children)
            {
                BuildNode(child, createdObj, styleSheet);
            }

            if (createdObj.transform is RectTransform rt)
            {
                Vector3 pos = rt.anchoredPosition3D;
                pos.z = 0;
                rt.anchoredPosition3D = pos;
                rt.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Binds UnityEvents (u-click) and variable references (u-bind) to the target UdonBehaviour.
        /// </summary>
        private static void ProcessLogic(GameObject obj, HorizonNode node)
        {
            if (node.Attributes.TryGetValue("u-click", out string methodName))
            {
                Debug.Log($"[Horizon] Processing u-click='{methodName}' for {obj.name}...");

                if (_targetLogic == null)
                {
                    Debug.LogError("[Horizon] ❌ FAILED: 'Backing Logic' is empty in the Inspector! Please assign your Udon script.");
                    return;
                }

                Button btn = obj.GetComponent<Button>();
                if (btn == null)
                {
                    Debug.LogError($"[Horizon] ❌ FAILED: Object '{obj.name}' has u-click but NO Button component.");
                    return;
                }

                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(_targetLogic);
                if (backing == null) backing = _targetLogic.GetComponent<UdonBehaviour>();

                if (backing != null)
                {
                    int count = btn.onClick.GetPersistentEventCount();
                    for (int i = count - 1; i >= 0; i--)
                        UnityEventTools.RemovePersistentListener(btn.onClick, i);

                    UnityEventTools.AddStringPersistentListener(
                        btn.onClick,
                        backing.SendCustomEvent,
                        methodName
                    );

                    EditorUtility.SetDirty(btn);
                    EditorUtility.SetDirty(obj);

                    Debug.Log($"[Horizon] ✅ SUCCESS: Bound '{methodName}' to button '{obj.name}'. Target: {backing.name}");
                }
                else
                {
                    Debug.LogError($"[Horizon] ❌ CRITICAL: Could not find UdonBehaviour on target object '{_targetLogic.name}'. Is UdonSharp setup correctly?");
                }
            }

            if (node.Attributes.TryGetValue("u-bind", out string varName))
            {
                Component comp = obj.GetComponent<TMP_InputField>();
                if (comp == null) comp = obj.GetComponent<Slider>();
                if (comp == null) comp = obj.GetComponent<Toggle>();
                if (comp == null) comp = obj.GetComponent<TextMeshProUGUI>();
                if (comp == null) comp = obj.GetComponent<Transform>();

                if (_targetLogic != null)
                {
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

            if (lg is HorizontalLayoutGroup hlg) { hlg.spacing = spacing; hlg.childAlignment = align; hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false; }
            if (lg is VerticalLayoutGroup vlg) { vlg.spacing = spacing; vlg.childAlignment = align; vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false; }
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
                flexH: flex > 0 ? flex : -1,
                flexW: flex > 0 ? flex : -1
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