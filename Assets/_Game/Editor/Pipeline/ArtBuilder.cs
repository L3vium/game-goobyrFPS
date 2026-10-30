using Com.IsartDigital.GoobyrFPS.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The shared materials (toon, transparent, glow, particles) in <c>Art/Resources/Art</c>, and
    /// one sky material per map coloured from its <see cref="MapDefinition"/>.
    /// </summary>
    public static class ArtBuilder
    {
        [MenuItem("Goobyr/Build/Materials")]
        public static void Build()
        {
            string lDir = EditorAssets.ART_RESOURCES;
            Material lToon = EditorAssets.Material(lDir + "/M_Toon.mat", Shader.Find("Goobyr/Toon"));
            lToon.enableInstancing = true;
            lToon.SetColor("_BaseColor", Color.white);

            Material lTransparent = EditorAssets.Material(lDir + "/M_ToonTransparent.mat", Shader.Find("Goobyr/ToonTransparent"));
            lTransparent.SetColor("_BaseColor", new Color(1, 1, 1, 0.5f));
            lTransparent.SetFloat("_Wave", 0f);

            Material lWater = EditorAssets.Material(lDir + "/M_Water.mat", Shader.Find("Goobyr/ToonTransparent"));
            lWater.SetColor("_BaseColor", new Color(0.3f, 0.65f, 0.8f, 0.72f));
            lWater.SetFloat("_Wave", 0.08f);

            Material lGlow = EditorAssets.Material(lDir + "/M_Glow.mat", Shader.Find("Goobyr/Glow"));
            SetBlend(lGlow, BlendMode.One, BlendMode.Zero, true, 2000);

            Material lAdd = EditorAssets.Material(lDir + "/M_ParticleAdd.mat", Shader.Find("Goobyr/Glow"));
            SetBlend(lAdd, BlendMode.One, BlendMode.One, false, 3100);
            lAdd.SetColor("_BaseColor", new Color(1.6f, 1.6f, 1.6f, 1f));

            Material lAlpha = EditorAssets.Material(lDir + "/M_ParticleAlpha.mat", Shader.Find("Goobyr/Glow"));
            SetBlend(lAlpha, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false, 3000);
            lAlpha.SetFloat("_SoftEdge", 0.8f);

            foreach (MapDefinition lMap in GameDatabase.Instance.maps)
            {
                Material lSky = EditorAssets.Material(EditorAssets.ROOT + "/Art/Generated/Sky_" + lMap.sceneName + ".mat", Shader.Find("Goobyr/SkyGradient"));
                lSky.SetColor("_TopColor", lMap.skyTop);
                lSky.SetColor("_HorizonColor", lMap.skyHorizon);
                lSky.SetColor("_BottomColor", Color.Lerp(lMap.fog, lMap.ambient, 0.5f));
                lSky.SetColor("_SunColor", lMap.sun * 0.8f);
                lSky.SetFloat("_SunSize", 0.012f);
                EditorUtility.SetDirty(lSky);
            }

            foreach (Material lMaterial in new[] { lToon, lTransparent, lWater, lGlow, lAdd, lAlpha }) EditorUtility.SetDirty(lMaterial);
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] materials");
        }

        private static void SetBlend(Material pMaterial, BlendMode pSrc, BlendMode pDst, bool pZWrite, int pQueue)
        {
            pMaterial.SetFloat("_SrcBlend", (float)pSrc);
            pMaterial.SetFloat("_DstBlend", (float)pDst);
            pMaterial.SetFloat("_ZWrite", pZWrite ? 1f : 0f);
            pMaterial.renderQueue = pQueue;
            pMaterial.SetOverrideTag("RenderType", pZWrite ? "Opaque" : "Transparent");
        }

        public static Material Sky(MapDefinition pMap) => AssetDatabase.LoadAssetAtPath<Material>(EditorAssets.ROOT + "/Art/Generated/Sky_" + pMap.sceneName + ".mat");
        public static Material Water => AssetDatabase.LoadAssetAtPath<Material>(EditorAssets.ART_RESOURCES + "/M_Water.mat");
    }
}
