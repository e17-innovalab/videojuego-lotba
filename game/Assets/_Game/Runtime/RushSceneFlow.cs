using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lotba.CityRush
{
    public enum RushLocation { Obelisco, LaBoca, LaBocaToObelisco }

    /// <summary>Shared scene names and the only cross-scene handoff: begin the selected run on arrival.</summary>
    public static class RushSceneFlow
    {
        public const string EntryPath = "Assets/_Game/Scenes/CityRush.unity";
        public const string ObeliscoPath = "Assets/_Game/Maps/Obelisco/Scenes/Obelisco.unity";
        public const string LaBocaPath = "Assets/_Game/Maps/LaBoca/Scenes/LaBoca.unity";
        public const string FlowPath = "Assets/_Game/Maps/Routes/Scenes/LaBocaToObelisco.unity";
        private const string PreferenceKey = "Lotba.CityRush.Location";
        private static bool startOnArrival;
        public static RushLocation Selected
        {
            get => (RushLocation)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(PreferenceKey, (int)value); PlayerPrefs.Save(); }
        }
        public static string PathFor(RushLocation location) => location == RushLocation.LaBocaToObelisco ? FlowPath : location == RushLocation.LaBoca ? LaBocaPath : ObeliscoPath;
        public static string NameFor(RushLocation location) => location == RushLocation.LaBocaToObelisco ? "LA BOCA → OBELISCO" : location == RushLocation.LaBoca ? "LA BOCA" : "OBELISCO";
        public static bool IsSelectedScene => SceneManager.GetActiveScene().path == PathFor(Selected);
        public static bool LoadSelected()
        {
            string path = PathFor(Selected);
            if (!Application.CanStreamedLevelBeLoaded(path)) return false;
            startOnArrival = true;
            SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            return true;
        }
        public static bool ConsumeStart()
        {
            bool start = startOnArrival; startOnArrival = false; return start;
        }
    }
}
