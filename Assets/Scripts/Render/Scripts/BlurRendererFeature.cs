using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public class BlurSettings
{
    [Tooltip("Number of downsample/upsample iterations. More = stronger blur. 3-8 is typical.")]
    [Range(1, 12)]
    public int iterations = 4;

    [Tooltip("Offset multiplier for the blur kernel. Higher = wider blur per iteration.")]
    [Range(0.5f, 3f)]
    public float offset = 1.0f;

    [Tooltip("Initial downscale factor before blur begins. 2 = start at half resolution.")]
    [Range(1, 4)]
    public int initialDownscale = 1;

    [Tooltip("When to capture the screen for blurring.")]
    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
}

public class BlurRendererFeature : ScriptableRendererFeature
{
    public BlurSettings settings = new BlurSettings();

    private BlurRenderPass _blurPass;
    private Material _blurMaterial;

    private static readonly string k_ShaderName = "Hidden/DualKawaseBlur";

    public override void Create()
    {
        var shader = Shader.Find(k_ShaderName);
        if (shader == null)
        {
            Debug.LogError($"BlurRendererFeature: Cannot find shader '{k_ShaderName}'. " +
                           "Make sure DualKawaseBlur.shader is in your project.");
            return;
        }

        _blurMaterial = CoreUtils.CreateEngineMaterial(shader);
        _blurPass = new BlurRenderPass(_blurMaterial, settings);
        _blurPass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_blurMaterial == null || _blurPass == null)
            return;

        if (renderingData.cameraData.isPreviewCamera)
            return;

        _blurPass.UpdateSettings(settings);
        renderer.EnqueuePass(_blurPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _blurMaterial != null)
        {
            CoreUtils.Destroy(_blurMaterial);
            _blurMaterial = null;
        }
    }
}
