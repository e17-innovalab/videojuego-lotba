using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lotba.Game
{
    public enum GameLocation { Obelisco, LaBoca, LaBocaToObelisco }

    /// <summary>Shared scene names and the only cross-scene handoff: begin the selected run on arrival.</summary>
    public static class GameSceneFlow
    {
        public const string EntryPath = "Assets/_LOTBA_Game/Scenes/Game.unity";
        public const string ObeliscoPath = "Assets/_LOTBA_Game/Maps/Obelisco/Scenes/Obelisco.unity";
        public const string LaBocaPath = "Assets/_LOTBA_Game/Maps/LaBoca/Scenes/LaBoca.unity";
        public const string FlowPath = "Assets/_LOTBA_Game/Maps/Routes/Scenes/LaBocaToObelisco.unity";
        private const string PreferenceKey = "Lotba.CityRush.Location";
        private static bool startOnArrival;
        public static GameLocation Selected
        {
            get => (GameLocation)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(PreferenceKey, (int)value); PlayerPrefs.Save(); }
        }
        public static string PathFor(GameLocation location) => location == GameLocation.LaBocaToObelisco ? FlowPath : location == GameLocation.LaBoca ? LaBocaPath : ObeliscoPath;
        public static string NameFor(GameLocation location) => location == GameLocation.LaBocaToObelisco ? "LA BOCA → OBELISCO" : location == GameLocation.LaBoca ? "LA BOCA" : "OBELISCO";
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
