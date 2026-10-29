using UnityEditor;
namespace Lotba.Game.Editor
{
    /// <summary>Updates generated defaults while preserving existing artist-authored map profiles.</summary>
    public static class GameAuthoringMenu
    {
        [MenuItem("LOTBA/Update maps and UI")]
        public static void Apply() => MapAssetSetup.Apply();
    }
}
