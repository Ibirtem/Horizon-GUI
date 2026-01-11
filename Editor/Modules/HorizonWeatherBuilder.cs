using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using TMPro;
using BlackHorizon.HorizonWeatherTime;
using UdonSharpEditor;
using System.IO;

namespace BlackHorizon.HorizonGUI.Editor
{
    public class HorizonWeatherBuilder : IHorizonModuleBuilder
    {
        public string ModuleName => "Weather";
        public string IconName => "partly_sunny_rain.png";

        [System.Serializable]
        private class PackageInfo
        {
            public string version;
        }

        public HorizonGUIModule BuildPage(GameObject container)
        {
            string version = "?.?.?";
            try
            {
                string path = "Packages/com.blackhorizon.horizonweathertime/package.json";
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var pkg = JsonUtility.FromJson<PackageInfo>(json);
                    version = pkg.version;
                }
            }
            catch { }

            // 1. LAYOUT
            GameObject page = HorizonGUIFactory.CreateScrollableColumn(
                $"Module_{ModuleName}",
                container,
                spacing: 20,
                padding: 20,
                flexGrow: 1,
                align: TextAnchor.UpperLeft,
                sbMarginTop: 20f,
                sbMarginBottom: 40f
            );

            GameObject scrollRoot = page.transform.parent.parent.gameObject;

            // 2. HEADER
            var tTitle = HorizonGUIFactory.CreateText(page, "Weather Control", HorizonGUIFactory.TextStyle.H1, TextAlignmentOptions.Left);
            HorizonGUIFactory.SetLayoutSize(tTitle.gameObject, minH: 60);

            // STATUS ROW
            GameObject statusRow = HorizonGUIFactory.CreateRow("StatusRow", page, spacing: 10, padding: 0);
            HorizonGUIFactory.SetLayoutSize(statusRow, minH: 30);

            var tStatus = HorizonGUIFactory.CreateText(statusRow, "Checking...", HorizonGUIFactory.TextStyle.BodyDim, TextAlignmentOptions.Left);
            HorizonGUIFactory.SetLayoutSize(tStatus.gameObject, flexW: 0);

            HorizonGUIFactory.CreateText(statusRow, "|", HorizonGUIFactory.TextStyle.BodyDim, TextAlignmentOptions.Left);
            var tVersion = HorizonGUIFactory.CreateText(statusRow, $"v{version}", HorizonGUIFactory.TextStyle.BodyDim, TextAlignmentOptions.Left);


            GameObject sep = HorizonGUIFactory.CreatePanel("Sep", page, new Color(1, 1, 1, 0.2f), null);
            HorizonGUIFactory.SetLayoutSize(sep, minH: 2);

            // =========================================================
            // 3. CONTROLS: TIME
            // =========================================================

            Toggle toggle = HorizonGUIFactory.CreateToggle(page, "Use Real-Time Sync", true);

            HorizonGUIFactory.CreateText(page, "Time of Day", HorizonGUIFactory.TextStyle.SmallDim, TextAlignmentOptions.Left);
            Slider slider = HorizonGUIFactory.CreateSlider(page, 0f, 1f, 0.25f);

            // =========================================================
            // 4. CONTROLS: WEATHER PROFILES
            // =========================================================

            GameObject sep2 = HorizonGUIFactory.CreatePanel("Sep2", page, new Color(1, 1, 1, 0.2f), null);
            HorizonGUIFactory.SetLayoutSize(sep2, minH: 2, prefH: 2);

            HorizonGUIFactory.CreateText(page, "Conditions", HorizonGUIFactory.TextStyle.SmallDim, TextAlignmentOptions.Left);

            GameObject profilesRow = HorizonGUIFactory.CreateRow("ProfilesRow", page, spacing: 15, padding: 0);
            HorizonGUIFactory.SetLayoutSize(profilesRow, minH: 80);

