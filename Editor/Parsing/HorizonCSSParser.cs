using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BlackHorizon.HorizonGUI.Editor.Parsing
{
    /// <summary>
    /// Represents a collection of CSS rules mapped to selectors.
    /// Provides specificity-based style computation for HorizonNodes.
    /// </summary>
    public class HorizonStyleSheet
    {
        /// <summary>
        /// Storage for CSS rules. Key: Selector (tag, .class, or #id), Value: Dictionary of properties.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> Rules = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// Calculates the final style for a node by applying rules in order of specificity:
        /// 1. Tag styles (lowest)
        /// 2. Class styles (medium)
        /// 3. ID styles (highest)
        /// </summary>
        /// <param name="node">The target node to compute styles for.</param>
        /// <returns>A dictionary containing all merged style properties.</returns>
        public Dictionary<string, string> GetComputedStyle(HorizonNode node)
        {
            var computed = new Dictionary<string, string>();

            if (Rules.ContainsKey(node.Tag))
                Merge(computed, Rules[node.Tag]);

            if (node.Attributes.TryGetValue("class", out string classAttr))
            {
                string[] classes = classAttr.Split(' ');
                foreach (var c in classes)
                {
                    string selector = "." + c.Trim();
                    if (Rules.ContainsKey(selector))
                        Merge(computed, Rules[selector]);
                }
            }

            if (node.Attributes.TryGetValue("id", out string idAttr))
            {
                string selector = "#" + idAttr.Trim();
                if (Rules.ContainsKey(selector))
                    Merge(computed, Rules[selector]);
            }

            return computed;
        }

        /// <summary>
        /// Merges properties from a source dictionary into the target, overwriting existing keys.
        /// </summary>
        private void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            foreach (var kvp in source)
            {
                target[kvp.Key] = kvp.Value;
            }
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
                        foreach (var p in properties)
                        {
                            sheet.Rules[cleanSel][p.Key] = p.Value;
                        }
                    }
                }
            }

            return sheet;
        }

        /// <summary>
        /// Parses a block of CSS rules (key:value; pairs) into a dictionary.
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

                if (!string.IsNullOrEmpty(prop))
                {
                    properties[prop] = val;
                }
            }
            return properties;
        }
    }
}