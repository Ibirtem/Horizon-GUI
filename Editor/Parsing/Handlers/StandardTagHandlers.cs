using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UdonSharpEditor;

namespace BlackHorizon.HorizonGUI.Editor.Parsing.Handlers
{
    /// <summary>
    /// Compiles standard container elements (div, view, section, container) into flex layout blocks.
    /// </summary>
    public class ContainerTagHandler : IHorizonTagHandler
    {
        public string[] SupportedTags => new[] { "view", "div", "section", "container" };

        public GameObject Build(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonBuildContext context)
        {
            GameObject go = HorizonGUIFactory.CreateBlock(HorizonStyleApplier.GetNodeName(node), parent);
            HorizonStyleApplier.ApplyContainerStyles(go, styles, node);
            HorizonStyleApplier.ApplyLayoutStyles(go, styles, node);
            return go;
        }
    }

    /// <summary>
    /// Compiles typography tags (text, h1, h2, p, label) into styled TextMeshProUGUI elements.
    /// Supports dynamic framework version interpolation via 'h-version' attribute.
    /// </summary>
    public class TextTagHandler : IHorizonTagHandler
    {
        public string[] SupportedTags => new[] { "text", "h1", "h2", "p", "label" };

        public GameObject Build(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonBuildContext context)
        {
            if (node.Attributes.ContainsKey("h-version"))
            {
                string ver = HorizonEditorUtils.GetVersion(null);
                if (!string.IsNullOrEmpty(node.TextContent) && node.TextContent.Contains("{v}"))
                    node.TextContent = node.TextContent.Replace("{v}", ver);
                else
                    node.TextContent = $"v{ver}";
            }

            var tmp = HorizonGUIFactory.CreateText(parent, node.TextContent);
            tmp.gameObject.name = HorizonStyleApplier.GetNodeName(node);

            HorizonStyleApplier.ApplyTextStyles(tmp, styles);
            HorizonStyleApplier.ApplyLayoutStyles(tmp.gameObject, styles);
            return tmp.gameObject;
        }
    }

    /// <summary>
    /// Compiles interactive buttons with background layers and transparent interaction overlays for hover effects.
    /// </summary>
    public class ButtonTagHandler : IHorizonTagHandler
    {
        public string[] SupportedTags => new[] { "button" };

        public GameObject Build(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonBuildContext context)
        {
            GameObject btnRoot = HorizonGUIFactory.CreatePanel(HorizonStyleApplier.GetNodeName(node), parent);
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

            HorizonStyleApplier.ApplyContainerStyles(btnRoot, styles, node);
            HorizonStyleApplier.ApplyLayoutStyles(btnRoot, styles);
            return btnRoot;
        }
    }

    /// <summary>
    /// Compiles form input fields. Supports type="range" (Sliders) and type="text" (TMP_InputField with placeholders).
    /// </summary>
    public class InputTagHandler : IHorizonTagHandler
    {
        public string[] SupportedTags => new[] { "input" };

        public GameObject Build(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonBuildContext context)
        {
            string type = node.Attributes.ContainsKey("type") ? node.Attributes["type"].ToLower() : "text";

            if (type == "range")
            {
                if (styles.ContainsKey("background-color")) styles.Remove("background-color");

                float min = HorizonStyleApplier.ParseFloat(node.Attributes, "min", 0f);
                float max = HorizonStyleApplier.ParseFloat(node.Attributes, "max", 1f);
                float val = HorizonStyleApplier.ParseFloat(node.Attributes, "value", 0f);

                Slider s = HorizonGUIFactory.CreateSlider(parent, min, max, val);
                GameObject go = s.gameObject;
                go.name = HorizonStyleApplier.GetNodeName(node);

                HorizonStyleApplier.ApplyLayoutStyles(go, styles, node);
                HorizonStyleApplier.ApplyContainerStyles(go, styles, node);
                return go;
            }

            Sprite bg = HorizonAssetPipeline.GetOrGenerateRoundedSprite();
            GameObject root = HorizonGUIFactory.CreatePanel(HorizonStyleApplier.GetNodeName(node), parent);
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

            if (node.Attributes.ContainsKey("readonly")) inp.readOnly = true;

            HorizonGUIFactory.SetLayoutSize(root, minH: 50, prefH: 50);
            HorizonStyleApplier.ApplyContainerStyles(root, styles, node);
            HorizonStyleApplier.ApplyLayoutStyles(root, styles);

            LayoutElement le = root.GetComponent<LayoutElement>();
            if (le != null) le.flexibleHeight = 0;

            return root;
        }
    }

