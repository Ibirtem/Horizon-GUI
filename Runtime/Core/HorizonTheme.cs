using UnityEngine;

namespace BlackHorizon.HorizonGUI
{
    [CreateAssetMenu(fileName = "HorizonTheme_Default", menuName = "Horizon/UI Theme")]
    public class HorizonTheme : ScriptableObject
    {
        [Header("Colors")]
        public Color primaryColor = new Color(1f, 1f, 1f, 0.9f);
        public Color secondaryColor = new Color(1f, 1f, 1f, 0.5f);
        public Color accentColor = new Color(0.2f, 0.6f, 1.0f, 1.0f);

        [Header("Backgrounds")]
        public Color glassColor = new Color(1f, 1f, 1f, 0.1f);
        public Color panelColor = new Color(0f, 0f, 0f, 0.2f);

        [Header("Typography Sizes")]
        public float sizeClock = 54f;
        public float sizeH1 = 48f;
        public float sizeH2 = 42f;
        public float sizeBody = 24f;
        public float sizeSmall = 18f;
    }
}