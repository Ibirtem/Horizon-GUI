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
        /// Supported CSS dimension unit types.
        /// </summary>
        public enum StyleUnitType
        {
            None,
            Pixel,
            Percent,
            Auto
        }

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
        /// Applies container-level visual properties and auto-configures layout groups ensuring child dimensions are always driven by uGUI.
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
                        img.pixelsPerUnitMultiplier = srcRadius / targetRadius;
                    }
                }
            }

            bool isRow = styles.ContainsKey("flex-direction") && styles["flex-direction"] == "row";
            float spacing = ParseFloat(styles, "gap", 0) + ParseFloat(styles, "spacing", 0);

            RectOffset padding = ParsePadding(styles);

            styles.TryGetValue("align-items", out string alignVal);
            styles.TryGetValue("justify-content", out string justifyVal);
            TextAnchor align = DetermineAlignment(isRow, alignVal, justifyVal);

            bool isStretch = string.IsNullOrEmpty(alignVal) || alignVal == "stretch";

            LayoutGroup lg = go.GetComponent<LayoutGroup>();
            if (lg == null && go.GetComponent<Slider>() == null && go.GetComponent<TMP_InputField>() == null)
            {
                lg = isRow ? (LayoutGroup)go.AddComponent<HorizontalLayoutGroup>() : go.AddComponent<VerticalLayoutGroup>();
            }

            if (lg != null)
            {
                lg.padding = padding;

                if (lg is HorizontalLayoutGroup hlg)
                {
                    hlg.spacing = spacing;
                    hlg.childAlignment = align;
                    hlg.childControlWidth = true;
                    hlg.childControlHeight = true;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = isStretch;
                }
                else if (lg is VerticalLayoutGroup vlg)
                {
                    vlg.spacing = spacing;
                    vlg.childAlignment = align;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = isStretch;
                    vlg.childForceExpandHeight = false;
                }
                else if (lg is GridLayoutGroup glg)
                {
                    glg.spacing = new Vector2(spacing, spacing);
                    glg.childAlignment = align;
                }
            }
        }

        /// <summary>
        /// Applies sizing, expansion weights (flex / flex-grow), and percentage dimensions to the element's LayoutElement.
        /// </summary>
        /// <param name="go">Target GameObject.</param>
        /// <param name="styles">Computed CSS properties map.</param>
        /// <param name="node">Source AST node checking for layout overrides.</param>
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

            float flexGrow = -1f;
            if (styles.TryGetValue("flex-grow", out string fgStr) && float.TryParse(fgStr.Replace("px", "").Trim(), out float fg))
            {
                flexGrow = fg;
            }
            else if (styles.TryGetValue("flex", out string fStr))
            {
                string[] parts = fStr.Trim().Split(' ');
                if (parts.Length > 0 && float.TryParse(parts[0], out float fVal))
                {
                    flexGrow = fVal;
                }
            }

            float? minW = null;
            float? prefW = null;
            float flexW = -1f;

            if (styles.TryGetValue("width", out string widthStr) && TryParseDimension(widthStr, out float wVal, out StyleUnitType wUnit))
            {
                if (wUnit == StyleUnitType.Pixel)
                {
                    minW = wVal;
                    prefW = wVal;
                    flexW = 0f;
                }
                else if (wUnit == StyleUnitType.Percent)
                {
                    flexW = wVal / 100f;
                    minW = 0f;
                    prefW = 0f; // Forces uGUI to distribute width purely by flexible ratio
                }
            }

            if (styles.TryGetValue("min-width", out string minWStr) && TryParseDimension(minWStr, out float parsedMinW, out StyleUnitType minWUnit) && minWUnit == StyleUnitType.Pixel)
            {
                minW = parsedMinW;
            }

            float? minH = null;
            float? prefH = null;
            float flexH = -1f;

            if (styles.TryGetValue("height", out string heightStr) && TryParseDimension(heightStr, out float hVal, out StyleUnitType hUnit))
            {
                if (hUnit == StyleUnitType.Pixel)
                {
                    minH = hVal;
                    prefH = hVal;
                    flexH = 0f;
                }
                else if (hUnit == StyleUnitType.Percent)
                {
                    flexH = hVal / 100f;
                    minH = 0f;
                    prefH = 0f;
                }
            }

            if (styles.TryGetValue("min-height", out string minHStr) && TryParseDimension(minHStr, out float parsedMinH, out StyleUnitType minHUnit) && minHUnit == StyleUnitType.Pixel)
            {
                minH = parsedMinH;
            }

            if (flexGrow >= 0)
            {
                if (flexW < 0) flexW = flexGrow;
                if (flexH < 0) flexH = flexGrow;
            }

            HorizonGUIFactory.SetLayoutSize(go,
                minW: minW,
                minH: minH,
                prefW: prefW,
                prefH: prefH,
                flexW: flexW,
                flexH: flexH
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
        /// Safely parses a float attribute/property from a dictionary, supporting both raw numbers and dimension units.
        /// </summary>
        /// <param name="attrs">Properties map.</param>
        /// <param name="key">Target property name.</param>
        /// <param name="def">Default fallback value.</param>
        /// <returns>Parsed float value or default fallback.</returns>
        public static float ParseFloat(Dictionary<string, string> attrs, string key, float def)
        {
            if (attrs.TryGetValue(key, out string val))
            {
                if (TryParseDimension(val, out float result, out _))
                {
                    return result;
                }
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

        /// <summary>
        /// Parses a CSS dimension string (e.g., '100px', '50%', 'auto') into a numeric value and its corresponding unit type.
        /// </summary>
        /// <param name="raw">The raw string from markup or stylesheet.</param>
        /// <param name="val">The extracted numerical value.</param>
        /// <param name="unit">The identified unit type.</param>
        /// <returns>True if parsing succeeded; otherwise false.</returns>
        public static bool TryParseDimension(string raw, out float val, out StyleUnitType unit)
        {
            val = 0f;
            unit = StyleUnitType.None;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            raw = raw.Trim().ToLowerInvariant();
            if (raw == "auto")
            {
                unit = StyleUnitType.Auto;
                return true;
            }

            if (raw.EndsWith("%"))
            {
                if (float.TryParse(raw.Substring(0, raw.Length - 1).Trim(), out val))
                {
                    unit = StyleUnitType.Percent;
                    return true;
                }
                return false;
            }

            if (raw.EndsWith("px"))
            {
                raw = raw.Substring(0, raw.Length - 2).Trim();
            }

            if (float.TryParse(raw, out val))
            {
                unit = StyleUnitType.Pixel;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Maps CSS flex alignment properties (align-items, justify-content) to Unity's TextAnchor enum.
        /// </summary>
        /// <param name="isRow">True if container is a horizontal row; false if vertical column.</param>
        /// <param name="alignItems">CSS cross-axis alignment string.</param>
        /// <param name="justifyContent">CSS main-axis alignment string.</param>
        /// <returns>Calculated Unity TextAnchor value.</returns>
        private static TextAnchor DetermineAlignment(bool isRow, string alignItems, string justifyContent)
        {
            alignItems = alignItems?.ToLowerInvariant() ?? "stretch";
            justifyContent = justifyContent?.ToLowerInvariant() ?? "flex-start";

            int crossPos = alignItems switch
            {
                "center" => 1,
                "flex-end" or "bottom" or "right" => 2,
                _ => 0
            };

            int mainPos = justifyContent switch
            {
                "center" => 1,
                "flex-end" or "bottom" or "right" => 2,
                _ => 0
            };

            if (isRow)
            {
                return (crossPos, mainPos) switch
                {
                    (0, 0) => TextAnchor.UpperLeft,
                    (0, 1) => TextAnchor.UpperCenter,
                    (0, 2) => TextAnchor.UpperRight,
                    (1, 0) => TextAnchor.MiddleLeft,
                    (1, 1) => TextAnchor.MiddleCenter,
                    (1, 2) => TextAnchor.MiddleRight,
                    (2, 0) => TextAnchor.LowerLeft,
                    (2, 1) => TextAnchor.LowerCenter,
                    (2, 2) => TextAnchor.LowerRight,
                    _ => TextAnchor.UpperLeft
                };
            }
            else
            {
                return (mainPos, crossPos) switch
                {
                    (0, 0) => TextAnchor.UpperLeft,
                    (0, 1) => TextAnchor.UpperCenter,
                    (0, 2) => TextAnchor.UpperRight,
                    (1, 0) => TextAnchor.MiddleLeft,
                    (1, 1) => TextAnchor.MiddleCenter,
                    (1, 2) => TextAnchor.MiddleRight,
                    (2, 0) => TextAnchor.LowerLeft,
                    (2, 1) => TextAnchor.LowerCenter,
                    (2, 2) => TextAnchor.LowerRight,
                    _ => TextAnchor.UpperLeft
                };
            }
        }

        /// <summary>
        /// Parses CSS padding declarations, supporting 1-value (all), 2-value (V/H), 3-value (T/H/B), and 4-value (TRBL) shorthand syntax.
        /// </summary>
        /// <param name="styles">Computed CSS properties map.</param>
        /// <returns>Initialized RectOffset containing directional pixel padding.</returns>
        public static RectOffset ParsePadding(Dictionary<string, string> styles)
        {
            int top = 0, right = 0, bottom = 0, left = 0;

            if (styles.TryGetValue("padding", out string padRaw) && !string.IsNullOrWhiteSpace(padRaw))
            {
                string[] parts = padRaw.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                var values = new List<int>();
                foreach (var part in parts)
                {
                    if (TryParseDimension(part, out float v, out _))
                    {
                        values.Add((int)v);
                    }
                }

                if (values.Count == 1)
                {
                    top = right = bottom = left = values[0];
                }
                else if (values.Count == 2)
                {
                    top = bottom = values[0];
                    left = right = values[1];
                }
                else if (values.Count == 3)
                {
                    top = values[0];
                    left = right = values[1];
                    bottom = values[2];
                }
                else if (values.Count >= 4)
                {
                    top = values[0];
                    right = values[1];
                    bottom = values[2];
                    left = values[3];
                }
            }

            if (styles.TryGetValue("padding-top", out string pt) && TryParseDimension(pt, out float ptVal, out _)) top = (int)ptVal;
            if (styles.TryGetValue("padding-bottom", out string pb) && TryParseDimension(pb, out float pbVal, out _)) bottom = (int)pbVal;
            if (styles.TryGetValue("padding-left", out string pl) && TryParseDimension(pl, out float plVal, out _)) left = (int)plVal;
            if (styles.TryGetValue("padding-right", out string pr) && TryParseDimension(pr, out float prVal, out _)) right = (int)prVal;

            return new RectOffset(left, right, top, bottom);
        }
    }
}