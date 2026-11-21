using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class GameHud
    {
        [SerializeField] private GameObject developmentLauncher, developmentOverlay;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || LOTBA_DEVELOPMENT_TOOLS
        [SerializeField] private DevelopmentToolsPanel developmentTools;
        public bool DevelopmentOpen => developmentTools != null && developmentTools.IsOpen;
        public void CloseDevelopment() => developmentTools?.Close();
        private void Start()
        {
            if (developmentTools == null) return;
            developmentTools.Initialize(FindFirstObjectByType<SessionReportTransport>());
        }
#else
        public bool DevelopmentOpen => false;
        public void CloseDevelopment() { }
        private void Start()
        {
            if (developmentLauncher != null) developmentLauncher.SetActive(false);
            if (developmentOverlay != null) developmentOverlay.SetActive(false);
        }
#endif
    }
}
