using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;

namespace BlackHorizon.HorizonGUI.Editor.Importers
{
    /// <summary>
    /// Forces Unity to recognize .css files as TextAssets.
    /// allows dragging .css files into TextAsset inspector slots.
    /// </summary>
    [ScriptedImporter(1, "css")]
    public class CSSImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath);
            TextAsset asset = new TextAsset(text);

            ctx.AddObjectToAsset("main", asset);
            ctx.SetMainObject(asset);
        }
    }
}