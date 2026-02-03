using UnityEngine;

namespace Player
{
    public class PlayerCameraRig : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform _yawPivot;
        [SerializeField] private Transform _pitchPivot;

        [Tooltip("Transform камеры (или её holder), который двигаем bob'ом/aim offset")]
        [SerializeField] private Transform _cameraRoot;

        private Camera _cam;

        private PlayerContext _ctx;
        private PlayerConfig _cfg;
        private InputFilter _input;

        private float _yaw;
        private float _pitch;

        // сглаживаем ВВОД, а не угол
        private Vector2 _lookVelocity;
        private Vector2 _smoothedLook;

        // base pose
        private Vector3 _baseCamLocalPos;
        private float _baseFov;

        // modules
        private HeadBobModule _headBob = new HeadBobModule();
        private AimModule _aim;

        // cached per-frame offsets (чтобы эффекты складывались, а не перетирались)
        private Vector3 _bobOffset;
        private Vector3 _aimOffset;

        private void Awake()
        {
            var ctx = GetComponentInParent<PlayerContext>();
            if (ctx != null) Initialize(ctx);
        }

        public void Initialize(PlayerContext context)
        {
            _ctx = context;
            _cam = _ctx.Camera;
            _cfg = _ctx.PlayerConfig;
            _input = _ctx.InputFilter;

            // 1) связать _cam и _cameraRoot один раз, без рассинхрона
            if (_cam == null)
                _cam = GetComponentInChildren<Camera>(true);

            if (_cameraRoot == null)
                _cameraRoot = (_cam != null) ? _cam.transform : null;

            // 2) базовые углы
            _yaw = _yawPivot.eulerAngles.y;

            float p = _pitchPivot.localEulerAngles.x;
            if (p > 180f) p -= 360f;
            _pitch = p;

            _lookVelocity = Vector2.zero;
            _smoothedLook = Vector2.zero;

            // 3) base pose
            if (_cameraRoot != null)
                _baseCamLocalPos = _cameraRoot.localPosition;

            if (_cam != null)
                _baseFov = _cam.fieldOfView;

            // 4) modules init
            _headBob.Reset();
            _aim = new AimModule();     // <-- ВОТ ТУТ и был твой NRE
            _aim.Reset();

            _bobOffset = Vector3.zero;
            _aimOffset = Vector3.zero;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (_cfg == null) return;

            TickLook(dt);
            TickHeadBob(dt);
            TickAim(dt);

            ApplyCameraPose();
        }

        private void TickLook(float dt)
        {
            if (!_cfg.CanLook) return;

            Vector2 rawLook = _input.Look();
            rawLook *= _cfg.LookSensitivity;
            rawLook.y *= _cfg.InvertY ? 1f : -1f;

            float smoothTime = Mathf.Max(0f, _cfg.LookSmoothTime);

            if (smoothTime > 0.0001f)
            {
                _smoothedLook = Vector2.SmoothDamp(
                    _smoothedLook,
                    rawLook,
                    ref _lookVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    dt
                );
            }
            else
            {
                _smoothedLook = rawLook;
            }

            _yaw += _smoothedLook.x;
            _pitch += _smoothedLook.y;

            Vector2 clamp = _cfg.PitchClamp;
            _pitch = Mathf.Clamp(_pitch, clamp.x, clamp.y);

            _yawPivot.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void TickAim(float dt)
        {
            if (_cameraRoot == null || _cam == null || _aim == null)
            {
                _aimOffset = Vector3.zero;
                return;
            }

            CameraState state = (_ctx != null) ? _ctx.CameraState : CameraState.Default;

            AimResult r = _aim.Evaluate(state, _cfg, dt);
            _aimOffset = r.CameraLocalOffset;

            // FOV тоже применяем “через базу”, чтобы не накапливать дрейф
            _cam.fieldOfView = r.Fov;
        }

        private void TickHeadBob(float dt)
        {
            if (_cameraRoot == null)
            {
                _bobOffset = Vector3.zero;
                return;
            }

            if (!_cfg.HeadBobEnabled)
            {
                _bobOffset = Vector3.zero;
                return;
            }

            MotorTelemetry t = (_ctx != null && _ctx.Motor != null) ? _ctx.Motor.Telemetry : default;
            _bobOffset = _headBob.Evaluate(t, _cfg, dt);
        }

        private void ApplyCameraPose()
        {
            if (_cameraRoot == null) return;

            // складываем offsets, а не перетираем
            _cameraRoot.localPosition = _baseCamLocalPos + _aimOffset + _bobOffset;

            // если AimModule не трогал FOV (например cam null), возвращаем базу
            if (_cam != null && _aim == null)
                _cam.fieldOfView = _baseFov;
        }

        private sealed class HeadBobModule
        {
            private float _phase;          // radians
            private float _weight;         // 0..1
            private float _weightVel;

            public void Reset()
            {
                _phase = 0f;
                _weight = 0f;
                _weightVel = 0f;
            }

            public Vector3 Evaluate(MotorTelemetry t, PlayerConfig cfg, float dt)
            {
                bool active = t.IsGrounded && t.Speed01 > cfg.HeadBobMinSpeed01 && t.State != PlayerState.Idle;
                float targetWeight = active ? 1f : 0f;

                float smoothTime = active ? cfg.HeadBobInTime : cfg.HeadBobOutTime;
                smoothTime = Mathf.Max(0.0001f, smoothTime);

                _weight = Mathf.SmoothDamp(_weight, targetWeight, ref _weightVel, smoothTime, Mathf.Infinity, dt);
                _weight = Mathf.Clamp01(_weight);

                if (_weight <= 0.0001f)
                    return Vector3.zero;

                float speedK = Evaluate01(cfg.HeadBobIntensityCurve, t.Speed01);

                float freq = Mathf.Max(0f, cfg.HeadBobFrequency);
                float freqK = Mathf.Lerp(1f, cfg.HeadBobFrequencyAtMaxSpeedMul, speedK);
                float omega = freq * freqK * Mathf.PI * 2f;

                _phase += omega * dt;
                if (_phase > 100000f) _phase = Mathf.Repeat(_phase, Mathf.PI * 2f);

                float sin = Mathf.Sin(_phase);
                float cos = Mathf.Cos(_phase * 2f);

                Vector2 amp = cfg.HeadBobAmplitude;
                float w = _weight * speedK;

                float x = sin * amp.x * w;
                float y = Mathf.Abs(cos) * amp.y * w;

                return new Vector3(x, y, 0f);
            }

            private static float Evaluate01(AnimationCurve curve, float x)
            {
                if (curve == null) return Mathf.Clamp01(x);
                return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(x)));
            }
        }
    }
}
