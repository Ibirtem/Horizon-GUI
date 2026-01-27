using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BlackHorizon.HorizonGUI.Editor.Parsing
{
    /// <summary>
    /// Represents a collection of CSS rules mapped to selectors.
    /// Manages style calculation logic, including specificity (Tag vs Class vs ID) and "User Agent" defaults.
    /// </summary>
    public class HorizonStyleSheet
    {
        // --- USER AGENT STYLESHEET ---
        private const string DEFAULT_STYLES = @"
            /* Typography */
            h1 { font-size: 48px; font-style: bold; color: #E6E6E6; margin-bottom: 10px; }
            h2 { font-size: 42px; font-style: bold; color: #E6E6E6; margin-bottom: 10px; }
            p, body { font-size: 24px; color: #E6E6E6; }
            label { font-size: 18px; color: #FFFFFF80; }
            text { font-size: 24px; color: #E6E6E6; }
            
            /* Containers */
            panel { background-color: #00000033; border-radius: 20px; }
            hr { background-color: #FFFFFF33; height: 5px; margin: 10px 0; } 
            
            /* Interactive */
            button { background-color: #FFFFFF0D; padding: 10px; align-items: center; justify-content: center; border-radius: 20px; }
            input { height: 50px; background-color: #FFFFFF1A; border-radius: 10px; }
            toggle { height: 50px; gap: 15px; align-items: center; }
            
            /* Grid */
            h-grid { align-items: flex-start; }
        ";

        /// <summary>
        /// The parsed rules from the user's CSS file. Key: Selector, Value: Property Dictionary.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> Rules = new Dictionary<string, Dictionary<string, string>>();

        // Static cache for default rules to parse them only once per Editor session.
        private static Dictionary<string, Dictionary<string, string>> _defaultRules;

        /// <summary>
        /// Creates a new empty stylesheet.
        /// Note: Defaults are loaded lazily via <see cref="EnsureDefaults"/> to prevent infinite recursion during parsing.
        /// </summary>
        public HorizonStyleSheet()
        {
        }

        /// <summary>
        /// Ensures the "User Agent" styles are parsed and cached.
        /// </summary>
        private static void EnsureDefaults()
        {
            if (_defaultRules != null) return;

            var defaultSheet = HorizonCSSParser.Parse(DEFAULT_STYLES);
            _defaultRules = defaultSheet.Rules;
        }

        /// <summary>
        /// Calculates the final style for a node by merging rules.
        /// Order of application: Defaults -> Tag -> Parent Context -> Class -> ID.
        /// </summary>
        /// <param name="node">The target node to compute styles for.</param>
        /// <returns>A merged dictionary of all active properties.</returns>
        public Dictionary<string, string> GetComputedStyle(HorizonNode node)
        {
            EnsureDefaults();

            var computed = new Dictionary<string, string>();

            ApplyRules(computed, _defaultRules, node);

            ApplyRules(computed, this.Rules, node);

            return computed;
        }

        /// <summary>
        /// Applies rules from a source collection based on specificity logic.
        /// </summary>
        private void ApplyRules(Dictionary<string, string> target, Dictionary<string, Dictionary<string, string>> sourceRules, HorizonNode node)
        {
            if (sourceRules == null) return;

            // 1. Tag (Lowest Specificity)
            if (sourceRules.ContainsKey(node.Tag))
                Merge(target, sourceRules[node.Tag]);

            // 2. Parent Context (e.g., ".header h1")
            if (node.Parent != null && node.Parent.Attributes.TryGetValue("class", out string pClassAttr))
            {
                string[] pClasses = pClassAttr.Split(' ');
                foreach (var pc in pClasses)
                {
                    string clean = pc.Trim();
                    if (string.IsNullOrEmpty(clean)) continue;

                    string ctxSelector = $".{clean} {node.Tag}";
                    if (sourceRules.ContainsKey(ctxSelector)) Merge(target, sourceRules[ctxSelector]);
                }
            }

            // 3. Class
            if (node.Attributes.TryGetValue("class", out string classAttr))
            {
                string[] classes = classAttr.Split(' ');
                foreach (var c in classes)
                {
                    string clean = c.Trim();
                    if (string.IsNullOrEmpty(clean)) continue;

                    string selector = "." + clean;
                    if (sourceRules.ContainsKey(selector)) Merge(target, sourceRules[selector]);
                }
            }

            // 4. ID (Highest Specificity)
            if (node.Attributes.TryGetValue("id", out string idAttr))
            {
                string selector = "#" + idAttr.Trim();
                if (sourceRules.ContainsKey(selector)) Merge(target, sourceRules[selector]);
            }
        }

        /// <summary>
        /// Merges properties from source to target, overwriting existing keys.
        /// </summary>
        private void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            foreach (var kvp in source) target[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// A lightweight CSS parser that translates raw text into a HorizonStyleSheet.
    /// Supports standard selectors, comments, and multiple property declarations.
    /// </summary>
    public static class HorizonCSSParser
    {
        private static readonly Regex CommentRegex = new Regex(@"/\*[\s\S]*?\*/");

        /// <summary>
        /// Parses a CSS string into a structured HorizonStyleSheet.
        /// </summary>
        /// <param name="cssText">Raw CSS text content.</param>
        /// <returns>An initialized HorizonStyleSheet object.</returns>
        public static HorizonStyleSheet Parse(string cssText)
        {
            var sheet = new HorizonStyleSheet();
            if (string.IsNullOrWhiteSpace(cssText)) return sheet;

            cssText = CommentRegex.Replace(cssText, "");
            cssText = cssText.Replace("\r\n", "\n");

            string[] blocks = cssText.Split('}');

            foreach (var block in blocks)
            {
                if (string.IsNullOrWhiteSpace(block)) continue;

                int openBraceIndex = block.IndexOf('{');
                if (openBraceIndex == -1) continue;

                string selectorPart = block.Substring(0, openBraceIndex).Trim();
                string rulesPart = block.Substring(openBraceIndex + 1).Trim();
                string[] selectors = selectorPart.Split(',');

                var properties = ParseProperties(rulesPart);

                foreach (var sel in selectors)
                {
                    string cleanSel = sel.Trim();
                    if (string.IsNullOrEmpty(cleanSel)) continue;

                    if (!sheet.Rules.ContainsKey(cleanSel))
                    {
                        sheet.Rules.Add(cleanSel, new Dictionary<string, string>(properties));
                    }
                    else
                    {
                        foreach (var p in properties) sheet.Rules[cleanSel][p.Key] = p.Value;
                    }
                }
            }
            return sheet;
        }

        /// <summary>
        /// Parses a block of CSS properties (e.g., "color: red; width: 10px;") into a dictionary.
        /// </summary>
        private static Dictionary<string, string> ParseProperties(string rulesPart)
        {
            var properties = new Dictionary<string, string>();
            string[] ruleLines = rulesPart.Split(';');

            foreach (var line in ruleLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                int colonIndex = line.IndexOf(':');
                if (colonIndex == -1) continue;

                string prop = line.Substring(0, colonIndex).Trim().ToLower();
                string val = line.Substring(colonIndex + 1).Trim();

                if (!string.IsNullOrEmpty(prop)) properties[prop] = val;
            }
            return properties;
        }
    }
}