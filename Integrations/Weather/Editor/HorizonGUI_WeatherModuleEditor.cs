using UnityEngine;
using UnityEditor;
using BlackHorizon.HorizonGUI.Editor;
using BlackHorizon.HorizonWeatherTime;

namespace BlackHorizon.HorizonGUI.Integrations.Weather.Editor
{
    [CustomEditor(typeof(HorizonGUI_WeatherModule))]
    public class HorizonGUI_WeatherModuleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            HorizonGUI_WeatherModule module = (HorizonGUI_WeatherModule)target;
            HorizonEditorUtils.DrawHorizonHeader("WEATHER MODULE", module);

            PerformAutoSetup(module);

            base.OnInspectorGUI();
        }

        /// <summary>
        /// Automatically links dependencies and bakes system version info into UI labels.
        /// </summary>
        private void PerformAutoSetup(HorizonGUI_WeatherModule module)
        {
            bool isDirty = false;

            // 1. Auto-Link System
            if (module.weatherSystem == null)
            {
                var found = Object.FindObjectOfType<WeatherTimeSystem>(true);
                if (found != null)
                {
                    Undo.RecordObject(module, "Auto-Link Weather");
                    module.weatherSystem = found;
                    isDirty = true;
                }
            }

            // 2. Bake Version into Cached Variable
            if (module.weatherSystem != null)
            {
                string version = HorizonEditorUtils.GetVersion(module.weatherSystem);
                string versionStr = $"v{version}";

                if (module.cachedVersion != versionStr)
                {
                    Undo.RecordObject(module, "Bake Version Cache");
                    module.cachedVersion = versionStr;
                    isDirty = true;
                }
            }

            if (isDirty)
            {
                EditorUtility.SetDirty(module);
                module.UpdateStatusVisuals();
            }

            if (module.Weather_VersionText != null && module.Weather_VersionText.text != module.cachedVersion)
            {
                Undo.RecordObject(module.Weather_VersionText, "Update Version Text");
                module.Weather_VersionText.text = module.cachedVersion;
            }
        }
    }
}