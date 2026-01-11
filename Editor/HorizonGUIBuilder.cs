using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UdonSharpEditor;
using UnityEditor.Events;

namespace BlackHorizon.HorizonGUI.Editor
{
    public class HorizonGUIBuilder
    {
        private const string SYSTEM_ROOT_NAME = "Horizon UI System";

        [MenuItem("GameObject/Horizon/Create UI System", false, 10)]
        public static void CreateNewSystem(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("Horizon UI System");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;

            var mgr = go.AddComponent<HorizonGUIManager>();

            var auth = go.AddComponent<HorizonGUIAuthoring>();

            auth.theme = HorizonGUIFactory.Theme;
        }

        public static void RebuildOnTarget(HorizonGUIManager manager)
        {
            GameObject systemRoot = manager.gameObject;

            // 1. CLEANING OLD
            while (systemRoot.transform.childCount > 0)
            {
                GameObject.DestroyImmediate(systemRoot.transform.GetChild(0).gameObject);
            }

            // 2. INPUT
            EnsureEventSystemInside(systemRoot);

            // 3. CANVAS
            GameObject canvasObj = InitializeCanvas("Visual_Canvas", systemRoot);
            Sprite bgSprite = HorizonGUIFactory.GetOrGenerateRoundedSprite();

            // 4. LAYOUT & MODULES
            BuildBackground(canvasObj, bgSprite);
            GameObject layoutRoot = BuildLayoutRoot(canvasObj);
            GameObject navContainer = BuildSidebar(layoutRoot, bgSprite);
            GameObject contentArea = BuildContentArea(layoutRoot);
            var clockTxt = BuildHeader(contentArea);

            GameObject pagesContainer = HorizonGUIFactory.CreateBlock("Pages", contentArea);
            HorizonGUIFactory.SetLayoutSize(pagesContainer, flexH: 1);
            HorizonGUIFactory.Stretch(pagesContainer);
            HorizonGUIFactory.CreateVerticalGroup(pagesContainer, 0, null, true, true);

            // 5. MODULES CREATION
            var builders = new List<IHorizonModuleBuilder>
            {
                new HorizonHomeBuilder(),
                new HorizonWeatherBuilder(),
                new HorizonAboutBuilder()
            };

            var createdModules = new List<HorizonGUIModule>();
            var createdButtons = new List<HorizonGUINavigationButton>();

            for (int i = 0; i < builders.Count; i++)
            {
                var builder = builders[i];
                HorizonGUIModule moduleScript = builder.BuildPage(pagesContainer);
                moduleScript.gameObject.SetActive(i == 0);
                createdModules.Add(moduleScript);

                if (builder is HorizonAboutBuilder)
                {
                    GameObject spacer = HorizonGUIFactory.CreateBlock("NavSpacer", navContainer);
                    HorizonGUIFactory.SetLayoutSize(spacer, flexH: 1);
                }

                Sprite icon = HorizonGUIFactory.LoadPackageSprite(builder.IconName);
                GameObject btnObj = HorizonGUIFactory.CreateIconButton($"Btn_{builder.ModuleName}", navContainer, bgSprite, icon);

                HorizonGUIFactory.SetLayoutSize(btnObj, minW: 80, minH: 80, prefW: 80, prefH: 80, flexH: 0);

                var btnScript = HorizonGUIFactory.AttachLogic<HorizonGUINavigationButton>(btnObj);

                HorizonGUIFactory.ConfigureLogic<HorizonGUINavigationButton>(btnObj, b =>
                {
                    b.Bind("manager", manager);
                    b.BindVal("tabIndex", i);
                    b.Bind("background", btnObj.GetComponent<Image>());
                    b.Bind("icon", btnObj.transform.Find("Icon").GetComponent<Image>());
                });

                createdButtons.Add(btnScript);
            }

            // 6. OVERLAY LAYER
            GameObject overlayObj = HorizonGUIFactory.CreateBlock("Overlay_Layer", canvasObj);
            HorizonGUIFactory.Stretch(overlayObj);

            // Dimmer
            Image overlayBg = overlayObj.AddComponent<Image>();
            overlayBg.color = new Color(0, 0, 0, 0.6f);
            overlayBg.raycastTarget = true;

            Button dismissBtn = overlayObj.AddComponent<Button>();
            dismissBtn.transition = Selectable.Transition.None;

            var mgrUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(manager);
            if (mgrUdon != null)
            {

                UnityEventTools.AddStringPersistentListener(
                    dismissBtn.onClick,
                    mgrUdon.SendCustomEvent,
                    "CloseOverlay"
                );
            }

            overlayObj.SetActive(false);

            // 7. FINALIZE MANAGER
            HorizonGUIFactory.ConfigureLogic<HorizonGUIManager>(systemRoot, m =>
            {
                m.Bind("clockText", clockTxt);
                m.Bind("pageContentContainer", pagesContainer.transform);
                m.Bind("overlayContainer", overlayObj);
                m.BindArray("modules", createdModules);
                m.BindArray("navigationButtons", createdButtons);
            });

            HorizonGUIBaker.BakeInterface(systemRoot.name);
        }

