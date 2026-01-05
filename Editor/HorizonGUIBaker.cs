using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.Events;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor.SceneManagement;
using VRC.Udon;
using BlackHorizon.HorizonGUI;

namespace BlackHorizon.HorizonGUI.Editor
{
    public static class HorizonGUIBaker
    {
        public static void BakeInterface(string rootName)
        {
            Debug.Log($"<b><color=#33FF33>[LOG]</color></b> <color=white>[HorizonGUI] Starting BAKE for '{rootName}'...</color>");

            GameObject root = GameObject.Find(rootName);
            if (root == null)
            {
                var mgr = Object.FindObjectOfType<HorizonGUIManager>();
                if (mgr != null) root = mgr.gameObject;
            }

            if (root == null)
            {
                Debug.LogError($"<b><color=#FF3333>[ERROR]</color></b> <color=white>[HorizonGUI] Bake failed. Root '{rootName}' not found.</color>");
                return;
            }

            var manager = root.GetComponent<HorizonGUIManager>();
            var allButtons = root.GetComponentsInChildren<HorizonGUINavigationButton>(true);

            bool dirty = false;

            foreach (var script in allButtons)
            {
                if (ProcessButton(script)) dirty = true;
            }

            if (manager != null)
            {
                SerializedObject so = new SerializedObject(manager);
                var btnsProp = so.FindProperty("navigationButtons");

                bool listChanged = btnsProp.arraySize != allButtons.Length;
                if (!listChanged)
                {
                    for (int i = 0; i < allButtons.Length; i++)
                    {
                        if (btnsProp.GetArrayElementAtIndex(i).objectReferenceValue != allButtons[i])
                        {
                            listChanged = true;
                            break;
                        }
                    }
                }

                if (listChanged)
                {
                    btnsProp.ClearArray();
                    btnsProp.arraySize = allButtons.Length;
                    for (int i = 0; i < allButtons.Length; i++) btnsProp.GetArrayElementAtIndex(i).objectReferenceValue = allButtons[i];
                    so.ApplyModifiedProperties();
                    dirty = true;
                }
            }

            if (dirty)
            {
                EditorUtility.SetDirty(root);
                if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("<b><color=#33FF33>[LOG]</color></b> <color=white>[HorizonGUI] Interface BAKED for VRChat successfully.</color>");
            }
            else
            {
                Debug.Log("<b><color=#33FF33>[LOG]</color></b> <color=white>[HorizonGUI] Bake finished. System is up to date.</color>");
            }
        }

        private static bool ProcessButton(HorizonGUINavigationButton script)
        {
            try
            {
                GameObject btnObj = script.gameObject;
                Button btn = btnObj.GetComponent<Button>();
                bool madeChanges = false;

                if (btn != null)
                {
                    var nav = btn.navigation;
                    if (nav.mode != Navigation.Mode.None)
                    {
                        nav.mode = Navigation.Mode.None;
                        btn.navigation = nav;
                        madeChanges = true;
                    }

                    int eventCount = btn.onClick.GetPersistentEventCount();
                    for (int i = eventCount - 1; i >= 0; i--)
                    {
                        UnityEventTools.RemovePersistentListener(btn.onClick, i);
                        madeChanges = true;
                    }

                    VRC.Udon.UdonBehaviour udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(script);
                    if (udon == null) udon = btnObj.GetComponent<VRC.Udon.UdonBehaviour>();

                    if (udon != null)
                    {
                        UnityEventTools.AddStringPersistentListener(
                            btn.onClick,
                            udon.SendCustomEvent,
                            "OnClick"
                        );
                        madeChanges = true;
                    }
                    else
                    {
                        Debug.LogError($"<b><color=#FF3333>[ERROR]</color></b> <color=white>[HorizonGUI] Critical: No UdonBehaviour for {btnObj.name}</color>");
                    }
                }

                if (madeChanges) EditorUtility.SetDirty(btnObj);
                return madeChanges;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"<b><color=#FF3333>[ERROR]</color></b> <color=white>[HorizonGUI] Exception on {script.gameObject.name}: {ex.Message}</color>");
                return false;
            }
        }
    }
}