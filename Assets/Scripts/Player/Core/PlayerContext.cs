using System;
using AN_;
using EffectSystem;
using UI.Flow;
using UnityEngine;

namespace Player
{
    public class PlayerContext : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private InputReader           _inputReader;
        [SerializeField] private PlayerConfig          _playerConfig;
        [SerializeField] private InputFilter           _inputFilter;
        [SerializeField] private Motor                 _motor;
        [SerializeField] private PlayerCameraRig       _playerCameraRig;
        [SerializeField] private CursorController      _cursorController;
        [SerializeField] private InteractionController _interactionController;
        [SerializeField] private InteractionDetector   _interactionDetector;
        [SerializeField] private Camera                _camera;
        [SerializeField] private EffectManager         _effectManager;
        [SerializeField] private ANService             _service;

        [Header("UI")]
        [Tooltip("Назначается через WebUIManagerBootstrap. Можно также вручную.")]
        [SerializeField] private WebUIManager _webUIManager;

        // ── Hotkey page ids ───────────────────────────────────────────────────
        // Вынесены в Inspector чтобы не хардкодить строки в коде.
        [Header("Hotkeys → Pages")]
        [SerializeField] private PageId _inventoryPageId = new("Inventory");

        public InputReader          InputReader          => _inputReader;
        public PlayerConfig         PlayerConfig         => _playerConfig;
        public InputFilter          InputFilter          => _inputFilter;
        public Motor                Motor                => _motor;
        public PlayerCameraRig      CameraRig            => _playerCameraRig;
        public CursorController     CursorController     => _cursorController;
        public InteractionController InteractionController => _interactionController;
        public InteractionDetector  InteractionDetector  => _interactionDetector;
        public Camera               Camera               => _camera;
        public EffectManager        EffectManager        => _effectManager;
        public ANService            Service              => _service;

        public CameraState CameraState { get; private set; } = CameraState.Default;
        public event Action<CameraState> CameraStateChanged;

        public PlayerMode Mode { get; private set; } = PlayerMode.Gameplay;
        public event Action<PlayerMode> ModeChanged;

        // ── IPageFlowBus shortcut ─────────────────────────────────────────────
        private IPageFlowBus FlowBus => _webUIManager;

        // Считаем количество открытых страниц — безопаснее чем bool
        private int _openPageCount;

        private void Awake()
        {
            if (_motor == null)               _motor               = GetComponentInChildren<Motor>();
            if (_playerCameraRig == null)     _playerCameraRig     = GetComponentInChildren<PlayerCameraRig>();
            if (_inputFilter == null)         _inputFilter         = GetComponentInChildren<InputFilter>();
            if (_cursorController == null)    _cursorController    = GetComponentInChildren<CursorController>();
            if (_webUIManager == null)        _webUIManager        = FindAnyObjectByType<WebUIManager>();

            if (_webUIManager != null)
            {
                _webUIManager.PageOpened += _ => { _openPageCount++; RefreshCursor(); };
                _webUIManager.PageClosed += _ => { _openPageCount = Mathf.Max(0, _openPageCount - 1); RefreshCursor(); };
            }

            ApplyMode(Mode);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            TickModeHotkeys();

            if (Mode == PlayerMode.Gameplay)
                UpdateCameraState();
            else
                SetCameraState(CameraState.Default);

            _motor.Tick(dt);
            _playerCameraRig.Tick(dt);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Hotkeys
        // ══════════════════════════════════════════════════════════════════════

        private void TickModeHotkeys()
        {
            if (_inputFilter == null || !_inputFilter.InventoryPressed()) return;

            if (FlowBus == null)
            {
                Debug.LogWarning("[PlayerContext] IPageFlowBus not set — inventory hotkey ignored.");
                return;
            }

            // Пока идёт анимация — игнорируем нажатия
            if (_webUIManager != null && _webUIManager.IsFlowInProgress) return;

            if (FlowBus.IsPageOpen(_inventoryPageId))
                FlowBus.ClosePage(_inventoryPageId);
            else
                FlowBus.OpenPage(_inventoryPageId);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Mode (вызывается только из flow-шагов: LockInputStep / UnlockInputStep)
        // ══════════════════════════════════════════════════════════════════════

        public void SetMode(PlayerMode newMode)
        {
            if (Mode == newMode) return;

            Mode = newMode;
            ApplyMode(newMode);
            ModeChanged?.Invoke(newMode);
        }

        private void ApplyMode(PlayerMode mode)
        {
            if (_inputFilter != null)
                _inputFilter.ApplyMode(mode);

            // Interaction выключается только в не-Gameplay режиме
            if (_interactionController != null)
                _interactionController.enabled = (mode == PlayerMode.Gameplay);

            if (_interactionDetector != null)
                _interactionDetector.enabled = (mode == PlayerMode.Gameplay);

            RefreshCursor();
        }

        /// <summary>
        /// Курсор виден если открыта хоть одна UI-страница ИЛИ режим не Gameplay.
        /// Вызывается при смене режима и при PageOpened/PageClosed.
        /// </summary>
        private void RefreshCursor()
        {
            if (_cursorController == null) return;
            bool showCursor = _openPageCount > 0 || Mode != PlayerMode.Gameplay;
            _cursorController.SetCursor(showCursor);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Camera
        // ══════════════════════════════════════════════════════════════════════

        private void UpdateCameraState()
        {
            bool aimHeld = _inputFilter != null && _inputFilter.Aim();
            bool canAim  = _playerConfig != null && _playerConfig.CanLook;

            var desired = (canAim && aimHeld) ? CameraState.Aim : CameraState.Default;
            SetCameraState(desired);
        }

        private void SetCameraState(CameraState newState)
        {
            if (CameraState == newState) return;
            CameraState = newState;
            CameraStateChanged?.Invoke(CameraState);
        }
    }
}