using UnityEngine;

namespace Player
{
    public struct AimResult
    {
        public Vector3 CameraLocalOffset;
        public float Fov;
    }

    public sealed class AimModule
    {
        private float _blend;       // 0..1
        private float _blendVel;

        public void Reset()
        {
            _blend = 0f;
            _blendVel = 0f;
        }

        public AimResult Evaluate(CameraState state, PlayerConfig cfg, float dt)
        {
            float target = (state == CameraState.Aim) ? 1f : 0f;

            // Время входа/выхода разное, чтобы можно было делать “быстрый прицел, медленный выход” и наоборот
            float smoothTime = (target > _blend) ? cfg.AimInTime : cfg.AimOutTime;
            smoothTime = Mathf.Max(0.0001f, smoothTime);

            _blend = Mathf.SmoothDamp(_blend, target, ref _blendVel, smoothTime, Mathf.Infinity, dt);
            _blend = Mathf.Clamp01(_blend);

            float k = Evaluate01(cfg.AimBlendCurve, _blend);

            AimResult r;
            r.CameraLocalOffset = Vector3.Lerp(Vector3.zero, cfg.AimCameraOffset, k);
            r.Fov = Mathf.Lerp(cfg.AimDefaultFov, cfg.AimFov, k);
            return r;
        }

        private static float Evaluate01(AnimationCurve curve, float x)
        {
            if (curve == null) return Mathf.Clamp01(x);
            return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(x)));
        }
    }
}