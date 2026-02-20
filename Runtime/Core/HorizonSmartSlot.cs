using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UdonSharp;

namespace BlackHorizon.HorizonGUI
{
    [AddComponentMenu("Horizon/Horizon Smart Slot")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HorizonSmartSlot : UdonSharpBehaviour
    {
        [Header("System")]
        public HorizonDataGrid gridManager;
        public int slotIndex = -1;
        public int currentDataId = -1;

        [Header("Bindings (Baked)")]
        [Tooltip("Keys for Text components (e.g. 'PlayerName')")]
        public string[] textKeys;
        [Tooltip("Target Text components")]
        public TextMeshProUGUI[] textTargets;

        [Tooltip("Keys for Image components (e.g. 'Avatar')")]
        public string[] imageKeys;
        [Tooltip("Target Image components")]
        public Image[] imageTargets;

        [Tooltip("Keys for RawImage components (e.g. 'LiveFeed')")]
        public string[] rawKeys;
        [Tooltip("Target RawImage components")]
        public RawImage[] rawTargets;

        [Tooltip("The button component to trigger clicks")]
        public Button mainButton;

        /// <summary>
        /// Updates a TextMeshPro component bound to the specific key via the HTML 'u-bind' attribute.
        /// </summary>
        public void SetText(string key, string value)
        {
            if (textKeys == null || textTargets == null) return;

            int count = Mathf.Min(textKeys.Length, textTargets.Length);

            for (int i = 0; i < count; i++)
            {
                if (textKeys[i] == key)
                {
                    if (textTargets[i] != null) textTargets[i].text = value;
                }
            }
        }

        /// <summary>
        /// Updates an Image component (Sprite) bound to the specific key via the HTML 'u-bind' attribute.
        /// Automatically manages the GameObject's active state based on whether the sprite is null.
        /// </summary>
        public void SetImage(string key, Sprite value)
        {
            if (imageKeys == null || imageTargets == null) return;

            int count = Mathf.Min(imageKeys.Length, imageTargets.Length);

            for (int i = 0; i < count; i++)
            {
                if (imageKeys[i] == key)
                {
                    if (imageTargets[i] != null)
                    {
                        imageTargets[i].sprite = value;
                        imageTargets[i].gameObject.SetActive(value != null);
                    }
                }
            }
        }

        /// <summary>
        /// Updates a RawImage component (RenderTexture/Texture2D) bound to the specific key via HTML.
        /// Used primarily for dynamic elements like Live Avatars or Video Streams.
        /// Automatically manages the GameObject's active state.
        /// </summary>
        /// <param name="key">The binding key (e.g. 'AvatarRaw').</param>
        /// <param name="value">The raw texture to assign.</param>
        public void SetRawTexture(string key, Texture value)
        {
            if (rawKeys == null || rawTargets == null) return;

            int count = Mathf.Min(rawKeys.Length, rawTargets.Length);

            for (int i = 0; i < count; i++)
            {
                if (rawKeys[i] == key)
                {
                    if (rawTargets[i] != null)
                    {
                        rawTargets[i].texture = value;
                        rawTargets[i].gameObject.SetActive(value != null);
                    }
                }
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