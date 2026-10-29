using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Lotba.CarreraEnBici.Editor
{
    /// <summary>Opens the imported reference without adding it to the game's build scenes.</summary>
    public static class LegacyPrototypeMenu
    {
        public const string ExamplePath = "Assets/_carreraEnBici/Scenes/SampleScene.unity";

        [MenuItem("LOTBA/Carrera en Bici/Open legacy example")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before opening the legacy example.");

            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before opening the legacy example.");

            // Play the scene the user opened instead of forcing the main game's entry scene.
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(ExamplePath, OpenSceneMode.Single);
        }
    }
}
