using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UdonSharp;
using System.IO;
using VRC.SDK3.Components;
using UdonSharpEditor;
using BlackHorizon.HorizonGUI;
using BlackHorizon.HorizonGUI.Services;

namespace BlackHorizon.HorizonGUI.Editor
{
    /// <summary>
    /// Core Factory class for generating the Horizon UI.
    /// Handles procedural asset generation, GameObject instantiation, layout configuration, 
    /// and safe UdonSharp component attachment.
    /// </summary>
    public static class HorizonGUIFactory
    {
        private const string GENERATED_SPRITE_PATH = "Assets/Horizon GUI/Core/Runtime/Textures/Horizon_RoundedBackground.png";

        #region Default Theme Constants

        // Default styling fallbacks. 
        // These are used when CSS does not provide specific overrides.

        public static readonly Color ColorGlass = new Color(1f, 1f, 1f, 0.1f);

        // Public accessors for compatibility with Compiler
        public static Color ColorGlassDark => ColorGlass;

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
        /// Defaults to a standard 20px radius (Multiplier ~3.2).
        /// </summary>
        public static GameObject CreatePanel(string name, GameObject parent)
        {
            GameObject go = CreateBlock(name, parent);
            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = Color.white;
            img.type = Image.Type.Sliced;
            img.sprite = HorizonAssetPipeline.GetOrGenerateRoundedSprite();
            img.pixelsPerUnitMultiplier = 1.0f;

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

        #region UI Widgets & Layout

        /// <summary>
        /// Creates a TextMeshProUGUI element with theme-compliant styling.
        /// </summary>
        /// <param name="parent">Parent container GameObject.</param>
        /// <param name="content">Initial text content.</param>
        /// <returns>The created TextMeshProUGUI component.</returns>
        public static TextMeshProUGUI CreateText(GameObject parent, string content)
        {
            GameObject go = CreateBlock("Label", parent);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.raycastTarget = false;

            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;

            return tmp;
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
        /// Returns booleans to indicate binding success for validation.
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

            public bool Bind(string propertyName, UnityEngine.Object value)
            {
                SerializedProperty prop = _so.FindProperty(propertyName);
                if (prop != null)
                {
                    prop.objectReferenceValue = value;
                    return true;
                }
                return false;
            }

            public bool BindVal(string propertyName, object value)
            {
                SerializedProperty prop = _so.FindProperty(propertyName);
                if (prop == null) return false;

                if (value is int i) prop.intValue = i;
                else if (value is float f) prop.floatValue = f;
                else if (value is bool b) prop.boolValue = b;
                else if (value is string s) prop.stringValue = s;
                else if (value is Color c) prop.colorValue = c;
                return true;
            }

            public bool BindArray<T>(string propertyName, System.Collections.Generic.List<T> list)
            {
                SerializedProperty prop = _so.FindProperty(propertyName);
                if (prop == null) return false;

                prop.ClearArray();
                prop.arraySize = list.Count;

                for (int i = 0; i < list.Count; i++)
                {
                    SerializedProperty element = prop.GetArrayElementAtIndex(i);
                    object value = list[i];

                    if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
                    {
                        element.objectReferenceValue = value as UnityEngine.Object;
                    }
                    else if (typeof(T) == typeof(string))
                    {
                        element.stringValue = value as string;
                    }
                    else if (typeof(T) == typeof(int))
                    {
                        element.intValue = (int)value;
                    }
                    else if (typeof(T) == typeof(float))
                    {
                        element.floatValue = (float)value;
                    }
                    else if (typeof(T) == typeof(bool))
                    {
                        element.boolValue = (bool)value;
                    }
                    else if (typeof(T) == typeof(Color))
                    {
                        element.colorValue = (Color)value;
                    }
                    else
                    {
                        Debug.LogWarning($"[HorizonLogicBinder] Unsupported array element type: {typeof(T).Name} for property '{propertyName}'");
                        return false;
                    }
                }

                return true;
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
            trackImg.sprite = HorizonAssetPipeline.GetOrGenerateRoundedSprite();
            trackImg.type = Image.Type.Sliced;
            trackImg.color = new Color(1, 1, 1, 0.03f);
            trackImg.pixelsPerUnitMultiplier = 3.0f;

            GameObject slidingArea = CreateBlock("Sliding Area", sbObj);
            Stretch(slidingArea);

            GameObject handle = CreateBlock("Handle", slidingArea);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.sprite = HorizonAssetPipeline.GetOrGenerateRoundedSprite();
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

        #endregion

        #region Layout & Interaction Helpers

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

            GameObject bgObj = CreatePanel("Background", container);
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(1, 1, 1, 0.1f);
            bgImg.sprite = HorizonAssetPipeline.GetOrGenerateRoundedSprite();
            bgImg.raycastTarget = true;
            bgImg.pixelsPerUnitMultiplier = 64f / 3f;

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(1, 0.5f);
            bgRect.sizeDelta = new Vector2(0, 6);

            GameObject handleArea = CreateBlock("Handle Slide Area", container);
            Stretch(handleArea);

            GameObject handle = CreatePanel("Handle", handleArea);
            Image hImg = handle.GetComponent<Image>();
            hImg.color = Color.white;
            hImg.sprite = HorizonAssetPipeline.GetOrGenerateRoundedSprite();
            hImg.raycastTarget = true;
            hImg.pixelsPerUnitMultiplier = 64f / 20f;

            RectTransform hRect = handle.GetComponent<RectTransform>();
            hRect.sizeDelta = new Vector2(40, 0);
            hRect.anchorMin = new Vector2(0, 0.5f);
            hRect.anchorMax = new Vector2(0, 0.5f);

            Slider slider = container.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.targetGraphic = hImg;
            slider.handleRect = hRect;
            return slider;
        }

        #endregion

        /// <summary>
        /// Dynamically locates a C# type by name and attaches it as an UdonSharp component.
        /// <para>
        /// <b>Search Strategy:</b><br/>
        /// 1. Direct Type.GetType (for fully qualified names).<br/>
        /// 2. Robust Lookup via TypeCache.
        /// </para>
        /// </summary>
        /// <param name="target">The GameObject to attach the script to.</param>
        /// <param name="typeName">The class name (e.g., "HorizonGUI_WeatherModule").</param>
        /// <returns>The attached behaviour, or a default HorizonGUIModule if not found.</returns>
        public static UdonSharpBehaviour AttachLogicByString(GameObject target, string typeName)
        {
            System.Type targetType = System.Type.GetType(typeName);

            if (targetType == null)
            {
                var derivedTypes = TypeCache.GetTypesDerivedFrom<UdonSharpBehaviour>();

                System.Type foundType = null;
                bool duplicateFound = false;

                foreach (var type in derivedTypes)
                {
                    if (type.Name == typeName)
                    {
                        if (foundType == null)
                        {
                            foundType = type;
                        }
                        else
                        {
                            duplicateFound = true;
                            Debug.LogWarning($"<color=yellow>[HorizonFactory]</color> Ambiguity Warning: Multiple scripts named '<b>{typeName}</b>' found:\n" +
                                             $"1. {foundType.FullName}\n" +
                                             $"2. {type.FullName}\n" +
                                             $"System is using the first one. Please specify namespace in HTML (u-script='Namespace.Class') to be precise.");
                        }
                    }
                }

                targetType = foundType;
            }

            if (targetType == null)
            {
                Debug.LogError($"<color=red>[HorizonFactory]</color> Could not find script type: '<b>{typeName}</b>'.\n" +
                               $"If you moved the script to a new namespace, verify it compiles correctly.");
                return AttachLogic<HorizonGUIModule>(target);
            }

            return UdonSharpUndo.AddComponent(target, targetType) as UdonSharpBehaviour;
        }
    }
}