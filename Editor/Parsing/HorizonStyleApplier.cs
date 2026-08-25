using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlackHorizon.HorizonGUI.Editor.Parsing
{
    /// <summary>
    /// Centralized styling engine that maps computed CSS rules to Unity UI components.
    /// Manages color parsing, dynamic 9-slice border-radius calculation, layout groups, and typography.
    /// </summary>
    public static class HorizonStyleApplier
    {
        /// <summary>
        /// Generates a readable hierarchy name for a GameObject based on node ID, class, or tag.
        /// Priority: id -> tag.class -> tag.
        /// </summary>
        /// <param name="node">Source AST node.</param>
        /// <returns>Formatted GameObject name string.</returns>
        public static string GetNodeName(HorizonNode node)
        {
            if (node.Attributes.ContainsKey("id")) return node.Attributes["id"];
            if (node.Attributes.ContainsKey("class")) return $"{node.Tag}.{node.Attributes["class"].Split(' ')[0]}";
            return node.Tag;
        }

        /// <summary>
        /// Applies container-level visual properties: background colors, procedural 9-slice borders,
        /// and auto-configures Horizontal/Vertical/Grid Layout Groups with padding and gap spacing.
        /// </summary>
        /// <param name="go">Target GameObject receiving styling.</param>
        /// <param name="styles">Computed CSS properties map.</param>
        /// <param name="node">Source AST node for context-aware class checks.</param>
        public static void ApplyContainerStyles(GameObject go, Dictionary<string, string> styles, HorizonNode node)
        {
            if (styles.TryGetValue("background-color", out string hex))
            {
                Image img = go.GetComponent<Image>() ?? go.AddComponent<Image>();

                if (ColorUtility.TryParseHtmlString(hex, out Color col))
                {
                    img.color = col;

                    if (col.a <= 0.01f)
                    {
                        img.raycastTarget = false;
                    }

                    if (img.sprite == null)
                    {
                        img.sprite = HorizonAssetPipeline.GetOrGenerateRoundedSprite();
                        img.type = Image.Type.Sliced;

                        // Procedural sprite base dimensions: 128x128 with 64px radius borders
                        const float srcRadius = 64f;
                        RectTransform rt = go.GetComponent<RectTransform>();

                        float w = ParseFloat(styles, "width", -1);
                        float h = ParseFloat(styles, "height", -1);
                        if (w <= 0) w = rt.rect.width;
                        if (h <= 0) h = rt.rect.height;
                        if (w <= 1) w = 100;
                        if (h <= 1) h = 100;

                        float minSide = Mathf.Min(w, h);
                        float maxPossibleRadius = minSide / 2f;
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

                        targetRadius = Mathf.Clamp(targetRadius, 1f, maxPossibleRadius);

                        // Adjust pixelsPerUnitMultiplier to scale the 64px slice to match the desired CSS border radius
                        img.pixelsPerUnitMultiplier = srcRadius / targetRadius;
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
            if (lg == null && go.GetComponent<Slider>() == null && go.GetComponent<TMP_InputField>() == null)
            {
                lg = isRow ? (LayoutGroup)go.AddComponent<HorizontalLayoutGroup>() : go.AddComponent<VerticalLayoutGroup>();
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
        /// Applies sizing, expansion weights (flex-grow), and positioning overrides to a LayoutElement component.
        /// </summary>
        /// <param name="go">Target GameObject.</param>
        /// <param name="styles">Computed CSS properties map.</param>
        /// <param name="node">Source AST node checking for 'ignore-layout'.</param>
        public static void ApplyLayoutStyles(GameObject go, Dictionary<string, string> styles, HorizonNode node = null)
        {
            if (node != null && node.Attributes.ContainsKey("ignore-layout"))
            {
                LayoutElement le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                return;
            }

            float w = ParseFloat(styles, "width", -1);
            float h = ParseFloat(styles, "height", -1);
            float flex = ParseFloat(styles, "flex-grow", -1);

            float flexW = flex >= 0 ? flex : (w > 0 ? 0 : -1);
            float flexH = flex >= 0 ? flex : (h > 0 ? 0 : -1);

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
        /// Applies typography rules (color, font-size, alignment, font-style) to a TextMeshProUGUI component.
        /// </summary>
        /// <param name="tmp">Target TextMeshPro component.</param>
        /// <param name="styles">Computed CSS properties map.</param>
        public static void ApplyTextStyles(TextMeshProUGUI tmp, Dictionary<string, string> styles)
        {
            if (styles.TryGetValue("color", out string hex) && ColorUtility.TryParseHtmlString(hex, out Color col))
                tmp.color = col;

            if (styles.ContainsKey("font-size"))
                tmp.fontSize = ParseFloat(styles, "font-size", tmp.fontSize);

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

        /// <summary>
        /// Parses a float attribute/property from a dictionary, automatically stripping 'px' units.
        /// </summary>
        /// <param name="attrs">Properties map.</param>
        /// <param name="key">Target property name.</param>
        /// <param name="def">Default fallback value.</param>
        /// <returns>Parsed float value or default fallback.</returns>
        public static float ParseFloat(Dictionary<string, string> attrs, string key, float def)
        {
            if (attrs.TryGetValue(key, out string val))
            {
                val = val.Replace("px", "").Trim();
                if (float.TryParse(val, out float result)) return result;
            }
            return def;
        }

        /// <summary>
        /// Parses an integer attribute/property from a dictionary.
        /// </summary>
        /// <param name="attrs">Properties map.</param>
        /// <param name="key">Target property name.</param>
        /// <param name="def">Default fallback value.</param>
        /// <returns>Parsed integer value or default fallback.</returns>
        public static int ParseInt(Dictionary<string, string> attrs, string key, int def)
        {
            if (attrs.TryGetValue(key, out string val) && int.TryParse(val, out int result))
                return result;
            return def;
        }
    }
}