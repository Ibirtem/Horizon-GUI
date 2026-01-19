using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BlackHorizon.HorizonGUI.Editor.Parsing
{
    /// <summary>
    /// A lightweight parser that converts HTML-like strings into a HorizonNode tree.
    /// Supports nested tags, attributes, and text content.
    /// </summary>
    public static class HorizonMarkupParser
    {
        private static readonly Regex AttrRegex = new Regex(@"(\w+(?:-\w+)*)(?:=(?:""([^""]*)""|'([^']*)'))?");

        public static HorizonNode Parse(string html)
        {
            HorizonNode root = new HorizonNode("root", null);
            HorizonNode current = root;

            html = Regex.Replace(html, @"<!--[\s\S]*?-->", "");

            int pos = 0;
            while (pos < html.Length)
            {
                int openTagStart = html.IndexOf('<', pos);

                if (openTagStart == -1)
                {
                    string remainingText = html.Substring(pos).Trim();
                    if (!string.IsNullOrEmpty(remainingText) && current != root)
                    {
                        current.TextContent = remainingText;
                    }
                    break;
                }

                if (openTagStart > pos)
                {
                    string text = html.Substring(pos, openTagStart - pos).Trim();
                    if (!string.IsNullOrEmpty(text) && current != root)
                    {
                        if (string.IsNullOrEmpty(current.TextContent))
                            current.TextContent = text;
                        else
                            current.TextContent += " " + text;
                    }
                }

                int tagEnd = html.IndexOf('>', openTagStart);
                if (tagEnd == -1) break;

                string tagContent = html.Substring(openTagStart + 1, tagEnd - openTagStart - 1);

                if (tagContent.StartsWith("/"))
                {
                    if (current.Parent != null)
                        current = current.Parent;
                }
                else
                {
                    bool isSelfClosing = tagContent.EndsWith("/");
                    if (isSelfClosing) tagContent = tagContent.Substring(0, tagContent.Length - 1).Trim();

                    int spaceIndex = tagContent.IndexOf(' ');
                    string tagName = (spaceIndex == -1) ? tagContent : tagContent.Substring(0, spaceIndex);

                    HorizonNode newNode = new HorizonNode(tagName, current);

                    if (spaceIndex != -1)
                    {
                        string attrString = tagContent.Substring(spaceIndex + 1);
                        MatchCollection matches = AttrRegex.Matches(attrString);
                        foreach (Match match in matches)
                        {
                            string key = match.Groups[1].Value;
                            string val = match.Groups[2].Success ? match.Groups[2].Value : (match.Groups[3].Success ? match.Groups[3].Value : "");
                            newNode.AddAttribute(key, val);
                        }
                    }

                    current.Children.Add(newNode);

                    if (!isSelfClosing)
                    {
                        if (tagName.ToLower() != "br" && tagName.ToLower() != "hr" && tagName.ToLower() != "img" && tagName.ToLower() != "input")
                        {
                            current = newNode;
                        }
                    }
                }

                pos = tagEnd + 1;
            }

            return root;
        }
    }
}