using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX.Rendering
{
    /// <summary>
    /// Volumetric fog and light shafts for URP (render graph). A half-resolution pass marches
    /// each pixel's view ray through height fog, samples the sun's cascaded shadow map along the
    /// way (lit where the sun reaches, dark in shadow: shafts), and stores in-scattered light +
    /// transmittance; a full-resolution pass composites it over the frame before post-processing.
    /// Settings come from the <see cref="GoobyrAtmosphere"/> volume component.
    /// </summary>
    public class VolumetricFogFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _shader;
        [SerializeField, Range(8, 64)] private int _steps = 28;

        private Material _material;
        private FogPass _pass;

        public override void Create()
        {
            if (_shader == null) _shader = Shader.Find("Hidden/Goobyr/VolumetricFog");
            if (_shader != null) _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new FogPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer pRenderer, ref RenderingData pData)
        {
            if (_material == null) return;
            CameraType lType = pData.cameraData.cameraType;
            if (lType != CameraType.Game && lType != CameraType.SceneView) return;
            if (pData.cameraData.renderType == CameraRenderType.Overlay) return;
            GoobyrAtmosphere lAtmosphere = VolumeManager.instance.stack.GetComponent<GoobyrAtmosphere>();
            if (lAtmosphere == null || !lAtmosphere.IsActive()) return;
            _pass.Setup(_material, lAtmosphere, _steps);
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            pRenderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool pDisposing) => CoreUtils.Destroy(_material);

        private class FogPass : ScriptableRenderPass
        {
            private static readonly int DENSITY = Shader.PropertyToID("_FogDensity");
            private static readonly int HEIGHT = Shader.PropertyToID("_FogHeight");
            private static readonly int FALLOFF = Shader.PropertyToID("_FogFalloff");
            private static readonly int AMBIENT = Shader.PropertyToID("_FogAmbient");
            private static readonly int SCATTER = Shader.PropertyToID("_FogSunScatter");
            private static readonly int ANISO = Shader.PropertyToID("_FogAnisotropy");
            private static readonly int NOISE = Shader.PropertyToID("_FogNoise");
            private static readonly int WIND = Shader.PropertyToID("_FogWind");
            private static readonly int DISTANCE = Shader.PropertyToID("_FogMaxDistance");
            private static readonly int STEPS = Shader.PropertyToID("_FogSteps");
            private static readonly int FOG_TEX = Shader.PropertyToID("_FogTex");

            private Material _material;

            private class MarchData { public Material material; public TextureHandle depth; }
            private class CompositeData { public Material material; public TextureHandle source; public TextureHandle fog; }

            public void Setup(Material pMaterial, GoobyrAtmosphere pAtmosphere, int pSteps)
            {
                _material = pMaterial;
                _material.SetFloat(DENSITY, pAtmosphere.density.value);
                _material.SetFloat(HEIGHT, pAtmosphere.baseHeight.value);
                _material.SetFloat(FALLOFF, pAtmosphere.falloff.value);
                _material.SetColor(AMBIENT, pAtmosphere.ambientColor.value);
                _material.SetFloat(SCATTER, pAtmosphere.sunScattering.value);
                _material.SetFloat(ANISO, pAtmosphere.anisotropy.value);
                _material.SetVector(NOISE, new Vector4(pAtmosphere.noiseStrength.value, pAtmosphere.noiseScale.value, 0, 0));
                _material.SetVector(WIND, pAtmosphere.wind.value);
                _material.SetFloat(DISTANCE, pAtmosphere.maxDistance.value);
                _material.SetFloat(STEPS, pSteps);
            }

            public override void RecordRenderGraph(RenderGraph pGraph, ContextContainer pFrame)
            {
                UniversalResourceData lResources = pFrame.Get<UniversalResourceData>();
                if (lResources.isActiveTargetBackBuffer || !lResources.cameraDepthTexture.IsValid()) return;

                TextureDesc lDesc = pGraph.GetTextureDesc(lResources.activeColorTexture);
                TextureDesc lFogDesc = lDesc;
                lFogDesc.width = Mathf.Max(1, lDesc.width / 2);
                lFogDesc.height = Mathf.Max(1, lDesc.height / 2);
                lFogDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                lFogDesc.name = "_GoobyrFog";
                lFogDesc.clearBuffer = false;
                lFogDesc.depthBufferBits = DepthBits.None;
                lFogDesc.msaaSamples = MSAASamples.None;
                TextureHandle lFog = pGraph.CreateTexture(lFogDesc);

                using (IRasterRenderGraphBuilder lBuilder = pGraph.AddRasterRenderPass("Goobyr Fog March", out MarchData lData))
                {
                    lData.material = _material;
                    lData.depth = lResources.cameraDepthTexture;
                    lBuilder.UseTexture(lData.depth);
                    // The sun's shadow map is a global texture.
                    lBuilder.UseAllGlobalTextures(true);
                    lBuilder.SetRenderAttachment(lFog, 0, AccessFlags.Write);
                    lBuilder.SetRenderFunc((MarchData pData, RasterGraphContext pContext) =>
                        Blitter.BlitTexture(pContext.cmd, new Vector4(1, 1, 0, 0), pData.material, 0));
                }

                TextureDesc lCopyDesc = lDesc;
                lCopyDesc.name = "_GoobyrFogSource";
                lCopyDesc.clearBuffer = false;
                TextureHandle lCopy = pGraph.CreateTexture(lCopyDesc);
                pGraph.AddBlitPass(lResources.activeColorTexture, lCopy, Vector2.one, Vector2.zero, passName: "Goobyr Fog Copy");

                using (IRasterRenderGraphBuilder lBuilder = pGraph.AddRasterRenderPass("Goobyr Fog Composite", out CompositeData lData))
                {
                    lData.material = _material;
                    lData.source = lCopy;
                    lData.fog = lFog;
                    lBuilder.UseTexture(lCopy);
                    lBuilder.UseTexture(lFog);
                    lBuilder.SetRenderAttachment(lResources.activeColorTexture, 0, AccessFlags.Write);
                    lBuilder.SetRenderFunc((CompositeData pData, RasterGraphContext pContext) =>
                    {
                        pData.material.SetTexture(FOG_TEX, pData.fog);
                        Blitter.BlitTexture(pContext.cmd, pData.source, new Vector4(1, 1, 0, 0), pData.material, 1);
                    });
                }
            }
        }
    }
}