            WeatherTimeSystem foundSystem = Object.FindObjectOfType<WeatherTimeSystem>(true);
            Sprite bgSprite = HorizonGUIFactory.GetOrGenerateRoundedSprite();

            // 1. CLEAR (Index 0)
            CreateProfileButton(profilesRow, foundSystem, 0, "partly_sunny.png", bgSprite);

            // 2. SNOW (Index 1)
            CreateProfileButton(profilesRow, foundSystem, 1, "snow_cloud.png", bgSprite);

            // 3. RAIN (Index 2)
            CreateProfileButton(profilesRow, foundSystem, 2, "rain_cloud.png", bgSprite);

            GameObject bottomSpacer = HorizonGUIFactory.CreateBlock("BottomSpacer", page);
            HorizonGUIFactory.SetLayoutSize(bottomSpacer, minH: 40);

            // =========================================================
            // 5. BINDING MAIN MODULE
            // =========================================================
            var moduleScript = HorizonGUIFactory.ConfigureLogic<HorizonGUI_WeatherModule>(scrollRoot, binder =>
            {
                binder.Bind("controlsContainer", page);
                binder.Bind("targetSystem", foundSystem);
                binder.Bind("statusText", tStatus);
                binder.Bind("versionText", tVersion);
                binder.Bind("realTimeToggle", toggle);
                binder.Bind("timeSlider", slider);
            });

            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(moduleScript);

            if (udon != null)
            {
                UnityEventTools.AddStringPersistentListener(toggle.onValueChanged, udon.SendCustomEvent, "OnRealTimeChanged");
                UnityEventTools.AddStringPersistentListener(slider.onValueChanged, udon.SendCustomEvent, "OnTimeSliderChanged");
            }
            else
            {
                Debug.LogError("[HorizonGUI] Critical: Failed to get Backing Udon Behaviour for Event Linking.");
            }

            return moduleScript;
        }

        private void CreateProfileButton(GameObject parent, WeatherTimeSystem system, int index, string iconName, Sprite bgSprite)
        {
            Sprite icon = HorizonGUIFactory.LoadPackageSprite(iconName);
            if (icon == null) icon = HorizonGUIFactory.LoadPackageSprite("information_source.png");

            GameObject btnObj = HorizonGUIFactory.CreateIconButton($"Btn_Profile_{index}", parent, bgSprite, icon);

            HorizonGUIFactory.SetLayoutSize(btnObj, minW: 80, minH: 80, prefW: 80, prefH: 80);

            Transform iconTrans = btnObj.transform.Find("Icon");
            if (iconTrans != null)
            {
                RectTransform iconRect = iconTrans.GetComponent<RectTransform>();
                iconRect.offsetMin = new Vector2(15, 15);
                iconRect.offsetMax = new Vector2(-15, -15);
            }

            Button uiBtn = btnObj.GetComponent<Button>();
            if (uiBtn != null)
            {
                ColorBlock cb = uiBtn.colors;
                cb.normalColor = new Color(1, 1, 1, 0.05f);
                cb.highlightedColor = new Color(1, 1, 1, 0.2f);
                cb.pressedColor = new Color(1, 1, 1, 0.5f);
                cb.selectedColor = new Color(1, 1, 1, 0.05f);
                cb.fadeDuration = 0.1f;
                uiBtn.colors = cb;

                Navigation nav = new Navigation();
                nav.mode = Navigation.Mode.None;
                uiBtn.navigation = nav;
            }

            var btnScript = HorizonGUIFactory.ConfigureLogic<HorizonGUI_WeatherProfileButton>(btnObj, b =>
            {
                b.Bind("targetSystem", system);
                b.BindVal("profileIndex", index);
            });

            var btnUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(btnScript);
            if (btnUdon != null)
            {
                UnityEventTools.AddStringPersistentListener(
                    uiBtn.onClick,
                    btnUdon.SendCustomEvent,
                    "OnClick"
                );
            }
        }
    }
}