using UnityEngine;
using UnityEditor;
using BlackHorizon.HorizonGUI.Editor;
using UnityEngine.Rendering.PostProcessing;
using System.IO;

namespace BlackHorizon.HorizonGUI.Integrations.PostProcessing.Editor
{
    /// <summary>
    /// Custom inspector for the Post Process Module.
    /// Provides automated environment setup, layer configuration, and template copying
    /// to ensure safe, non-destructive user modification of Post Processing profiles.
    /// </summary>
    [CustomEditor(typeof(HorizonGUI_PostProcessModule))]
    public class HorizonGUI_PostProcessModuleEditor : UnityEditor.Editor
    {
        private const string USER_PROFILES_DIR = "Assets/Horizon GUI/Horizon-Dashboard/Profiles";

        public override void OnInspectorGUI()
        {
            HorizonGUI_PostProcessModule module = (HorizonGUI_PostProcessModule)target;
            HorizonEditorUtils.DrawHorizonHeader("POST PROCESS MODULE", module);

            EditorGUILayout.HelpBox("Automatically copies default PP profiles to your Assets folder, assigns 'Water' layers, and links them to the scene.", MessageType.Info);

            GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);

            if (GUILayout.Button("Auto-Setup Environment", GUILayout.Height(30)))
            {
                PerformAutoSetup(module);
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            base.OnInspectorGUI();
        }

        /// <summary>
        /// Orchestrates the automated setup of the Post Processing environment.
        /// Configures layer masks, initializes volume containers, and links profile templates.
        /// </summary>
        private void PerformAutoSetup(HorizonGUI_PostProcessModule module)
        {
            int waterLayer = LayerMask.NameToLayer("Water");

            if (waterLayer == -1)
            {
                Debug.LogWarning("<color=yellow>[Horizon PP]</color> 'Water' layer not found. Please ensure the 'Water' layer exists in Project Settings > Tags and Layers.");
                return;
            }

            module.gameObject.layer = waterLayer;

            if (Camera.main != null)
            {
                PostProcessLayer layer = Camera.main.GetComponent<PostProcessLayer>();

                if (layer != null)
                {
                    if ((layer.volumeLayer.value & (1 << waterLayer)) == 0)
                    {
                        Undo.RecordObject(layer, "Add Water Layer to PostProcess");
                        layer.volumeLayer.value |= (1 << waterLayer);
                        Debug.Log("<color=cyan>[Horizon PP]</color> Added 'Water' layer to Main Camera's PostProcessLayer.");
                    }
                }
            }

            Transform container = module.transform.Find("PP_Overrides");

            if (container == null)
            {
                GameObject containerObj = new GameObject("PP_Overrides");
                containerObj.transform.SetParent(module.transform);
                container = containerObj.transform;
                containerObj.layer = waterLayer;

                Undo.RegisterCreatedObjectUndo(containerObj, "Create PP Container");
            }

            module.VolumeObj_Bloom = EnsureVolumeObject(container, "Override_Bloom", "HorizonTemplate_Bloom", waterLayer);
            module.VolumeObj_AO = EnsureVolumeObject(container, "Override_AO", "HorizonTemplate_AO", waterLayer);
            module.VolumeObj_CA = EnsureVolumeObject(container, "Override_CA", "HorizonTemplate_CA", waterLayer);
            module.VolumeObj_Grain = EnsureVolumeObject(container, "Override_Grain", "HorizonTemplate_Grain", waterLayer);

            EditorUtility.SetDirty(module);
            Debug.Log("<color=#33FF33>[Horizon PP]</color> Setup complete! Profiles copied and linked securely.");
        }

        /// <summary>
        /// Retrieves or creates a child GameObject equipped with a PostProcessVolume component.
        /// Automatically assigns the requested profile template if one is not already assigned.
        /// </summary>
        private GameObject EnsureVolumeObject(Transform parent, string objName, string templateName, int targetLayer)
        {
            Transform existingTransform = parent.Find(objName);
            GameObject volumeObj;

            if (existingTransform != null)
            {
                volumeObj = existingTransform.gameObject;
            }
            else
            {
                volumeObj = new GameObject(objName);
                volumeObj.transform.SetParent(parent);
                Undo.RegisterCreatedObjectUndo(volumeObj, $"Create {objName}");
            }

            volumeObj.layer = targetLayer;

            PostProcessVolume volume = volumeObj.GetComponent<PostProcessVolume>();

            if (volume == null)
            {
                volume = volumeObj.AddComponent<PostProcessVolume>();
                volume.isGlobal = true;
                volume.priority = 50;
            }

            if (volume.sharedProfile == null)
            {
                PostProcessProfile profile = GetOrCopyProfile(templateName, objName);

                if (profile != null)
                {
                    Undo.RecordObject(volume, "Assign Profile");
                    volume.sharedProfile = profile;
                }
            }

            return volumeObj;
        }

        /// <summary>
        /// Locates a specific template profile within the project, copies it into the user's
        /// persistent profile directory to prevent overwriting originals, and returns the copy.
        /// </summary>
        private PostProcessProfile GetOrCopyProfile(string templateName, string destinationFileName)
        {
            if (!Directory.Exists(USER_PROFILES_DIR))
            {
                Directory.CreateDirectory(USER_PROFILES_DIR);
                AssetDatabase.Refresh();
            }

            string destinationPath = $"{USER_PROFILES_DIR}/{destinationFileName}.asset";
            PostProcessProfile existingProfile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(destinationPath);

            if (existingProfile != null)
            {
                return existingProfile;
            }

            string[] guids = AssetDatabase.FindAssets($"{templateName} t:PostProcessProfile");

            if (guids.Length == 0)
            {
                Debug.LogWarning($"<color=yellow>[Horizon PP]</color> Template profile '{templateName}' not found. Make sure it is included in the package.");
                return null;
            }

            string sourcePath = AssetDatabase.GUIDToAssetPath(guids[0]);

            if (AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                AssetDatabase.Refresh();
                Debug.Log($"<color=cyan>[Horizon PP]</color> Copied template '{templateName}' to '{destinationPath}'.");

                return AssetDatabase.LoadAssetAtPath<PostProcessProfile>(destinationPath);
            }

            Debug.LogWarning($"<color=yellow>[Horizon PP]</color> Failed to copy template '{templateName}' to '{destinationPath}'.");
            return null;
        }
    }
}