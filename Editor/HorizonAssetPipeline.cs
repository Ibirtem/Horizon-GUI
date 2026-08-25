using UnityEngine;
using UnityEditor;
using System.IO;

namespace BlackHorizon.HorizonGUI.Editor
{
    /// <summary>
    /// Handles procedural generation, loading, and resolution of visual assets (sprites, materials) for Horizon GUI.
    /// </summary>
    public static class HorizonAssetPipeline
    {
        private const string GENERATED_SPRITE_PATH = "Assets/Horizon GUI/Core/Runtime/Textures/Horizon_RoundedBackground.png";
        private const string GLASS_MATERIAL_PATH = "Assets/Horizon GUI/Core/Runtime/Materials/HorizonGlass.mat";
        private const string GLASS_SHADER_NAME = "Horizon/UI/Glass Blur";

        /// <summary>
        /// Retrieves or procedurally generates a 128x128 9-sliced circular/rounded sprite.
        /// </summary>
        /// <returns>The cached or newly generated Sprite asset.</returns>
        public static Sprite GetOrGenerateRoundedSprite()
        {
            string dir = Path.GetDirectoryName(GENERATED_SPRITE_PATH);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(GENERATED_SPRITE_PATH);
            if (existing != null) return existing;

            const int size = 128;
            const float radius = 64f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(64, 64);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - dist + 0.5f));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(GENERATED_SPRITE_PATH, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(GENERATED_SPRITE_PATH, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(GENERATED_SPRITE_PATH) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.spriteBorder = new Vector4(64, 64, 64, 64);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(GENERATED_SPRITE_PATH);
        }

        /// <summary>
        /// Retrieves or creates the Glass material referencing the blur shader.
        /// </summary>
        /// <returns>The Material instance configured for UI glass blur, or null if shader is missing.</returns>
        public static Material GetGlassMaterial()
        {
            string folderPath = Path.GetDirectoryName(GLASS_MATERIAL_PATH);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(GLASS_MATERIAL_PATH);
            if (mat == null)
            {
                Shader shader = Shader.Find(GLASS_SHADER_NAME);
                if (shader == null)
                {
                    Debug.LogError($"[HorizonAssetPipeline] Shader '{GLASS_SHADER_NAME}' not found. Ensure Shaders folder is imported correctly.");
                    return null;
                }

                mat = new Material(shader);
                mat.SetFloat("_BlurSize", 8.0f);
                mat.SetColor("_Color", new Color(0.9f, 0.95f, 1.0f, 0.3f));

                AssetDatabase.CreateAsset(mat, GLASS_MATERIAL_PATH);
            }

            return mat;
        }

        /// <summary>
        /// Resolves a sprite by filename using explicit overrides, scoped search folders, or global search fallback.
        /// </summary>
        /// <param name="filename">Image file name (with or without extension).</param>
        /// <param name="map">Optional resource map defining search scopes and explicit key overrides.</param>
        /// <returns>The resolved Sprite, or null if no matching asset was found.</returns>
        public static Sprite LoadSprite(string filename, HorizonResourceMap map)
        {
            if (string.IsNullOrEmpty(filename)) return null;

            if (map != null)
            {
                Sprite overrideSprite = map.GetOverride(filename);
                if (overrideSprite != null) return overrideSprite;
            }

            string searchName = Path.GetFileNameWithoutExtension(filename);
            string[] searchFolders = (map != null && map.searchFolders.Count > 0) ? map.searchFolders.ToArray() : null;
            string[] guids = AssetDatabase.FindAssets(searchName, searchFolders);

            if (guids.Length == 0)
            {
                Debug.LogWarning($"[HorizonAssetPipeline] Icon '{filename}' not found. Checked in: {(searchFolders != null ? string.Join(", ", searchFolders) : "Entire Project")}");
                return null;
            }

            string bestPath = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string ext = Path.GetExtension(path).ToLower();

                if (ext != ".png" && ext != ".jpg" && ext != ".psd" && ext != ".tga") continue;

                if (Path.HasExtension(filename) && path.EndsWith(filename, System.StringComparison.OrdinalIgnoreCase))
                {
                    bestPath = path;
                    break;
                }

                if (bestPath == null) bestPath = path;
            }

            if (bestPath == null) return null;

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(bestPath);
            if (sprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(bestPath);
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            return sprite;
        }

        /// <summary>
        /// Finds a package/system icon by filename across the project.
        /// </summary>
        /// <param name="filename">Icon filename.</param>
        /// <returns>The Sprite instance if found, otherwise null.</returns>
        public static Sprite LoadPackageSprite(string filename)
        {
            return LoadSprite(filename, null);
        }
    }
}