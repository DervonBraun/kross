using System;
using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(PlayerContext))]
    public class InteractionDetector : MonoBehaviour
    {
        public enum DetectorActiveMode { DefaultOnly, AimOnly, Both }
        [SerializeField] private DetectorActiveMode _activeMode = DetectorActiveMode.Both;
        public event Action<Component> TargetChanged;

        [Header("Detection")]
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;

        private float _maxDistance = 3f;
        private float _sphereRadius = 0.15f;
        private LayerMask _interactionMask = ~0;
        
        [Header("Target Resolve (optional)")]
        [Tooltip("Если true — будет отдавать ближайший родительский компонент, подходящий под интерфейсы (Aim/Default).")]
        [SerializeField] private bool _resolveToInteractableParent = true;

        private PlayerContext _context;
        private PlayerConfig _playerConfig;
        private Camera _camera;

        private Component _currentTarget;
        private bool _active;

        private void Awake()
        {
            _context = GetComponent<PlayerContext>();
            
            _playerConfig = _context.PlayerConfig;
            _maxDistance = _playerConfig.MaxDistance;
            _sphereRadius = _playerConfig.SphereRadius;
            _interactionMask = _playerConfig.InteractionMask;

            // Камера может быть не прокинута на момент Awake — подстрахуемся в Update
            _camera = _context.Camera != null ? _context.Camera : Camera.main;

            _context.CameraStateChanged += OnCameraStateChanged;

            // Инициализируем активность сразу, чтобы не ждать первого события
            OnCameraStateChanged(_context.CameraState);
        }

        private void OnDestroy()
        {
            if (_context != null)
                _context.CameraStateChanged -= OnCameraStateChanged;
        }

        private void OnCameraStateChanged(CameraState state)
        {
            _active = _activeMode == DetectorActiveMode.Both ||
                      (_activeMode == DetectorActiveMode.AimOnly && state == CameraState.Aim) ||
                      (_activeMode == DetectorActiveMode.DefaultOnly && state == CameraState.Default);

            if (!_active)
                ClearTarget(forceNotify: true);
        }

        private void Update()
        {
            if (!_active)
                return;

            // Камера могла смениться/появиться позже
            if (_context.Camera != null) _camera = _context.Camera;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return; // вообще нет камеры — нечего делать

            var target = DetectTarget();

            if (ReferenceEquals(target, _currentTarget))
                return;

            _currentTarget = target;
            TargetChanged?.Invoke(_currentTarget);
        }

        private void ClearTarget(bool forceNotify)
        {
            if (_currentTarget == null)
            {
                if (forceNotify) TargetChanged?.Invoke(null);
                return;
            }

            _currentTarget = null;
            TargetChanged?.Invoke(null);
        }

        private Component DetectTarget()
        {
            Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);

            if (!Physics.SphereCast(
                    ray,
                    _sphereRadius,
                    out RaycastHit hit,
                    _maxDistance,
                    _interactionMask,
                    _triggerInteraction))
            {
                return null;
            }

            // Базовый вариант: вернуть компонент коллайдера (но это часто "кусок" объекта)
            var raw = hit.collider != null ? (Component)hit.collider : null;
            if (raw == null) return null;

            if (!_resolveToInteractableParent)
                return raw;

            // Нормализация: поднимаемся к родителю, который реально интерактивен
            // (под два интерфейса, которые ты хочешь: Default/Aim)
            // Если у тебя пока есть только IInteractableAim — это тоже ок.
            var aim = hit.collider.GetComponentInParent<IInteractableAim>();
            if (aim is Component aimComp) return aimComp;

            var def = hit.collider.GetComponentInParent<IInteractableDefault>();
            if (def is Component defComp) return defComp;

            // Если нет интерфейсов, но маска пропустила — вернём raw (или null, как тебе удобнее).
            // Я возвращаю raw, чтобы ты мог видеть "что именно ловится" при отладке.
            return raw;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_camera == null) return;

            Gizmos.color = _active ? Color.cyan : Color.gray;

            Vector3 origin = _camera.transform.position;
            Vector3 dir = _camera.transform.forward;

            Gizmos.DrawLine(origin, origin + dir * _maxDistance);

            // Покажем сферу в конце луча
            Gizmos.DrawWireSphere(origin + dir * _maxDistance, _sphereRadius);
        }
#endif
    }
}
