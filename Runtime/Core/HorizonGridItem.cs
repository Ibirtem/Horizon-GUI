using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
#endif

namespace BlackHorizon.HorizonGUI
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [AddComponentMenu("Horizon/Horizon Grid Item")]
#if UDONSHARP
    public class HorizonGridItem : UdonSharpBehaviour
#else
    public class HorizonGridItem : MonoBehaviour
#endif
    {
        [Header("References")]
        public HorizonDataGrid gridManager;
        public TextMeshProUGUI titleText;
        public Image iconImage;
        public Image backgroundImage;

        [Header("Runtime State (Debug)")]
        public int currentDataId = -1;
        public int slotIndex = -1;

        /// <summary>
        /// Updates the visual content of this slot.
        /// Called by HorizonDataGrid during pagination/refresh.
        /// </summary>
        public void UpdateView(int dataId, string text, Sprite icon)
        {
            currentDataId = dataId;

            if (titleText != null) titleText.text = text;

            if (iconImage != null)
            {
                if (icon != null)
                {
                    iconImage.sprite = icon;
                }

                iconImage.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Unity Event callback linked to the Button component.
        /// </summary>
        public void OnClick()
        {
            if (gridManager != null)
            {
                gridManager.OnItemClicked(slotIndex, currentDataId);
            }
        }
    }
}