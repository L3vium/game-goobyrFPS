using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Review tools for the generated maps: renders a camera to a PNG (any point, any time of day),
    /// and an architecture test scene (a building on a lawn under a sun) for quick iterations.
    /// </summary>
    public static class MapShots
    {
        /// <summary>Renders the open scene from a point toward another into a PNG.</summary>
        public static string Shot(string pPath, Vector3 pFrom, Vector3 pTo, float pFov = 60f, int pWidth = 1600, int pHeight = 900, float pFar = 1500f)
        {
            GameObject lObject = new GameObject("ShotCamera");
            Camera lCamera = lObject.AddComponent<Camera>();
            lObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            lCamera.transform.position = pFrom;
            lCamera.transform.rotation = Quaternion.LookRotation((pTo - pFrom).normalized);
            lCamera.fieldOfView = pFov;
            lCamera.nearClipPlane = 0.05f;
            lCamera.farClipPlane = pFar;
            RenderTexture lTarget = new RenderTexture(pWidth, pHeight, 24, RenderTextureFormat.ARGB32);
            lTarget.antiAliasing = 4;
            lCamera.targetTexture = lTarget;
            lCamera.Render();
            lCamera.Render();
            RenderTexture.active = lTarget;
            Texture2D lImage = new Texture2D(pWidth, pHeight, TextureFormat.RGB24, false);
            lImage.ReadPixels(new Rect(0, 0, pWidth, pHeight), 0, 0);
            lImage.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(pPath));
            File.WriteAllBytes(pPath, lImage.EncodeToPNG());
            lCamera.targetTexture = null;
            Object.DestroyImmediate(lTarget);
            Object.DestroyImmediate(lImage);
            Object.DestroyImmediate(lObject);
            return pPath;
        }

        /// <summary>A point <paramref name="pUp"/> metres over the land (terrain only, not roofs).</summary>
        public static Vector3 Eye(float pX, float pZ, float pUp = 1.7f)
        {
            Terrain lLand = Terrain.activeTerrain;
            float y = lLand != null ? lLand.SampleHeight(new Vector3(pX, 0, pZ)) + lLand.transform.position.y : 0f;
            return new Vector3(pX, y + pUp, pZ);
        }

        /// <summary>Applies one of the map's time-of-day presets in edit mode (0 dawn, 1 noon, 2 dusk, 3 night).</summary>
        public static void Time(int pPreset)
        {
            FX.TimeOfDay lTime = Object.FindFirstObjectByType<FX.TimeOfDay>();
            if (lTime == null) return;
            System.Reflection.MethodInfo lApply = typeof(FX.TimeOfDay).GetMethod("Apply", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (lApply != null) lApply.Invoke(lTime, new object[] { pPreset });
        }

        [MenuItem("Goobyr/Debug/Arch Test Scene")]
        public static void ArchTest()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ArchLib.Reset();
            ArchBuilder.MeshPrefix = "ArchTest";
            GameObject lGround = new GameObject("Ground");
            ArchMesh lMesh = new ArchMesh();
            lMesh.BoxMinMax(ArchLib.Surface("leafy_grass", 4f), new Vector3(-60, -6, -60), new Vector3(100, 0, 100));
            lMesh.Flush(lGround.transform, "ArchTest_Ground");
            GameObject lSun = new GameObject("Sun");
            Light lLight = lSun.AddComponent<Light>();
            lLight.type = LightType.Directional;
            lLight.intensity = 2.2f;
            lLight.shadows = LightShadows.Soft;
            lLight.color = new Color(1f, 0.95f, 0.88f);
            lSun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = lLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);
            Transform lRoot = new GameObject("ArchTest").transform;
            ArchResult r = ArchBuilder.Build(MapSanLazaro.CasaSpec(Vector3.zero, 0f), lRoot);
            MapKit lFake = null;
            _ = lFake;
            _ = r;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), EditorAssets.SCENES + "/ArchTest.unity");
        }
    }
}
