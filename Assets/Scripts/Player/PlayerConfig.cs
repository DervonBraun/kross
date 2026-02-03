using UnityEngine;

namespace Player
{
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Player")]
    public class PlayerConfig : ScriptableObject
    {
        [Header("InputFilter")]
        [SerializeField] private bool _canMove = true;
        [SerializeField] private bool _canSprint = true;
        [SerializeField] private bool _canLook = true;
        [SerializeField] private bool _canAim = true;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 10f;
        [SerializeField, Min(0.01f)] private float _acceleration = 10f;
        [SerializeField, Min(0.01f)] private float _deceleration = 10f;
        [SerializeField, Min(0.01f)] private float _accelerationTime = 0.12f;
        [SerializeField, Min(0.01f)] private float _decelerationTime = 0.12f;
        [SerializeField] private AnimationCurve _accelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve _decelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Look")]
        [SerializeField] private float _lookSensitivity = 1.0f;
        [SerializeField] private bool _invertY;

        [Tooltip("Границы pitch (в градусах), чтобы не ломать шею")]
        [SerializeField] private Vector2 _pitchClamp = new Vector2(-85f, 85f);

        [Tooltip("Время сглаживания поворота камеры (сек)")]
        [SerializeField, Min(0.0f)] private float _lookSmoothTime = 0.08f;

        [Tooltip("Профиль сглаживания 0..1 -> 0..1 (чем выше, тем быстрее догоняем)")]
        [SerializeField] private AnimationCurve _lookSmoothCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Ограничение скорости поворота (deg/sec). 0 = без ограничения")]
        [SerializeField, Min(0f)] private float _lookMaxSpeed;
        
        [Header("Aim")]
        [SerializeField] private float _aimFov = 55f;
        [SerializeField] private float _aimDefaultFov = 75f;
        [SerializeField] private Vector3 _aimCameraOffset = new Vector3(0.06f, -0.03f, 0.0f);
        [SerializeField, Min(0.01f)] private float _aimInTime = 0.08f;
        [SerializeField, Min(0.01f)] private float _aimOutTime = 0.10f;
        [SerializeField] private AnimationCurve _aimBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Header("HeadBob")]
        [SerializeField] private bool _headBobEnabled = true;

        [Tooltip("Амплитуда bob (X=вбок, Y=вверх) в local space камеры")]
        [SerializeField] private Vector2 _headBobAmplitude = new Vector2(0.03f, 0.04f);

        [Tooltip("Базовая частота (шаги)")]
        [SerializeField, Min(0f)] private float _headBobFrequency = 1.8f;

        [Tooltip("Множитель частоты на максимальной скорости (1 = без изменения)")]
        [SerializeField, Min(0f)] private float _headBobFrequencyAtMaxSpeedMul = 1.25f;

        [Tooltip("Порог скорости (0..1), ниже которого bob выключен")]
        [SerializeField, Range(0f, 1f)] private float _headBobMinSpeed01 = 0.08f;

        [Tooltip("Время входа bob при начале движения")]
        [SerializeField, Min(0.01f)] private float _headBobInTime = 0.10f;

        [Tooltip("Время выхода bob при остановке")]
        [SerializeField, Min(0.01f)] private float _headBobOutTime = 0.12f;

        [Tooltip("Интенсивность bob от скорости (X=speed01, Y=intensity)")]
        [SerializeField] private AnimationCurve _headBobIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Header("Interaction")]
        [SerializeField] private float _maxDistance = 5f;
        [SerializeField] private float _sphereRadius = 0.15f;
        [SerializeField] private LayerMask _interactionMask = ~0;

        public bool HeadBobEnabled => _headBobEnabled;
        public Vector2 HeadBobAmplitude => _headBobAmplitude;
        public float HeadBobFrequency => _headBobFrequency;
        public float HeadBobFrequencyAtMaxSpeedMul => _headBobFrequencyAtMaxSpeedMul;
        public float HeadBobMinSpeed01 => _headBobMinSpeed01;
        public float HeadBobInTime => _headBobInTime;
        public float HeadBobOutTime => _headBobOutTime;
        public AnimationCurve HeadBobIntensityCurve => _headBobIntensityCurve;

        
        public float AimFov => _aimFov;
        public float AimDefaultFov => _aimDefaultFov;
        public Vector3 AimCameraOffset => _aimCameraOffset;
        public float AimInTime => _aimInTime;
        public float AimOutTime => _aimOutTime;
        public AnimationCurve AimBlendCurve => _aimBlendCurve;

        public bool CanMove => _canMove;
        public bool CanSprint => _canSprint;
        public bool CanLook => _canLook;
        public bool CanAim => _canAim;

        public float MoveSpeed => _moveSpeed;
        public float Acceleration => _acceleration;
        public float Deceleration => _deceleration;
        public float AccelerationTime => _accelerationTime; // (опечатка не нужна, оставляю как предупреждение мира)
        public float DecelerationTime => _decelerationTime;
        public AnimationCurve AccelerationCurve => _accelCurve;
        public AnimationCurve DecelerationCurve => _decelCurve;

        public float LookSensitivity => _lookSensitivity;
        public bool InvertY => _invertY;
        public Vector2 PitchClamp => _pitchClamp;
        public float LookSmoothTime => _lookSmoothTime;
        public AnimationCurve LookSmoothCurve => _lookSmoothCurve;
        public float LookMaxSpeed => _lookMaxSpeed;
        
        public float MaxDistance => _maxDistance;
        public float SphereRadius => _sphereRadius;
        public LayerMask InteractionMask => _interactionMask;
        
    }
}
