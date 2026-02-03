using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerContext))]
    public class Motor : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private PlayerState _playerState;
        public PlayerState PlayerState => _playerState;

        [Header("Refs")]
        [SerializeField] private Transform _moveSpace; // yaw pivot камеры

        private PlayerContext _ctx;
        private PlayerConfig _cfg;
        private InputFilter _input;
        private CharacterController _cc;

        // runtime
        private Vector3 _planarVel;     // XZ velocity (m/s)
        private float _verticalVel;     // Y velocity (m/s)
        private float _accelT;          // seconds in accel phase
        private float _decelT;          // seconds in decel phase
        private bool _wasInput;

        private const float InputDeadZone = 0.02f;
        private const float GroundStick = -2.0f;
        private const float Gravity = -25f;

        // коллизии
        private Vector3 _lastHitNormal;
        private float _lastHitTime;
        private const float HitMemory = 0.08f; // небольшой “буфер памяти” чтобы не дрожать
        
        private MotorTelemetry _telemetry;
        public MotorTelemetry Telemetry => _telemetry;

        private void Awake()
        {
            _ctx = GetComponent<PlayerContext>();
            _cc = GetComponent<CharacterController>();

            _input = _ctx.InputFilter;
            _cfg = _ctx.PlayerConfig;

            if (_moveSpace == null) _moveSpace = transform;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            // 1) input -> wish dir world (XZ)
            Vector2 move2 = _input.Move();
            bool hasInput = move2.sqrMagnitude > (InputDeadZone * InputDeadZone);

            Vector3 local = new Vector3(move2.x, 0f, move2.y);
            if (local.sqrMagnitude > 1f) local.Normalize();

            Vector3 fwd = _moveSpace.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = _moveSpace.right; right.y = 0f; right.Normalize();
            Vector3 wishDir = (right * local.x + fwd * local.z);
            if (wishDir.sqrMagnitude > 0.0001f) wishDir.Normalize();

            _playerState = hasInput ? PlayerState.Walk : PlayerState.Idle;

            // 2) фазовые таймеры (сбрасываем только при смене режима)
            if (hasInput && !_wasInput)
            {
                _accelT = 0f;
            }
            else if (!hasInput && _wasInput)
            {
                _decelT = 0f;
            }
            _wasInput = hasInput;

            // 3) ЦЕЛЕВАЯ скорость (вектор) + инерция через кривые
            Vector3 targetVel = hasInput ? wishDir * _cfg.MoveSpeed : Vector3.zero;

            if (hasInput)
            {
                _decelT = 0f;

                // “насколько быстро тянемся к target”
                float accelAlpha = Step01(ref _accelT, _cfg.AccelerationTime, _cfg.AccelerationCurve, dt);
                float accelRate = Mathf.Max(0.001f, _cfg.Acceleration); // базовая “резкость” из SO

                // комбинируем: MoveTowards дает предсказуемую физику, кривая управляет “долей” скорости за кадр
                float maxDelta = accelRate * accelAlpha * dt * _cfg.MoveSpeed; 
                // ^ умножение на MoveSpeed делает шкалу accelRate удобной (можешь убрать, если не нравится)

                _planarVel = Vector3.MoveTowards(_planarVel, targetVel, maxDelta);

                // поворот направления: сглаживаем сам targetVel, но без потери инерции
                // (мы уже двигаем _planarVel к targetVel, так что отдельный Slerp чаще лишний)
            }
            else
            {
                _accelT = 0f;

                float decelAlpha = Step01(ref _decelT, _cfg.DecelerationTime, _cfg.DecelerationCurve, dt);
                float decelRate = Mathf.Max(0.001f, _cfg.Deceleration);

                float maxDelta = decelRate * decelAlpha * dt * _cfg.MoveSpeed;
                _planarVel = Vector3.MoveTowards(_planarVel, Vector3.zero, maxDelta);

                // чистим микроскорость, чтобы не ползал вечность
                if (_planarVel.sqrMagnitude < 0.0001f) _planarVel = Vector3.zero;
            }

            // 4) анти-залипание: если недавно был удар о стену, убираем компонент скорости в стену
            // Это ключевой момент против “липкой инерции”.
            if (Time.time - _lastHitTime <= HitMemory)
            {
                // убираем составляющую, направленную В нормаль (то есть "в стену")
                _planarVel = Vector3.ProjectOnPlane(_planarVel, _lastHitNormal);
            }

            // 5) гравитация
            if (_cc.isGrounded && _verticalVel < 0f)
                _verticalVel = GroundStick;
            else
                _verticalVel += Gravity * dt;

            // 6) Move
            Vector3 velocity = _planarVel + Vector3.up * _verticalVel;
            CollisionFlags flags = _cc.Move(velocity * dt);

            // 7) если уперлись боком прямо сейчас, режем скорость сразу (без ожидания памяти)
            if ((flags & CollisionFlags.Sides) != 0)
            {
                // _lastHitNormal выставится в OnControllerColliderHit, но на всякий случай:
                if (Time.time - _lastHitTime <= HitMemory)
                    _planarVel = Vector3.ProjectOnPlane(_planarVel, _lastHitNormal);

                // если игрок жмет в стену, и скорость уже почти ноль, окончательно гасим, чтобы не дрожало
                if (_planarVel.sqrMagnitude < 0.0004f)
                    _planarVel = Vector3.zero;
            }

            // 8) ограничиваем максимальную скорость, чтобы никакая математика не разогнала выше MoveSpeed
            float max = Mathf.Max(0f, _cfg.MoveSpeed);
            float mag = _planarVel.magnitude;
            if (mag > max)
                _planarVel = _planarVel * (max / mag);
            
            // 9) telemetry
            _telemetry.State = _playerState;
            _telemetry.IsGrounded = _cc.isGrounded;

            _telemetry.PlanarVelocity = _planarVel;
            _telemetry.PlanarSpeed = _planarVel.magnitude;

            float denom = Mathf.Max(0.0001f, _cfg.MoveSpeed);
            _telemetry.Speed01 = Mathf.Clamp01(_telemetry.PlanarSpeed / denom);

        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // интересуют стены, не пол
            Vector3 n = hit.normal;
            if (n.y > 0.5f) return;

            _lastHitNormal = n;
            _lastHitTime = Time.time;
        }

        private static float Step01(ref float t, float duration, AnimationCurve curve, float dt)
        {
            if (duration <= 0.0001f)
            {
                t = duration;
                return 1f;
            }

            t = Mathf.Min(t + dt, duration);
            float x = Mathf.Clamp01(t / duration);

            if (curve == null) return x;
            return Mathf.Clamp01(curve.Evaluate(x));
        }
    }
}
