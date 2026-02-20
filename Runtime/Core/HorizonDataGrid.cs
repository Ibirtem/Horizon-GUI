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
    [AddComponentMenu("Horizon/Horizon Data Grid")]
#if UDONSHARP
    public class HorizonDataGrid : UdonSharpBehaviour
#else
    public class HorizonDataGrid : MonoBehaviour
#endif
    {
        [Header("Pool Configuration")]
        [Tooltip("The fixed array of reuseable UI slots.")]
        public HorizonSmartSlot[] slotPool;

        [Tooltip("How many items to skip per page.")]
        public int itemsPerPage = 16;

        [Header("Event Linking")]
        [Tooltip("The behaviour that receives the click event.")]
        public UdonBehaviour targetCallback;
        [Tooltip("The custom event name to call on target.")]
        public string callbackEventName = "OnGridItemSelected";
        [Tooltip("Variable name in target to store the Item ID.")]
        public string targetVariableInt = "_lastEventInt";

        [Header("Callbacks")]
        [Tooltip("Optional: External behaviour to notify when the view refreshes (pagination/load).")]
        public UdonBehaviour viewUpdateListener;
        [Tooltip("Event name to call on the listener.")]
        public string onViewUpdateEvent = "OnGridViewUpdated";

        private const string DEFAULT_TEXT_KEY = "MainText";
        private const string DEFAULT_ICON_KEY = "MainIcon";

        [Header("Navigation (Optional)")]
        public TextMeshProUGUI pageIndicator;
        public Button prevButton;
        public Button nextButton;

        // --- Data Storage ---
        private int[] _dataIds;
        private string[] _dataNames;
        private Sprite[] _dataIcons;
        private int _totalItems = 0;
        private int _currentPage = 0;

        private void Start()
        {
            RefreshView();
        }

        /// <summary>
        /// Loads new data into the grid and resets to page 0.
        /// </summary>
        /// <param name="ids">Unique IDs for logic (e.g. Item ID).</param>
        /// <param name="names">Display names.</param>
        /// <param name="icons">Optional icons (can be null).</param>
        public void LoadData(int[] ids, string[] names, Sprite[] icons)
        {
            _dataIds = ids;
            _dataNames = names;
            _dataIcons = icons;

            if (_dataIds != null) _totalItems = _dataIds.Length;
            else _totalItems = 0;

            _currentPage = 0;
            RefreshView();
        }

        /// <summary>
        /// Refreshes the visual state of the slots based on current data and page.
        /// </summary>
        public void RefreshView()
        {
            if (slotPool == null) return;

            int startIndex = _currentPage * itemsPerPage;
            int poolSize = slotPool.Length;

            for (int i = 0; i < poolSize; i++)
            {
                HorizonSmartSlot item = slotPool[i];
                if (item == null) continue;

                int dataIndex = startIndex + i;

                if (dataIndex < _totalItems)
                {
                    item.gameObject.SetActive(true);
                    item.currentDataId = _dataIds[dataIndex];

                    if (_dataNames != null && dataIndex < _dataNames.Length)
                        item.SetText(DEFAULT_TEXT_KEY, _dataNames[dataIndex]);

                    Sprite icon = null;
                    if (_dataIcons != null && dataIndex < _dataIcons.Length)
                        icon = _dataIcons[dataIndex];

                    item.SetImage(DEFAULT_ICON_KEY, icon);
                }
                else
                {
                    item.gameObject.SetActive(false);
                }
            }

            UpdatePaginationControls();

            if (viewUpdateListener != null && !string.IsNullOrEmpty(onViewUpdateEvent))
            {
                viewUpdateListener.SendCustomEvent(onViewUpdateEvent);
            }
        }

        public void OnItemClicked(int slotIndex, int dataId)
        {
            Debug.Log($"[HorizonDataGrid] Clicked Item ID: {dataId} (Slot {slotIndex})");

            if (targetCallback != null)
            {
                targetCallback.SetProgramVariable(targetVariableInt, dataId);
                targetCallback.SendCustomEvent(callbackEventName);
            }
        }

        // --- Pagination Logic ---

        public void NextPage()
        {
            if ((_currentPage + 1) * itemsPerPage < _totalItems)
            {
                _currentPage++;
                RefreshView();
            }
        }

        public void PrevPage()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RefreshView();
            }
        }

        private void UpdatePaginationControls()
        {
            if (pageIndicator != null)
            {
                int totalPages = Mathf.CeilToInt((float)_totalItems / itemsPerPage);
                if (totalPages == 0) totalPages = 1;
                pageIndicator.text = $"{_currentPage + 1} / {totalPages}";
            }

            if (prevButton != null) prevButton.interactable = (_currentPage > 0);
            if (nextButton != null) nextButton.interactable = ((_currentPage + 1) * itemsPerPage < _totalItems);
        }
    }
}