        // --- HELPERS ---

        private static void EnsureEventSystemInside(GameObject parent)
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                Debug.Log("[HorizonGUI] External EventSystem detected. Skipping creation.");
                return;
            }

            GameObject esObj = new GameObject("System_Input");
            esObj.transform.SetParent(parent.transform);
            esObj.AddComponent<EventSystem>();

            System.Type newInputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (newInputModuleType != null)
            {
                esObj.AddComponent(newInputModuleType);
            }
            else
            {
                var stdInput = esObj.AddComponent<StandaloneInputModule>();

                stdInput.horizontalAxis = "NonExistentAxis_H";
                stdInput.verticalAxis = "NonExistentAxis_V";
                stdInput.submitButton = "Submit";
                stdInput.cancelButton = "Cancel";
            }
        }

        private static GameObject InitializeCanvas(string name, GameObject parent)
        {
            Vector2 canvasSize = new Vector2(1000, 600);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.001f;

            var rootRect = go.AddComponent<RectTransform>();
            rootRect.sizeDelta = canvasSize;

            go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
            go.AddComponent<GraphicRaycaster>();

            HorizonGUIFactory.AddInteraction(go, canvasSize);

            return go;
        }

        // --- OLD HELPERS ---

        private static void BuildBackground(GameObject root, Sprite bgSprite)
        {
            GameObject container = CreateBlockSafe("Background", root);
            HorizonGUIFactory.Stretch(container);
            var maskImg = container.AddComponent<Image>();
            maskImg.sprite = bgSprite; maskImg.type = Image.Type.Sliced;
            var mask = container.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject blur = CreatePanelSafe("Glass", container, HorizonGUIFactory.ColorGlassDark, bgSprite);
            HorizonGUIFactory.Stretch(blur);
            blur.GetComponent<Image>().material = HorizonGUIFactory.GetGlassMaterial();
            blur.transform.SetAsFirstSibling();
        }

        private static GameObject BuildLayoutRoot(GameObject parent)
        {
            GameObject layoutRoot = HorizonGUIFactory.CreateRow("Layout", parent, padding: 0);
            HorizonGUIFactory.Stretch(layoutRoot);

            return layoutRoot;
        }

        private static GameObject BuildSidebar(GameObject parent, Sprite bgSprite)
        {
            // Sidebar Panel
            GameObject sidebar = CreatePanelSafe("Sidebar", parent, HorizonGUIFactory.ColorSidebar, bgSprite);
            HorizonGUIFactory.SetLayoutSize(sidebar, minW: 80, prefW: 80, flexW: 0);

            GameObject navContainer = CreateBlockSafe("NavContainer", sidebar);
            HorizonGUIFactory.Stretch(navContainer);

            HorizonGUIFactory.CreateVerticalGroup(navContainer, 10, new RectOffset(0, 0, 0, 0), true, true, TextAnchor.UpperCenter);

            return navContainer;
        }

        private static GameObject BuildContentArea(GameObject parent)
        {
            return HorizonGUIFactory.CreateColumn("ContentArea", parent, spacing: 0, padding: 0, flexGrow: 1);
        }

        private static TextMeshProUGUI BuildHeader(GameObject parent)
        {
            GameObject header = HorizonGUIFactory.CreateRow("Header", parent, padding: 20, align: TextAnchor.MiddleRight);
            HorizonGUIFactory.SetLayoutSize(header, minH: 80, flexH: 0);

            // Clock
            GameObject clockObj = CreateBlockSafe("Clock", header);
            HorizonGUIFactory.SetLayoutSize(clockObj, minW: 880, minH: 60);
            var clockTxt = HorizonGUIFactory.CreateText(clockObj, "12:00", HorizonGUIFactory.TextStyle.Clock, TextAlignmentOptions.Right);

            // Separator Container
            GameObject headerSepContainer = CreateBlockSafe("HeaderSepContainer", parent);
            HorizonGUIFactory.SetLayoutSize(headerSepContainer, minH: 20, flexH: 0);
            var vlg = HorizonGUIFactory.CreateVerticalGroup(headerSepContainer, 0, new RectOffset(20, 20, 9, 9));
            vlg.childForceExpandHeight = true;
            CreatePanelSafe("Line", headerSepContainer, new Color(1, 1, 1, 0.3f), null);

            return clockTxt;
        }

        private static GameObject CreateBlockSafe(string name, GameObject parent) => HorizonGUIFactory.CreateBlock(name, parent);
        private static GameObject CreatePanelSafe(string name, GameObject parent, Color color, Sprite sprite) => HorizonGUIFactory.CreatePanel(name, parent, color, sprite);
    }
}