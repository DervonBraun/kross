using UnityEngine;

public class CompressionMotionDriver : MonoBehaviour
{
    [Header("Motion → Quality")]
    [Tooltip("deg/sec ниже этого считаем 'почти стоим'")]
    public float thresholdDegPerSec = 30f;

    [Tooltip("deg/sec при котором motion=1")]
    public float fullDegPerSec = 240f;

    [Tooltip("Насколько быстро ухудшаем качество при резком движении")]
    public float riseSpeed = 12f;

    [Tooltip("Насколько быстро восстанавливаем качество")]
    public float recoverSpeed = 2.5f;

    [Header("Keyframe Pulse")]
    [Tooltip("Каждые N секунд даём 'ключевой кадр' (очистка артефактов)")]
    public float keyframeEverySeconds = 1.25f;

    [Tooltip("Длительность импульса (сек). 0.03 ≈ 2 кадра на 60fps")]
    public float keyframePulseSeconds = 0.03f;

    [Header("Shader Globals")]
    public string motionParam = "_CompMotion";
    public string keyframeParam = "_CompKeyframe";

    Quaternion _prevRot;
    float _motion;
    float _keyframeT;
    float _keyframeTimer;

    void OnEnable()
    {
        _prevRot = transform.rotation;
        _motion = 0f;
        _keyframeT = 0f;
        _keyframeTimer = 0f;

        Shader.SetGlobalFloat(motionParam, 0f);
        Shader.SetGlobalFloat(keyframeParam, 0f);
    }

    void Update()
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-6f);

        // Angular speed (deg/sec)
        Quaternion cur = transform.rotation;
        float ang = Quaternion.Angle(_prevRot, cur); // degrees per frame
        float degPerSec = ang / dt;
        _prevRot = cur;

        // Map to 0..1
        float target = Mathf.InverseLerp(thresholdDegPerSec, fullDegPerSec, degPerSec);
        target = Mathf.Clamp01(target);

        // Fast rise, slow recover
        float speed = (target > _motion) ? riseSpeed : recoverSpeed;
        _motion = Mathf.MoveTowards(_motion, target, speed * dt);

        // Keyframe pulse (optional)
        _keyframeTimer += dt;
        if (keyframeEverySeconds > 0f && _keyframeTimer >= keyframeEverySeconds)
        {
            _keyframeTimer = 0f;
            _keyframeT = keyframePulseSeconds;
        }

        if (_keyframeT > 0f)
        {
            _keyframeT -= dt;
            Shader.SetGlobalFloat(keyframeParam, 1f);
        }
        else
        {
            Shader.SetGlobalFloat(keyframeParam, 0f);
        }

        Shader.SetGlobalFloat(motionParam, _motion);
    }
}
