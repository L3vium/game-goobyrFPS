using System.Collections.Generic;
using System.IO;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            CharacterBuilder.Build();
            PrefabBuilder.Build();
            PbrLibrary.Build();
            new MapSanLazaro().Build();
            new MapQasrAlRimal().Build();
            new MapWeisshorn().Build();
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

        /// <summary>
        /// The app icon: the agent's head and shoulders (black suit, red tie) in three-quarter view,
        /// a warm key, a cold rim on the dark side, a near-black backdrop - rendered by a camera in a
        /// preview scene from the real character, 1024 px.
        /// </summary>
        [MenuItem("Goobyr/Build/Icon")]
        public static void Icon()
        {
            const int S = 1024;
            Scene lScene = EditorSceneManager.NewPreviewScene();
            RenderTexture lTarget = new RenderTexture(S, S, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            Texture2D lTexture = new Texture2D(S, S, TextureFormat.RGBA32, false);
            try
            {
                GameObject lSource = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterBuilder.DIR + "/Chr_Agent/Chr_Agent.fbx");
                GameObject lAgent = Object.Instantiate(lSource);
                SceneManager.MoveGameObjectToScene(lAgent, lScene);
                // Full detail only: no LOD switching in a one-off render.
                Object.DestroyImmediate(lAgent.GetComponent<LODGroup>());
                foreach (SkinnedMeshRenderer lSkin in lAgent.GetComponentsInChildren<SkinnedMeshRenderer>()) if (lSkin.name.EndsWith("_LOD1")) Object.DestroyImmediate(lSkin.gameObject);
                AnimationClip lIdle = CharacterBuilder.Clip("Idle");
                if (lIdle != null) lIdle.SampleAnimation(lAgent, 0f);
                lAgent.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 160f, 0f));
                Transform lHead = lAgent.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
                Vector3 lFace = lHead.position + Vector3.up * 0.05f;

                // Warm key from the camera's right, a hard cold rim from behind on both edges, a faint fill.
                IconLight(lScene, Quaternion.Euler(22f, -35f, 0f), new Color(1f, 0.84f, 0.68f), 1.7f);
                IconLight(lScene, Quaternion.Euler(12f, 150f, 0f), new Color(0.5f, 0.72f, 1f), 4.5f);
                IconLight(lScene, Quaternion.Euler(8f, -150f, 0f), new Color(0.6f, 0.75f, 1f), 3f);
                IconLight(lScene, Quaternion.Euler(-5f, 20f, 0f), new Color(0.3f, 0.32f, 0.4f), 0.35f);

                Camera lCamera = new GameObject("IconCamera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(lCamera.gameObject, lScene);
                lCamera.scene = lScene;
                lCamera.clearFlags = CameraClearFlags.SolidColor;
                lCamera.backgroundColor = new Color(0.035f, 0.038f, 0.045f, 1f);
                lCamera.fieldOfView = 22f;
                lCamera.nearClipPlane = 0.1f;
                lCamera.farClipPlane = 10f;
                lCamera.transform.position = lFace + lAgent.transform.forward * 2.05f + lAgent.transform.right * -0.45f + Vector3.down * 0.12f;
                lCamera.transform.LookAt(lFace + Vector3.down * 0.2f);
                lCamera.targetTexture = lTarget;
                lCamera.Render();

                RenderTexture.active = lTarget;
                lTexture.ReadPixels(new Rect(0, 0, S, S), 0, 0);
                lTexture.Apply();
                RenderTexture.active = null;
                lCamera.targetTexture = null;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(lScene);
                lTarget.Release();
            }

            string lPath = EditorAssets.ROOT + "/Art/Icon.png";
            File.WriteAllBytes(lPath, lTexture.EncodeToPNG());
            Object.DestroyImmediate(lTexture);
            AssetDatabase.ImportAsset(lPath, ImportAssetOptions.ForceUpdate);
            TextureImporter lImporter = AssetImporter.GetAtPath(lPath) as TextureImporter;
            if (lImporter != null)
            {
                lImporter.alphaIsTransparency = true;
                lImporter.mipmapEnabled = false;
                lImporter.maxTextureSize = 1024;
                lImporter.SaveAndReimport();
            }
            Texture2D lIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(lPath);
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { lIcon }, IconKind.Any);
            PlayerSettings.SplashScreen.show = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] icon");
        }

        private static void IconLight(Scene pScene, Quaternion pRotation, Color pColor, float pIntensity)
        {
            Light lLight = new GameObject("IconLight").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(lLight.gameObject, pScene);
            lLight.type = LightType.Directional;
            lLight.transform.rotation = pRotation;
            lLight.color = pColor;
            lLight.intensity = pIntensity;
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
