using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.IO;

namespace BlackHorizon.HorizonGUI.Editor
{
    public class HorizonAboutBuilder : IHorizonModuleBuilder
    {
        public string ModuleName => "About";
        public string IconName => "information_source.png";

        [System.Serializable]
        private class PackageInfo
        {
            public string version;
            public string displayName;
        }

        public HorizonGUIModule BuildPage(GameObject container)
        {
            string version = "?.?.?";
            string pkgName = "Horizon GUI";
            try
            {
                string path = "Packages/com.blackhorizon.horizongui/package.json";
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var pkg = JsonUtility.FromJson<PackageInfo>(json);
                    version = pkg.version;
                    pkgName = pkg.displayName;
                }
            }
            catch { }

            GameObject page = HorizonGUIFactory.CreateColumn($"Module_{ModuleName}", container, spacing: 10, padding: 30, align: TextAnchor.UpperCenter);

            var tHeader = HorizonGUIFactory.CreateText(page, pkgName, 42, Color.white, TextAlignmentOptions.Center);
            tHeader.fontStyle = FontStyles.Bold;
            HorizonGUIFactory.SetLayoutSize(tHeader.gameObject, minH: 50);

            var tVer = HorizonGUIFactory.CreateText(page, $"Version: {version}", 24, HorizonGUIFactory.ColorTextDim, TextAlignmentOptions.Center);
            HorizonGUIFactory.SetLayoutSize(tVer.gameObject, minH: 30);

            var tAuth = HorizonGUIFactory.CreateText(page, "Author: Ibirtem", 24, HorizonGUIFactory.ColorTextDim, TextAlignmentOptions.Center);
            HorizonGUIFactory.SetLayoutSize(tAuth.gameObject, minH: 30);

            // Spacer
            GameObject spacer = HorizonGUIFactory.CreateBlock("Spacer", page);
            HorizonGUIFactory.SetLayoutSize(spacer, flexH: 1);

            GameObject sep = HorizonGUIFactory.CreatePanel("Sep", page, new Color(1, 1, 1, 0.2f), null);
            HorizonGUIFactory.SetLayoutSize(sep, minH: 2, prefH: 2);
            sep.GetComponent<LayoutElement>().preferredWidth = 400;

            GameObject linksContainer = HorizonGUIFactory.CreateColumn("Links", page, spacing: 10, padding: 10, align: TextAnchor.UpperCenter);
            HorizonGUIFactory.SetLayoutSize(linksContainer, flexH: 0);

            TMP_InputField githubInput = CreateLinkRow(linksContainer, "GitHub", "icon_github.png");

            TMP_InputField boostyInput = CreateLinkRow(linksContainer, "Boosty", "icon_boosty.png");

            // ================================================================
            // LOGIC
            // ================================================================
            return HorizonGUIFactory.ConfigureLogic<HorizonGUI_AboutModule>(page, logic =>
            {
                // GitHub
                logic.Bind("githubField", githubInput);
                logic.BindVal("githubUrl", "https://github.com/Ibirtem/WeatherSystem");

                // Boosty
                logic.Bind("linkField", boostyInput);
                logic.BindVal("boostyUrl", "https://boosty.to/ibirtem");
            });
        }

        private TMP_InputField CreateLinkRow(GameObject parent, string name, string iconFileName)
        {
            GameObject row = HorizonGUIFactory.CreateRow($"Row_{name}", parent, spacing: 15, align: TextAnchor.MiddleCenter);
            HorizonGUIFactory.SetLayoutSize(row, minH: 40, prefH: 40);

            GameObject iconObj = HorizonGUIFactory.CreateBlock("Icon", row);
            HorizonGUIFactory.SetLayoutSize(iconObj, minW: 32, minH: 32, prefW: 32, prefH: 32);

            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = HorizonGUIFactory.LoadPackageSprite(iconFileName);
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;

            GameObject inputBg = HorizonGUIFactory.CreatePanel("InputBg", row, new Color(1, 1, 1, 0.05f), HorizonGUIFactory.GetOrGenerateRoundedSprite());
            HorizonGUIFactory.SetLayoutSize(inputBg, flexW: 1, minH: 36);

            Image bgImg = inputBg.GetComponent<Image>();
            bgImg.pixelsPerUnitMultiplier = 3.0f;

            GameObject textViewport = HorizonGUIFactory.CreateBlock("TextArea", inputBg);
            RectTransform vpRect = textViewport.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = new Vector2(12, 0);
            vpRect.offsetMax = new Vector2(-12, 0);

            RectMask2D mask = textViewport.AddComponent<RectMask2D>();
            mask.padding = new Vector4(0, 0, 0, 0);

            var tInput = HorizonGUIFactory.CreateText(textViewport, "", 18, new Color(1, 1, 1, 0.8f), TextAlignmentOptions.Left);
            tInput.verticalAlignment = VerticalAlignmentOptions.Middle;
            HorizonGUIFactory.Stretch(tInput.gameObject);

            // InputField
            TMP_InputField inp = inputBg.AddComponent<TMP_InputField>();
            inp.textViewport = textViewport.GetComponent<RectTransform>();
            inp.textComponent = tInput;
            inp.readOnly = false;
            inp.selectionColor = new Color(0.2f, 0.6f, 1.0f, 0.5f);

            inp.targetGraphic = bgImg;
            inp.transition = Selectable.Transition.ColorTint;
            var cols = inp.colors;
            cols.highlightedColor = new Color(1, 1, 1, 0.1f);
            cols.selectedColor = new Color(1, 1, 1, 0.1f);
            inp.colors = cols;

            return inp;
        }
    }
}