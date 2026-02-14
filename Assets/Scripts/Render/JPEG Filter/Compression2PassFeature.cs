using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Render
{
    public class UIBlur2PassFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;

            [Header("Material")]
            [Tooltip("Hidden/Kross/UIBlur_Separable")]
            public Material blurMaterial;

            [Header("Quality")]
            [Range(1, 4)] public int downsample = 2;
            [Range(0f, 40f)] public float radiusPx = 12f;

            [Header("Output")]
            public string blurTexName = "_KrossUIBlurTex";
        }

        static readonly int _SourceTexId = Shader.PropertyToID("_SourceTex");
        static readonly int _DirId       = Shader.PropertyToID("_Dir");
        static readonly int _RadiusId    = Shader.PropertyToID("_RadiusPx");
        static readonly Vector4 kScaleBias = new Vector4(1, 1, 0, 0);

        public Settings settings = new Settings();
        BlurRGPass _pass;

        public override void Create()
        {
            _pass = new BlurRGPass(settings)
            {
                renderPassEvent = settings.passEvent
            };
        }



        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.blurMaterial == null)
                return;

            renderer.EnqueuePass(_pass);
        }
        
        class BlurRGPass : ScriptableRenderPass
        {
            readonly Settings _s;
            readonly int _blurNameId;

            public BlurRGPass(Settings s)
            {
                _s = s;
                _blurNameId = Shader.PropertyToID(string.IsNullOrEmpty(_s.blurTexName) ? "_KrossUIBlurTex" : _s.blurTexName);
            }

            class PassData
            {
                public TextureHandle source;
                public TextureHandle destination;
                public Material material;
                public Vector2 dir;
                public float radius;
            }

            class SetGlobalData
            {
                public TextureHandle tex;
                public int nameId;
            }
            private void AddCopyPass(RenderGraph rg, TextureHandle src, TextureHandle dst)
            {
                using (var builder = rg.AddRasterRenderPass<PassData>("UIBlur_CopyDownsample", out var passData))
                {
                    passData.source = src;
                    passData.destination = dst;

                    builder.AllowPassCulling(false);

                    builder.UseTexture(passData.source); // если доступно: builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(passData.destination, 0);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
                    });
                }
            }
            private void AddBlurPass(
                RenderGraph rg,
                string name,
                TextureHandle src,
                TextureHandle dst,
                Material mat,
                Vector2 dir,
                float radiusPx)
            {
                using (var builder = rg.AddRasterRenderPass<PassData>(name, out var passData))
                {
                    passData.source = src;
                    passData.destination = dst;
                    passData.material = mat;
                    passData.dir = dir;
                    passData.radius = radiusPx;

                    builder.AllowPassCulling(false);

                    builder.UseTexture(passData.source); // если доступно: builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(passData.destination, 0);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetVector(_DirId, data.dir);
                        data.material.SetFloat(_RadiusId, data.radius);

                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_s.blurMaterial == null)
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData   = frameData.Get<UniversalCameraData>();

                var camDesc = cameraData.cameraTargetDescriptor;

                int ds = Mathf.Clamp(_s.downsample, 1, 4);
                int w = Mathf.Max(1, camDesc.width  / ds);
                int h = Mathf.Max(1, camDesc.height / ds);

                // RT desc (downsampled, no depth, no MSAA)
                var rtDesc = new TextureDesc(w, h)
                {
                    depthBufferBits = DepthBits.None,
                    msaaSamples = MSAASamples.None,
                    colorFormat = camDesc.graphicsFormat,
                    name = "UIBlur_TmpA"
                };
                
                TextureHandle tmpA = renderGraph.CreateTexture(rtDesc);

                rtDesc.name = "UIBlur_TmpB";
                TextureHandle tmpB = renderGraph.CreateTexture(rtDesc);

                rtDesc.name = "UIBlur_Final";
                TextureHandle blurFinal = renderGraph.CreateTexture(rtDesc);

                // Convert radius in full-res pixels to downsample space
                float radiusOnDs = _s.radiusPx / ds;

                // Source is the current active camera color
                TextureHandle src = resourceData.activeColorTexture;

                // ---- Pass 0: Copy/Downsample src -> tmpA (just blit) ----
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("UIBlur Copy/Downsample", out var passData))
                {
                    passData.material = null;
                    passData.source = src;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(tmpA, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source, kScaleBias, 0, false);
                    });
                }

                // ---- Pass 1: Horizontal tmpA -> tmpB ----
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("UIBlur Horizontal", out var passData))
                {
                    passData.material = _s.blurMaterial;
                    passData.source = tmpA;
                    passData.dir = new Vector2(1, 0);
                    passData.radius = radiusOnDs;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(tmpB, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetTexture(_SourceTexId, data.source);
                        data.material.SetVector(_DirId, data.dir);
                        data.material.SetFloat(_RadiusId, data.radius);

                        Blitter.BlitTexture(ctx.cmd, data.source, kScaleBias, data.material, 0);
                    });
                }

                // ---- Pass 2: Vertical tmpB -> blurFinal ----
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("UIBlur Vertical", out var passData))
                {
                    passData.material = _s.blurMaterial;
                    passData.source = tmpB;
                    passData.dir = new Vector2(0, 1);
                    passData.radius = radiusOnDs;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(blurFinal, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetTexture(_SourceTexId, data.source);
                        data.material.SetVector(_DirId, data.dir);
                        data.material.SetFloat(_RadiusId, data.radius);

                        Blitter.BlitTexture(ctx.cmd, data.source, kScaleBias, data.material, 0);
                    });
                }
                

                // ---- Pass 3: Make it global for UI shaders ----
                using (var builder = renderGraph.AddRasterRenderPass<SetGlobalData>("UIBlur SetGlobal", out var passData))
                {
                    passData.tex = blurFinal;
                    passData.nameId = _blurNameId;

                    builder.UseTexture(passData.tex, AccessFlags.Read);
                    // attachment не нужен, мы просто ставим global

                    builder.SetRenderFunc(static (SetGlobalData data, RasterGraphContext ctx) =>
                    {
                        // В твоей ветке URP это обычно нормально работает:
                        // TextureHandle используется как источник в Blitter и как shader texture binding.
                        ctx.cmd.SetGlobalTexture(data.nameId, data.tex);
                    });
                }
                
            }
        }
    }
}
