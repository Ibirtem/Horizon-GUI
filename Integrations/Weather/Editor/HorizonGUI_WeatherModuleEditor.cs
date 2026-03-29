using UnityEngine;
using UnityEditor;
using BlackHorizon.HorizonGUI.Editor;

#if HORIZON_WEATHER_INTEGRATION
using BlackHorizon.HorizonWeatherTime;
#endif

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
#if HORIZON_WEATHER_INTEGRATION
            bool isDirty = false;

            // 1. Auto-Link System
            if (module.weatherSystemObj == null)
            {
                var found = Object.FindObjectOfType<WeatherTimeSystem>(true);
                if (found != null)
                {
                    Undo.RecordObject(module, "Auto-Link Weather");
                    module.weatherSystemObj = found.gameObject;
                    isDirty = true;
                }
            }

            // 2. Bake Version into Cached Variable
            if (module.weatherSystemObj != null)
            {
                WeatherTimeSystem wts = module.weatherSystemObj.GetComponent<WeatherTimeSystem>();
                if (wts != null)
                {
                    string version = HorizonEditorUtils.GetVersion(wts);
                    string versionStr = $"v{version}";

                    if (module.cachedVersion != versionStr)
                    {
                        Undo.RecordObject(module, "Bake Version Cache");
                        module.cachedVersion = versionStr;
                        isDirty = true;
                    }
                }
            }

            if (isDirty) EditorUtility.SetDirty(module);
#else
            EditorGUILayout.HelpBox("WeatherTimeSystem package missing. Module functionality is disabled.", MessageType.Info);
#endif
        }
    }
}