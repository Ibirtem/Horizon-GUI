using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UdonSharp;
using System.IO;
using VRC.SDK3.Components;
using UdonSharpEditor;
using BlackHorizon.HorizonGUI;

namespace BlackHorizon.HorizonGUI.Editor
{
    /// <summary>
    /// Core Factory class for generating the Horizon UI.
    /// Handles procedural asset generation, GameObject instantiation, layout configuration, 
    /// and safe UdonSharp component attachment.
    /// </summary>
    public static class HorizonGUIFactory
    {
        private static HorizonTheme _currentTheme;
        private static HorizonTheme _overrideTheme;
        private const string GENERATED_SPRITE_PATH = "Assets/Horizon GUI/Textures/Horizon_RoundedBackground.png";

        #region Theme Management

        /// <summary>
        /// Provides a temporary theme context for a specific build operation.
        /// </summary>
        public static void SetThemeContext(HorizonTheme theme) => _overrideTheme = theme;

        /// <summary>
        /// Retrieves the active UI theme. Defaults to the first found HorizonTheme asset in the project.
        /// </summary>
        public static HorizonTheme Theme
        {
            get
            {
                if (_overrideTheme != null) return _overrideTheme;
                if (_currentTheme == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:HorizonTheme");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        _currentTheme = AssetDatabase.LoadAssetAtPath<HorizonTheme>(path);
                    }
                    else
                    {
                        _currentTheme = ScriptableObject.CreateInstance<HorizonTheme>();
                    }
                }
                return _currentTheme;
            }
        }

        public static Color ColorGlassDark => Theme.glassColor;
        public static Color ColorSidebar => Theme.panelColor;
        public static Color ColorTextWhite => Theme.primaryColor;
        public static Color ColorTextDim => Theme.secondaryColor;

        #endregion

        #region Core Builder Methods

        /// <summary>
        /// Creates a base UI GameObject with a RectTransform and sets the UI layer.
        /// </summary>
        public static GameObject CreateBlock(string name, GameObject parent, Vector2? size = null)
        {
            GameObject go = new GameObject(name);
            RectTransform rect = go.AddComponent<RectTransform>();
            if (parent != null) go.transform.SetParent(parent.transform, false);

            if (size.HasValue) rect.sizeDelta = size.Value;
            else Stretch(go);

            go.layer = LayerMask.NameToLayer("UI");
            return go;
        }

