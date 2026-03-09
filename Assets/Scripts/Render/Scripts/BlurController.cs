using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Runtime controller for the Dual Kawase Blur effect.
/// Attach to any GameObject to control blur at runtime.
/// 
/// Usage:
///   BlurController.Instance.SetBlur(true, 6, 1.5f);
///   BlurController.Instance.SetBlur(false);
/// </summary>
[ExecuteAlways]
public class BlurController : MonoBehaviour
{
    public static BlurController Instance { get; private set; }

    [Header("Blur Settings")]
    [Tooltip("Enable/disable the blur effect")]
    public bool enableBlur = true;

    [Range(1, 12)]
    [Tooltip("Number of blur iterations. Higher = stronger blur.")]
    public int iterations = 4;

    [Range(0.5f, 3.0f)]
    [Tooltip("Blur offset per sample.")]
    public float offset = 1.0f;

    [Range(1, 4)]
    [Tooltip("Initial downscale factor. 2 = start at half resolution (faster).")]
    public int initialDownscale = 1;

    [Header("Animation")]
    [Tooltip("Smoothly transition blur on/off")]
    public bool animateTransition = false;
    public float transitionSpeed = 5.0f;

    private BlurRendererFeature _blurFeature;
    private float _currentIterationFloat;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        FindBlurFeature();
        _currentIterationFloat = enableBlur ? iterations : 0;
    }

    private void Update()
    {
        if (_blurFeature == null)
        {
            FindBlurFeature();
            if (_blurFeature == null) return;
        }

        if (animateTransition)
        {
            float target = enableBlur ? iterations : 0;
            _currentIterationFloat = Mathf.MoveTowards(_currentIterationFloat, target, transitionSpeed * Time.deltaTime);
            int currentIter = Mathf.RoundToInt(_currentIterationFloat);

            _blurFeature.settings.iterations = Mathf.Max(1, currentIter);
            _blurFeature.SetActive(enableBlur && currentIter > 0);
        }
        else
        {
            _blurFeature.settings.iterations = iterations;
            _blurFeature.SetActive(enableBlur);
        }

        _blurFeature.settings.offset = offset;
        _blurFeature.settings.initialDownscale = initialDownscale;
    }

    /// <summary>
    /// Set blur parameters at runtime.
    /// </summary>
    public void SetBlur(bool enabled, int blurIterations = -1, float blurOffset = -1f)
    {
        enableBlur = enabled;
        if (blurIterations > 0) iterations = blurIterations;
        if (blurOffset > 0) offset = blurOffset;
    }

    private void FindBlurFeature()
    {
        var urpAsset = UniversalRenderPipeline.asset;
        if (urpAsset == null) return;

        var rendererDataList = urpAsset.rendererDataList;
        if (rendererDataList == null) return;

        foreach (var rendererData in rendererDataList)
        {
            if (rendererData == null) continue;
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is BlurRendererFeature blurFeature)
                {
                    _blurFeature = blurFeature;
                    return;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
