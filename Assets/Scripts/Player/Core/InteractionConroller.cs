using System;
using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(PlayerContext))]
    public class InteractionController : MonoBehaviour
    {
        public event Action<CameraState> CameraStateChanged;
        public event Action<bool> DefaultInteractableChanged; // UI для прицела
        public event Action<bool> AimInteractableChanged;     // пригодится для Lens/индикаторов

        public bool HasDefaultInteractable { get; private set; }
        public bool HasAimInteractable { get; private set; }

        private InteractionDetector _detector;
        private InputFilter _inputFilter;
        private PlayerContext _context;

        private Component _currentTarget;

        private void Awake()
        {
            _context = GetComponent<PlayerContext>();
            _detector = _context.InteractionDetector;
            _inputFilter = _context.InputFilter;

            if (_detector != null)
                _detector.TargetChanged += OnTargetChanged;

            if (_context != null)
                _context.CameraStateChanged += OnCameraStateChanged;

            // стартовое состояние
            OnCameraStateChanged(_context.CameraState);
            RecomputeInteractables();
        }

        private void OnDestroy()
        {
            if (_detector != null)
                _detector.TargetChanged -= OnTargetChanged;

            if (_context != null)
                _context.CameraStateChanged -= OnCameraStateChanged;
        }

        private void Update()
        {
            if (_currentTarget == null) return;
            if (_inputFilter == null) return;
            if (!_inputFilter.InteractPressed()) return; // важно: одно нажатие = один интеракт

            if (_context.CameraState == CameraState.Aim)
                TryAimInteract(_currentTarget);
            else
                TryDefaultInteract(_currentTarget);

            // после интеракции состояние "можно/нельзя" могло поменяться (например, дверь открылась/закрылась)
            RecomputeInteractables();
        }

        private void OnCameraStateChanged(CameraState state)
        {
            CameraStateChanged?.Invoke(state);

            // В Aim прицел будет скрыт, но мы всё равно можем поддерживать флаги актуальными.
            RecomputeInteractables();
        }

        private void OnTargetChanged(Component target)
        {
            _currentTarget = target;
            RecomputeInteractables();
        }

        private void RecomputeInteractables()
        {
            // target может быть null
            bool canDefault = false;
            bool canAim = false;

            if (_currentTarget != null)
            {
                var def = _currentTarget.GetComponentInParent<IInteractableDefault>();
                if (def != null) canDefault = def.CanInteractDefault(_context);

                var aim = _currentTarget.GetComponentInParent<IInteractableAim>();
                if (aim != null) canAim = aim.CanInteractAim(_context);
            }

            SetDefaultFlag(canDefault);
            SetAimFlag(canAim);
        }

        private void SetDefaultFlag(bool value)
        {
            if (HasDefaultInteractable == value) return;
            HasDefaultInteractable = value;
            DefaultInteractableChanged?.Invoke(value);
        }

        private void SetAimFlag(bool value)
        {
            if (HasAimInteractable == value) return;
            HasAimInteractable = value;
            AimInteractableChanged?.Invoke(value);
        }

        private void TryAimInteract(Component target)
        {
            var aim = target.GetComponentInParent<IInteractableAim>();
            if (aim == null) return;
            if (!aim.CanInteractAim(_context)) return;
            aim.InteractAim(_context);
        }

        private void TryDefaultInteract(Component target)
        {
            var def = target.GetComponentInParent<IInteractableDefault>();
            if (def == null) return;
            if (!def.CanInteractDefault(_context)) return;
            def.InteractDefault(_context);
        }
    }
}
