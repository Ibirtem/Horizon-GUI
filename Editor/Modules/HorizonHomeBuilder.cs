using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

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

            // 2. GRID GENERATION
            int poolSize = 64;
            Vector2 cellSize = new Vector2(64, 64);
            var gridManager = HorizonGUIFactory.CreateDataGrid("PlayerGrid", page, poolSize, cellSize);

            Sprite circle = HorizonGUIFactory.GetOrGenerateRoundedSprite();

            foreach (var item in gridManager.slotPool)
            {
                if (item == null) continue;

                if (item.backgroundImage != null)
                {
                    item.backgroundImage.sprite = circle;
                    item.backgroundImage.type = Image.Type.Sliced;
                    item.backgroundImage.color = new Color(1, 1, 1, 0.1f);
                }

                if (item.iconImage != null)
                {
                    HorizonGUIFactory.Stretch(item.iconImage.gameObject, 0);
                    item.iconImage.sprite = circle;
                    item.iconImage.type = Image.Type.Sliced;
                    item.iconImage.color = new Color(1, 1, 1, 0.6f);
                }

                Transform hoverTrans = item.transform.Find("Interaction_Overlay");
                if (hoverTrans != null)
                {
                    Image hoverImg = hoverTrans.GetComponent<Image>();
                    hoverImg.sprite = circle;
                    hoverImg.type = Image.Type.Sliced;
                    hoverImg.color = Color.white;
                }

                if (item.titleText != null) item.titleText.gameObject.SetActive(false);
            }

            // ================================================================
            // LOGIC (SMART API)
            // ================================================================

            return HorizonGUIFactory.ConfigureLogic<HorizonGUI_HomeModule>(page, logic =>
            {
                logic.Bind("instanceInfoText", tInfo);
                logic.Bind("playerGrid", gridManager);

                HorizonGUIFactory.ConfigureLogic<HorizonDataGrid>(gridManager.gameObject, gridBinder =>
                {
                    gridBinder.Bind("targetCallback", logic.TargetScript);
                    gridBinder.BindVal("callbackEventName", "OnPlayerSlotClicked");
                    gridBinder.BindVal("targetVariableInt", "_lastEventInt");
                });
            });
        }
    }
}