using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// <c>Goobyr/Test/AI Showcase (San Lazaro)</c>: frame strips of the AI on a real map. Opens
    /// Map_SanLazaro, enters play mode, hosts a match on it and <see cref="AiShowcaseRunner"/> walks the
    /// player into a guard's restricted post with a gun out (the guard engages), then fires near the
    /// crowd (it stampedes), saving game-view frames and third-person renders as PNGs in
    /// <see cref="OutputFolder"/> (EditorPrefs <c>Goobyr.AiShowcase.Out</c>, default Logs/ai_showcase). The
    /// scene that was open comes back after. Refuses to run over unsaved scene changes.
    /// </summary>
    [InitializeOnLoad]
    public static class AiShowcase
    {
        private const string PENDING = "Goobyr.AiShowcase.Pending", RESTORE = "Goobyr.AiShowcase.Restore", OUT = "Goobyr.AiShowcase.Out";
        private const string MAP = "Assets/_Game/Scenes/Map_SanLazaro.unity";

        public static string OutputFolder
        {
            get => EditorPrefs.GetString(OUT, Path.Combine(Application.dataPath, "..", "Logs", "ai_showcase"));
            set => EditorPrefs.SetString(OUT, value);
        }

        static AiShowcase()
        {
            EditorApplication.playModeStateChanged -= HandlePlayMode;
            EditorApplication.playModeStateChanged += HandlePlayMode;
        }

        [MenuItem("Goobyr/Test/AI Showcase (San Lazaro)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[AI] showcase: already in play mode"); return; }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[AI] showcase: '" + SceneManager.GetSceneAt(i).name + "' has unsaved changes - save or revert it first"); return; }
            SessionState.SetString(RESTORE, SceneManager.GetActiveScene().path);
            SessionState.SetBool(PENDING, true);
            EditorSceneManager.OpenScene(MAP, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void HandlePlayMode(PlayModeStateChange pChange)
        {
            if (pChange == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PENDING, false))
            {
                SessionState.SetBool(PENDING, false);
                AiShowcaseRunner lRunner = new GameObject("AiShowcaseRunner").AddComponent<AiShowcaseRunner>();
                lRunner.OutputFolder = OutputFolder;
            }
            else if (pChange == PlayModeStateChange.EnteredEditMode)
            {
                string lScene = SessionState.GetString(RESTORE, "");
                if (string.IsNullOrEmpty(lScene)) return;
                SessionState.EraseString(RESTORE);
                if (File.Exists(Path.Combine(Application.dataPath, "..", lScene))) EditorSceneManager.OpenScene(lScene, OpenSceneMode.Single);
            }
        }
    }
}
