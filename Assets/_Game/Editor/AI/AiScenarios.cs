using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// <c>Goobyr/Test/AI Scenarios</c>: the AI's behaviour checked in play mode, end to end. Opens an
    /// empty scene, enters play mode, and <see cref="AiScenarioRunner"/> builds a small arena (public,
    /// restricted and hostile ground, a wall, two exits), starts a host and runs seeded cases against
    /// real NPCs and a real player avatar; the results go to <c>Logs/ai_scenarios.json</c>, then play
    /// mode ends and the scene that was open comes back. Refuses to run over unsaved scene changes.
    /// </summary>
    [InitializeOnLoad]
    public static class AiScenarios
    {
        private const string PENDING = "Goobyr.AiScenarios.Pending", RESTORE = "Goobyr.AiScenarios.Restore";
        public const string RESULTS = "Logs/ai_scenarios.json";

        static AiScenarios()
        {
            EditorApplication.playModeStateChanged -= HandlePlayMode;
            EditorApplication.playModeStateChanged += HandlePlayMode;
        }

        [MenuItem("Goobyr/Test/AI Scenarios")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[AI] scenarios: already in play mode"); return; }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[AI] scenarios: '" + SceneManager.GetSceneAt(i).name + "' has unsaved changes - save or revert it first"); return; }
            SessionState.SetString(RESTORE, SceneManager.GetActiveScene().path);
            SessionState.SetBool(PENDING, true);
            string lResults = Path.Combine(Application.dataPath, "..", RESULTS);
            if (File.Exists(lResults)) File.Delete(lResults);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void HandlePlayMode(PlayModeStateChange pChange)
        {
            if (pChange == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PENDING, false))
            {
                SessionState.SetBool(PENDING, false);
                // Editor scripts can't be components: the runner's coroutine runs on a runtime host.
                AiScenarioRunner lRunner = new AiScenarioRunner { ResultsPath = Path.Combine(Application.dataPath, "..", RESULTS) };
                AI.CoroutineHost.Create("AiScenarioRunner").StartCoroutine(lRunner.Run());
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
