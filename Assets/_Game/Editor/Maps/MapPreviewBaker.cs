using System.IO;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The room's picture of a map (<see cref="MapDefinition.preview"/>): a shot from the ground across
    /// to one of its landmarks, an agent standing in the foreground with their back to us. The first map's
    /// picture is also the title screen's backdrop. Runs on the map just built, once its scene is saved.
    /// </summary>
    public static class MapPreviewBaker
    {
        private const int WIDTH = 1280, HEIGHT = 720;
        private const string FOLDER = EditorAssets.ROOT + "/Art/Generated/Previews";

        /// <summary>Each map's picture, one at a time, opening its scene: run on its own, after the maps are built.</summary>
        [MenuItem("Goobyr/Build/Map Pictures")]
        public static void BakeAll()
        {
            foreach (MapDefinition lMap in GameDatabase.Instance.maps)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"{EditorAssets.SCENES}/{lMap.sceneName}.unity");
                Terrain lLand = Object.FindFirstObjectByType<Terrain>();
                if (lLand != null) Bake(lMap, lLand);
            }
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        }

        public static void Bake(MapDefinition pMap, Terrain pLand)
        {
            TimeOfDay lTime = Object.FindFirstObjectByType<TimeOfDay>();
            if (lTime != null) lTime.Apply(pMap.timeOfDay);

            Vector3 lLook = Target(pLand, out Vector3 lFrom);
            GameObject lRig = new GameObject("PreviewCamera");
            Camera lCamera = lRig.AddComponent<Camera>();
            lCamera.fieldOfView = 42f;
            lCamera.nearClipPlane = 0.1f;
            lCamera.farClipPlane = 1200f;
            lCamera.clearFlags = CameraClearFlags.Skybox;
            lCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            lRig.transform.position = lFrom;
            // Nearly level, tipped up just enough: the agent stays in frame, the sky fills the top.
            Vector3 lAim = Vector3.ProjectOnPlane(lLook - lFrom, Vector3.up).normalized;
            lRig.transform.rotation = Quaternion.LookRotation(lAim + Vector3.up * 0.1f);

            GameObject lAgent = Agent(lRig.transform, pLand);
            RenderTexture lTarget = new RenderTexture(WIDTH, HEIGHT, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            lCamera.targetTexture = lTarget;
            lCamera.Render();
            RenderTexture lActive = RenderTexture.active;
            RenderTexture.active = lTarget;
            Texture2D lRead = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGB24, false);
            lRead.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
            lRead.Apply();
            RenderTexture.active = lActive;
            byte[] lPng = lRead.EncodeToPNG();
            Object.DestroyImmediate(lRead);
            lCamera.targetTexture = null;
            lTarget.Release();
            Object.DestroyImmediate(lRig);
            if (lAgent != null) Object.DestroyImmediate(lAgent);

            EditorAssets.EnsureFolder(FOLDER);
            string lPath = $"{FOLDER}/Preview_{pMap.sceneName}.png";
            File.WriteAllBytes(lPath, lPng);
            AssetDatabase.ImportAsset(lPath, ImportAssetOptions.ForceUpdate);
            pMap.preview = AssetDatabase.LoadAssetAtPath<Texture2D>(lPath);
            EditorUtility.SetDirty(pMap);

            if (GameDatabase.Instance.maps.Count > 0 && GameDatabase.Instance.maps[0] == pMap)
            {
                string lTitleDir = EditorAssets.ART_RESOURCES + "/Title";
                EditorAssets.EnsureFolder(lTitleDir);
                File.WriteAllBytes(lTitleDir + "/TitleArt.png", lPng);
                AssetDatabase.ImportAsset(lTitleDir + "/TitleArt.png", ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Build] {pMap.displayName} picture: {lPath}");
        }

        /// <summary>A landmark to look at, and a spot on open ground with a clear view of it.</summary>
        private static Vector3 Target(Terrain pLand, out Vector3 pFrom)
        {
            MapPoi[] lPois = Object.FindObjectsByType<MapPoi>(FindObjectsSortMode.None);
            Vector3 lBest = Vector3.zero, lBestFrom = new Vector3(0f, 40f, -60f);
            int lBestScore = -1;
            foreach (MapPoi lPoi in lPois)
            {
                if (lPoi.Type != MapPoi.Kind.Landmark) continue;
                Vector3 lAt = lPoi.transform.position + Vector3.up * 4f;
                // From afar first: the whole building and some sky, an open patch of level ground to stand on.
                foreach (float lDistance in new[] { 75f, 55f, 38f })
                    for (int d = 0; d < 12; d++)
                    {
                        float a = d * Mathf.PI / 6f;
                        Vector3 lGround = lAt + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * lDistance;
                        lGround.y = pLand.SampleHeight(lGround) + pLand.transform.position.y;
                        Vector3 lSpot = lGround + Vector3.up * 1.55f;
                        if (Physics.Linecast(lSpot, lAt, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.CheckSphere(lSpot + Vector3.up, 2.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        Vector3 lFlat = Vector3.ProjectOnPlane(lAt - lSpot, Vector3.up).normalized;
                        float lAhead = pLand.SampleHeight(lSpot + lFlat * 3f) + pLand.transform.position.y;
                        if (Mathf.Abs(lAhead - lGround.y) > 0.5f) continue;
                        int lScore = Mathf.RoundToInt(lDistance);
                        foreach (MapPoi lOther in lPois) if (Vector3.Distance(lOther.transform.position, lAt) < 26f) lScore += 2;
                        if (lScore > lBestScore) { lBestScore = lScore; lBest = lAt; lBestFrom = lSpot; }
                    }
            }
            pFrom = lBestFrom;
            return lBest;
        }

        /// <summary>An agent a few metres ahead and to one side of the camera, seen from behind, standing on the ground.</summary>
        private static GameObject Agent(Transform pCamera, Terrain pLand)
        {
            GameObject lModel = AssetDatabase.LoadAssetAtPath<GameObject>(EditorAssets.ROOT + "/Art/Characters/Chr_Rook/Chr_Rook.fbx");
            if (lModel == null) return null;
            GameObject lAgent = Object.Instantiate(lModel);
            Vector3 lFlat = Vector3.ProjectOnPlane(pCamera.forward, Vector3.up).normalized;
            Vector3 lAt = pCamera.position + lFlat * 3.4f + pCamera.right * 1.35f;
            lAt.y = pLand.SampleHeight(lAt) + pLand.transform.position.y;
            lAgent.transform.SetPositionAndRotation(lAt, Quaternion.LookRotation(lFlat));
            string lClipPath = CharacterBuilder.ANIM_DIR;
            foreach (string lGuid in AssetDatabase.FindAssets("Idle t:AnimationClip", new[] { lClipPath }))
            {
                foreach (Object lSub in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(lGuid)))
                    if (lSub is AnimationClip lClip && lClip.name == CharacterBuilder.REFERENCE_CLIP)
                    {
                        lClip.SampleAnimation(lAgent, 0.5f);
                        lAgent.transform.SetPositionAndRotation(lAt, Quaternion.LookRotation(lFlat));
                        return lAgent;
                    }
            }
            return lAgent;
        }
    }
}
