using System;
using UnityEngine;

namespace Player
{
    [Flags]
    public enum InputMask
    {
        None        = 0,
        Move        = 1 << 0,
        Look        = 1 << 1,
        Sprint      = 1 << 2,
        Aim         = 1 << 3,
        Interact    = 1 << 4,
        Inventory   = 1 << 5,
        Ui          = 1 << 6,
    }

    [RequireComponent(typeof(PlayerContext))]
    public sealed class InputFilter : MonoBehaviour
    {
        private PlayerContext _playerContext;

        private InputReader _inputReader;
        private PlayerConfig _playerConfig;

        // текущая маска разрешений по режиму
        public InputMask Mask { get; private set; }

        private void Awake()
        {
            _playerContext = GetComponent<PlayerContext>();
            _inputReader = _playerContext.InputReader;
            _playerConfig = _playerContext.PlayerConfig;

            if (!_inputReader) Debug.LogError("Input reader not found");
            if (!_playerConfig) Debug.LogError("Player config not found");

            // Если ты добавил ModeChanged в PlayerContext (как я показывал), подключаемся:
            _playerContext.ModeChanged += OnModeChanged;

            // первичная синхронизация
            ApplyMode(_playerContext.Mode);
        }

        private void OnDestroy()
        {
            if (_playerContext != null)
                _playerContext.ModeChanged -= OnModeChanged;
        }

        private void OnModeChanged(PlayerMode mode) => ApplyMode(mode);

        public void ApplyMode(PlayerMode mode)
        {
            // Gameplay: всё как обычно.
            // UiInventory: оставляем только Inventory (чтобы закрыть) + (опционально) Ui
            Mask = mode switch
            {
                PlayerMode.Gameplay =>
                    InputMask.Move | InputMask.Look | InputMask.Sprint | InputMask.Aim | InputMask.Interact | InputMask.Inventory,

                PlayerMode.UiInventory =>
                    InputMask.Inventory | InputMask.Ui,

                _ => InputMask.None
            };
        }

        private bool Allow(InputMask m) => (Mask & m) != 0;

        // ====== Gameplay actions ======

        public Vector2 Move()
        {
            if (!Allow(InputMask.Move)) return Vector2.zero;
            return _playerConfig.CanMove ? _inputReader.Move : Vector2.zero;
        }

        public Vector2 Look()
        {
            if (!Allow(InputMask.Look)) return Vector2.zero;
            return _playerConfig.CanLook ? _inputReader.Look : Vector2.zero;
        }

        public bool Sprint()
        {
            if (!Allow(InputMask.Sprint)) return false;
            return _playerConfig.CanSprint && _inputReader.Sprint;
        }

        public bool Aim()
        {
            if (!Allow(InputMask.Aim)) return false;
            return _playerConfig.CanAim && _inputReader.Aim;
        }

        public bool InteractPressed()
        {
            // Важно: consume делаем только если действие разрешено.
            // Иначе ты “съешь” интеракт пока UI открыт и потом удивишься.
            if (!Allow(InputMask.Interact)) return false;
            return _inputReader.ConsumeInteractPressed();
        }

        // ====== Inventory toggle ======
        // Я не знаю, как именно у тебя устроен InputReader.Inventory: bool held или pressed.
        // Сейчас у тебя Inventory() => _inputReader.Inventory, значит это скорее held.
        // Для toggle лучше иметь pressed-this-frame. Если такого нет, сделаем детектор фронта.

        private bool _prevInventory;

        /// <summary>
        /// Toggle-событие "нажали кнопку инвентаря" (один раз на нажатие).
        /// Работает и в UI режиме (чтобы закрывать).
        /// </summary>
        public bool InventoryPressed()
        {
            if (!Allow(InputMask.Inventory)) return false;
            return _inputReader.ConsumeInventoryPressed();
        }


        /// <summary>
        /// Если где-то нужен hold-сигнал (редко для инвентаря), оставим.
        /// </summary>
        public bool InventoryHeld()
        {
            if (!Allow(InputMask.Inventory)) return false;
            return _inputReader.InventoryOpen;
        }
    }
}