    /// <summary>
    /// Compiles binary switches (Toggle) equipped with 9-slice background and checkmark graphics.
    /// </summary>
    public class ToggleTagHandler : IHorizonTagHandler
    {
        public string[] SupportedTags => new[] { "toggle" };

        public GameObject Build(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonBuildContext context)
        {
            float spacing = styles.ContainsKey("gap") ? HorizonStyleApplier.ParseFloat(styles, "gap", 15) : 15;
            GameObject root = HorizonGUIFactory.CreateRow(HorizonStyleApplier.GetNodeName(node), parent, spacing: spacing, align: TextAnchor.MiddleLeft);

            GameObject bgObj = HorizonGUIFactory.CreatePanel("Background", root);
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(1, 1, 1, 0.1f);
            bgImg.sprite = HorizonAssetPipeline.GetOrGenerateRoundedSprite();
            bgImg.pixelsPerUnitMultiplier = 64f / 20f;
            bgImg.raycastTarget = true;
            HorizonGUIFactory.SetLayoutSize(bgObj, 40, 40, 40, 40);

            GameObject checkObj = HorizonGUIFactory.CreatePanel("Checkmark", bgObj);
            Image checkImg = checkObj.GetComponent<Image>();
            checkImg.color = Color.white;
            checkImg.sprite = HorizonAssetPipeline.LoadPackageSprite("checkmark.png");

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
                HorizonStyleApplier.ApplyTextStyles(label, styles);
            }

