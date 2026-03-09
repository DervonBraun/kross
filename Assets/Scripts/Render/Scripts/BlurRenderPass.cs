using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class BlurRenderPass : ScriptableRenderPass
{
    private Material _material;
    private BlurSettings _settings;

    private static readonly int s_BlurOffsetId = Shader.PropertyToID("_BlurOffset");
    private static readonly int s_GlobalBlurTextureId = Shader.PropertyToID("_GlobalBlurTexture");

    private const int k_DownsamplePass = 0;
    private const int k_UpsamplePass = 1;

    public BlurRenderPass(Material material, BlurSettings settings)
    {
        _material = material;
        _settings = settings;
        profilingSampler = new ProfilingSampler("DualKawaseBlur");
    }

    public void UpdateSettings(BlurSettings settings)
    {
        _settings = settings;
    }

    // ------------------------------------------------------------------
    // Pass data for Render Graph
    // ------------------------------------------------------------------
    private class BlitPassData
    {
        public TextureHandle source;
        public Material material;
        public int passIndex;
        public float offset;
    }

    private class GlobalTexturePassData
    {
        public TextureHandle blurTexture;
    }

    // ------------------------------------------------------------------
    // Render Graph recording
    // ------------------------------------------------------------------
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_material == null || _settings.iterations < 1)
            return;

        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();

        TextureHandle source = resourceData.activeColorTexture;

        if (!source.IsValid())
            return;

        int baseWidth = cameraData.cameraTargetDescriptor.width / _settings.initialDownscale;
        int baseHeight = cameraData.cameraTargetDescriptor.height / _settings.initialDownscale;
        baseWidth = Mathf.Max(1, baseWidth);
        baseHeight = Mathf.Max(1, baseHeight);

        int iterations = _settings.iterations;

        // ------------------------------------------------------------------
        // Calculate sizes for the mip chain
        // ------------------------------------------------------------------
        int[] mipWidths = new int[iterations + 1];
        int[] mipHeights = new int[iterations + 1];
        mipWidths[0] = baseWidth;
        mipHeights[0] = baseHeight;

        for (int i = 1; i <= iterations; i++)
        {
            mipWidths[i] = Mathf.Max(1, mipWidths[i - 1] / 2);
            mipHeights[i] = Mathf.Max(1, mipHeights[i - 1] / 2);
        }

        // ------------------------------------------------------------------
        // Allocate downsample textures (mip 1 .. iterations)
        // ------------------------------------------------------------------
        TextureHandle[] downTextures = new TextureHandle[iterations];
        for (int i = 0; i < iterations; i++)
        {
            var desc = new TextureDesc(mipWidths[i + 1], mipHeights[i + 1])
            {
                colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"_BlurDown{i}"
            };
            downTextures[i] = renderGraph.CreateTexture(desc);
        }

        // ------------------------------------------------------------------
        // Allocate upsample textures
        // Index 0 = same size as last downsample (smallest)
        // Last index = base resolution (full size blur result)
        // ------------------------------------------------------------------
        TextureHandle[] upTextures = new TextureHandle[iterations];
        for (int i = 0; i < iterations; i++)
        {
            // Upsample goes from smallest back to base resolution
            // i=0 -> same as mip[iterations] (smallest)
            // i=iterations-1 -> mip[1] size... but we want final = base res
            int upMipIdx = iterations - i;
            int texW, texH;

            if (i == iterations - 1)
            {
                // Final upsample = full camera resolution (not base, full!)
                texW = cameraData.cameraTargetDescriptor.width;
                texH = cameraData.cameraTargetDescriptor.height;
            }
            else
            {
                texW = mipWidths[upMipIdx - 1];
                texH = mipHeights[upMipIdx - 1];
            }

            var desc = new TextureDesc(texW, texH)
            {
                colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"_BlurUp{i}"
            };
            upTextures[i] = renderGraph.CreateTexture(desc);
        }

        // ------------------------------------------------------------------
        // DOWNSAMPLE PASSES
        // ------------------------------------------------------------------
        TextureHandle currentSource = source;
        for (int i = 0; i < iterations; i++)
        {
            currentSource = AddBlitPass(renderGraph, currentSource, downTextures[i],
                k_DownsamplePass, _settings.offset, $"Blur Down {i}");
        }

        // ------------------------------------------------------------------
        // UPSAMPLE PASSES
        // ------------------------------------------------------------------
        currentSource = downTextures[iterations - 1];
        for (int i = 0; i < iterations; i++)
        {
            currentSource = AddBlitPass(renderGraph, currentSource, upTextures[i],
                k_UpsamplePass, _settings.offset, $"Blur Up {i}");
        }

        // ------------------------------------------------------------------
        // SET GLOBAL TEXTURE via Unsafe pass
        // (RasterRenderPass cannot set global textures reliably in RG)
        // ------------------------------------------------------------------
        TextureHandle finalBlur = upTextures[iterations - 1];

        using (var builder = renderGraph.AddUnsafePass<GlobalTexturePassData>("Set _GlobalBlurTexture", out var passData))
        {
            passData.blurTexture = finalBlur;
            builder.UseTexture(finalBlur, AccessFlags.Read);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((GlobalTexturePassData data, UnsafeGraphContext ctx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                cmd.SetGlobalTexture(s_GlobalBlurTextureId, data.blurTexture);
            });
        }
    }

    // ------------------------------------------------------------------
    // Helper: single blit pass
    // ------------------------------------------------------------------
    private TextureHandle AddBlitPass(
        RenderGraph renderGraph,
        TextureHandle source,
        TextureHandle destination,
        int passIndex,
        float offset,
        string passName)
    {
        using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(passName, out var passData))
        {
            passData.source = source;
            passData.material = _material;
            passData.passIndex = passIndex;
            passData.offset = offset;

            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((BlitPassData data, RasterGraphContext ctx) =>
            {
                data.material.SetFloat(s_BlurOffsetId, data.offset);
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
            });
        }

        return destination;
    }
}
