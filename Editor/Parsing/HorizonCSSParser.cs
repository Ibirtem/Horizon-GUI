using System.Collections.Generic;
using UnityEngine;

namespace BlackHorizon.HorizonGUI.Editor.Parsing
{
    public class HorizonStyleSheet
    {
        public Dictionary<string, Dictionary<string, string>> Rules = new Dictionary<string, Dictionary<string, string>>();

        public Dictionary<string, string> GetComputedStyle(HorizonNode node)
        {
            var computed = new Dictionary<string, string>();

            // 1. Tag styles
            if (Rules.ContainsKey(node.Tag)) Merge(computed, Rules[node.Tag]);

            // 2. Class styles
            if (node.Attributes.ContainsKey("class"))
            {
                string[] classes = node.Attributes["class"].Split(' ');
                foreach (var c in classes)
                {
                    string selector = "." + c;
                    if (Rules.ContainsKey(selector)) Merge(computed, Rules[selector]);
                }
            }

            // 3. ID styles
            if (node.Attributes.ContainsKey("id"))
            {
                string selector = "#" + node.Attributes["id"];
                if (Rules.ContainsKey(selector)) Merge(computed, Rules[selector]);
            }

            return computed;
        }

        private void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            foreach (var kvp in source)
            {
                if (target.ContainsKey(kvp.Key)) target[kvp.Key] = kvp.Value;
                else target.Add(kvp.Key, kvp.Value);
            }
        }
    }

    public static class HorizonCSSParser
    {
        public static HorizonStyleSheet Parse(string cssText)
        {
            var sheet = new HorizonStyleSheet();
            if (string.IsNullOrEmpty(cssText)) return sheet;

            cssText = System.Text.RegularExpressions.Regex.Replace(cssText, @"/\*[\s\S]*?\*/", "");

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

                var properties = new Dictionary<string, string>();
                string[] ruleLines = rulesPart.Split(';');

                foreach (var line in ruleLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int colonIndex = line.IndexOf(':');
                    if (colonIndex == -1) continue;

                    string prop = line.Substring(0, colonIndex).Trim().ToLower();
                    string val = line.Substring(colonIndex + 1).Trim();

                    if (!properties.ContainsKey(prop))
                        properties.Add(prop, val);
                }

                foreach (var sel in selectors)
                {
                    string cleanSel = sel.Trim();
                    if (!string.IsNullOrEmpty(cleanSel))
                    {
                        if (!sheet.Rules.ContainsKey(cleanSel))
                            sheet.Rules.Add(cleanSel, new Dictionary<string, string>(properties));
                        else
                        {
                            foreach (var p in properties)
                                if (!sheet.Rules[cleanSel].ContainsKey(p.Key))
                                    sheet.Rules[cleanSel].Add(p.Key, p.Value);
                        }
                    }
                }
            }

            return sheet;
        }
    }
}