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
    /// Handles the creation of GameObjects, UI components, and procedural assets.
    /// </summary>
    public static class HorizonGUIFactory
    {
        // --- 1. THEME MANAGEMENT ---
        private static HorizonTheme _currentTheme;
        private static HorizonTheme _overrideTheme;

        /// <summary>
        /// Sets the theme for a specific build operation (e.g. from the manager).
        /// </summary>
        public static void SetThemeContext(HorizonTheme theme)
        {
            _overrideTheme = theme;
        }

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

        private const string GENERATED_SPRITE_PATH = "Assets/Horizon GUI/Textures/Horizon_RoundedBackground.png";

        // --- 2. CORE BUILDER METHODS ---

        /// <summary>
        /// Creates a basic UI GameObject with a RectTransform.
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
        /// Creates a GameObject with an Image component (Panel).
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
        /// <typeparam name="T">The UdonSharpBehaviour type.</typeparam>
        /// <param name="target">The target GameObject.</param>
        /// <returns>The attached UdonSharp component (Proxy).</returns>
        public static T AttachLogic<T>(GameObject target) where T : UdonSharpBehaviour
        {
            T existing = target.GetComponent<T>();
            if (existing != null) return existing;

            return UdonSharpUndo.AddComponent<T>(target);
        }

        /// <summary>
        /// Adds a BoxCollider and VRCUiShape to make the UI interactable in VRChat.
        /// </summary>
        public static void AddInteraction(GameObject go, Vector2 size)
        {
            go.layer = 0;
            BoxCollider col = go.GetComponent<BoxCollider>();
            if (col == null) col = go.AddComponent<BoxCollider>();

            col.size = new Vector3(size.x, size.y, 25f);
            col.isTrigger = true;
            col.center = new Vector3(0, 0, 0f);

            if (go.GetComponent<VRCUiShape>() == null) go.AddComponent<VRCUiShape>();
        }

        // --- UTILS ---

        /// <summary>
        /// Sets RectTransform anchors to stretch the element to fill its parent.
        /// </summary>
        public static void Stretch(GameObject go, float padding = 0)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        // --- ASSET GENERATION ---

        /// <summary>
        /// Generates a procedural rounded rectangle sprite and saves it to disk.
        /// Used for the "Glassmorphism" background to avoid external dependencies.
        /// </summary>
        public static Sprite GetOrGenerateRoundedSprite()
        {
            string dir = Path.GetDirectoryName(GENERATED_SPRITE_PATH);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(GENERATED_SPRITE_PATH);
            if (existing != null) return existing;

            int size = 128;
            float radius = 48f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            Vector2 tl = new Vector2(radius, size - radius);
            Vector2 tr = new Vector2(size - radius, size - radius);
            Vector2 bl = new Vector2(radius, radius);
            Vector2 br = new Vector2(size - radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float alpha = 0.0f;
                    // Center block
                    if (x > radius && x < size - radius) alpha = 1.0f;
                    else if (y > radius && y < size - radius) alpha = 1.0f;
                    else
                    {
                        // Corners
                        float d = float.MaxValue;
                        if (x <= radius && y >= size - radius) d = Vector2.Distance(p, tl);
                        else if (x >= size - radius && y >= size - radius) d = Vector2.Distance(p, tr);
                        else if (x <= radius && y <= radius) d = Vector2.Distance(p, bl);
                        else if (x >= size - radius && y <= radius) d = Vector2.Distance(p, br);
                        if (d <= radius) alpha = 1.0f;
                    }
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(GENERATED_SPRITE_PATH, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(GENERATED_SPRITE_PATH, ImportAssetOptions.ForceUpdate);

            // Set import settings for 9-slice scaling
            TextureImporter importer = AssetImporter.GetAtPath(GENERATED_SPRITE_PATH) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.spriteBorder = new Vector4(radius, radius, radius, radius);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(GENERATED_SPRITE_PATH);
        }

        /// <summary>
        /// Retrieves the custom Glass shader/material. Creates it if missing.
        /// </summary>
        public static Material GetGlassMaterial()
        {
            string matPath = "Assets/Horizon GUI/Materials/HorizonGlass.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Horizon/UI/Glass Blur");
                if (shader == null) return null;
                string dir = Path.GetDirectoryName(matPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                mat = new Material(shader);
                mat.SetFloat("_BlurSize", 8.0f);
                mat.SetColor("_Color", new Color(0.9f, 0.95f, 1.0f, 0.3f));
                AssetDatabase.CreateAsset(mat, matPath);
                AssetDatabase.Refresh();
            }
            return mat;
        }

        public static Sprite LoadPackageSprite(string filename)
        {
            string searchName = Path.GetFileNameWithoutExtension(filename);

            string[] guids = AssetDatabase.FindAssets(searchName);

            if (guids.Length == 0)
            {
                Debug.LogWarning($"[HorizonGUI] Icon not found in project: {filename} (Search term: {searchName})");
                return null;
            }

            string bestPath = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!path.EndsWith(".png") && !path.EndsWith(".jpg") && !path.EndsWith(".psd"))
                    continue;

                if (path.Contains("Horizon") || path.Contains("GUI") || path.Contains("Textures"))
                {
                    bestPath = path;
                    break;
                }

                if (bestPath == null) bestPath = path;
            }

            if (bestPath == null)
            {
                Debug.LogWarning($"[HorizonGUI] Found assets named '{searchName}', but none looked like a texture.");
                return null;
            }

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

        // --- TEXT & BUTTONS ---

        public enum TextStyle
        {
            H1,
            H2,
            Body,
            BodyDim,
            Small,
            SmallDim,
            Clock
        }

        public static TextMeshProUGUI CreateText(GameObject parent, string content, TextStyle style, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            GameObject go = CreateBlock("Label", parent);
            Stretch(go);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.alignment = align;
            tmp.raycastTarget = false;

            switch (style)
            {
                case TextStyle.H1:
                    tmp.fontSize = Theme.sizeH1;
                    tmp.color = Theme.primaryColor;
                    tmp.fontStyle = FontStyles.Bold;
                    break;
                case TextStyle.H2:
                    tmp.fontSize = Theme.sizeH2;
                    tmp.color = Theme.primaryColor;
                    tmp.fontStyle = FontStyles.Bold;
                    break;
                case TextStyle.Body:
                    tmp.fontSize = Theme.sizeBody;
                    tmp.color = Theme.primaryColor;
                    break;
                case TextStyle.BodyDim:
                    tmp.fontSize = Theme.sizeBody;
                    tmp.color = Theme.secondaryColor;
                    break;
                case TextStyle.Small:
                    tmp.fontSize = Theme.sizeSmall;
                    tmp.color = Theme.primaryColor;
                    break;
                case TextStyle.SmallDim:
                    tmp.fontSize = Theme.sizeSmall;
                    tmp.color = Theme.secondaryColor;
                    break;
                case TextStyle.Clock:
                    tmp.fontSize = Theme.sizeClock;
                    tmp.color = Theme.secondaryColor;
                    break;
            }

            return tmp;
        }

        /// <summary>
        /// Creates a standard Horizon UI Button structure.
        /// Hierarchy: Root (Transparent, Button Logic) -> Icon (Image).
        /// </summary>
        public static GameObject CreateIconButton(string name, GameObject parent, Sprite bgSprite, Sprite iconSprite)
        {
            // 1. Root Panel (Invisible background for hitbox)
            GameObject btnObj = CreatePanel(name, parent, new Color(1, 1, 1, 0.0f), bgSprite);
            Image bgImg = btnObj.GetComponent<Image>();
            bgImg.raycastTarget = true;

            // 2. Icon Image
            GameObject iconObj = CreateBlock("Icon", btnObj);
            Stretch(iconObj, 20);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.color = Color.white;
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // 3. Button Component
            Button b = btnObj.AddComponent<Button>();
            b.targetGraphic = bgImg;
            b.transition = Selectable.Transition.ColorTint;

            // Set visual states (Invisible Normal, Visible Highlight/Press)
            ColorBlock cb = b.colors;
            cb.normalColor = new Color(1, 1, 1, 0f);
            cb.highlightedColor = new Color(1, 1, 1, 0.1f);
            cb.pressedColor = new Color(1, 1, 1, 0.3f);
            cb.selectedColor = new Color(1, 1, 1, 0.0f);
            cb.fadeDuration = 0.1f;
            b.colors = cb;

            return btnObj;
        }

        // --- LAYOUT HELPERS ---

        public static VerticalLayoutGroup CreateVerticalGroup(GameObject parent, float spacing, RectOffset padding = null, bool controlWidth = true, bool controlHeight = true, TextAnchor align = TextAnchor.UpperLeft)
        {
            GameObject target = parent.GetComponent<RectTransform>() != null ? parent : CreateBlock("VGroup", parent);

            var vlg = target.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = padding ?? new RectOffset(0, 0, 0, 0);
            vlg.childAlignment = align;
            vlg.childControlWidth = controlWidth;
            vlg.childControlHeight = controlHeight;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            return vlg;
        }

        public static HorizontalLayoutGroup CreateHorizontalGroup(GameObject parent, float spacing, RectOffset padding = null, bool controlWidth = true, bool controlHeight = true, TextAnchor align = TextAnchor.UpperLeft)
        {
            var hlg = parent.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.padding = padding ?? new RectOffset(0, 0, 0, 0);
            hlg.childAlignment = align;
            hlg.childControlWidth = controlWidth;
            hlg.childControlHeight = controlHeight;
            hlg.childForceExpandWidth = false;
            return hlg;
        }

        public static void SetLayoutSize(GameObject go, float? minW = null, float? minH = null, float? prefW = null, float? prefH = null, float flexW = -1, float flexH = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();

            if (minW.HasValue) le.minWidth = minW.Value;
            if (minH.HasValue) le.minHeight = minH.Value;
            if (prefW.HasValue) le.preferredWidth = prefW.Value;
            if (prefH.HasValue) le.preferredHeight = prefH.Value;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
        }

        // --- HIGH-LEVEL SMART CONTAINERS (CSS-LIKE) ---

        /// <summary>
        /// Creates a vertical flex container (Column).
        /// Combines GameObject creation, LayoutElement (sizing), and VerticalLayoutGroup in one call.
        /// </summary>
        /// <param name="name">Name of the GameObject.</param>
        /// <param name="parent">Parent GameObject.</param>
        /// <param name="spacing">Space between children (pixels).</param>
        /// <param name="padding">Internal padding (pixels).</param>
        /// <param name="flexGrow">If > 0, the container will expand to fill available space (LayoutElement.flexibleHeight).</param>
        /// <param name="align">Alignment of children.</param>
        /// <returns>The created container GameObject.</returns>
        public static GameObject CreateColumn(string name, GameObject parent, float spacing = 0, int padding = 0, float flexGrow = 0, TextAnchor align = TextAnchor.UpperLeft)
        {
            GameObject go = CreateBlock(name, parent);

            // 1. Layout Logic (External sizing)
            if (flexGrow > 0) SetLayoutSize(go, flexH: flexGrow);
            else SetLayoutSize(go, flexH: 0); // Default to auto/fixed

            // 2. Group Logic (Internal organization)
            CreateVerticalGroup(go, spacing, new RectOffset(padding, padding, padding, padding), true, true, align);

            return go;
        }

        /// <summary>
        /// Creates a horizontal flex container (Row).
        /// </summary>
        /// <param name="name">Name of the GameObject.</param>
        /// <param name="parent">Parent GameObject.</param>
        /// <param name="spacing">Space between children.</param>
        /// <param name="flexGrow">If > 0, expands width (LayoutElement.flexibleWidth).</param>
        public static GameObject CreateRow(string name, GameObject parent, float spacing = 0, int padding = 0, float flexGrow = 0, TextAnchor align = TextAnchor.MiddleLeft)
        {
            GameObject go = CreateBlock(name, parent);

            if (flexGrow > 0) SetLayoutSize(go, flexW: flexGrow);

            CreateHorizontalGroup(go, spacing, new RectOffset(padding, padding, padding, padding), true, true, align);

            return go;
        }

        /// <summary>
        /// Creates a Grid container.
        /// Replaces the manual creation of GridLayoutGroup.
        /// </summary>
        /// <param name="cellSize">Size of each grid item (width, height).</param>
        /// <param name="spacing">Gap between items (x, y).</param>
        /// <param name="flexGrow">If > 0, the grid container expands to fill space.</param>
        public static GameObject CreateGrid(string name, GameObject parent, Vector2 cellSize, Vector2 spacing, float flexGrow = 0, int padding = 0)
        {
            GameObject go = CreateBlock(name, parent);

            // Layout Element
            if (flexGrow > 0) SetLayoutSize(go, flexH: flexGrow, flexW: 1);

            // Grid Layout Group
            GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = new RectOffset(padding, padding, padding, padding);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperLeft;

            return go;
        }

        // --- LOGIC BINDING SYSTEM (Smart API) ---

        /// <summary>
        /// Attaches a script and provides a clean API to set its variables.
        /// Replaces manual SerializedObject manipulation.
        /// </summary>
        public static T ConfigureLogic<T>(GameObject target, System.Action<HorizonLogicBinder> config) where T : UdonSharpBehaviour
        {
            // 1. Attach the component safely
            T script = AttachLogic<T>(target);

            // 2. Create the binder wrapper
            HorizonLogicBinder binder = new HorizonLogicBinder(script);

            // 3. Execute user configuration
            config(binder);

            // 4. Save changes
            binder.Apply();

            return script;
        }

        /// <summary>
        /// Helper class to simplify SerializedObject property assignment.
        /// </summary>
        public class HorizonLogicBinder
        {
            private SerializedObject _so;

            public HorizonLogicBinder(UnityEngine.Object target)
            {
                _so = new SerializedObject(target);
            }

            /// <summary>
            /// Binds a single object reference (GameObject, Component, Material, etc).
            /// </summary>
            public void Bind(string propertyName, UnityEngine.Object value)
            {
                SerializedProperty prop = _so.FindProperty(propertyName);
                if (prop != null)
                {
                    prop.objectReferenceValue = value;
                }
                else
                {
                    Debug.LogError($"[HorizonGUI] Property '{propertyName}' not found in {_so.targetObject.name}!");
                }
            }

            /// <summary>
            /// Binds a primitive value (int, float, bool, string).
            /// </summary>
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

            /// <summary>
            /// Automatically handles arrays/lists of objects.
            /// No more manual loops!
            /// </summary>
            public void BindArray(string propertyName, System.Collections.Generic.List<GameObject> list)
            {
                SerializedProperty prop = _so.FindProperty(propertyName);
                if (prop == null)
                {
                    Debug.LogError($"[HorizonGUI] Array Property '{propertyName}' not found!");
                    return;
                }

                prop.ClearArray();
                prop.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
                }
            }

            public void BindArray<TComp>(string propertyName, System.Collections.Generic.List<TComp> list) where TComp : Component
            {
                SerializedProperty prop = _so.FindProperty(propertyName);
                if (prop == null)
                {
                    Debug.LogError($"[HorizonGUI] Array Property '{propertyName}' not found!");
                    return;
                }

                prop.ClearArray();
                prop.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
                }
            }

            public void Apply()
            {
                _so.ApplyModifiedProperties();
            }

            private UdonSharpBehaviour _script;
            public UdonSharpBehaviour TargetScript => _script;
            public HorizonLogicBinder(UdonSharpBehaviour target)
            {
                _script = target;
                _so = new SerializedObject(target);
            }
        }

        // --- COMPLEX WIDGETS ---

        /// <summary>
        /// Creates a complete Data Grid with a pool of reusable slots.
        /// </summary>
        /// <param name="name">Name of the Grid Object.</param>
        /// <param name="parent">Parent container.</param>
        /// <param name="poolSize">How many slots to create (fixed pool).</param>
        /// <param name="cellSize">Size of each cell (x, y).</param>
        /// <param name="eventTarget">The UdonBehaviour that will receive clicks.</param>
        /// <param name="eventName">The custom event name to call.</param>
        /// <returns>The HorizonDataGrid manager script.</returns>
        public static HorizonDataGrid CreateDataGrid(
            string name,
            GameObject parent,
            int poolSize,
            Vector2 cellSize,
            UdonSharpBehaviour eventTarget = null,
            string eventName = "OnItemSelected"
        )
        {
            // 1. Create Container (Grid Layout)
            GameObject gridObj = CreateGrid(name, parent, cellSize, new Vector2(10, 10), flexGrow: 1, padding: 10);

            // 2. Attach Manager
            var manager = AttachLogic<HorizonDataGrid>(gridObj);

            // 3. Generate Pool
            var generatedSlots = new System.Collections.Generic.List<HorizonGridItem>();
            Sprite slotBg = GetOrGenerateRoundedSprite();
            Sprite defIcon = LoadPackageSprite("information_source.png");

            for (int i = 0; i < poolSize; i++)
            {
                string slotName = $"Slot_{i:00}";

                GameObject slotObj = CreateIconButton(slotName, gridObj, slotBg, defIcon);

                var itemScript = AttachLogic<HorizonGridItem>(slotObj);

                ConfigureLogic<HorizonGridItem>(slotObj, binder =>
                {
                    binder.Bind("gridManager", manager);
                    binder.BindVal("slotIndex", i);
                    binder.Bind("titleText", null);

                    var img = slotObj.GetComponent<Image>();
                    var iconTrans = slotObj.transform.Find("Icon");

                    if (img != null) binder.Bind("backgroundImage", img);
                    if (iconTrans != null) binder.Bind("iconImage", iconTrans.GetComponent<Image>());
                });

                // Add Text component specifically for Grid Item if not present
                if (slotObj.GetComponentInChildren<TextMeshProUGUI>() == null)
                {
                    var t = CreateText(slotObj, "Item Name", TextStyle.Small, TextAlignmentOptions.Center);
                    t.rectTransform.anchorMin = new Vector2(0, 0);
                    t.rectTransform.anchorMax = new Vector2(1, 0);
                    t.rectTransform.pivot = new Vector2(0.5f, 0);
                    t.rectTransform.offsetMin = new Vector2(5, 5);
                    t.rectTransform.offsetMax = new Vector2(-5, 25);

                    ConfigureLogic<HorizonGridItem>(slotObj, binder => binder.Bind("titleText", t));
                }

                Button btn = slotObj.GetComponent<Button>();
                var itemUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(itemScript);

                if (btn != null && itemUdon != null)
                {
                    int eventCount = btn.onClick.GetPersistentEventCount();
                    for (int k = eventCount - 1; k >= 0; k--)
                        UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, k);

                    UnityEditor.Events.UnityEventTools.AddStringPersistentListener(
                        btn.onClick,
                        itemUdon.SendCustomEvent,
                        "OnClick"
                    );
                }

                generatedSlots.Add(itemScript);
            }

            // 4. Configure Manager
            ConfigureLogic<HorizonDataGrid>(gridObj, binder =>
            {
                binder.BindArray("slotPool", generatedSlots);
                binder.BindVal("itemsPerPage", poolSize);

                if (eventTarget != null)
                {
                    var targetBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(eventTarget);
                    binder.Bind("targetCallback", targetBacking);
                    binder.BindVal("callbackEventName", eventName);
                }
            });

            return manager;
        }

        // --- FORM CONTROLS (Sliders & Toggles) ---

        /// <summary>
        /// Creates a styled Toggle (Checkbox) with a label.
        /// </summary>
        /// <param name="parent">The parent GameObject (usually a layout container).</param>
        /// <param name="labelText">The text displayed next to the toggle.</param>
        /// <param name="isOn">Initial state of the toggle.</param>
        /// <returns>The created Unity UI Toggle component.</returns>
        public static Toggle CreateToggle(GameObject parent, string labelText, bool isOn)
        {
            // 1. Container (Row)
            GameObject container = CreateRow("Toggle_" + labelText, parent, spacing: 10, padding: 0);
            SetLayoutSize(container, minH: 40);

            // 2. Checkbox Background
            GameObject bgObj = CreatePanel("Background", container, new Color(1, 1, 1, 0.1f), GetOrGenerateRoundedSprite());
            SetLayoutSize(bgObj, minW: 60, minH: 60);
            bgObj.transform.localScale = Vector3.one * 0.5f;
            bgObj.GetComponent<Image>().raycastTarget = true;

            // 3. Checkmark (Icon inside)
            GameObject checkObj = CreatePanel("Checkmark", bgObj, Theme.primaryColor, GetOrGenerateRoundedSprite());
            Stretch(checkObj, 10);

            // 4. Label
            CreateText(container, labelText, TextStyle.Body, TextAlignmentOptions.Left);

            Toggle toggle = container.AddComponent<Toggle>();
            toggle.isOn = isOn;
            toggle.targetGraphic = bgObj.GetComponent<Image>();
            toggle.graphic = checkObj.GetComponent<Image>();
            toggle.toggleTransition = Toggle.ToggleTransition.Fade;

            return toggle;
        }

        /// <summary>
        /// Creates a styled Slider.
        /// </summary>
        /// <param name="parent">The parent GameObject.</param>
        /// <param name="min">Minimum value.</param>
        /// <param name="max">Maximum value.</param>
        /// <param name="value">Initial value.</param>
        /// <returns>The created Unity UI Slider component.</returns>
        public static Slider CreateSlider(GameObject parent, float min, float max, float value)
        {
            GameObject container = CreateBlock("Slider", parent);
            SetLayoutSize(container, minH: 40, flexW: 1);

            // 1. Background (Track)
            GameObject bgObj = CreatePanel("Background", container, new Color(1, 1, 1, 0.1f), GetOrGenerateRoundedSprite());
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(1, 0.5f);
            bgRect.sizeDelta = new Vector2(0, 6);
            bgRect.anchoredPosition = Vector2.zero;

            bgObj.GetComponent<Image>().raycastTarget = true;

            // 2. Handle Slide Area (Invisible container for the handle movement)
            GameObject handleArea = CreateBlock("Handle Slide Area", container);
            Stretch(handleArea);
            RectTransform haRect = handleArea.GetComponent<RectTransform>();
            haRect.offsetMin = new Vector2(20, 0);
            haRect.offsetMax = new Vector2(-20, 0);

            // 3. Handle (The Circle)
            GameObject handle = CreatePanel("Handle", handleArea, Color.white, GetOrGenerateRoundedSprite());
            RectTransform hRect = handle.GetComponent<RectTransform>();

            hRect.sizeDelta = new Vector2(40, 40);
            hRect.anchorMin = new Vector2(0, 0.5f);
            hRect.anchorMax = new Vector2(0, 0.5f);

            Image hImg = handle.GetComponent<Image>();
            hImg.type = Image.Type.Simple;
            hImg.preserveAspect = true;

            hImg.raycastTarget = true;

            // 4. Component Logic
            Slider slider = container.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.targetGraphic = hImg;
            slider.handleRect = hRect;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        /// <summary>
        /// Creates a vertical column that scrolls if content overflows.
        /// Structure: Root (ScrollRect) -> Viewport (Mask) -> Content (Layout) + Scrollbar.
        /// </summary>
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
            // 1. Root Container (ScrollRect)
            GameObject root = CreateBlock(name, parent);
            if (flexGrow > 0) SetLayoutSize(root, flexH: flexGrow, flexW: 1);

            Image rootImg = root.AddComponent<Image>();
            rootImg.color = new Color(0, 0, 0, 0);

            ScrollRect scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 25f;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            // 2. Viewport (Mask)
            GameObject viewport = CreateBlock("Viewport", root);
            RectTransform vpRect = viewport.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = new Vector2(0, 0);

            vpRect.offsetMax = new Vector2(-24, 0);

            viewport.AddComponent<RectMask2D>();
            Image vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);

            // 3. Content
            GameObject content = CreateBlock("Content", viewport);
            RectTransform cRect = content.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0, 1);
            cRect.anchorMax = new Vector2(1, 1);
            cRect.pivot = new Vector2(0.5f, 1);
            cRect.sizeDelta = Vector2.zero;

            CreateVerticalGroup(content, spacing, new RectOffset(padding, padding, padding, padding), true, true, align);
            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vpRect;
            scroll.content = cRect;

            // 4. Scrollbar (Glass Style)
            GameObject sbObj = CreateBlock("Scrollbar Vertical", root);
            RectTransform sbRect = sbObj.GetComponent<RectTransform>();

            sbRect.anchorMin = new Vector2(1, 0);
            sbRect.anchorMax = new Vector2(1, 1);
            sbRect.pivot = new Vector2(1, 1);

            sbRect.offsetMin = new Vector2(-24, sbMarginBottom);
            sbRect.offsetMax = new Vector2(-4, -sbMarginTop);

            // Background (Track)
            Image trackImg = sbObj.AddComponent<Image>();
            trackImg.sprite = GetOrGenerateRoundedSprite();
            trackImg.type = Image.Type.Sliced;
            trackImg.color = new Color(1, 1, 1, 0.03f);

            trackImg.pixelsPerUnitMultiplier = 3.0f;

            // Sliding Area
            GameObject slidingArea = CreateBlock("Sliding Area", sbObj);
            Stretch(slidingArea);

            // Handle
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

        // --- SMART EVENT BINDING ---

        /// <summary>
        /// Binds a button click to a Udon event with parameters (int/string).
        /// Adds a HorizonEventCaller proxy to the button object.
        /// </summary>
        /// <param name="buttonObj">GameObject with the Button component.</param>
        /// <param name="target">The UdonBehaviour to receive the call.</param>
        /// <param name="eventName">Name of the method to call (e.g., "OnSlotClicked").</param>
        /// <param name="intVal">Integer parameter to pass.</param>
        /// <param name="stringVal">String parameter to pass (optional).</param>
        public static void BindEventWithArgs(GameObject buttonObj, UdonSharpBehaviour target, string eventName, int intVal = 0, string stringVal = "")
        {
            Button btn = buttonObj.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogError($"[HorizonGUI] Cannot bind event: {buttonObj.name} has no Button component.");
                return;
            }

            var caller = AttachLogic<HorizonEventCaller>(buttonObj);

            ConfigureLogic<HorizonEventCaller>(buttonObj, binder =>
            {
                var targetUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(target);

                binder.Bind("targetBehaviour", targetUdon);
                binder.BindVal("eventName", eventName);
                binder.BindVal("intPayload", intVal);
                binder.BindVal("stringPayload", stringVal);
            });

            int eventCount = btn.onClick.GetPersistentEventCount();
            for (int i = eventCount - 1; i >= 0; i--)
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, i);

            var callerUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(caller);
            if (callerUdon != null)
            {
                UnityEditor.Events.UnityEventTools.AddStringPersistentListener(
                    btn.onClick,
                    callerUdon.SendCustomEvent,
                    "OnClick"
                );
            }
        }
    }
}