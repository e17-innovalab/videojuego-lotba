using UnityEditor;
using UnityEditor.SceneManagement;

namespace Lotba.Game.Editor
{
    /// <summary>Navigation shortcuts only; opening a scene never generates content.</summary>
    public static class GameSceneMenu
    {
        [MenuItem("LOTBA/Open game")]
        public static void OpenGame() => Open("Assets/_LOTBA_Game/Scenes/Game.unity");

        [MenuItem("LOTBA/Open art preview")]
        public static void OpenArtPreview() => Open("Assets/_LOTBA_Game/Scenes/ArtPreview.unity");

        private static void Open(string path)
        {
            if (EditorApplication.isPlaying) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }
    }
}
