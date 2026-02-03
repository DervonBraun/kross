using System;
using Player.EffectSystem;
using UnityEngine;

namespace Player
{
    public class PlayerContext : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private InputReader _inputReader;
        [SerializeField] private PlayerConfig _playerConfig;
        [SerializeField] private InputFilter _inputFilter;
        [SerializeField] private Motor _motor;
        [SerializeField] private PlayerCameraRig _playerCameraRig;
        [SerializeField] private CursorController _cursorController;
        [SerializeField] private InteractionController _interactionController;
        [SerializeField] private InteractionDetector _interactionDetector;
        [SerializeField] private Camera _camera;
        [SerializeField] private EffectManager _effectManager;

        public InputReader InputReader => _inputReader;
        public PlayerConfig PlayerConfig => _playerConfig;
        public InputFilter InputFilter => _inputFilter;
        public Motor Motor => _motor;
        public PlayerCameraRig CameraRig => _playerCameraRig;
        public CursorController CursorController => _cursorController;
        public InteractionController InteractionController => _interactionController;
        public InteractionDetector InteractionDetector => _interactionDetector;
        public Camera Camera => _camera;
        public EffectManager EffectManager => _effectManager;

        public CameraState CameraState { get; private set; } = CameraState.Default;

        public event Action<CameraState> CameraStateChanged;

        private void Awake()
        {
            if (_motor == null) _motor = GetComponentInChildren<Motor>();
            if (_playerCameraRig == null) _playerCameraRig = GetComponentInChildren<PlayerCameraRig>();
            if (_inputFilter == null) _inputFilter = GetComponentInChildren<InputFilter>();
            if (_cursorController == null) _cursorController = GetComponentInChildren<CursorController>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            UpdateCameraState();

            _motor.Tick(dt);
            _playerCameraRig.Tick(dt);
        }

        private void UpdateCameraState()
        {
            bool aimHeld = _inputFilter.Aim();
            bool canAim = _playerConfig != null && _playerConfig.CanLook;

            var desiredState = (canAim && aimHeld)
                ? CameraState.Aim
                : CameraState.Default;

            SetCameraState(desiredState);
        }

        private void SetCameraState(CameraState newState)
        {
            if (CameraState == newState)
                return;

            CameraState = newState;
            CameraStateChanged?.Invoke(CameraState);
        }
    }
}
