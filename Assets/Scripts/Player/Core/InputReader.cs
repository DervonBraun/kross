using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class InputReader : MonoBehaviour, IDisposable
    {
        private InputSystem_Actions _input;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }

        // Текущее состояние (если тебе оно реально нужно для UI)
        public bool InventoryOpen { get; private set; }

        // Событие "нажали кнопку инвентаря" в этом кадре
        public bool InventoryPressedThisFrame { get; private set; }

        public bool JumpPressed { get; private set; }
        public bool InteractPressedThisFrame { get; private set; }
        public bool Sprint { get; private set; }
        public bool Aim { get; private set; }

        private void Awake()
        {
            _input = new InputSystem_Actions();

            _input.Player.Move.performed += OnMove;
            _input.Player.Move.canceled += OnMove;

            _input.Player.Look.performed += OnLook;
            _input.Player.Look.canceled += OnLook;

            _input.Player.Interact.performed += OnInteract;

            _input.Player.Inventory.performed += OnInventory;

            _input.Player.Sprint.performed += OnSprint;
            _input.Player.Sprint.canceled += OnSprint;

            _input.Player.Aim.performed += OnAim;
            _input.Player.Aim.canceled += OnAim;

            _input.Player.Jump.performed += OnJumpPressed;
        }

        private void OnEnable() => _input.Player.Enable();
        private void OnDisable() => _input.Player.Disable();

        private void OnInventory(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            InventoryOpen = !InventoryOpen;
            InventoryPressedThisFrame = true;
        }

        public bool ConsumeInventoryPressed()
        {
            if (!InventoryPressedThisFrame) return false;
            InventoryPressedThisFrame = false;
            return true;
        }

        private void OnMove(InputAction.CallbackContext context) => Move = context.ReadValue<Vector2>();
        private void OnLook(InputAction.CallbackContext context) => Look = context.ReadValue<Vector2>();

        private void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
                InteractPressedThisFrame = true;
        }

        public bool ConsumeInteractPressed()
        {
            if (!InteractPressedThisFrame) return false;
            InteractPressedThisFrame = false;
            return true;
        }

        private void OnAim(InputAction.CallbackContext context) => Aim = context.performed;
        private void OnSprint(InputAction.CallbackContext context) => Sprint = context.performed;

        private void OnJumpPressed(InputAction.CallbackContext context)
        {
            if (context.performed)
                JumpPressed = true;
        }

        // Вызывай это ОДИН раз в конце Update где-нибудь (обычно в PlayerContext)
        public void ResetFrameInputs()
        {
            JumpPressed = false;
            InventoryPressedThisFrame = false;
            // InteractPressedThisFrame НЕ сбрасываем тут, он уже consume’ится
        }

        public void Dispose()
        {
            if (_input == null) return;

            _input.Player.Move.performed -= OnMove;
            _input.Player.Move.canceled -= OnMove;

            _input.Player.Look.performed -= OnLook;
            _input.Player.Look.canceled -= OnLook;

            _input.Player.Interact.performed -= OnInteract;

            _input.Player.Inventory.performed -= OnInventory;

            _input.Player.Sprint.performed -= OnSprint;
            _input.Player.Sprint.canceled -= OnSprint;

            _input.Player.Aim.performed -= OnAim;
            _input.Player.Aim.canceled -= OnAim;

            _input.Player.Jump.performed -= OnJumpPressed;

            _input.Dispose();
        }

        private void OnDestroy() => Dispose();
    }
}
