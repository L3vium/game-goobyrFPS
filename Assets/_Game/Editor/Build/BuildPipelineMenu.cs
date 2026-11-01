using System.Collections.Generic;
using System.IO;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The top of the pipeline: the title scene, the build list, the icon, everything in order
    /// (<c>Goobyr/Build/All</c>), and the Windows player (<c>Goobyr/Build/Player</c>).
    /// </summary>
    public static class BuildPipelineMenu
    {
        public const string OUTPUT = "Builds/Windows/GoobyrFPS.exe";

        [MenuItem("Goobyr/Build/All (data, art, prefabs, sounds, maps, title)")]
        public static void All()
        {
            DataBuilder.Build(false);
            ArtBuilder.Build();
            SfxBuilder.Build();
            PrefabBuilder.Build();
            PbrLibrary.Build();
            new MapSanLazaro().Build();
            new MapQasrAlRimal().Build();
            new MapSuburb().Build();
            Title();
            Icon();
            Debug.Log("[Build] all done");
        }

        [MenuItem("Goobyr/Build/Title Scene")]
        public static void Title()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(PrefabBuilder.GameRoot);
            new GameObject("MenuUI").AddComponent<MenuUI>();
            new GameObject("TitleDiorama").AddComponent<TitleDiorama>();
            MapDefinition lMap = GameDatabase.Instance.maps[0];
            RenderSettings.skybox = ArtBuilder.Sky(lMap);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = lMap.skyTop;
            RenderSettings.ambientEquatorColor = lMap.skyHorizon;
            RenderSettings.ambientGroundColor = lMap.ambient * 0.6f;
            RenderSettings.fog = false;
            string lPath = EditorAssets.SCENES + "/Title.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), lPath);
            Scenes();
            Debug.Log("[Build] title scene");
        }

        [MenuItem("Goobyr/Build/Scene List")]
        public static void Scenes()
        {
            List<EditorBuildSettingsScene> lScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(EditorAssets.SCENES + "/Title.unity", true) };
            foreach (MapDefinition lMap in GameDatabase.Instance.maps) lScenes.Add(new EditorBuildSettingsScene(EditorAssets.SCENES + "/" + lMap.sceneName + ".unity", true));
            EditorBuildSettings.scenes = lScenes.ToArray();
        }

        /// <summary>The app icon: an original Goob face with a crosshair over one eye.</summary>
        [MenuItem("Goobyr/Build/Icon")]
        public static void Icon()
        {
            const int S = 512;
            Texture2D lTexture = new Texture2D(S, S, TextureFormat.RGBA32, false);
            Color lBack = new Color(0.98f, 0.45f, 0.45f), lSkin = new Color(1f, 0.84f, 0.7f), lInk = new Color(0.1f, 0.1f, 0.14f), lHat = new Color(0.25f, 0.28f, 0.35f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Vector2 p = new Vector2(x, y) / S - new Vector2(0.5f, 0.5f);
                    float lCorner = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
                    Color c = lCorner < 0.47f ? lBack : new Color(0, 0, 0, 0);
                    if (p.magnitude < 0.33f) c = lSkin;
                    if (Mathf.Abs(p.x) < 0.32f && p.y > 0.16f && p.y < 0.2f) c = lHat;
                    if (Mathf.Abs(p.x) < 0.2f && p.y > 0.2f && p.y < 0.36f) c = lHat;
                    Vector2 lEyeL = p - new Vector2(-0.11f, 0.03f), lEyeR = p - new Vector2(0.11f, 0.03f);
                    if (lEyeL.magnitude < 0.09f || lEyeR.magnitude < 0.09f) c = Color.white;
                    if ((lEyeL - new Vector2(0.02f, 0)).magnitude < 0.04f || (lEyeR - new Vector2(-0.02f, 0)).magnitude < 0.04f) c = lInk;
                    float lRing = lEyeR.magnitude;
                    if (lRing > 0.13f && lRing < 0.15f) c = lInk;
                    if ((Mathf.Abs(lEyeR.x) < 0.008f || Mathf.Abs(lEyeR.y) < 0.008f) && lRing > 0.1f && lRing < 0.2f) c = lInk;
                    if (Mathf.Abs(p.x) < 0.07f && p.y > -0.17f && p.y < -0.14f) c = lInk;
                    lTexture.SetPixel(x, y, c);
                }
            lTexture.Apply();
            string lPath = EditorAssets.ROOT + "/Art/Icon.png";
            EditorAssets.EnsureFolder(EditorAssets.ROOT + "/Art");
            File.WriteAllBytes(lPath, lTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(lPath, ImportAssetOptions.ForceUpdate);
            Texture2D lIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(lPath);
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { lIcon }, IconKind.Any);
            PlayerSettings.SplashScreen.show = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] icon");
        }

        [MenuItem("Goobyr/Build/Player (Windows)")]
        public static void Player()
        {
            Scenes();
            PlayerSettings.SplashScreen.show = false;
            // Static batching would copy every kit mesh into combined meshes (1.5 GB per map):
            // the SRP batcher and the distance culling handle the draw calls instead.
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneWindows64, false);
            BuildPlayerOptions lOptions = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path),
                locationPathName = OUTPUT,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport lReport = BuildPipeline.BuildPlayer(lOptions);
            if (lReport.summary.result == BuildResult.Succeeded)
            {
                File.Copy("steam_appid.txt", Path.Combine(Path.GetDirectoryName(OUTPUT), "steam_appid.txt"), true);
                Debug.Log($"[Build] player OK: {OUTPUT} ({lReport.summary.totalSize / (1024 * 1024)} MB, {lReport.summary.totalTime.TotalSeconds:0}s)");
            }
            else Debug.LogError($"[Build] player FAILED: {lReport.summary.result}, {lReport.summary.totalErrors} errors");
        }
    }
}