            HorizonStyleApplier.ApplyContainerStyles(root, styles, node);
            HorizonStyleApplier.ApplyLayoutStyles(root, styles);
            return root;
        }
    }

    /// <summary>
    /// Compiles static Image and dynamic RawImage elements (with custom rounded corner shaders).
    /// </summary>
    public class IconTagHandler : IHorizonTagHandler
    {
        public string[] SupportedTags => new[] { "icon", "img" };

        public GameObject Build(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonBuildContext context)
        {
            GameObject go = HorizonGUIFactory.CreateBlock(HorizonStyleApplier.GetNodeName(node), parent);
            bool isRaw = node.Attributes.ContainsKey("raw");

            if (isRaw)
            {
                RawImage img = go.AddComponent<RawImage>();
                img.raycastTarget = false;
                img.color = Color.white;

                Shader s = Shader.Find("Horizon/UI/Rounded RawImage");
                if (s != null) img.material = new Material(s);
            }
            else
            {
                Image img = go.AddComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;

                if (node.Attributes.TryGetValue("src", out string src))
                {
                    img.sprite = HorizonAssetPipeline.LoadSprite(src, context.ResourceMap);
                    img.color = (img.sprite != null) ? Color.white : Color.magenta;
                }
            }

            float w = HorizonStyleApplier.ParseFloat(styles, "width", -1);
            float h = HorizonStyleApplier.ParseFloat(styles, "height", -1);

            if (w > 0 || h > 0)
            {
                HorizonGUIFactory.SetLayoutSize(go,
                    minW: w > 0 ? w : (float?)null,
                    minH: h > 0 ? h : (float?)null,
                    prefW: w > 0 ? w : (float?)null,
                    prefH: h > 0 ? h : (float?)null);
            }

            HorizonStyleApplier.ApplyLayoutStyles(go, styles, node);
            return go;
        }
    }

    /// <summary>
    /// Compiles horizontal dividers (&lt;hr&gt;) using an inner panel with customizable padding and height.
    /// </summary>
    public class SeparatorTagHandler : IHorizonTagHandler
    {
        public string[] SupportedTags => new[] { "hr" };

        public GameObject Build(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonBuildContext context)
        {
            GameObject wrapper = HorizonGUIFactory.CreateBlock(HorizonStyleApplier.GetNodeName(node), parent);
            GameObject line = HorizonGUIFactory.CreatePanel("Visual_Line", wrapper);
            Image img = line.GetComponent<Image>();
            img.sprite = null;

            HorizonStyleApplier.ApplyContainerStyles(line, styles, node);

            float height = HorizonStyleApplier.ParseFloat(styles, "height", 2);
            float width = HorizonStyleApplier.ParseFloat(styles, "width", -1);

            HorizonGUIFactory.SetLayoutSize(wrapper,
                minH: height,
                prefH: height,
                prefW: width > 0 ? width : (float?)null,
                flexW: width > 0 ? 0 : 1
            );

            float padding = HorizonStyleApplier.ParseFloat(styles, "padding", 0);
            RectTransform lineRect = line.GetComponent<RectTransform>();
            lineRect.anchorMin = Vector2.zero;
            lineRect.anchorMax = Vector2.one;
            lineRect.offsetMin = new Vector2(padding, 0);
            lineRect.offsetMax = new Vector2(-padding, 0);

            return wrapper;
        }
    }

    /// <summary>
    /// Compiles complete vertical scrolling viewports including ScrollRect, Viewport mask, and Scrollbars.
    /// </summary>
    public class ScrollTagHandler : IHorizonTagHandler
    {
        public string[] SupportedTags => new[] { "scroll" };

        public GameObject Build(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonBuildContext context)
        {
            GameObject content = HorizonGUIFactory.CreateScrollableColumn(HorizonStyleApplier.GetNodeName(node), parent);
            if (content.transform.parent != null && content.transform.parent.parent != null)
            {
                GameObject root = content.transform.parent.parent.gameObject;
                HorizonStyleApplier.ApplyLayoutStyles(root, styles);
                HorizonStyleApplier.ApplyContainerStyles(content, styles, node);
            }
            return content;
        }
    }

    /// <summary>
    /// Compiles high-performance DataGrids (&lt;h-grid&gt;) using an object-pooled slot architecture.
    /// Extracts child markup as a prototype template, bakes SmartSlot bindings, and links Udon callbacks.
    /// </summary>
    public class GridTagHandler : IHorizonTagHandler
    {
        public string[] SupportedTags => new[] { "h-grid" };

        public GameObject Build(HorizonNode node, GameObject parent, Dictionary<string, string> styles, HorizonBuildContext context)
        {
            int poolSize = HorizonStyleApplier.ParseInt(node.Attributes, "pool", 64);
            float w = HorizonStyleApplier.ParseFloat(node.Attributes, "cell-w", 100);
            float h = HorizonStyleApplier.ParseFloat(node.Attributes, "cell-h", 100);

            if (!styles.ContainsKey("gap") && !styles.ContainsKey("spacing"))
            {
                float attrSpacing = HorizonStyleApplier.ParseFloat(node.Attributes, "spacing", 10);
                styles["gap"] = $"{attrSpacing}px";
            }

            GameObject gridObj = HorizonGUIFactory.CreateGrid(HorizonStyleApplier.GetNodeName(node), parent, new Vector2(w, h), Vector2.zero);
            HorizonStyleApplier.ApplyLayoutStyles(gridObj, styles, node);
            HorizonStyleApplier.ApplyContainerStyles(gridObj, styles, node);

            var manager = HorizonGUIFactory.AttachLogic<HorizonDataGrid>(gridObj);

            // 1. Prepare Prototype Template (Setting IsBuildingTemplate ensures all recursive descendants are tagged)
            GameObject prototype = null;
            if (node.Children.Count > 0)
            {
                GameObject tempHolder = new GameObject("Temp_Holder");
                tempHolder.SetActive(false);
                bool previousTemplateState = context.IsBuildingTemplate;
                context.IsBuildingTemplate = true;

                try
                {
                    context.NodeBuilder(node.Children[0], tempHolder, context);
                }
                finally
                {
                    context.IsBuildingTemplate = previousTemplateState;
                }

                if (tempHolder.transform.childCount > 0)
                {
                    prototype = tempHolder.transform.GetChild(0).gameObject;
                }
                else
                {
                    Object.DestroyImmediate(tempHolder);
                }
            }

            bool isTemplated = (prototype != null);
            var slots = new List<HorizonSmartSlot>();

            if (prototype == null)
            {
                Debug.LogError($"<color=red>[HorizonCompiler]</color> Grid '<b>{HorizonStyleApplier.GetNodeName(node)}</b>' has no template! Add children to <h-grid> in HTML.");
                HorizonCompiler.IncrementValidationErrors();
            }

            // 2. Instantiate and wire pooled SmartSlots
            for (int i = 0; i < poolSize; i++)
            {
                string slotName = $"Slot_{i:00}";
                GameObject slotObj;

                if (prototype != null)
                {
                    slotObj = Object.Instantiate(prototype, gridObj.transform);
                    slotObj.name = slotName;
                    slotObj.SetActive(true);
                }
                else
                {
                    slotObj = new GameObject(slotName + "_ERROR");
                    slotObj.transform.SetParent(gridObj.transform);
                    var errImg = slotObj.AddComponent<Image>();
                    errImg.color = Color.red;
                }

                var slotLogic = HorizonGUIFactory.AttachLogic<HorizonSmartSlot>(slotObj);
                BakeSmartSlotBindings(slotObj, slotLogic, isTemplated);

                HorizonGUIFactory.ConfigureLogic<HorizonSmartSlot>(slotObj, binder =>
                {
                    binder.Bind("gridManager", manager);
                    binder.BindVal("slotIndex", i);
                });

                Button btn = slotObj.GetComponent<Button>() ?? slotObj.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    HorizonGUIFactory.ConfigureLogic<HorizonSmartSlot>(slotObj, binder => binder.Bind("mainButton", btn));
                    var backingItem = UdonSharpEditorUtility.GetBackingUdonBehaviour(slotLogic);
                    if (backingItem != null)
                    {
                        UnityEditor.Events.UnityEventTools.AddStringPersistentListener(
                            btn.onClick,
                            backingItem.SendCustomEvent,
                            "OnClick"
                        );
                    }
                }

                slots.Add(slotLogic);
            }

            if (prototype != null && prototype.transform.parent != null)
            {
                Object.DestroyImmediate(prototype.transform.parent.gameObject);
            }

            HorizonGUIFactory.ConfigureLogic<HorizonDataGrid>(gridObj, binder =>
            {
                binder.BindArray("slotPool", slots);
                binder.BindVal("itemsPerPage", poolSize);
            });

            return gridObj;
        }

        /// <summary>
        /// Scans an instantiated slot prototype for '__BIND__' name markers and populates the SmartSlot lookup arrays.
        /// </summary>
        private static void BakeSmartSlotBindings(GameObject root, HorizonSmartSlot slot, bool isTemplated)
        {
            var textKeys = new List<string>();
            var textTargets = new List<TextMeshProUGUI>();
            var imgKeys = new List<string>();
            var imgTargets = new List<Image>();
            var rawKeys = new List<string>();
            var rawTargets = new List<RawImage>();

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

                var raws = root.GetComponentsInChildren<RawImage>();
                foreach (var raw in raws)
                {
                    if (raw.gameObject != root)
                    {
                        rawKeys.Add("MainRaw"); rawTargets.Add(raw);
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
                        if (txt != null) { textKeys.Add(key); textTargets.Add(txt); }

                        var img = tr.GetComponent<Image>();
                        if (img != null) { imgKeys.Add(key); imgTargets.Add(img); }

                        var raw = tr.GetComponent<RawImage>();
                        if (raw != null) { rawKeys.Add(key); rawTargets.Add(raw); }

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
                binder.BindArray("rawKeys", rawKeys);
                binder.BindArray("rawTargets", rawTargets);
            });
        }
    }
}