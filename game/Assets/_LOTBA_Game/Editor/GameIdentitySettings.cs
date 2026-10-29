using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Lotba.Game.Editor
{
    /// <summary>Keeps the application display name aligned with the runtime identity.</summary>
    public sealed class GameIdentitySettings : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        [InitializeOnLoadMethod]
        private static void ScheduleSync() => EditorApplication.delayCall += Synchronize;

        public void OnPreprocessBuild(BuildReport report) => Synchronize();

        private static void Synchronize()
        {
            if (PlayerSettings.productName != GameIdentity.Title)
                PlayerSettings.productName = GameIdentity.Title;
        }
    }
}
