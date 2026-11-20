using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class GameHud
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || LOTBA_DEVELOPMENT_TOOLS
        private DevelopmentToolsPanel developmentTools;
        public bool DevelopmentOpen => developmentTools != null && developmentTools.IsOpen;
        public void CloseDevelopment() => developmentTools?.Close();
        private void Start()
        {
            developmentTools = gameObject.AddComponent<DevelopmentToolsPanel>();
            var icon = configurationButtons[0].transform.Find("Ícono")?.GetComponent<UnityEngine.UI.Image>();
            developmentTools.Initialize(readyPanel.transform, start.GetComponentInChildren<TMPro.TMP_Text>(true).font,
                FindFirstObjectByType<SessionReportTransport>(), icon != null ? icon.sprite : null);
        }
#else
        public bool DevelopmentOpen => false;
        public void CloseDevelopment() { }
#endif
    }
}
