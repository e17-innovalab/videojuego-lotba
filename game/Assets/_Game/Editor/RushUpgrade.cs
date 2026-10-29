using UnityEditor;
namespace Lotba.CityRush.Editor
{
    /// <summary>Updates generated defaults while preserving existing artist-authored map profiles.</summary>
    public static class RushUpgrade
    {
        [MenuItem("LOTBA/Update City Rush maps and UI")]
        public static void Apply() => RushMapSetup.Apply();
    }
}
