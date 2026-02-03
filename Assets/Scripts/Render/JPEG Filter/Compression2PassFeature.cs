using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class Compression2PassFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;

        [Header("Materials")]
        public Material pass1Compress;   // Hidden/Compression/Pass1_Jpegish
        public Material pass2Temporal;   // Hidden/Compression/Pass2_Temporal

        [Header("History")]
        [Tooltip("Формат history RT. ARGB32 обычно ок.")]
        public RenderTextureFormat historyFormat = RenderTextureFormat.ARGB32;

        [Tooltip("Если хочешь дешевле, можно хранить history в половинке разрешения")]
        public bool halfResHistory = false;

        [Tooltip("Имя текстуры history для шейдера Pass2")]
        public string historyTexName = "_CompHistoryTex";
    }
    static readonly int _SourceTexId = Shader.PropertyToID("_SourceTex");
    public Settings settings = new Settings();

    CompressionRGPass _pass;

    public override void Create()
    {
        _pass = new CompressionRGPass(settings)
        {
            renderPassEvent = settings.passEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.pass1Compress == null || settings.pass2Temporal == null)
            return;

        renderer.EnqueuePass(_pass);
    }

    // ===== Camera History Item =====
    public class CompressionHistoryType : CameraHistoryItem
    {
        int _id;
        Hash128 _descKey;
        public bool valid;

        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            _id = MakeId(0);
            valid = false;
        }

        public override void Reset()
        {
            valid = false;
            ReleaseHistoryFrameRT(_id);
        }

        public RTHandle currentTexture => GetCurrentFrameRT(_id);
        public RTHandle previousTexture => GetPreviousFrameRT(_id);

        public void Update(RenderTextureDescriptor desc, RenderTextureFormat fmt, bool halfRes)
        {
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.colorFormat = fmt;

            if (halfRes)
            {
                desc.width = Mathf.Max(1, desc.width / 2);
                desc.height = Mathf.Max(1, desc.height / 2);
            }

            var key = Hash128.Compute(ref desc);

            // If descriptor changed, drop old history and mark invalid
            if (currentTexture != null && _descKey != key)
            {
                valid = false;
                ReleaseHistoryFrameRT(_id);
            }

            if (currentTexture == null)
            {
                AllocHistoryFrameRT(_id, 2, ref desc, "CompressionHistory");
                _descKey = key;
                valid = false;
            }
        }
    }

    // ===== RenderGraph Pass =====
    class CompressionRGPass : ScriptableRenderPass
    {
        readonly Settings _s;
        readonly int _histNameId;

        static readonly int _HistValidId = Shader.PropertyToID("_CompHistoryValid");
        static readonly Vector4 kScaleBias = new Vector4(1, 1, 0, 0);

        public CompressionRGPass(Settings s)
        {
            _s = s;
            _histNameId = Shader.PropertyToID(string.IsNullOrEmpty(_s.historyTexName) ? "_CompHistoryTex" : _s.historyTexName);
        }

        class Pass1Data
        {
            public Material mat;
            public TextureHandle src;
        }

        class Pass2Data
        {
            public Material mat;
            public TextureHandle srcTemp;
            public TextureHandle histPrev;
            public int histNameId;

            public int histValidId;
            public float histValid;
        }

        class CopyData
        {
            public TextureHandle src;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_s.pass1Compress == null || _s.pass2Temporal == null)
                return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            // No history manager? no party.
            if (cameraData.historyManager == null)
                return;

            // Request / get history
            cameraData.historyManager.RequestAccess<CompressionHistoryType>();
            var history = cameraData.historyManager.GetHistoryForWrite<CompressionHistoryType>();
            if (history == null)
                return;

            // Allocate/update history RTs
            var camDesc = cameraData.cameraTargetDescriptor;
            history.Update(camDesc, _s.historyFormat, _s.halfResHistory);

            // Import external RTHandles into RG
            TextureHandle histCur  = renderGraph.ImportTexture(history.currentTexture);
            TextureHandle histPrev = renderGraph.ImportTexture(history.previousTexture);

            // Temp (full res) for pass1 output
            var tempDesc = new TextureDesc(camDesc.width, camDesc.height)
            {
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                colorFormat = camDesc.graphicsFormat,
                name = "CompressionTemp"
            };
            TextureHandle temp = renderGraph.CreateTexture(tempDesc);

            // Decide if we can use history this frame
            bool useHistory = history.valid;

            // ---------------- Pass1: activeColor -> temp ----------------
            using (var builder = renderGraph.AddRasterRenderPass<Pass1Data>("Compression Pass1 (JPEG-ish)", out var passData))
            {
                passData.mat = _s.pass1Compress;
                passData.src = resourceData.activeColorTexture;

                builder.UseTexture(passData.src, AccessFlags.Read);
                builder.SetRenderAttachment(temp, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (Pass1Data data, RasterGraphContext ctx) =>
                {
                    data.mat.SetTexture(_SourceTexId, data.src);
                    Blitter.BlitTexture(ctx.cmd, data.src, kScaleBias, data.mat, 0);
                });
            }

            // ---------------- Pass2: temp + histPrev -> activeColor ----------------
            using (var builder = renderGraph.AddRasterRenderPass<Pass2Data>("Compression Pass2 (Temporal)", out var passData))
            {
                passData.mat = _s.pass2Temporal;
                passData.srcTemp = temp;
                passData.histPrev = histPrev;
                passData.histNameId = _histNameId;

                passData.histValidId = _HistValidId;
                passData.histValid = useHistory ? 1f : 0f;

                builder.UseTexture(passData.srcTemp, AccessFlags.Read);
                builder.UseTexture(passData.histPrev, AccessFlags.Read);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (Pass2Data data, RasterGraphContext ctx) =>
                {
                    data.mat.SetTexture(_SourceTexId, data.srcTemp);
                    data.mat.SetTexture(data.histNameId, data.histPrev);
                    data.mat.SetFloat(data.histValidId, data.histValid);

                    Blitter.BlitTexture(ctx.cmd, data.srcTemp, kScaleBias, data.mat, 0);
                });
            }

            // ---------------- Copy: activeColor -> histCur (write history) ----------------
            using (var builder = renderGraph.AddRasterRenderPass<CopyData>("Compression History Copy", out var passData))
            {
                passData.src = resourceData.activeColorTexture;

                builder.UseTexture(passData.src, AccessFlags.Read);
                builder.SetRenderAttachment(histCur, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (CopyData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.src, kScaleBias, 0, false);
                });
            }

            // Mark history ready for next frame
            history.valid = true;
        }
    }
}
