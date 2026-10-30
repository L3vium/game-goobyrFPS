using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX.Rendering
{
    /// <summary>
    /// Draws <see cref="InstinctVision"/> (render graph): grades the frame (dim, cold, faint edges,
    /// the scan wave), then draws every highlighted renderer as a glowing silhouette through the
    /// walls. Runs before post-processing so the bloom makes the silhouettes glow.
    /// </summary>
    public class InstinctFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _shader;

        private Material _grade;
        private Material[] _slots;
        private InstinctPass _pass;

        private static readonly int AMOUNT = Shader.PropertyToID("_InstinctAmount");
        private static readonly int WAVE = Shader.PropertyToID("_InstinctWave");
        private static readonly int WAVE_FADE = Shader.PropertyToID("_InstinctWaveFade");
        private static readonly int COLOR = Shader.PropertyToID("_Color");

        public override void Create()
        {
            if (_shader == null) _shader = Shader.Find("Hidden/Goobyr/Instinct");
            if (_shader == null) return;
            _grade = CoreUtils.CreateEngineMaterial(_shader);
            _slots = new Material[InstinctVision.Palette.Length];
            for (int i = 0; i < _slots.Length; i++) _slots[i] = CoreUtils.CreateEngineMaterial(_shader);
            // After the fog (same event, later in the feature list), before post-processing.
            _pass = new InstinctPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer pRenderer, ref RenderingData pData)
        {
            if (_grade == null || !InstinctVision.Active) return;
            if (pData.cameraData.cameraType != CameraType.Game || pData.cameraData.renderType == CameraRenderType.Overlay) return;
            Shader.SetGlobalFloat(AMOUNT, InstinctVision.Amount);
            Shader.SetGlobalFloat(WAVE, InstinctVision.Wave);
            Shader.SetGlobalFloat(WAVE_FADE, InstinctVision.WaveFade);
            for (int i = 0; i < _slots.Length; i++) _slots[i].SetColor(COLOR, InstinctVision.Palette[i]);
            _pass.Setup(_grade, _slots);
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            pRenderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool pDisposing)
        {
            CoreUtils.Destroy(_grade);
            if (_slots != null) foreach (Material lMaterial in _slots) CoreUtils.Destroy(lMaterial);
        }

        private class InstinctPass : ScriptableRenderPass
        {
            private const int GRADE_PASS = 0;
            private const int HIGHLIGHT_PASS = 1;

            private Material _grade;
            private Material[] _slots;

            private class GradeData { public Material material; public TextureHandle source; }
            private class HighlightData { public Material[] slots; }

            public void Setup(Material pGrade, Material[] pSlots)
            {
                _grade = pGrade;
                _slots = pSlots;
            }

            public override void RecordRenderGraph(RenderGraph pGraph, ContextContainer pFrame)
            {
                UniversalResourceData lResources = pFrame.Get<UniversalResourceData>();
                if (lResources.isActiveTargetBackBuffer || !lResources.cameraDepthTexture.IsValid()) return;

                TextureDesc lDesc = pGraph.GetTextureDesc(lResources.activeColorTexture);
                lDesc.name = "_GoobyrInstinctSource";
                lDesc.clearBuffer = false;
                TextureHandle lCopy = pGraph.CreateTexture(lDesc);
                pGraph.AddBlitPass(lResources.activeColorTexture, lCopy, Vector2.one, Vector2.zero, passName: "Goobyr Instinct Copy");

                using (IRasterRenderGraphBuilder lBuilder = pGraph.AddRasterRenderPass("Goobyr Instinct Grade", out GradeData lData))
                {
                    lData.material = _grade;
                    lData.source = lCopy;
                    lBuilder.UseTexture(lCopy);
                    lBuilder.UseTexture(lResources.cameraDepthTexture);
                    lBuilder.SetRenderAttachment(lResources.activeColorTexture, 0, AccessFlags.Write);
                    lBuilder.SetRenderFunc((GradeData pData, RasterGraphContext pContext) =>
                        Blitter.BlitTexture(pContext.cmd, pData.source, new Vector4(1, 1, 0, 0), pData.material, GRADE_PASS));
                }

                if (InstinctVision.Draws.Count == 0) return;
                using (IRasterRenderGraphBuilder lBuilder = pGraph.AddRasterRenderPass("Goobyr Instinct Highlights", out HighlightData lData))
                {
                    lData.slots = _slots;
                    lBuilder.UseTexture(lResources.cameraDepthTexture);
                    lBuilder.SetRenderAttachment(lResources.activeColorTexture, 0, AccessFlags.Write);
                    lBuilder.SetRenderAttachmentDepth(lResources.activeDepthTexture, AccessFlags.ReadWrite);
                    lBuilder.AllowPassCulling(false);
                    lBuilder.SetRenderFunc((HighlightData pData, RasterGraphContext pContext) =>
                    {
                        foreach (InstinctVision.Draw lDraw in InstinctVision.Draws)
                        {
                            Renderer lRenderer = lDraw.renderer;
                            if (lRenderer == null) continue;
                            Material lMaterial = pData.slots[(int)lDraw.slot];
                            for (int i = 0; i < lDraw.submeshes; i++) pContext.cmd.DrawRenderer(lRenderer, lMaterial, i, HIGHLIGHT_PASS);
                        }
                    });
                }
            }
        }
    }
}
