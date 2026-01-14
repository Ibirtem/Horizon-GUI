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

        private void PerformAutoSetup(HorizonGUI_WeatherModule module)
        {
            bool isDirty = false;

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

            if (module.weatherSystem != null && module.weatherVersionText != null)
            {
                string version = HorizonEditorUtils.GetVersion(module.weatherSystem);
                string versionStr = $"v{version}";
                if (module.weatherVersionText.text != versionStr)
                {
                    Undo.RecordObject(module.weatherVersionText, "Bake Version");
                    module.weatherVersionText.text = versionStr;
                    isDirty = true;
                }
            }

            if (isDirty)
            {
                EditorUtility.SetDirty(module);
                module.UpdateStatusVisuals();
            }
        }
    }
}