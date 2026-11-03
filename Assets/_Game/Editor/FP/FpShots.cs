using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// <c>Goobyr/Test/First Person Shots (San Lazaro)</c>: the first-person review set. Opens
    /// Map_SanLazaro, enters play mode, hosts a match and <see cref="FpShotsRunner"/> stands the player
    /// in the plaza, then for every family of held item renders the view (world and arms, post included,
    /// no HUD) at 16:9 and 21:9: hip and aimed for each gun, the fists, mid-jab, mid-cross, the takedown,
    /// both reloads, a throwable held and mid-throw. PNGs go to <see cref="OutputFolder"/> (EditorPrefs
    /// <c>Goobyr.FpShots.Out</c>, default Logs/fp_shots); the scene that was open comes back after.
    /// </summary>
    [InitializeOnLoad]
    public static class FpShots
    {
        private const string PENDING = "Goobyr.FpShots.Pending", RESTORE = "Goobyr.FpShots.Restore", OUT = "Goobyr.FpShots.Out";
        private const string MAP = "Assets/_Game/Scenes/Map_SanLazaro.unity";

        public static string OutputFolder
        {
            get => EditorPrefs.GetString(OUT, Path.Combine(Application.dataPath, "..", "Logs", "fp_shots"));
            set => EditorPrefs.SetString(OUT, value);
        }

        /// <summary>Set while a run is in progress: a domain reload in play mode resumes it.</summary>
        public const string RUNNING = "Goobyr.FpShots.Running";

        static FpShots()
        {
            EditorApplication.playModeStateChanged -= HandlePlayMode;
            EditorApplication.playModeStateChanged += HandlePlayMode;
            if (SessionState.GetBool(RUNNING, false))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool(PENDING, false))
                {
                    Debug.Log("[FP] shots: scripts reloaded in play mode, resuming");
                    EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) FpShotsRunner.Begin(OutputFolder, true); };
                }
                else if (!EditorApplication.isPlayingOrWillChangePlaymode) SessionState.EraseBool(RUNNING);
            }
        }

        [MenuItem("Goobyr/Test/First Person Shots (San Lazaro)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[FP] shots: already in play mode"); return; }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[FP] shots: '" + SceneManager.GetSceneAt(i).name + "' has unsaved changes - save or revert it first"); return; }
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
                FpShotsRunner.Begin(OutputFolder);
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
