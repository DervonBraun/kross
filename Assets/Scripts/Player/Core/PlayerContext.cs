using System;
using AN_;
using EffectSystem;
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
        [SerializeField] private ANService _service;

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
        public ANService Service => _service;

        // Старое
        public CameraState CameraState { get; private set; } = CameraState.Default;
        public event Action<CameraState> CameraStateChanged;

        // Новое: режим игрока
        public PlayerMode Mode { get; private set; } = PlayerMode.Gameplay;
        public event Action<PlayerMode> ModeChanged;

        private void Awake()
        {
            if (_motor == null) _motor = GetComponentInChildren<Motor>();
            if (_playerCameraRig == null) _playerCameraRig = GetComponentInChildren<PlayerCameraRig>();
            if (_inputFilter == null) _inputFilter = GetComponentInChildren<InputFilter>();
            if (_cursorController == null) _cursorController = GetComponentInChildren<CursorController>();

            ApplyMode(Mode); // стартовая синхронизация
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1) Глобальные хоткеи/режимы (инвентарь)
            TickModeHotkeys();

            // 2) Камера-state обновляем только когда Gameplay (иначе Aim будет “липнуть” логически)
            if (Mode == PlayerMode.Gameplay)
                UpdateCameraState();
            else
                SetCameraState(CameraState.Default);

            // 3) Тики. Можно тикать всегда, если Motor/CameraRig берут инпут через InputFilter (там будет ноль).
            _motor.Tick(dt);
            _playerCameraRig.Tick(dt);
        }

        private void TickModeHotkeys()
        {
            // ВАЖНО: InventoryPressed() должен быть завязан на InputFilter,
            // чтобы не ловить “лишнее” в неподходящих режимах, но при этом
            // оставаться доступным для закрытия UI.
            if (_inputFilter != null && _inputFilter.InventoryPressed())
            {
                ToggleInventoryMode();
            }
        }

        private void ToggleInventoryMode()
        {
            var next = Mode == PlayerMode.Gameplay ? PlayerMode.UiInventory : PlayerMode.Gameplay;
            SetMode(next);
        }

        public void SetMode(PlayerMode newMode)
        {
            if (Mode == newMode) return;

            Mode = newMode;
            ApplyMode(newMode);
            ModeChanged?.Invoke(newMode);
        }

        private void ApplyMode(PlayerMode mode)
        {
            // 1) Инпут
            if (_inputFilter != null)
                _inputFilter.ApplyMode(mode);

            // 2) Курсор
            if (_cursorController != null)
            {
                bool ui = mode != PlayerMode.Gameplay;
                _cursorController.SetCursor(ui); 
                // Если у тебя другой API, замени на:
                // Cursor.visible / Cursor.lockState внутри CursorController
            }

            // 3) (Опционально) взаимодействие в мире
            // Не обязательно выключать компоненты, если они используют InputFilter,
            // но если твой InteractionController не гейтится фильтром, то проще так:
            if (_interactionController != null)
                _interactionController.enabled = (mode == PlayerMode.Gameplay);

            if (_interactionDetector != null)
                _interactionDetector.enabled = (mode == PlayerMode.Gameplay);
        }

        private void UpdateCameraState()
        {
            bool aimHeld = _inputFilter != null && _inputFilter.Aim();
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