        /// <summary>
        /// Creates a UI panel with an Image component, supporting 9-slicing.
        /// </summary>
        public static GameObject CreatePanel(string name, GameObject parent, Color color, Sprite sprite = null)
        {
            GameObject go = CreateBlock(name, parent);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                // Default multiplier for crisp edges on rounded rectangles
                img.pixelsPerUnitMultiplier = 3.0f;
            }
            return go;
        }

        /// <summary>
        /// Attaches a UdonSharp behaviour to a GameObject in a VRChat-safe way.
        /// </summary>
        /// <remarks>
        /// <b>CRITICAL ARCHITECTURE NOTE:</b><br/>
        /// We MUST use <see cref="UdonSharpUndo.AddComponent{T}"/> instead of standard Unity <c>AddComponent</c>.
        /// <br/><br/>
        /// <b>Why?</b><br/>
        /// Standard <c>AddComponent</c> creates only the C# Proxy. UdonSharp then attempts to create the backing 
        /// <c>UdonBehaviour</c> asynchronously or on the next refresh. This causes a race condition where
        /// <c>GetBackingUdonBehaviour</c> returns null during the Bake process, leading to crashes.
        /// <br/><br/>
        /// <see cref="UdonSharpUndo.AddComponent{T}"/> ensures both the Proxy and the Backing Behaviour 
        /// are created and linked <b>immediately</b>.
        /// </remarks>
        public static T AttachLogic<T>(GameObject target) where T : UdonSharpBehaviour
        {
            T existing = target.GetComponent<T>();
            if (existing != null) return existing;
            return UdonSharpUndo.AddComponent<T>(target);
        }

        /// <summary>
        /// Configures a GameObject with physics components required for VRChat UI interaction.
        /// </summary>
        /// <param name="go">Target GameObject (usually the Canvas root or an input field).</param>
        /// <param name="size">The size of the interaction area (width, height).</param>
        public static void AddInteraction(GameObject go, Vector2 size)
        {
            go.layer = 0; 

            BoxCollider col = go.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = go.AddComponent<BoxCollider>();
            }

            col.size = new Vector3(size.x, size.y, 25f);
            col.isTrigger = true;
            col.center = Vector3.zero;

            if (go.GetComponent<VRCUiShape>() == null)
            {
                go.AddComponent<VRCUiShape>();
            }
        }

        #endregion

        #region Asset Generation & Loading

        /// <summary>
        /// Procedurally generates a circular sprite for use in 9-slicing.
        /// Configured with specific borders to allow both perfect circles and rounded rectangles.
        /// </summary>
        public static Sprite GetOrGenerateRoundedSprite()
        {
            string dir = Path.GetDirectoryName(GENERATED_SPRITE_PATH);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(GENERATED_SPRITE_PATH);
            if (existing != null) return existing;

            int size = 128;
            float radius = 64f;
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
        /// Generates or retrieves the glass material using the custom blur shader.
        /// </summary>
        public static Material GetGlassMaterial()
        {
            string matPath = "Assets/Horizon GUI/Materials/HorizonGlass.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Horizon/UI/Glass Blur");
                if (shader == null) return null;
                mat = new Material(shader);
                mat.SetFloat("_BlurSize", 8.0f);
                mat.SetColor("_Color", new Color(0.9f, 0.95f, 1.0f, 0.3f));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        /// <summary>
        /// Searches the project for a sprite asset matching the given filename.
        /// Prioritizes assets within 'Horizon' or 'Textures' folders to ensure the correct icon is loaded.
        /// </summary>
        /// <param name="filename">The name of the icon file (with or without extension).</param>
        /// <returns>The found Sprite, or null if no matching image asset exists.</returns>
        public static Sprite LoadPackageSprite(string filename)
        {
            string searchName = Path.GetFileNameWithoutExtension(filename);
            
            string[] guids = AssetDatabase.FindAssets(searchName);

            if (guids.Length == 0)
            {
                Debug.LogWarning($"[HorizonGUI] Icon not found in project: {filename}");
                return null;
            }

            string bestPath = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                string ext = Path.GetExtension(path).ToLower();
                if (ext != ".png" && ext != ".jpg" && ext != ".psd" && ext != ".tga")
                    continue;

                if (path.Contains("Horizon") || path.Contains("GUI") || path.Contains("Textures"))
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

        #endregion

        #region UI Widgets & Layout

        public enum TextStyle { H1, H2, Body, BodyDim, Small, SmallDim, Clock }

        /// <summary>
        /// Creates a TextMeshProUGUI element with theme-compliant styling.
        /// </summary>
        public static TextMeshProUGUI CreateText(GameObject parent, string content, TextStyle style, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            GameObject go = CreateBlock("Label", parent);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.alignment = align;
            tmp.raycastTarget = false;

            switch (style)
            {
                case TextStyle.H1: tmp.fontSize = Theme.sizeH1; tmp.fontStyle = FontStyles.Bold; break;
                case TextStyle.H2: tmp.fontSize = Theme.sizeH2; tmp.fontStyle = FontStyles.Bold; break;
                case TextStyle.Body: tmp.fontSize = Theme.sizeBody; tmp.color = Theme.primaryColor; break;
                case TextStyle.BodyDim: tmp.fontSize = Theme.sizeBody; tmp.color = Theme.secondaryColor; break;
                case TextStyle.Small: tmp.fontSize = Theme.sizeSmall; tmp.color = Theme.primaryColor; break;
                case TextStyle.SmallDim: tmp.fontSize = Theme.sizeSmall; tmp.color = Theme.secondaryColor; break;
                case TextStyle.Clock: tmp.fontSize = Theme.sizeClock * 1.3f; tmp.color = Theme.secondaryColor; break;
            }
            return tmp;
        }

        /// <summary>
        /// Constructs a circular icon button with independent background and interaction layers.
        /// </summary>
        public static GameObject CreateIconButton(string name, GameObject parent, Sprite bgSprite, Sprite iconSprite)
        {
            GameObject btnObj = CreatePanel(name, parent, new Color(1, 1, 1, 0.0f), bgSprite);
            btnObj.GetComponent<Image>().raycastTarget = true;

            GameObject hoverObj = CreatePanel("Interaction_Overlay", btnObj, Color.white, bgSprite);
            Stretch(hoverObj);
            hoverObj.GetComponent<Image>().raycastTarget = false;
            hoverObj.AddComponent<LayoutElement>().ignoreLayout = true;

            GameObject iconObj = CreateBlock("Icon", btnObj);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.22f, 0.22f);
            iconRect.anchorMax = new Vector2(0.78f, 0.78f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            Button b = btnObj.AddComponent<Button>();
            b.targetGraphic = hoverObj.GetComponent<Image>();
            b.transition = Selectable.Transition.ColorTint;

            ColorBlock cb = b.colors;
            cb.normalColor = Color.clear;
            cb.highlightedColor = new Color(1, 1, 1, 0.1f);
            cb.pressedColor = new Color(1, 1, 1, 0.2f);
            cb.fadeDuration = 0.05f;
            b.colors = cb;

            return btnObj;
        }

        #endregion

        #region Logic Binding API

        /// <summary>
        /// Safely attaches an UdonSharp script and allows configuration of its serialized properties.
        /// </summary>
        public static T ConfigureLogic<T>(GameObject target, System.Action<HorizonLogicBinder> config) where T : UdonSharpBehaviour
        {
            T script = AttachLogic<T>(target);
            HorizonLogicBinder binder = new HorizonLogicBinder(script);
            config(binder);
            binder.Apply();
            return script;
        }

        /// <summary>
        /// Helper class to streamline property binding via SerializedObject.
        /// </summary>
        public class HorizonLogicBinder
        {
            private SerializedObject _so;
            private UdonSharpBehaviour _script;

            public UdonSharpBehaviour TargetScript => _script;

            public HorizonLogicBinder(UdonSharpBehaviour target)
            {
                _script = target;
                _so = new SerializedObject(target);
            }

            public void Bind(string propertyName, UnityEngine.Object value)
            {
                SerializedProperty prop = _so.FindProperty(propertyName);
                if (prop != null) prop.objectReferenceValue = value;
            }

            public void BindVal(string propertyName, object value)
            {
                SerializedProperty prop = _so.FindProperty(propertyName);
                if (prop == null) return;
                if (value is int i) prop.intValue = i;
                else if (value is float f) prop.floatValue = f;
                else if (value is bool b) prop.boolValue = b;
                else if (value is string s) prop.stringValue = s;
                else if (value is Color c) prop.colorValue = c;
            }

            public void BindArray<TComp>(string propertyName, System.Collections.Generic.List<TComp> list) where TComp : UnityEngine.Object
            {
                SerializedProperty prop = _so.FindProperty(propertyName);
                if (prop == null) return;
                prop.ClearArray();
                prop.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }

            public void Apply() => _so.ApplyModifiedProperties();
        }

        #endregion

        #region Complex Widgets

        /// <summary>
        /// Creates a complete vertical scrolling system.
        /// Structure: Root (ScrollRect) -> Viewport (Mask) -> Content (Vertical Layout) + Scrollbar (Glass Style).
        /// </summary>
        /// <param name="name">Name of the root GameObject.</param>
        /// <param name="parent">Parent container.</param>
        /// <param name="spacing">Spacing between items in the content area.</param>
        /// <param name="padding">Internal padding for the content.</param>
        /// <param name="flexGrow">Weight for layout expansion.</param>
        /// <param name="align">Alignment of items within the scrollable area.</param>
        /// <param name="sbMarginTop">Offset for the scrollbar from the top edge.</param>
        /// <param name="sbMarginBottom">Offset for the scrollbar from the bottom edge.</param>
        /// <returns>The 'Content' GameObject where items should be parented.</returns>
        public static GameObject CreateScrollableColumn(
            string name,
            GameObject parent,
            float spacing = 0,
            int padding = 0,
            float flexGrow = 1,
            TextAnchor align = TextAnchor.UpperLeft,
            float sbMarginTop = 20f,
            float sbMarginBottom = 20f
        )
        {
            // 1. Root Container with ScrollRect component
            GameObject root = CreateBlock(name, parent);
            if (flexGrow > 0) SetLayoutSize(root, flexH: flexGrow, flexW: 1);

            Image rootImg = root.AddComponent<Image>();
            rootImg.color = new Color(0, 0, 0, 0);

            ScrollRect scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 25f;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            // 2. Viewport
            GameObject viewport = CreateBlock("Viewport", root);
            RectTransform vpRect = viewport.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = new Vector2(-24, 0);

            viewport.AddComponent<RectMask2D>();
            Image vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);

            // 3. Content Area
            GameObject content = CreateBlock("Content", viewport);
            RectTransform cRect = content.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0, 1);
            cRect.anchorMax = new Vector2(1, 1);
            cRect.pivot = new Vector2(0.5f, 1);
            cRect.sizeDelta = Vector2.zero;

            // Setup vertical layout for content
            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(padding, padding, padding, padding);
            vlg.childAlignment = align;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Auto-resize content to fit children
            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vpRect;
            scroll.content = cRect;

            // 4. Scrollbar
            GameObject sbObj = CreateBlock("Scrollbar Vertical", root);
            RectTransform sbRect = sbObj.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1, 0);
            sbRect.anchorMax = new Vector2(1, 1);
            sbRect.pivot = new Vector2(1, 1);
            sbRect.offsetMin = new Vector2(-24, sbMarginBottom);
            sbRect.offsetMax = new Vector2(-4, -sbMarginTop);

            Image trackImg = sbObj.AddComponent<Image>();
            trackImg.sprite = GetOrGenerateRoundedSprite();
            trackImg.type = Image.Type.Sliced;
            trackImg.color = new Color(1, 1, 1, 0.03f);
            trackImg.pixelsPerUnitMultiplier = 3.0f;

            GameObject slidingArea = CreateBlock("Sliding Area", sbObj);
            Stretch(slidingArea);

            GameObject handle = CreateBlock("Handle", slidingArea);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.sprite = GetOrGenerateRoundedSprite();
            handleImg.type = Image.Type.Sliced;
            handleImg.color = new Color(1, 1, 1, 0.3f);
            handleImg.pixelsPerUnitMultiplier = 3.0f;

            Scrollbar sbComp = sbObj.AddComponent<Scrollbar>();
            sbComp.handleRect = handle.GetComponent<RectTransform>();
            sbComp.targetGraphic = handleImg;
            sbComp.direction = Scrollbar.Direction.BottomToTop;

            scroll.verticalScrollbar = sbComp;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = -3;

            return content;
        }

        /// <summary>
        /// Creates a high-performance data grid with a fixed pool of reusable slots.
        /// </summary>
        public static HorizonDataGrid CreateDataGrid(string name, GameObject parent, int poolSize, Vector2 cellSize, UdonSharpBehaviour eventTarget = null, string eventName = "OnItemSelected", bool useCircleStyle = false)
        {
            GameObject gridObj = CreateGrid(name, parent, cellSize, new Vector2(10, 10), flexGrow: 1, padding: 10);
            var manager = AttachLogic<HorizonDataGrid>(gridObj);
            Sprite circleSprite = GetOrGenerateRoundedSprite();
            var slots = new System.Collections.Generic.List<HorizonGridItem>();

            for (int i = 0; i < poolSize; i++)
            {
                GameObject slotObj = CreateIconButton($"Slot_{i:00}", gridObj, circleSprite, null);
                if (useCircleStyle)
                {
                    Image bg = slotObj.GetComponent<Image>();
                    bg.color = new Color(1, 1, 1, 0.1f);
                    Transform icon = slotObj.transform.Find("Icon");
                    if (icon != null)
                    {
                        Image iconImg = icon.GetComponent<Image>();
                        Stretch(icon.gameObject, 0);
                        iconImg.sprite = circleSprite;
                        iconImg.type = Image.Type.Sliced;
                        iconImg.color = new Color(1, 1, 1, 0.6f);
                    }
                }

                var item = AttachLogic<HorizonGridItem>(slotObj);
                ConfigureLogic<HorizonGridItem>(slotObj, binder =>
                {
                    binder.Bind("gridManager", manager);
                    binder.BindVal("slotIndex", i);
                });
                slots.Add(item);
            }

            ConfigureLogic<HorizonDataGrid>(gridObj, binder =>
            {
                binder.BindArray("slotPool", slots);
                binder.BindVal("itemsPerPage", poolSize);
                if (eventTarget != null)
                {
                    binder.Bind("targetCallback", UdonSharpEditorUtility.GetBackingUdonBehaviour(eventTarget));
                    binder.BindVal("callbackEventName", eventName);
                }
            });
            return manager;
        }

        #endregion

        #region Layout & Interaction Helpers

        /// <summary>
        /// Checks for an existing EventSystem in the scene. If none is found, creates a new one 
        /// with a StandaloneInputModule to enable UI interactions.
        /// </summary>
        /// <param name="parent">The GameObject to host the new EventSystem if creation is required.</param>
        public static void EnsureEventSystemInside(GameObject parent)
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;

            GameObject esObj = new GameObject("System_Input");
            esObj.transform.SetParent(parent.transform);
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();

            var input = esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            // Standard VRChat/Unity input axes
            input.horizontalAxis = "Horizontal";
            input.verticalAxis = "Vertical";
        }

        /// <summary>
        /// Configures the RectTransform anchors to fully expand and fill the parent container.
        /// </summary>
        /// <param name="go">Target GameObject with RectTransform.</param>
        /// <param name="padding">Internal offset from the parent's edges.</param>
        public static void Stretch(GameObject go, float padding = 0)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>
        /// Adds or retrieves a LayoutElement component and configures sizing constraints.
        /// Used by LayoutGroups to calculate the final size of UI elements.
        /// </summary>
        /// <param name="go">Target GameObject.</param>
        /// <param name="minW">Minimum width. If null, property remains unchanged.</param>
        /// <param name="minH">Minimum height.</param>
        /// <param name="prefW">Preferred width for flexible layouts.</param>
        /// <param name="prefH">Preferred height.</param>
        /// <param name="flexW">Flexible width weight (0 for fixed, 1+ for expansion).</param>
        /// <param name="flexH">Flexible height weight.</param>
        public static void SetLayoutSize(GameObject go, float? minW = null, float? minH = null, float? prefW = null, float? prefH = null, float flexW = -1, float flexH = -1)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (minW.HasValue) le.minWidth = minW.Value;
            if (minH.HasValue) le.minHeight = minH.Value;
            if (prefW.HasValue) le.preferredWidth = prefW.Value;
            if (prefH.HasValue) le.preferredHeight = prefH.Value;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
        }

        /// <summary>
        /// Creates a container with a GridLayoutGroup component.
        /// Ideal for uniform elements like inventory slots or player galleries.
        /// </summary>
        public static GameObject CreateGrid(string name, GameObject parent, Vector2 cellSize, Vector2 spacing, float flexGrow = 0, int padding = 0)
        {
            GameObject go = CreateBlock(name, parent);
            if (flexGrow > 0) SetLayoutSize(go, flexH: flexGrow, flexW: 1);

            GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = new RectOffset(padding, padding, padding, padding);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperLeft;
            return go;
        }

        /// <summary>
        /// Creates a horizontal layout container (CSS Row equivalent).
        /// Automatically controls child width and height.
        /// </summary>
        public static GameObject CreateRow(string name, GameObject parent, float spacing = 0, int padding = 0, float flexGrow = 0, TextAnchor align = TextAnchor.MiddleLeft)
        {
            GameObject go = CreateBlock(name, parent);
            if (flexGrow > 0) SetLayoutSize(go, flexW: flexGrow);

            HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.padding = new RectOffset(padding, padding, padding, padding);
            hlg.childAlignment = align;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            return go;
        }

        /// <summary>
        /// Creates a vertical layout container (CSS Column equivalent).
        /// Configured to automatically control children's size while maintaining fixed spacing.
        /// </summary>
        public static GameObject CreateColumn(string name, GameObject parent, float spacing = 0, int padding = 0, float flexGrow = 0, TextAnchor align = TextAnchor.UpperLeft)
        {
            GameObject go = CreateBlock(name, parent);
            if (flexGrow > 0) SetLayoutSize(go, flexH: flexGrow);

            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(padding, padding, padding, padding);
            vlg.childAlignment = align;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            return go;
        }

        #endregion

        #region Form Controls

        public static Slider CreateSlider(GameObject parent, float min, float max, float value)
        {
            GameObject container = CreateBlock("Slider", parent);
            SetLayoutSize(container, minH: 40, flexW: 1);

            GameObject bgObj = CreatePanel("Background", container, new Color(1, 1, 1, 0.1f), GetOrGenerateRoundedSprite());
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(1, 0.5f);
            bgRect.sizeDelta = new Vector2(0, 6);
            bgObj.GetComponent<Image>().raycastTarget = true;

            GameObject handleArea = CreateBlock("Handle Slide Area", container);
            Stretch(handleArea);
            RectTransform haRect = handleArea.GetComponent<RectTransform>();
            haRect.offsetMin = new Vector2(20, 0);
            haRect.offsetMax = new Vector2(-20, 0);

            GameObject handle = CreatePanel("Handle", handleArea, Color.white, GetOrGenerateRoundedSprite());
            RectTransform hRect = handle.GetComponent<RectTransform>();
            hRect.sizeDelta = new Vector2(40, 40);
            hRect.anchorMin = new Vector2(0, 0.5f);
            hRect.anchorMax = new Vector2(0, 0.5f);
            Image hImg = handle.GetComponent<Image>();
            hImg.raycastTarget = true;

            Slider slider = container.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.targetGraphic = hImg;
            slider.handleRect = hRect;
            return slider;
        }

        #endregion
    }
}