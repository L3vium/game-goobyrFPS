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

            // Water: depth-tinted, refracting, rippling, foaming on the shores (Goobyr/Water).
            Material lWater = EditorAssets.Material(lDir + "/M_Water.mat", Shader.Find("Goobyr/Water"));
            lWater.shader = Shader.Find("Goobyr/Water");
            lWater.SetColor("_ShallowColor", new Color(0.32f, 0.62f, 0.6f));
            lWater.SetColor("_DeepColor", new Color(0.04f, 0.2f, 0.27f));
            lWater.SetFloat("_Swell", 0.06f);

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
                Material lSky = EditorAssets.Material(EditorAssets.ROOT + "/Art/Generated/Sky_" + lMap.sceneName + ".mat", Shader.Find("Goobyr/Sky"));
                lSky.SetColor("_ZenithColor", lMap.skyTop);
                lSky.SetColor("_HorizonColor", lMap.skyHorizon);
                lSky.SetColor("_GroundColor", Color.Lerp(lMap.fog, lMap.ambient, 0.5f));
                lSky.SetColor("_HazeColor", lMap.hazeColor);
                lSky.SetColor("_SunColor", lMap.sun);
                lSky.SetColor("_CloudShade", lMap.cloudShade);
                lSky.SetFloat("_CloudCoverage", lMap.cloudCoverage);
                EditorUtility.SetDirty(lSky);
                VolumeProfileFor(lMap);
            }

            foreach (Material lMaterial in new[] { lToon, lTransparent, lWater, lGlow, lAdd, lAlpha }) EditorUtility.SetDirty(lMaterial);
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] materials");
        }

        /// <summary>The map's post stack: atmosphere (volumetric fog), white balance, grading, bloom, vignette.</summary>
        public static VolumeProfile VolumeProfileFor(MapDefinition pMap)
        {
            string lPath = EditorAssets.ROOT + "/Art/Generated/Volume_" + pMap.sceneName + ".asset";
            AssetDatabase.DeleteAsset(lPath);
            VolumeProfile lProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(lProfile, lPath);

            FX.Rendering.GoobyrAtmosphere lAtmosphere = Add<FX.Rendering.GoobyrAtmosphere>(lProfile);
            lAtmosphere.density.Override(pMap.volumetricDensity);
            lAtmosphere.baseHeight.Override(pMap.volumetricHeight);
            lAtmosphere.falloff.Override(pMap.volumetricFalloff);
            lAtmosphere.ambientColor.Override(pMap.volumetricAmbient);
            lAtmosphere.sunScattering.Override(pMap.sunScattering);
            lAtmosphere.maxDistance.Override(260f);

            UnityEngine.Rendering.Universal.WhiteBalance lBalance = Add<UnityEngine.Rendering.Universal.WhiteBalance>(lProfile);
            lBalance.temperature.Override(pMap.temperature);
            lBalance.tint.Override(pMap.tint);
            UnityEngine.Rendering.Universal.ColorAdjustments lColor = Add<UnityEngine.Rendering.Universal.ColorAdjustments>(lProfile);
            lColor.saturation.Override(pMap.saturation);
            lColor.contrast.Override(pMap.contrast);
            lColor.postExposure.Override(pMap.postExposure);
            UnityEngine.Rendering.Universal.ShadowsMidtonesHighlights lSmh = Add<UnityEngine.Rendering.Universal.ShadowsMidtonesHighlights>(lProfile);
            lSmh.shadows.Override(new Vector4(pMap.shadowsTint.r, pMap.shadowsTint.g, pMap.shadowsTint.b, 0f));
            lSmh.highlights.Override(new Vector4(pMap.highlightsTint.r, pMap.highlightsTint.g, pMap.highlightsTint.b, 0f));
            UnityEngine.Rendering.Universal.Bloom lBloom = Add<UnityEngine.Rendering.Universal.Bloom>(lProfile);
            lBloom.threshold.Override(1.05f);
            lBloom.intensity.Override(0.8f);
            lBloom.scatter.Override(0.72f);
            UnityEngine.Rendering.Universal.Vignette lVignette = Add<UnityEngine.Rendering.Universal.Vignette>(lProfile);
            lVignette.intensity.Override(0.22f);
            lVignette.smoothness.Override(0.5f);
            UnityEngine.Rendering.Universal.Tonemapping lTone = Add<UnityEngine.Rendering.Universal.Tonemapping>(lProfile);
            lTone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
            EditorUtility.SetDirty(lProfile);
            return lProfile;
        }

        private static T Add<T>(VolumeProfile pProfile) where T : VolumeComponent
        {
            T lComponent = pProfile.Add<T>(true);
            lComponent.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(lComponent, pProfile);
            return lComponent;
        }

        public static VolumeProfile Volume(MapDefinition pMap) => AssetDatabase.LoadAssetAtPath<VolumeProfile>(EditorAssets.ROOT + "/Art/Generated/Volume_" + pMap.sceneName + ".asset");

        /// <summary>Installs the volumetric fog feature on the PC renderer (once).</summary>
        [MenuItem("Goobyr/Build/Rendering Features")]
        public static void InstallFeatures()
        {
            UnityEngine.Rendering.Universal.UniversalRendererData lRenderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(EditorAssets.ROOT + "/Settings/PC_Renderer.asset");
            if (lRenderer == null) { Debug.LogError("[Build] PC_Renderer not found"); return; }
            // Order matters: the instinct grade runs after the fog, both before post-processing.
            InstallFeature<FX.Rendering.VolumetricFogFeature>(lRenderer, "Goobyr Volumetric Fog", "Hidden/Goobyr/VolumetricFog");
            InstallFeature<FX.Rendering.InstinctFeature>(lRenderer, "Goobyr Instinct", "Hidden/Goobyr/Instinct");
            // Softer, longer shadows for the bigger maps.
            UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset lAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(EditorAssets.ROOT + "/Settings/PC_RPAsset.asset");
            if (lAsset != null)
            {
                lAsset.shadowDistance = 140f;
                lAsset.shadowCascadeCount = 4;
                lAsset.supportsCameraDepthTexture = true;
                EditorUtility.SetDirty(lAsset);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] rendering features");
        }

        /// <summary>
        /// Adds a renderer feature once and points it at its shader, so the player build includes
        /// the hidden shader (Shader.Find alone would not).
        /// </summary>
        private static void InstallFeature<T>(UnityEngine.Rendering.Universal.UniversalRendererData pRenderer, string pName, string pShader) where T : UnityEngine.Rendering.Universal.ScriptableRendererFeature
        {
            T lFeature = (T)pRenderer.rendererFeatures.Find(f => f is T);
            if (lFeature == null)
            {
                lFeature = ScriptableObject.CreateInstance<T>();
                lFeature.name = pName;
                AssetDatabase.AddObjectToAsset(lFeature, pRenderer);
                pRenderer.rendererFeatures.Add(lFeature);
                SerializedObject lRendererData = new SerializedObject(pRenderer);
                SerializedProperty lMap = lRendererData.FindProperty("m_RendererFeatureMap");
                lMap.arraySize++;
                lMap.GetArrayElementAtIndex(lMap.arraySize - 1).longValue = lFeature.GetInstanceID();
                lRendererData.ApplyModifiedPropertiesWithoutUndo();
            }
            SerializedObject lSerialized = new SerializedObject(lFeature);
            lSerialized.FindProperty("_shader").objectReferenceValue = Shader.Find(pShader);
            lSerialized.ApplyModifiedPropertiesWithoutUndo();
            lFeature.Create();
            pRenderer.SetDirty();
            EditorUtility.SetDirty(pRenderer);
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
