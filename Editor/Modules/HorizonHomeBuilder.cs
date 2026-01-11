using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace BlackHorizon.HorizonGUI.Editor
{
    public class HorizonHomeBuilder : IHorizonModuleBuilder
    {
        public string ModuleName => "Home";
        public string IconName => "house_with_garden.png";

        public HorizonGUIModule BuildPage(GameObject container)
        {
            // 1. LAYOUT & VISUALS 
            GameObject page = HorizonGUIFactory.CreateColumn($"Module_{ModuleName}", container, spacing: 20, padding: 20);

            var tInfo = HorizonGUIFactory.CreateText(page, "Loading...", HorizonGUIFactory.TextStyle.BodyDim, TMPro.TextAlignmentOptions.Left);
            HorizonGUIFactory.SetLayoutSize(tInfo.gameObject, minH: 30);

            GameObject sep = HorizonGUIFactory.CreatePanel("Sep", page, new Color(1, 1, 1, 0.2f), null);
            HorizonGUIFactory.SetLayoutSize(sep, minH: 2, prefH: 2);

            GameObject gridObj = HorizonGUIFactory.CreateGrid("PlayerGrid", page, new Vector2(64, 64), new Vector2(15, 15), flexGrow: 1);

            var slots = new System.Collections.Generic.List<GameObject>();
            Sprite circle = HorizonGUIFactory.GetOrGenerateRoundedSprite();
            for (int i = 0; i < 32; i++)
            {
                GameObject slot = HorizonGUIFactory.CreatePanel($"Slot_{i}", gridObj, new Color(1, 1, 1, 0.1f), circle);
                slot.AddComponent<Mask>().showMaskGraphic = true;
                GameObject inner = HorizonGUIFactory.CreatePanel("Visual", slot, new Color(1, 1, 1, 0.8f), circle);
                HorizonGUIFactory.Stretch(inner);
                slot.SetActive(false);
                slots.Add(slot);
            }

            // ================================================================
            // LOGIC (SMART API)
            // ================================================================

            return HorizonGUIFactory.ConfigureLogic<HorizonGUI_HomeModule>(page, logic =>
            {
                logic.Bind("instanceInfoText", tInfo);

                logic.BindArray("playerSlots", slots);
            });
        }
    }
}