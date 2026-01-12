using System.Collections.Generic;

namespace BlackHorizon.HorizonGUI.Editor.Parsing
{
    /// <summary>
    /// Represents a single node in the UI tree (e.g., a div, a button, or a text block).
    /// </summary>
    public class HorizonNode
    {
        public string Tag { get; set; }
        public string TextContent { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public HorizonNode Parent { get; set; }
        public List<HorizonNode> Children { get; set; } = new List<HorizonNode>();

        public HorizonNode(string tag, HorizonNode parent)
        {
            Tag = tag;
            Parent = parent;
        }

        public void AddAttribute(string key, string value)
        {
            if (!Attributes.ContainsKey(key))
                Attributes.Add(key, value);
            else
                Attributes[key] = value;
        }
    }
}